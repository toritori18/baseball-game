# プロ野球試合結果トラッカー

NPB（日本プロ野球）の全試合結果を自動取得・表示するローカル専用Webアプリ。読売ジャイアンツの試合をハイライト表示し、勝敗予想も確認できる。

## 機能

- 今日の全試合結果一覧表示（ジャイアンツ戦をハイライト）
- 試合詳細画面（打者成績・投手成績・得点シーン）
- 勝敗予想（ピタゴラス勝率 + 先発投手ERA）

## 技術スタック

詳細は [docs/tech-stack.md](docs/tech-stack.md) を参照してください。

## セットアップ

詳細は [docs/setup.md](docs/setup.md) を参照してください。

```bash
dotnet restore BaseballGame/BaseballGame.csproj
dotnet run --project BaseballGame
```

起動後、ブラウザで `https://localhost:5001` を開く。

## 注意事項

- ローカル専用・個人利用・非商用
- npb.jp のデータを使用しているため外部公開・再配布は禁止
- 本アプリは個人学習目的で作成しています。npb.jp へのアクセスはリクエスト間隔を設けた最小限のスクレイピングのみ行っており、取得したデータの再配布・商用利用は一切行いません。

## ドキュメント

- [セットアップガイド](docs/setup.md)
- [技術スタック](docs/tech-stack.md)
- [Git運用ルール](docs/git-rules.md)
- [コントリビュートガイド](docs/contributing.md)
