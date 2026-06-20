using BaseballGame.Models;

namespace BaseballGame.Services;

public class PredictionService(NpbScraperService scraper)
{
    public async Task<List<GamePrediction>> GetPredictionsAsync(List<GameResult> games)
    {
        var standings = await scraper.GetStandingsAsync();
        var predictions = new List<GamePrediction>();

        foreach (var game in games)
        {
            string? awayStarter = null, homeStarter = null;
            if (!string.IsNullOrEmpty(game.DetailUrl))
                (awayStarter, homeStarter) = await scraper.GetStartersFromDetailAsync(game.DetailUrl);

            var prediction = BuildPrediction(game, standings, awayStarter, homeStarter);
            predictions.Add(prediction);
        }

        return predictions;
    }

    private static GamePrediction BuildPrediction(
        GameResult game,
        Dictionary<string, double> standings,
        string? awayStarter,
        string? homeStarter)
    {
        // 勝率を取得（データなしの場合は0.5）
        var homeWinRate = standings.TryGetValue(game.HomeTeam, out var hw) ? hw : 0.5;
        var awayWinRate = standings.TryGetValue(game.AwayTeam, out var aw) ? aw : 0.5;

        // 正規化して対戦上の勝率を算出
        var total = homeWinRate + awayWinRate;
        var homeProb = total > 0 ? homeWinRate / total : 0.5;
        var awayProb = 1.0 - homeProb;

        var comment = GenerateComment(game.HomeTeam, game.AwayTeam, homeProb, awayProb, homeStarter, awayStarter);

        return new GamePrediction
        {
            HomeTeam = game.HomeTeam,
            AwayTeam = game.AwayTeam,
            HomeWinProbability = homeProb,
            AwayWinProbability = awayProb,
            HomeStarterName = homeStarter,
            AwayStarterName = awayStarter,
            Comment = comment
        };
    }

    private static string GenerateComment(
        string homeTeam, string awayTeam,
        double homeProb, double awayProb,
        string? homeStarter, string? awayStarter)
    {
        var favorite = homeProb >= awayProb ? homeTeam : awayTeam;
        var favoriteProb = Math.Max(homeProb, awayProb);

        var strengthComment = favoriteProb > 0.60
            ? $"{favorite}の勝率は今季上位で地力が高い。"
            : "両チームの勝率は拮抗しており実力差は小さい。";

        var starterComment = (homeStarter, awayStarter) switch
        {
            (not null, not null) => $"先発: {awayStarter} vs {homeStarter}。",
            (not null, null)     => $"先発: {homeStarter}（{homeTeam}）。",
            (null, not null)     => $"先発: {awayStarter}（{awayTeam}）。",
            _                    => "先発投手情報が未公開のため投手評価は未反映。"
        };

        return $"{strengthComment}{starterComment}";
    }
}
