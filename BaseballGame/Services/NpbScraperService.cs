using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using BaseballGame.Models;

namespace BaseballGame.Services;

/// <summary>
/// npb.jp からHTMLをスクレイピングして試合情報を取得し、IMemoryCacheでキャッシュする
/// </summary>
public class NpbScraperService(HttpClient httpClient, IMemoryCache cache)
{
    private const string BaseUrl = "https://npb.jp";
    private static readonly TimeSpan RequestInterval = TimeSpan.FromSeconds(1);
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private DateTime _lastRequestAt = DateTime.MinValue;

    /// <summary>
    /// 1秒以上のリクエスト間隔を保証してURLのHTMLを取得する
    /// </summary>
    /// <param name="url">取得対象のURL</param>
    /// <returns>レスポンスHTML文字列</returns>
    // リクエスト間隔を最低1秒確保してから取得する
    private async Task<string> GetHtmlAsync(string url)
    {
        await _requestGate.WaitAsync();
        try
        {
            var wait = RequestInterval - (DateTime.UtcNow - _lastRequestAt);
            // 前回リクエストから1秒未満の場合は残り時間だけ待機
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait);
            }
            var html = await httpClient.GetStringAsync(url);
            // レスポンス受信後に時刻を記録してレスポンス間隔を正確に計測する
            _lastRequestAt = DateTime.UtcNow;
            return html;
        }
        finally
        {
            _requestGate.Release();
        }
    }

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
        // チーム成績ページの短縮形
        { "読売", "巨人" },
        { "東京ヤクルト", "ヤクルト" },
        { "広島東洋", "広島" },
        { "横浜DeNA", "DeNA" },
        { "北海道日本ハム", "日本ハム" },
        { "東北楽天", "楽天" },
        { "千葉ロッテ", "ロッテ" },
        { "埼玉西武", "西武" },
        { "福岡ソフトバンク", "ソフトバンク" },
    };

    /// <summary>
    /// チーム正式名称を表示用の短縮名に変換する
    /// </summary>
    /// <param name="fullName">正式名称またはページ上の表記</param>
    /// <returns>短縮チーム名。マッピングにない場合はTrimした入力値をそのまま返す</returns>
    private static string NormalizeTeamName(string fullName) =>
        TeamNameMap.TryGetValue(fullName.Trim(), out var shortName) ? shortName : fullName.Trim();


    /// <summary>
    /// 当日の全試合結果を取得する
    /// </summary>
    /// <returns>当日の試合結果一覧。スクレイピング失敗時は空リスト</returns>
    public async Task<List<GameResult>> GetTodayResultsAsync()
    {
        var today = DateTime.Today;
        var cacheKey = $"games_{today:yyyyMMdd}";

        // キャッシュが存在する場合はスクレイピングを省略して返す
        if (cache.TryGetValue(cacheKey, out List<GameResult>? cached))
        {
            return cached!;
        }

        var results = await ScrapeGameListAsync(today);
        cache.Set(cacheKey, results, TimeSpan.FromMinutes(5));
        return results;
    }

    /// <summary>
    /// 指定日の全試合情報をnpb.jpからスクレイピングして取得する
    /// </summary>
    /// <param name="date">取得対象の日付</param>
    /// <returns>試合情報のリスト。スクレイピング失敗時は空リスト</returns>
    private async Task<List<GameResult>> ScrapeGameListAsync(DateTime date)
    {
        var results = new List<GameResult>();
        try
        {
            var url = $"{BaseUrl}/games/{date:yyyy}/";
            var html = await GetHtmlAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var gameLinks = doc.DocumentNode.SelectNodes(
                "//div[@id='game_score']//a[@class='link_block']");

            // 試合リンクが見つからない場合は空リストを返す
            if (gameLinks == null)
            {
                return results;
            }

            // 各試合リンクをパースして結果リストに追加
            foreach (var link in gameLinks)
            {
                var game = ParseGameLink(link, date);
                if (game != null)
                {
                    results.Add(game);
                }
            }
        }
        catch
        {
            // スクレイピング失敗時は空リストを返す
        }

        return results;
    }

    /// <summary>
    /// 試合リンクのHTMLノードから1試合分のサマリーを生成する
    /// </summary>
    /// <param name="link">試合リンクを表すHTMLノード</param>
    /// <param name="date">試合日</param>
    /// <returns>試合サマリー。パース失敗時はnull</returns>
    private GameResult? ParseGameLink(HtmlNode link, DateTime date)
    {
        try
        {
            var detailPath = link.GetAttributeValue("href", "");

            var awayImg = link.SelectSingleNode(".//td[@class='team1']//img");
            var homeImg = link.SelectSingleNode(".//td[@class='team2']//img");
            var awayTeam = NormalizeTeamName(awayImg?.GetAttributeValue("alt", "") ?? "");
            var homeTeam = NormalizeTeamName(homeImg?.GetAttributeValue("alt", "") ?? "");

            // チーム名が取得できない場合は無効なデータとしてスキップ
            if (string.IsNullOrEmpty(awayTeam) || string.IsNullOrEmpty(homeTeam))
            {
                return null;
            }

            var scoreTds = link.SelectNodes(".//td[@class='score']");
            int? awayScore = null, homeScore = null;
            // スコア欄が2列以上ある場合のみ数値に変換
            if (scoreTds?.Count >= 2)
            {
                if (int.TryParse(scoreTds[0].InnerText.Trim(), out var a))
                {
                    awayScore = a;
                }
                if (int.TryParse(scoreTds[1].InnerText.Trim(), out var h))
                {
                    homeScore = h;
                }
            }

            var stateNode = link.SelectSingleNode(".//td[@class='state']");
            var stateText = stateNode?.InnerText.Trim() ?? "";

            // 試合状態テキストとスコアの有無から試合ステータスを判定
            string status;
            if (stateText.Contains("中止"))
            {
                status = "中止";
            }
            else if (stateText.Contains("終了"))
            {
                status = "終了";
            }
            else if (awayScore.HasValue && homeScore.HasValue)
            {
                status = "試合中";
            }
            else
            {
                status = "試合前";
            }

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

    /// <summary>
    /// 試合詳細（打撃・投手成績・得点シーン）を取得する
    /// </summary>
    /// <param name="detailUrl">試合詳細ページのURL</param>
    /// <returns>試合詳細情報。取得失敗時はnull</returns>
    public async Task<GameDetail?> GetGameDetailAsync(string detailUrl)
    {
        var cacheKey = $"box_{detailUrl}";

        // キャッシュが存在する場合はスクレイピングを省略して返す
        if (cache.TryGetValue(cacheKey, out GameDetail? cached))
        {
            return cached;
        }

        var detail = await ScrapeGameDetailAsync(detailUrl);
        // 取得成功時のみキャッシュに保存（終了試合は長期TTL）
        if (detail != null)
        {
            var isFinished = detail.Summary.Status == "終了";
            var ttl = isFinished ? TimeSpan.FromDays(7) : TimeSpan.FromMinutes(10);
            cache.Set(cacheKey, detail, ttl);
        }
        return detail;
    }

    /// <summary>
    /// box.htmlとplaybyplay.htmlをスクレイピングして試合詳細を組み立てる
    /// </summary>
    /// <param name="detailUrl">試合詳細ページのURL</param>
    /// <returns>試合詳細情報。取得失敗時はnull</returns>
    private async Task<GameDetail?> ScrapeGameDetailAsync(string detailUrl)
    {
        try
        {
            var boxUrl = detailUrl.TrimEnd('/') + "/box.html";
            var pbpUrl = detailUrl.TrimEnd('/') + "/playbyplay.html";

            // box.html は必須、playbyplay.html は試合前に存在しない場合があるため任意
            // 両タスクを起動してから順次 await する（セマフォにより1リクエストずつ直列実行）
            var boxTask = GetHtmlAsync(boxUrl);
            var pbpTask = TryGetStringAsync(pbpUrl);
            var boxHtml = await boxTask;
            var pbpHtml = await pbpTask;

            var boxDoc = new HtmlDocument();
            boxDoc.LoadHtml(boxHtml);

            List<ScoringPlay> scoringPlays = [];
            // プレイバイプレイHTMLが取得できた場合のみ得点シーンを解析
            if (pbpHtml != null)
            {
                var pbpDoc = new HtmlDocument();
                pbpDoc.LoadHtml(pbpHtml);
                scoringPlays = ParseScoringPlays(pbpDoc);
            }

            var summary = ParseBoxSummary(boxDoc, detailUrl);
            var (awayBatting, homeBatting) = ParseBattingStats(boxDoc);
            var (awayPitching, homePitching) = ParsePitchingStats(boxDoc);
            var (announcedAway, announcedHome) = ParseAnnouncedStarters(boxDoc);

            return new GameDetail
            {
                Summary = summary,
                AwayBatting = awayBatting,
                HomeBatting = homeBatting,
                AwayPitching = awayPitching,
                HomePitching = homePitching,
                ScoringPlays = scoringPlays,
                AwayAnnouncedStarter = announcedAway,
                HomeAnnouncedStarter = announcedHome,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// HTML取得を試みて失敗した場合はnullを返す
    /// </summary>
    /// <param name="url">取得対象のURL</param>
    /// <returns>レスポンスHTML文字列。取得失敗時はnull</returns>
    private async Task<string?> TryGetStringAsync(string url)
    {
        try
        {
            return await GetHtmlAsync(url);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// box.htmlのHTMLドキュメントからチーム・スコア・試合ステータスを抽出する
    /// </summary>
    /// <param name="doc">box.htmlのHtmlDocument</param>
    /// <param name="detailUrl">試合詳細ページのURL（試合日パース用）</param>
    /// <returns>試合サマリー</returns>
    private GameResult ParseBoxSummary(HtmlDocument doc, string detailUrl)
    {
        // linescore の top(表=away) / bottom(裏=home) からチーム名・スコアを取得
        var awayTeamNode = doc.DocumentNode.SelectSingleNode(
            "//tr[@class='top']//span[@class='hide_sp']");
        var homeTeamNode = doc.DocumentNode.SelectSingleNode(
            "//tr[@class='bottom']//span[@class='hide_sp']");
        var awayScoreNode = doc.DocumentNode.SelectSingleNode(
            "//tr[@class='top']//td[@class='total-1']");
        var homeScoreNode = doc.DocumentNode.SelectSingleNode(
            "//tr[@class='bottom']//td[@class='total-1']");

        var awayTeam = NormalizeTeamName(awayTeamNode?.InnerText ?? "");
        var homeTeam = NormalizeTeamName(homeTeamNode?.InnerText ?? "");
        int? awayScore = int.TryParse(awayScoreNode?.InnerText.Trim(), out var a) ? a : null;
        int? homeScore = int.TryParse(homeScoreNode?.InnerText.Trim(), out var h) ? h : null;

        var gameInfoText = doc.DocumentNode.SelectSingleNode(
            "//p[@class='game_info']")?.InnerText ?? "";
        var isFinished = gameInfoText.Contains("試合終了");

        // 試合情報テキストとスコアの有無から試合ステータスを判定
        string status;
        if (isFinished)
        {
            status = "終了";
        }
        else if (awayScore.HasValue && homeScore.HasValue)
        {
            status = "試合中";
        }
        else
        {
            status = "試合前";
        }

        // detailUrl（例: https://npb.jp/games/2025/0621/gf/）から年・月・日を抽出して試合日を設定する
        var dateMatch = Regex.Match(detailUrl, @"/games/(\d{4})/(\d{2})(\d{2})/");
        var gameDate = dateMatch.Success
            && int.TryParse(dateMatch.Groups[1].Value, out var yr)
            && int.TryParse(dateMatch.Groups[2].Value, out var mo)
            && int.TryParse(dateMatch.Groups[3].Value, out var dy)
            ? new DateTime(yr, mo, dy)
            : DateTime.Today;

        return new GameResult
        {
            DetailUrl = detailUrl,
            AwayTeam = awayTeam,
            HomeTeam = homeTeam,
            AwayScore = awayScore,
            HomeScore = homeScore,
            GameDate = gameDate,
            Status = status
        };
    }

    /// <summary>
    /// box.htmlのHTMLドキュメントからビジター・ホームの打撃成績リストを取得する
    /// </summary>
    /// <param name="doc">box.htmlのHtmlDocument</param>
    /// <returns>（ビジター打撃成績リスト, ホーム打撃成績リスト）のタプル</returns>
    private (List<BattingLine> away, List<BattingLine> home) ParseBattingStats(HtmlDocument doc)
    {
        static List<BattingLine> ParseTable(HtmlDocument doc, string tableId)
        {
            var lines = new List<BattingLine>();
            var rows = doc.DocumentNode.SelectNodes(
                $"//div[@id='{tableId}']//tbody/tr");
            // テーブル行が存在しない場合は空リストを返す
            if (rows == null)
            {
                return lines;
            }

            // 各行を打者成績としてパース
            foreach (var row in rows)
            {
                var cols = row.SelectNodes(".//td");
                // 必要な列数（7列）に満たない行はスキップ
                if (cols == null || cols.Count < 7)
                {
                    continue;
                }

                // col: 打順,守備,選手(player),打数,得点,安打,打点,...
                var playerNode = row.SelectSingleNode(".//td[@class='player']//a")
                              ?? row.SelectSingleNode(".//td[@class='player']");
                var name = playerNode?.InnerText.Trim() ?? cols[2].InnerText.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                lines.Add(new BattingLine
                {
                    PlayerName = name,
                    AtBats = int.TryParse(cols[3].InnerText.Trim(), out var ab) ? ab : 0,
                    Runs   = int.TryParse(cols[4].InnerText.Trim(), out var r)  ? r  : 0,
                    Hits   = int.TryParse(cols[5].InnerText.Trim(), out var h)  ? h  : 0,
                    Rbi    = int.TryParse(cols[6].InnerText.Trim(), out var rbi)? rbi: 0,
                });
            }
            return lines;
        }

        // 表(top)=away打席、裏(bottom)=home打席
        return (ParseTable(doc, "table_top_b"), ParseTable(doc, "table_bottom_b"));
    }

    /// <summary>
    /// box.htmlのHTMLドキュメントからビジター・ホームの投手成績リストを取得する
    /// </summary>
    /// <param name="doc">box.htmlのHtmlDocument</param>
    /// <returns>（ビジター投手成績リスト, ホーム投手成績リスト）のタプル</returns>
    private (List<PitchingLine> away, List<PitchingLine> home) ParsePitchingStats(HtmlDocument doc)
    {
        static List<PitchingLine> ParseTable(HtmlDocument doc, string tableId)
        {
            var lines = new List<PitchingLine>();
            var rows = doc.DocumentNode.SelectNodes(
                $"//div[@id='{tableId}']//tbody/tr");
            // テーブル行が存在しない場合は空リストを返す
            if (rows == null)
            {
                return lines;
            }

            // 各行を投手成績としてパース
            foreach (var row in rows)
            {
                var cols = row.SelectNodes(".//td");
                // 必要な列数（14列）に満たない行はスキップ
                if (cols == null || cols.Count < 14)
                {
                    continue;
                }

                var marker = cols[0].InnerText.Trim();
                var playerNode = row.SelectSingleNode(".//td[@class='player']//a")
                              ?? row.SelectSingleNode(".//td[@class='player']");
                var name = playerNode?.InnerText.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                // 投球回は nested table_inning の th テキスト（整数部）
                var innNode = row.SelectSingleNode(".//table[@class='table_inning']//th");
                var innings = innNode?.InnerText.Trim() ?? "";

                // 自責点は col[13]
                var erText = cols[13].InnerText.Trim();

                var playerHref = playerNode?.GetAttributeValue("href", null);
                var playerUrl = playerHref != null
                    ? (playerHref.StartsWith("http") ? playerHref : BaseUrl + playerHref)
                    : null;

                lines.Add(new PitchingLine
                {
                    PlayerName = name,
                    InningsPitched = innings,
                    EarnedRuns = int.TryParse(erText, out var er) ? er : 0,
                    IsWin  = marker.Contains("○"),
                    IsLoss = marker.Contains("●"),
                    IsSave = marker == "S",
                    PlayerUrl = playerUrl,
                });
            }
            return lines;
        }

        // 表(top)=home投手、裏(bottom)=away投手
        return (ParseTable(doc, "table_bottom_p"), ParseTable(doc, "table_top_p"));
    }

    /// <summary>
    /// playbyplay.htmlのHTMLドキュメントから打点を含む得点シーンを抽出する
    /// </summary>
    /// <param name="doc">playbyplay.htmlのHtmlDocument</param>
    /// <returns>得点シーンのリスト</returns>
    private List<ScoringPlay> ParseScoringPlays(HtmlDocument doc)
    {
        var plays = new List<ScoringPlay>();
        var progressDiv = doc.DocumentNode.SelectSingleNode("//div[@id='progress']");
        if (progressDiv == null)
        {
            return plays;
        }

        var currentInning = 0;
        var currentIsTop = true;
        var currentTeam = "";

        // 進行表の各子ノード（見出し・テーブル）を順番に処理
        foreach (var node in progressDiv.ChildNodes)
        {
            // 回・表裏・チームの見出し要素からイニング情報を更新
            if (node.Name == "h5")
            {
                var text = node.InnerText.Trim();
                var inningMatch = Regex.Match(text, @"(\d+)回");
                var teamMatch   = Regex.Match(text, @"（(.+?)の攻撃）");
                if (inningMatch.Success)
                {
                    currentInning = int.Parse(inningMatch.Groups[1].Value);
                }
                currentIsTop = text.Contains("表");
                currentTeam = teamMatch.Success ? teamMatch.Groups[1].Value : "";
            }
            // 打席結果テーブルから得点シーンを抽出
            else if (node.Name == "table")
            {
                var rows = node.SelectNodes(".//tr");
                // 行が存在しない場合はスキップ
                if (rows == null)
                {
                    continue;
                }

                // 各打席行を検査して打点がある場合のみ得点シーンとして記録
                foreach (var row in rows)
                {
                    var cells = row.SelectNodes(".//td");
                    // 必要な列数（5列）に満たない行はスキップ
                    if (cells == null || cells.Count < 5)
                    {
                        continue;
                    }

                    var result = cells[4].InnerText.Trim();
                    // 打点を含む打席のみ得点シーンとして記録
                    if (!result.Contains("（打点"))
                    {
                        continue;
                    }

                    var batterNode = row.SelectSingleNode(".//td[3]//a");
                    var batter = batterNode?.InnerText.Trim() ?? cells[2].InnerText.Trim();

                    plays.Add(new ScoringPlay
                    {
                        Inning      = currentInning,
                        IsTop       = currentIsTop,
                        Team        = currentTeam,
                        Batter      = batter,
                        Description = result,
                    });
                }
            }
        }

        return plays;
    }

    /// <summary>
    /// box.htmlのHTMLドキュメントから予告先発投手名を取得する
    /// </summary>
    /// <param name="doc">box.htmlのHtmlDocument</param>
    /// <returns>（ビジター予告先発, ホーム予告先発）のタプル。情報がない場合はnull</returns>
    private static (string? Away, string? Home) ParseAnnouncedStarters(HtmlDocument doc)
    {
        // 予告先発行: <th>予告先発</th><td>away投手</td><td>home投手</td>
        var th = doc.DocumentNode.SelectSingleNode("//th[contains(text(),'予告先発')]");
        // 予告先発行が存在しない場合はnullを返す
        if (th == null)
        {
            return (null, null);
        }

        var tds = th.ParentNode.SelectNodes(".//td");
        // away/home両方の先発情報が揃わない場合はnullを返す
        if (tds == null || tds.Count < 2)
        {
            return (null, null);
        }

        var away = tds[0].InnerText.Trim();
        var home = tds[1].InnerText.Trim();
        return (
            string.IsNullOrEmpty(away) ? null : away,
            string.IsNullOrEmpty(home) ? null : home
        );
    }

    // -------------------------
    // 先発・勝敗投手（box.html、キャッシュ共有）
    // -------------------------

    /// <summary>
    /// 先発投手・勝敗投手・セーブ投手を取得する
    /// </summary>
    /// <param name="detailUrl">試合詳細ページのURL</param>
    /// <returns>away先発・home先発・勝利・敗戦・セーブ投手名のタプル。取得失敗時はすべてnull</returns>
    public async Task<(string? AwayStarter, string? HomeStarter, string? WinPitcher, string? LossPitcher, string? SavePitcher)>
        GetStartersFromDetailAsync(string detailUrl)
    {
        // GetGameDetailAsync と同じ box_ キャッシュを共有して二重取得を防ぐ
        var detail = await GetGameDetailAsync(detailUrl);
        // 詳細取得失敗時はすべてnullを返す
        if (detail == null)
        {
            return (null, null, null, null, null);
        }

        var all = detail.AwayPitching.Concat(detail.HomePitching);
        return (
            detail.AwayPitching.FirstOrDefault()?.PlayerName ?? detail.AwayAnnouncedStarter,
            detail.HomePitching.FirstOrDefault()?.PlayerName ?? detail.HomeAnnouncedStarter,
            all.FirstOrDefault(p => p.IsWin)?.PlayerName,
            all.FirstOrDefault(p => p.IsLoss)?.PlayerName,
            all.FirstOrDefault(p => p.IsSave)?.PlayerName
        );
    }

    // -------------------------
    // 先発投手ERA取得
    // -------------------------

    /// <summary>
    /// home・away両先発投手の今季防御率（ERA）を取得する
    /// </summary>
    /// <param name="detailUrl">試合詳細ページのURL</param>
    /// <returns>home先発ERA・away先発ERAのタプル。取得できない場合はnull</returns>
    public async Task<(double? HomeEra, double? AwayEra)> GetStarterErasAsync(string detailUrl)
    {
        var cacheKey = $"era_{detailUrl}";
        // キャッシュが存在する場合はスクレイピングを省略して返す
        if (cache.TryGetValue(cacheKey, out (double?, double?) cached))
        {
            return cached;
        }

        var detail = await GetGameDetailAsync(detailUrl);
        // 詳細取得失敗時はnullを返す
        if (detail == null)
        {
            return (null, null);
        }

        var homeStarter = detail.HomePitching.FirstOrDefault();
        var awayStarter = detail.AwayPitching.FirstOrDefault();

        var homeTask = homeStarter?.PlayerUrl != null
            ? FetchPlayerEraAsync(homeStarter.PlayerUrl)
            : Task.FromResult<double?>(null);
        var awayTask = awayStarter?.PlayerUrl != null
            ? FetchPlayerEraAsync(awayStarter.PlayerUrl)
            : Task.FromResult<double?>(null);

        await Task.WhenAll(homeTask, awayTask);

        var result = (homeTask.Result, awayTask.Result);
        var isFinished = detail.Summary.Status == "終了";
        cache.Set(cacheKey, result, isFinished ? TimeSpan.FromDays(1) : TimeSpan.FromHours(1));
        return result;
    }

    /// <summary>
    /// 選手ページから今季防御率（ERA）を取得する
    /// </summary>
    /// <param name="playerUrl">選手ページのURL</param>
    /// <returns>防御率。取得できない場合はnull</returns>
    private async Task<double?> FetchPlayerEraAsync(string playerUrl)
    {
        var cacheKey = $"player_era_{playerUrl}";
        // キャッシュが存在する場合はスクレイピングを省略して返す
        if (cache.TryGetValue(cacheKey, out double? cached))
        {
            return cached;
        }

        try
        {
            var html = await GetHtmlAsync(playerUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var era = ParseEraFromPlayerPage(doc);
            cache.Set(cacheKey, era, TimeSpan.FromHours(1));
            return era;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 選手ページのHTMLドキュメントから「防御率」列の値を読み取る
    /// </summary>
    /// <param name="doc">選手ページのHtmlDocument</param>
    /// <returns>防御率。該当列が見つからない場合はnull</returns>
    private static double? ParseEraFromPlayerPage(HtmlDocument doc)
    {
        // 投手成績テーブルの "防御率" 列を探す
        var tables = doc.DocumentNode.SelectNodes("//table");
        // テーブルが存在しない場合はERAなし
        if (tables == null)
        {
            return null;
        }

        // 各テーブルを調べて防御率列を含むものを探す
        foreach (var table in tables)
        {
            var headers = table.SelectNodes(".//thead//th | .//tr[1]//th");
            // ヘッダーなしのテーブルはスキップ
            if (headers == null)
            {
                continue;
            }

            var eraIdx = -1;
            // ヘッダーを走査して「防御率」列のインデックスを特定
            for (var i = 0; i < headers.Count; i++)
            {
                // 「防御率」ヘッダーが見つかったらインデックスを記録して終了
                if (headers[i].InnerText.Trim() == "防御率")
                {
                    eraIdx = i;
                    break;
                }
            }
            // 防御率列が見つからないテーブルはスキップ
            if (eraIdx < 0)
            {
                continue;
            }

            // 今季の行（最初の tbody tr）から防御率を取得
            var firstRow = table.SelectSingleNode(".//tbody//tr");
            // tbody行がない場合はスキップ
            if (firstRow == null)
            {
                continue;
            }

            var cols = firstRow.SelectNodes(".//td");
            // 列数が不足している場合はスキップ
            if (cols == null || cols.Count <= eraIdx)
            {
                continue;
            }

            var eraText = cols[eraIdx].InnerText.Trim();
            // 有効な防御率が取得できた場合のみ返す
            if (double.TryParse(eraText, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var era) && era >= 0)
            {
                return era;
            }
        }

        return null;
    }

    // -------------------------
    // 順位表（勝率）
    // -------------------------

    /// <summary>
    /// 全チームの現在勝率をリーグ順位表から取得する
    /// </summary>
    /// <returns>チーム名をキー、勝率を値とする辞書</returns>
    public async Task<Dictionary<string, double>> GetStandingsAsync()
    {
        const string cacheKey = "standings";

        // キャッシュが存在する場合はスクレイピングを省略して返す
        if (cache.TryGetValue(cacheKey, out Dictionary<string, double>? cached))
        {
            return cached!;
        }

        var standings = await ScrapeStandingsAsync();
        cache.Set(cacheKey, standings, TimeSpan.FromHours(1));
        return standings;
    }

    /// <summary>
    /// セ・パ両リーグの順位表ページをスクレイピングして全チームの勝率を取得する
    /// </summary>
    /// <returns>チーム名をキー、勝率を値とする辞書</returns>
    private async Task<Dictionary<string, double>> ScrapeStandingsAsync()
    {
        var result = new Dictionary<string, double>();
        // セ・パ両リーグのURLを順番にスクレイピング
        foreach (var leagueUrl in new[] { $"{BaseUrl}/cl/", $"{BaseUrl}/pl/" })
        {
            try
            {
                var html = await GetHtmlAsync(leagueUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var rows = doc.DocumentNode.SelectNodes(
                    "//div[@class='standing_table']//table//tbody//tr");
                // 順位表行が存在しない場合はスキップ
                if (rows == null)
                {
                    continue;
                }

                // 各チームの勝率を順位表から取得
                foreach (var row in rows)
                {
                    var teamNode = row.SelectSingleNode(".//th//span[@class='hide_sp']")
                                ?? row.SelectSingleNode(".//th");
                    var cols = row.SelectNodes(".//td");
                    // チーム名・勝率が取得できない行はスキップ
                    if (teamNode == null || cols == null || cols.Count < 5)
                    {
                        continue;
                    }

                    var team = NormalizeTeamName(teamNode.InnerText.Trim());
                    var winRateText = cols[4].InnerText.Trim();
                    if (double.TryParse(winRateText, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var winRate))
                    {
                        result[team] = winRate;
                    }
                }
            }
            catch { }
        }
        return result;
    }

    // -------------------------
    // チーム得失点（ピタゴラス勝率用）
    // -------------------------

    /// <summary>
    /// 全チームの今季総得点（RS）と総失点（RA）を取得する
    /// </summary>
    /// <returns>チーム名をキー、RS/RAのタプルを値とする辞書</returns>
    public async Task<Dictionary<string, (int RS, int RA)>> GetTeamRunStatsAsync()
    {
        const string cacheKey = "team_runs";

        // キャッシュが存在する場合はスクレイピングを省略して返す
        if (cache.TryGetValue(cacheKey, out Dictionary<string, (int, int)>? cached))
        {
            return cached!;
        }

        var stats = await ScrapeTeamRunStatsAsync();
        cache.Set(cacheKey, stats, TimeSpan.FromHours(1));
        return stats;
    }

    /// <summary>
    /// セ・パ両リーグのチーム打撃・投手成績ページをスクレイピングして全チームの総得点・総失点を取得する
    /// </summary>
    /// <returns>チーム名をキー、（RS=総得点, RA=総失点）のタプルを値とする辞書</returns>
    private async Task<Dictionary<string, (int RS, int RA)>> ScrapeTeamRunStatsAsync()
    {
        var result = new Dictionary<string, (int RS, int RA)>();
        var year = DateTime.Today.Year;

        // 各リーグのチーム打撃・投手成績ページを並行取得
        var urls = new[]
        {
            ($"{BaseUrl}/bis/{year}/stats/tmb_c.html", true,  true),   // CL打撃(RS)
            ($"{BaseUrl}/bis/{year}/stats/tmp_c.html", true,  false),  // CL投手(RA)
            ($"{BaseUrl}/bis/{year}/stats/tmb_p.html", false, true),   // PL打撃(RS)
            ($"{BaseUrl}/bis/{year}/stats/tmp_p.html", false, false),  // PL投手(RA)
        };

        var tasks = urls.Select(u => GetHtmlAsync(u.Item1)).ToArray();
        try
        {
            await Task.WhenAll(tasks);
        }
        catch { }

        // 各ページの取得結果を処理してチームごとの得失点を集計
        for (var i = 0; i < tasks.Length; i++)
        {
            // 取得失敗したページはスキップ
            if (!tasks[i].IsCompletedSuccessfully)
            {
                continue;
            }
            var (_, _, isRS) = urls[i];

            var doc = new HtmlDocument();
            doc.LoadHtml(tasks[i].Result);
            var rows = doc.DocumentNode.SelectNodes("//table//tbody//tr[@class='ststats']");
            // 統計行が存在しない場合はスキップ
            if (rows == null)
            {
                continue;
            }

            // 各チームの行から得点または失点を取得
            foreach (var row in rows)
            {
                var cols = row.SelectNodes(".//td");
                // 列が存在しない行はスキップ
                if (cols == null)
                {
                    continue;
                }

                var teamRaw = cols[0].InnerText.Trim();
                // リンクテキストの場合はaタグのInnerText
                var teamLink = row.SelectSingleNode(".//td[1]//a");
                if (teamLink != null)
                {
                    teamRaw = teamLink.InnerText.Trim();
                }
                var team = NormalizeTeamName(teamRaw);

                // チーム打撃ページの場合は得点(RS)を取得
                if (isRS)
                {
                    // チーム打撃: col[5]=得点
                    if (cols.Count > 5 && int.TryParse(cols[5].InnerText.Trim(), out var rs))
                    {
                        if (!result.ContainsKey(team))
                        {
                            result[team] = (0, 0);
                        }
                        result[team] = (rs, result[team].RA);
                    }
                }
                // チーム投手ページの場合は失点(RA)を取得
                else
                {
                    // チーム投手: col[22]=失点
                    if (cols.Count > 22 && int.TryParse(cols[22].InnerText.Trim(), out var ra))
                    {
                        if (!result.ContainsKey(team))
                        {
                            result[team] = (0, 0);
                        }
                        result[team] = (result[team].RS, ra);
                    }
                }
            }
        }

        return result;
    }
}
