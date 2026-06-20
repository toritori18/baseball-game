# CLAUDE.md — プロ野球試合結果トラッカー

## プロジェクト概要

詳細は [README.md](README.md) を参照。このファイルはClaude Codeがプロジェクトを理解するためのガイドです。

## 技術スタック

| 役割 | 技術 |
|---|---|
| UI / サーバー | ASP.NET Core Blazor Server (.NET 8) |
| スクレイピング | HtmlAgilityPack |
| データ管理 | IMemoryCache |
| スタイル | Bootstrap 5 |

詳細は [docs/tech-stack.md](docs/tech-stack.md) を参照してください。

## Git運用ルール

Gitに関するルールは以下のファイルを参照してください:

**[docs/git-rules.md](docs/git-rules.md)**

## ディレクトリ構成

```
baseball-game/
├── CLAUDE.md               # このファイル
├── README.md               # プロジェクト説明
├── BaseballGame.sln
├── .gitignore
├── .claude/                # Claude Code設定
│   ├── settings.json       # 権限設定
│   └── commands/           # カスタムスラッシュコマンド
│       ├── git/            # /git:branch、/git:push
│       ├── server/         # /server:start、/server:stop
│       ├── setup.md        # /setup
│       ├── build.md        # /build
│       └── lint.md         # /lint
├── BaseballGame/           # Blazor Serverプロジェクト
│   ├── BaseballGame.csproj
│   ├── Program.cs
│   ├── App.razor
│   ├── Components/
│   │   ├── Layout/         # レイアウトコンポーネント
│   │   └── Pages/          # ページコンポーネント
│   ├── Services/           # スクレイピング・ビジネスロジック
│   ├── Models/             # データモデル
│   └── wwwroot/            # 静的ファイル
└── docs/                   # ドキュメント
    ├── git-rules.md        # Git運用ルール
    ├── tech-stack.md       # 技術スタック
    ├── setup.md            # セットアップガイド
    └── contributing.md     # コントリビュートガイド
```

## コーディング規約・禁止事項

詳細は [docs/contributing.md](docs/contributing.md) を参照してください。

コードを書く際に特に注意すること：

- コメントは日本語で書く
- APIキー・シークレットはコードに直書きせず、`.env.local` 経由で参照する
- SQLファイルは `docs/sql/` フォルダに作成する
