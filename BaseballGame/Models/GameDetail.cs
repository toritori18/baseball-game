namespace BaseballGame.Models;

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

public record BattingLine
{
    public string PlayerName { get; init; } = "";
    public int AtBats { get; init; }
    public int Runs { get; init; }
    public int Hits { get; init; }
    public int Rbi { get; init; }
}

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

public record ScoringPlay
{
    public int Inning { get; init; }
    public bool IsTop { get; init; }
    public string Team { get; init; } = "";
    public string Batter { get; init; } = "";
    public string Description { get; init; } = "";
}
