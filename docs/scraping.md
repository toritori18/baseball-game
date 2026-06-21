# スクレイピング設計

## 取得先 URL と用途

| URL | 用途 | キャッシュ TTL |
|---|---|---|
| `/games/{year}/` | 当日試合一覧 | 5分 |
| `/scores/{year}/MMDD/{code}/box.html` | スコア・打撃・投手成績・得点シーン | 終了試合7日 / 進行中10分 |
| `/scores/{year}/MMDD/{code}/playbyplay.html` | 得点シーン（打点付き打席） | box.html と同キャッシュ |
| `/cl/` `/pl/` | リーグ順位表（勝率） | 1時間 |
| `/bis/{year}/stats/tmb_c.html` 等 4ページ | チーム得点(RS)/失点(RA) | 1時間（キー: `team_runs`） |
| `/bis/{year}/players/{id}.html` | 先発投手の防御率(ERA) | 1時間（選手ページごと） |

## box.html のテーブル ID とチーム対応

```
div#table_top_b     → away 打撃
div#table_bottom_b  → home 打撃
div#table_top_p     → home 投手（表=ビジター攻撃=ホーム投球）
div#table_bottom_p  → away 投手
```

`tr.top` = 客（away）、`tr.bottom` = 主（home）。

## ピタゴラス勝率（ERA補正あり）

```
homeAdj = homeRS × awayStarterERA
awayAdj = awayRS × homeStarterERA
homeWin% = homeAdj² / (homeAdj² + awayAdj²)
```

ERA が取得できない先発にはリーグ平均 3.80 を代入。
