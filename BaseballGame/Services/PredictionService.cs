using BaseballGame.Models;

namespace BaseballGame.Services;

public class PredictionService(NpbScraperService scraper)
{
    public async Task<List<GamePrediction>> GetPredictionsAsync(List<GameResult> games)
    {
        // 順位表とチーム得失点を並行取得
        var standingsTask = scraper.GetStandingsAsync();
        var runStatsTask  = scraper.GetTeamRunStatsAsync();
        await Task.WhenAll(standingsTask, runStatsTask);

        var standings = standingsTask.Result;
        var runStats  = runStatsTask.Result;
        var predictions = new List<GamePrediction>();

        foreach (var game in games)
        {
            string? awayStarter = null, homeStarter = null;
            string? winPitcher = null, lossPitcher = null, savePitcher = null;
            double? homeEra = null, awayEra = null;

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

        if (hasHomeRuns && hasAwayRuns && homeRuns.RS + homeRuns.RA > 0 && awayRuns.RS + awayRuns.RA > 0)
        {
            homeRS = homeRuns.RS; homeRA = homeRuns.RA;
            awayRS = awayRuns.RS; awayRA = awayRuns.RA;

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
            else
            {
                var homePyth = Pythagorean(homeRS, homeRA);
                var awayPyth = Pythagorean(awayRS, awayRA);
                var total = homePyth + awayPyth;
                homeProb = total > 0 ? homePyth / total : 0.5;
            }
            awayProb = 1.0 - homeProb;
        }
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

    // ピタゴラス勝率: RS² / (RS² + RA²)
    private static double Pythagorean(int rs, int ra)
    {
        if (rs + ra == 0) return 0.5;
        return (double)rs * rs / ((double)rs * rs + (double)ra * ra);
    }
}
