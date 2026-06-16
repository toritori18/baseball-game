param(
    [Parameter(Mandatory=$true)]
    [string]$name
)

# ブランチ名の規則チェック（feature/ fix/ docs/ のみ許可）
if ($name -notmatch '^(feature|fix|docs)/.+') {
    Write-Host "ERROR: Branch name must match feature/<name>, fix/<name>, or docs/<name>." -ForegroundColor Red
    Write-Host "  See docs/git-rules.md for details." -ForegroundColor Red
    exit 1
}

git checkout -b $name
if (-not $?) {
    Write-Host "ERROR: Failed to create branch '$name'." -ForegroundColor Red
    exit 1
}

Write-Host "Created and switched to branch: $name" -ForegroundColor Green
