using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using BaseballGame.Models;

namespace BaseballGame.Services;

public class NpbScraperService(HttpClient httpClient, IMemoryCache cache)
{
    private const string BaseUrl = "https://npb.jp";

    private static readonly Dictionary<string, string> TeamNameMap = new()
    {
        { "読売ジャイアンツ", "巨人" },
        { "中日ドラゴンズ", "中日" },
        { "広島東洋カープ", "広島" },
        { "東京ヤクルトスワローズ", "ヤクルト" },
        { "横浜DeNAベイスターズ", "DeNA" },
        { "阪神タイガース", "阪神" },
        { "福岡ソフトバンクホークス", "ソフトバンク" },
        { "北海道日本ハムファイターズ", "日本ハム" },
        { "オリックス・バファローズ", "オリックス" },
        { "東北楽天ゴールデンイーグルス", "楽天" },
        { "千葉ロッテマリーンズ", "ロッテ" },
        { "埼玉西武ライオンズ", "西武" },
    };

    private static string NormalizeTeamName(string fullName) =>
        TeamNameMap.TryGetValue(fullName, out var shortName) ? shortName : fullName;

    public async Task<List<GameResult>> GetTodayResultsAsync()
    {
        var today = DateTime.Today;
        var cacheKey = $"games_{today:yyyyMMdd}";

        if (cache.TryGetValue(cacheKey, out List<GameResult>? cached))
            return cached!;

        var results = await ScrapeGameListAsync(today);
        cache.Set(cacheKey, results, TimeSpan.FromMinutes(5));
        return results;
    }

    public async Task<GameDetail?> GetGameDetailAsync(string detailUrl)
    {
        var cacheKey = $"detail_{detailUrl}";

        if (cache.TryGetValue(cacheKey, out GameDetail? cached))
            return cached;

        var detail = await ScrapeGameDetailAsync(detailUrl);
        if (detail != null)
            cache.Set(cacheKey, detail, TimeSpan.FromMinutes(10));
        return detail;
    }

    public async Task<(string? AwayStarter, string? HomeStarter)> GetStartersFromDetailAsync(string detailUrl)
    {
        var cacheKey = $"starters_{detailUrl}";
        if (cache.TryGetValue(cacheKey, out (string?, string?) cached))
            return cached;

        try
        {
            // box.html の投手表を使う（player-order は継投後に更新されるため不適）
            var boxUrl = detailUrl.TrimEnd('/') + "/box.html";
            var html = await httpClient.GetStringAsync(boxUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // NPB URL 形式は HOME-VISITOR のため:
            //   table_top_p (表イニング = ビジター打席) = ホームチームの投手 → ゲームカード左チームの先発
            //   table_bottom_p (裏イニング = ホーム打席) = ビジターチームの投手 → ゲームカード右チームの先発
            // 本スクレイパーでは td.team1(左) を AwayTeam、td.team2(右) を HomeTeam と命名しているため
            //   AwayStarter = table_top_p の先頭投手（実際のホームチーム先発）
            //   HomeStarter = table_bottom_p の先頭投手（実際のビジターチーム先発）
            var awayStarter = GetFirstPitcherFromTable(doc, "table_top_p");
            var homeStarter = GetFirstPitcherFromTable(doc, "table_bottom_p");

            var result = (awayStarter, homeStarter);
            cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
            return result;
        }
        catch
        {
            return (null, null);
        }
    }

    private static string? GetFirstPitcherFromTable(HtmlDocument doc, string tableId)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            $"//div[@id='{tableId}']//tbody/tr[1]//td[@class='player']//a");
        var name = node?.InnerText.Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    public async Task<Dictionary<string, double>> GetStandingsAsync()
    {
        var cacheKey = "standings";

        if (cache.TryGetValue(cacheKey, out Dictionary<string, double>? cached))
            return cached!;

        var standings = await ScrapeStandingsAsync();
        cache.Set(cacheKey, standings, TimeSpan.FromHours(1));
        return standings;
    }

    private async Task<List<GameResult>> ScrapeGameListAsync(DateTime date)
    {
        var results = new List<GameResult>();
        try
        {
            var url = $"{BaseUrl}/games/{date:yyyy}/";
            var html = await httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 実際のHTML構造: div#game_score 内の a.link_block が各試合カード
            var gameLinks = doc.DocumentNode.SelectNodes(
                "//div[@id='game_score']//a[@class='link_block']");

            if (gameLinks == null) return results;

            foreach (var link in gameLinks)
            {
                var game = ParseGameLink(link, date);
                if (game != null)
                    results.Add(game);
            }
        }
        catch
        {
            // スクレイピング失敗時は空リストを返す
        }

        return results;
    }

    private GameResult? ParseGameLink(HtmlNode link, DateTime date)
    {
        try
        {
            var detailPath = link.GetAttributeValue("href", "");

            // チーム名: td.team1 / td.team2 の img alt 属性
            var awayImg = link.SelectSingleNode(".//td[@class='team1']//img");
            var homeImg = link.SelectSingleNode(".//td[@class='team2']//img");
            var awayTeam = NormalizeTeamName(awayImg?.GetAttributeValue("alt", "") ?? "");
            var homeTeam = NormalizeTeamName(homeImg?.GetAttributeValue("alt", "") ?? "");

            if (string.IsNullOrEmpty(awayTeam) || string.IsNullOrEmpty(homeTeam))
                return null;

            // スコア: td.score が2つ（左=アウェー、右=ホーム）。"*" は未開始/中止
            var scoreTds = link.SelectNodes(".//td[@class='score']");
            int? awayScore = null, homeScore = null;
            if (scoreTds?.Count >= 2)
            {
                if (int.TryParse(scoreTds[0].InnerText.Trim(), out var a)) awayScore = a;
                if (int.TryParse(scoreTds[1].InnerText.Trim(), out var h)) homeScore = h;
            }

            // 状態: td.state に「（球場名）\nイニング or 終了 or 中止」
            var stateNode = link.SelectSingleNode(".//td[@class='state']");
            var stateText = stateNode?.InnerText.Trim() ?? "";

            string status;
            if (stateText.Contains("中止")) status = "中止";
            else if (stateText.Contains("終了")) status = "終了";
            else if (awayScore.HasValue && homeScore.HasValue) status = "試合中";
            else status = "試合前";

            return new GameResult
            {
                GameId = detailPath,
                DetailUrl = string.IsNullOrEmpty(detailPath) ? "" : $"{BaseUrl}{detailPath}",
                AwayTeam = awayTeam,
                HomeTeam = homeTeam,
                AwayScore = awayScore,
                HomeScore = homeScore,
                GameDate = date,
                Status = status
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<GameDetail?> ScrapeGameDetailAsync(string detailUrl)
    {
        try
        {
            var html = await httpClient.GetStringAsync(detailUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var summary = ParseDetailSummary(doc, detailUrl);
            var (homeBatting, awayBatting) = ParseBattingStats(doc);
            var (homePitching, awayPitching) = ParsePitchingStats(doc);
            var scoringPlays = ParseScoringPlays(doc);

            return new GameDetail
            {
                Summary = summary,
                HomeBatting = homeBatting,
                AwayBatting = awayBatting,
                HomePitching = homePitching,
                AwayPitching = awayPitching,
                ScoringPlays = scoringPlays
            };
        }
        catch
        {
            return null;
        }
    }

    private GameResult ParseDetailSummary(HtmlDocument doc, string detailUrl)
    {
        var awayTeam = doc.DocumentNode.SelectSingleNode("//span[@class='away_team']")?.InnerText.Trim() ?? "";
        var homeTeam = doc.DocumentNode.SelectSingleNode("//span[@class='home_team']")?.InnerText.Trim() ?? "";
        var awayScoreText = doc.DocumentNode.SelectSingleNode("//span[@class='away_score']")?.InnerText.Trim();
        var homeScoreText = doc.DocumentNode.SelectSingleNode("//span[@class='home_score']")?.InnerText.Trim();

        int? awayScore = int.TryParse(awayScoreText, out var aw) ? aw : null;
        int? homeScore = int.TryParse(homeScoreText, out var hw) ? hw : null;

        return new GameResult
        {
            DetailUrl = detailUrl,
            AwayTeam = NormalizeTeamName(awayTeam),
            HomeTeam = NormalizeTeamName(homeTeam),
            AwayScore = awayScore,
            HomeScore = homeScore,
            GameDate = DateTime.Today,
            Status = awayScore.HasValue ? "終了" : "試合前"
        };
    }

    private (List<BattingLine> home, List<BattingLine> away) ParseBattingStats(HtmlDocument doc)
    {
        static List<BattingLine> ParseTable(HtmlNodeCollection? rows)
        {
            var lines = new List<BattingLine>();
            if (rows == null) return lines;
            foreach (var row in rows.Skip(1))
            {
                var cols = row.SelectNodes(".//td");
                if (cols == null || cols.Count < 5) continue;
                lines.Add(new BattingLine
                {
                    PlayerName = cols[0].InnerText.Trim(),
                    AtBats = int.TryParse(cols[1].InnerText.Trim(), out var ab) ? ab : 0,
                    Hits = int.TryParse(cols[2].InnerText.Trim(), out var h) ? h : 0,
                    HomeRuns = int.TryParse(cols[3].InnerText.Trim(), out var hr) ? hr : 0,
                    Rbi = int.TryParse(cols[4].InnerText.Trim(), out var rbi) ? rbi : 0
                });
            }
            return lines;
        }

        var tables = doc.DocumentNode.SelectNodes("//table[contains(@class,'batting')]");
        var home = tables?.Count > 0 ? ParseTable(tables[0].SelectNodes(".//tr")) : [];
        var away = tables?.Count > 1 ? ParseTable(tables[1].SelectNodes(".//tr")) : [];
        return (home, away);
    }

    private (List<PitchingLine> home, List<PitchingLine> away) ParsePitchingStats(HtmlDocument doc)
    {
        static List<PitchingLine> ParseTable(HtmlNodeCollection? rows)
        {
            var lines = new List<PitchingLine>();
            if (rows == null) return lines;
            foreach (var row in rows.Skip(1))
            {
                var cols = row.SelectNodes(".//td");
                if (cols == null || cols.Count < 4) continue;
                var name = cols[0].InnerText.Trim();
                lines.Add(new PitchingLine
                {
                    PlayerName = name.Replace("○", "").Replace("●", "").Trim(),
                    InningsPitched = cols[1].InnerText.Trim(),
                    EarnedRuns = int.TryParse(cols[2].InnerText.Trim(), out var er) ? er : 0,
                    IsWin = name.Contains("○"),
                    IsLoss = name.Contains("●")
                });
            }
            return lines;
        }

        var tables = doc.DocumentNode.SelectNodes("//table[contains(@class,'pitching')]");
        var home = tables?.Count > 0 ? ParseTable(tables[0].SelectNodes(".//tr")) : [];
        var away = tables?.Count > 1 ? ParseTable(tables[1].SelectNodes(".//tr")) : [];
        return (home, away);
    }

    private List<ScoringPlay> ParseScoringPlays(HtmlDocument doc)
    {
        var plays = new List<ScoringPlay>();
        var rows = doc.DocumentNode.SelectNodes("//table[contains(@class,'scoring')]//tr[position()>1]");
        if (rows == null) return plays;

        foreach (var row in rows)
        {
            var cols = row.SelectNodes(".//td");
            if (cols == null || cols.Count < 3) continue;
            plays.Add(new ScoringPlay
            {
                Inning = int.TryParse(cols[0].InnerText.Trim(), out var inn) ? inn : 0,
                Team = cols[1].InnerText.Trim(),
                Description = cols[2].InnerText.Trim()
            });
        }
        return plays;
    }

    private async Task<Dictionary<string, double>> ScrapeStandingsAsync()
    {
        var result = new Dictionary<string, double>();
        foreach (var leagueUrl in new[] { $"{BaseUrl}/cl/", $"{BaseUrl}/pl/" })
        {
            try
            {
                var html = await httpClient.GetStringAsync(leagueUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // div.standing_table > table の各行: th=チーム名, td[0]=試合,td[1]=勝,td[2]=敗,td[3]=分,td[4]=勝率
                var rows = doc.DocumentNode.SelectNodes("//div[@class='standing_table']//table//tbody//tr");
                if (rows == null) continue;

                foreach (var row in rows)
                {
                    var teamNode = row.SelectSingleNode(".//th//span[@class='hide_sp']")
                                ?? row.SelectSingleNode(".//th");
                    var cols = row.SelectNodes(".//td");
                    if (teamNode == null || cols == null || cols.Count < 5) continue;

                    var team = NormalizeTeamName(teamNode.InnerText.Trim());
                    // 勝率は "." で始まる形式（例: .547）
                    var winRateText = cols[4].InnerText.Trim();
                    if (double.TryParse(winRateText, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var winRate))
                    {
                        result[team] = winRate;
                    }
                }
            }
            catch
            {
                // リーグ取得失敗時はスキップ
            }
        }
        return result;
    }
}
