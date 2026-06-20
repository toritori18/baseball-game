namespace BaseballGame.Models;

public record GamePrediction
{
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";
    public double HomeWinProbability { get; init; }
    public double AwayWinProbability { get; init; }
    public string? HomeStarterName { get; init; }
    public double? HomeStarterEra { get; init; }
    public string? AwayStarterName { get; init; }
    public double? AwayStarterEra { get; init; }
    public bool IsStarterAvailable => HomeStarterName != null || AwayStarterName != null;
    public string Comment { get; init; } = "";
}
