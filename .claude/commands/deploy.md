本番環境へのデプロイを行います。

## 自動デプロイ（推奨）

`main` ブランチへのプッシュでホスティングサービスが自動的にデプロイします。

```powershell
# feature ブランチの変更を main にマージしてプッシュ
git checkout main
git merge <branch-name>
git push origin main
```

## 手動デプロイ

### Vercel の場合

```powershell
# プレビューデプロイ
npx vercel

# 本番デプロイ
npx vercel --prod
```

## デプロイ前チェックリスト

デプロイ前に以下を確認してください:

1. 型チェック・静的解析が通ること: `/lint`
2. 本番ビルドが通ること: `/build`
3. `.env.local` の環境変数がホスティングサービスのプロジェクト設定にも登録されていること
