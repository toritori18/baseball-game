namespace BaseballGame.Models;

/// <summary>
/// 試合詳細画面で表示する打撃・投手成績と得点シーンをまとめたモデル
/// </summary>
public record GameDetail
{
    public GameResult Summary { get; init; } = new();
    public List<BattingLine> HomeBatting { get; init; } = [];
    public List<BattingLine> AwayBatting { get; init; } = [];
    public List<PitchingLine> HomePitching { get; init; } = [];
    public List<PitchingLine> AwayPitching { get; init; } = [];
    public List<ScoringPlay> ScoringPlays { get; init; } = [];
    public string? AwayAnnouncedStarter { get; init; }
    public string? HomeAnnouncedStarter { get; init; }
}

/// <summary>
/// 打者1人分の打撃成績（打数・得点・安打・打点）
/// </summary>
public record BattingLine
{
    public string PlayerName { get; init; } = "";
    public int AtBats { get; init; }
    public int Runs { get; init; }
    public int Hits { get; init; }
    public int Rbi { get; init; }
}

/// <summary>
/// 投手1人分の投球成績（投球回・自責点・勝敗セーブ・選手ページURL）
/// </summary>
public record PitchingLine
{
    public string PlayerName { get; init; } = "";
    public string InningsPitched { get; init; } = "";
    public int EarnedRuns { get; init; }
    public bool IsWin { get; init; }
    public bool IsLoss { get; init; }
    public bool IsSave { get; init; }
    public string? PlayerUrl { get; init; }
}

/// <summary>
/// 得点シーン1件（イニング・表裏・チーム・打者・打席結果説明）
/// </summary>
public record ScoringPlay
{
    public int Inning { get; init; }
    public bool IsTop { get; init; }
    public string Team { get; init; } = "";
    public string Batter { get; init; } = "";
    public string Description { get; init; } = "";
}
