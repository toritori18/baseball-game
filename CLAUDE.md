# CLAUDE.md — {{プロジェクト名}}

## プロジェクト概要

詳細は [README.md](README.md) を参照。このファイルはClaude Codeがプロジェクトを理解するためのガイドです。

## 技術スタック

| 役割 | 技術 |
|---|---|

詳細は [docs/tech-stack.md](docs/tech-stack.md) を参照してください。

## Git運用ルール

Gitに関するルールは以下のファイルを参照してください:

**[docs/git-rules.md](docs/git-rules.md)**

## ディレクトリ構成

```
{{プロジェクト名}}/
├── CLAUDE.md               # このファイル
├── README.md               # プロジェクト説明
├── package.json            # 依存関係・スクリプト
├── .gitignore
├── .env.example            # 環境変数のサンプル
├── .claude/                # Claude Code設定
│   ├── settings.json       # 権限設定
│   └── commands/           # カスタムスラッシュコマンド（.md + 実行スクリプト）
│       ├── git/            # /git:branch、/git:push
│       ├── server/         # /server:start、/server:stop
│       ├── db/             # /db:migrate
│       ├── setup.md        # /setup
│       ├── build.md        # /build
│       ├── lint.md         # /lint
│       └── deploy.md       # /deploy
├── src/                    # ソースコード
│   ├── components/         # UIコンポーネント
│   ├── pages/              # ページ
│   ├── utils/              # ユーティリティ
│   ├── assets/             # 画像・フォントなどの静的リソース
│   └── styles/             # スタイルシート
├── docs/                   # ドキュメント
│   ├── git-rules.md        # Git運用ルール
│   ├── tech-stack.md       # 技術スタック
│   ├── setup.md            # セットアップガイド
│   ├── contributing.md     # コントリビュートガイド
│   └── sql/                # SQLファイル（マイグレーション・初期データ等）
```

## コーディング規約・禁止事項

詳細は [docs/contributing.md](docs/contributing.md) を参照してください。

コードを書く際に特に注意すること：

- コメントは日本語で書く
- APIキー・シークレットはコードに直書きせず、`.env.local` 経由で参照する
- SQLファイルは `docs/sql/` フォルダに作成する
