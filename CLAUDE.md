# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

詳細は [README.md](README.md) 参照。

## コマンド

```powershell
# 開発サーバー起動（ポート 5198。既存プロセスを自動停止して再起動）
.\.claude\commands\server\start.ps1

# ビルドのみ（サーバー起動せずコンパイルエラー確認）
dotnet build BaseballGame/BaseballGame.csproj --output "$env:TEMP\bb_build_check"

# 実行（ビルドあり）
dotnet run --project BaseballGame

# 実行（ビルドスキップ）
dotnet run --project BaseballGame --no-build
```

テストプロジェクトは存在しない。

## 技術スタック

詳細は [docs/tech-stack.md](docs/tech-stack.md) 参照。

## ディレクトリ構成

```
baseball-game/
├── BaseballGame/
│   ├── Components/
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor
│   │   │   └── NavMenu.razor
│   │   ├── Pages/
│   │   │   ├── Home.razor              ← 試合一覧画面
│   │   │   ├── GameDetailPage.razor    ← 詳細画面（予想・打撃・投手・得点シーン）
│   │   │   └── Error.razor
│   │   ├── BattingTable.razor          ← 打撃成績テーブルコンポーネント
│   │   └── PitchingTable.razor         ← 投手成績テーブルコンポーネント
│   ├── Models/
│   │   ├── GameResult.cs               ← 試合一覧用サマリーモデル
│   │   ├── GameDetail.cs               ← 詳細画面用モデル（打撃・投手・得点シーン）
│   │   └── GamePrediction.cs           ← 勝率予想モデル（RS/RA・ERA・勝敗投手）
│   ├── Services/
│   │   ├── NpbScraperService.cs        ← スクレイピング・キャッシュ一元管理
│   │   └── PredictionService.cs        ← ピタゴラス勝率計算
│   ├── Program.cs                      ← DI 設定・ミドルウェア
│   └── BaseballGame.csproj
├── .claude/
│   ├── commands/                       ← スラッシュコマンド定義
│   │   └── server/start.ps1            ← 開発サーバー起動スクリプト
│   ├── factcheck.md                    ← ハルシネーション防止ルール（フックで自動参照）
│   └── settings.json                   ← Claude Code 設定・フック定義
├── CLAUDE.md
└── BaseballGame.sln
```

## スクレイピング設計

詳細は [docs/scraping.md](docs/scraping.md) 参照。

## Git ルール

詳細は [docs/git-rules.md](docs/git-rules.md) 参照。

## コーディング規約

- コメントは**日本語**で書く
- シークレット・API キーはコードに直書きしない（`.env.local` 経由）
