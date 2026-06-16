param(
    [Parameter(Mandatory=$true)]
    [string]$message
)

# .env.local などシークレットファイルが追跡されていないか確認
$secrets = git ls-files --error-unmatch .env.local 2>$null
if ($secrets) {
    Write-Host "ERROR: .env.local is tracked by git. Remove it with: git rm --cached .env.local" -ForegroundColor Red
    exit 1
}

# 追跡済みファイルのみステージング（未追跡の新規ファイルは含めない）
git add -u
if (-not $?) {
    Write-Host "ERROR: git add failed." -ForegroundColor Red
    exit 1
}

# 新規ファイル（未追跡）があれば警告して表示
$untracked = git ls-files --others --exclude-standard
if ($untracked) {
    Write-Host "WARNING: The following new files are NOT staged (add manually if needed):" -ForegroundColor Yellow
    $untracked | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
}

# コミット
git commit -m $message
if (-not $?) {
    Write-Host "ERROR: git commit failed." -ForegroundColor Red
    exit 1
}

# 現在のブランチを取得
$branch = git rev-parse --abbrev-ref HEAD

# main への直接プッシュは git-rules.md により禁止（fix/ ブランチをマージ後にのみ許可）
if ($branch -eq "main") {
    Write-Host "ERROR: Direct push to main is not allowed. Follow git-rules.md:" -ForegroundColor Red
    Write-Host "  - Feature: push to feature/<name> and create a PR" -ForegroundColor Red
    Write-Host "  - Bug fix: merge fix/<name> into main locally, then push" -ForegroundColor Red
    exit 1
}

git push origin $branch
if (-not $?) {
    Write-Host "ERROR: git push failed." -ForegroundColor Red
    exit 1
}

Write-Host "Pushed to origin/$branch" -ForegroundColor Green
