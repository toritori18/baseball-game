using BaseballGame.Models;

namespace BaseballGame.Services;

/// <summary>
/// 試合一覧をもとにピタゴラス勝率・先発ERA・勝敗投手を組み合わせた勝率予想を生成する
/// </summary>
public class PredictionService(NpbScraperService scraper)
{
    /// <summary>
    /// 試合一覧に対して勝率予想を生成する
    /// </summary>
    /// <param name="games">予想対象の試合一覧</param>
    /// <returns>各試合の勝率予想リスト</returns>
    public async Task<List<GamePrediction>> GetPredictionsAsync(List<GameResult> games)
    {
        // 順位表とチーム得失点を並行取得
        var standingsTask = scraper.GetStandingsAsync();
        var runStatsTask  = scraper.GetTeamRunStatsAsync();
        await Task.WhenAll(standingsTask, runStatsTask);

        var standings = standingsTask.Result;
        var runStats  = runStatsTask.Result;
        var predictions = new List<GamePrediction>();

        // 各試合の勝率予想を構築
        foreach (var game in games)
        {
            string? awayStarter = null, homeStarter = null;
            string? winPitcher = null, lossPitcher = null, savePitcher = null;
            double? homeEra = null, awayEra = null;

            // 詳細URLがある試合のみ先発投手・ERAを取得
            if (!string.IsNullOrEmpty(game.DetailUrl))
            {
                var pitchersTask = scraper.GetStartersFromDetailAsync(game.DetailUrl);
                var eraTask      = scraper.GetStarterErasAsync(game.DetailUrl);
                await Task.WhenAll(pitchersTask, eraTask);

                var pitchers = pitchersTask.Result;
                awayStarter = pitchers.AwayStarter;
                homeStarter = pitchers.HomeStarter;
                winPitcher  = pitchers.WinPitcher;
                lossPitcher = pitchers.LossPitcher;
                savePitcher = pitchers.SavePitcher;

                homeEra = eraTask.Result.HomeEra;
                awayEra = eraTask.Result.AwayEra;
            }

            var prediction = BuildPrediction(
                game, standings, runStats,
                awayStarter, homeStarter,
                winPitcher, lossPitcher, savePitcher,
                homeEra, awayEra);
            predictions.Add(prediction);
        }

        return predictions;
    }

    /// <summary>
    /// 試合情報・順位表・チーム得失点・ERA をもとに勝率予想を組み立てる
    /// </summary>
    /// <param name="game">予想対象の試合</param>
    /// <param name="standings">全チームの勝率辞書</param>
    /// <param name="runStats">全チームの総得点・総失点辞書</param>
    /// <param name="awayStarter">ビジター先発投手名</param>
    /// <param name="homeStarter">ホーム先発投手名</param>
    /// <param name="winPitcher">勝利投手名</param>
    /// <param name="lossPitcher">敗戦投手名</param>
    /// <param name="savePitcher">セーブ投手名</param>
    /// <param name="homeEra">ホーム先発ERA</param>
    /// <param name="awayEra">ビジター先発ERA</param>
    /// <returns>ピタゴラス勝率（ERA補正あり/なし）または順位表ベースの勝率予想</returns>
    private static GamePrediction BuildPrediction(
        GameResult game,
        Dictionary<string, double> standings,
        Dictionary<string, (int RS, int RA)> runStats,
        string? awayStarter, string? homeStarter,
        string? winPitcher, string? lossPitcher, string? savePitcher,
        double? homeEra, double? awayEra)
    {
        double homeProb, awayProb;
        int homeRS = 0, homeRA = 0, awayRS = 0, awayRA = 0;
        bool eraAdjusted = false;

        var hasHomeRuns = runStats.TryGetValue(game.HomeTeam, out var homeRuns);
        var hasAwayRuns = runStats.TryGetValue(game.AwayTeam, out var awayRuns);

        // RS/RAが両チーム揃っている場合はピタゴラス勝率を計算
        if (hasHomeRuns && hasAwayRuns && homeRuns.RS + homeRuns.RA > 0 && awayRuns.RS + awayRuns.RA > 0)
        {
            homeRS = homeRuns.RS; homeRA = homeRuns.RA;
            awayRS = awayRuns.RS; awayRA = awayRuns.RA;

            // ERA情報がある場合はERA補正ピタゴラス勝率を優先
            if (homeEra.HasValue || awayEra.HasValue)
            {
                // ERA補正ピタゴラス: homeAdj = homeRS × awayERA, awayAdj = awayRS × homeERA
                // ERA高い先発 → 相手打線が得点しやすい
                const double leagueAvg = 3.80;
                var hAdj = homeRS * (awayEra ?? leagueAvg);
                var aAdj = awayRS * (homeEra ?? leagueAvg);
                homeProb = hAdj * hAdj / (hAdj * hAdj + aAdj * aAdj);
                eraAdjusted = true;
            }
            // ERA情報がない場合は通常のピタゴラス勝率
            else
            {
                var homePyth = Pythagorean(homeRS, homeRA);
                var awayPyth = Pythagorean(awayRS, awayRA);
                var total = homePyth + awayPyth;
                homeProb = total > 0 ? homePyth / total : 0.5;
            }
            awayProb = 1.0 - homeProb;
        }
        // RS/RAが取得できない場合は順位表の勝率にフォールバック
        else
        {
            var homeWinRate = standings.TryGetValue(game.HomeTeam, out var hw) ? hw : 0.5;
            var awayWinRate = standings.TryGetValue(game.AwayTeam, out var aw) ? aw : 0.5;
            var total = homeWinRate + awayWinRate;
            homeProb = total > 0 ? homeWinRate / total : 0.5;
            awayProb = 1.0 - homeProb;
        }

        return new GamePrediction
        {
            HomeTeam           = game.HomeTeam,
            AwayTeam           = game.AwayTeam,
            HomeWinProbability = homeProb,
            AwayWinProbability = awayProb,
            HomeStarterName    = homeStarter,
            AwayStarterName    = awayStarter,
            HomeStarterEra     = homeEra,
            AwayStarterEra     = awayEra,
            HomeRunsScored     = homeRS,
            HomeRunsAllowed    = homeRA,
            AwayRunsScored     = awayRS,
            AwayRunsAllowed    = awayRA,
            WinPitcher         = winPitcher,
            LossPitcher        = lossPitcher,
            SavePitcher        = savePitcher,
            Comment            = eraAdjusted ? "先発ERA補正あり（ピタゴラス勝率）" : "",
        };
    }

    /// <summary>
    /// ピタゴラス勝率（RS² / (RS² + RA²)）を計算する
    /// </summary>
    /// <param name="rs">総得点</param>
    /// <param name="ra">総失点</param>
    /// <returns>ピタゴラス勝率。RS+RA=0の場合は0.5を返す</returns>
    private static double Pythagorean(int rs, int ra)
    {
        if (rs + ra == 0)
        {
            return 0.5;
        }
        return (double)rs * rs / ((double)rs * rs + (double)ra * ra);
    }
}
