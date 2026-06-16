データベースのマイグレーションを実行してください。

実行対象フォルダ: docs/sql/

プロジェクトのDBに応じて以下の手順を実行してください:

## Supabase の場合
1. Supabase Dashboard > SQL Editor を開く
2. docs/sql/ フォルダ内の SQL ファイルを順番にコピーして貼り付ける
3. 「Run」ボタンをクリックして実行する
4. Table Editor でテーブルが存在することを確認する

## その他のDB（Prisma / Drizzle 等）の場合
プロジェクトのマイグレーションコマンドを使用してください:

```powershell
# 例: Prisma
npx prisma migrate dev

# 例: Drizzle
npx drizzle-kit push
```

注意事項:
- テーブルがすでに存在する場合は `IF NOT EXISTS` により安全にスキップされます
- 本番環境では事前にバックアップを取得してください
