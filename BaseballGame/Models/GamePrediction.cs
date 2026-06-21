namespace BaseballGame.Models;

/// <summary>
/// ピタゴラス勝率・先発ERA・勝敗投手を含む1試合分の勝率予想
/// </summary>
public record GamePrediction
{
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";
    public double HomeWinProbability { get; init; }
    public double AwayWinProbability { get; init; }
    public string? HomeStarterName { get; init; }
    public string? AwayStarterName { get; init; }
    public bool IsStarterAvailable => HomeStarterName != null || AwayStarterName != null;
    public string Comment { get; init; } = "";

    // ピタゴラス勝率の根拠
    public int HomeRunsScored { get; init; }
    public int HomeRunsAllowed { get; init; }
    public int AwayRunsScored { get; init; }
    public int AwayRunsAllowed { get; init; }
    public bool HasRunStats => HomeRunsScored > 0 || AwayRunsScored > 0;

    // 先発ERA
    public double? HomeStarterEra { get; init; }
    public double? AwayStarterEra { get; init; }
    public bool HasEraStats => HomeStarterEra.HasValue || AwayStarterEra.HasValue;

    // 勝敗投手
    public string? WinPitcher { get; init; }
    public string? LossPitcher { get; init; }
    public string? SavePitcher { get; init; }
    public bool HasPitcherResult => WinPitcher != null || LossPitcher != null;
}
