namespace BaseballGame.Models;

/// <summary>
/// 試合一覧画面で表示する1試合分のサマリー情報
/// </summary>
public record GameResult
{
    public string GameId { get; init; } = "";
    public string DetailUrl { get; init; } = "";
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";
    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }
    public DateTime GameDate { get; init; }
    public string Status { get; init; } = "";
    public bool IsGiantsGame => HomeTeam == "巨人" || AwayTeam == "巨人";
}
