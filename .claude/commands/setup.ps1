# Initial setup script

Write-Host "=== Setup ===" -ForegroundColor Cyan

# .NET 8 SDK の確認
Write-Host "[1/3] Checking .NET SDK version..."
$dotnetVersion = dotnet --version 2>$null
if (-not $?) {
    Write-Host "ERROR: .NET SDK がインストールされていません。https://dotnet.microsoft.com/download からインストールしてください。" -ForegroundColor Red
    exit 1
}
if (-not $dotnetVersion.StartsWith("8.")) {
    Write-Host "ERROR: .NET 8 SDK が必要です。現在のバージョン: $dotnetVersion" -ForegroundColor Red
    exit 1
}
Write-Host ".NET SDK $dotnetVersion を確認しました。" -ForegroundColor Green

# パッケージの復元
Write-Host "[2/3] Restoring packages..."
dotnet restore
if (-not $?) {
    Write-Host "ERROR: dotnet restore に失敗しました。" -ForegroundColor Red
    exit 1
}

# .env.local の作成
Write-Host "[3/3] Creating .env.local..."
if (Test-Path ".env.local") {
    Write-Host ".env.local already exists. Skipping." -ForegroundColor Yellow
} else {
    Copy-Item ".env.example" ".env.local"
    Write-Host ".env.local を作成しました。必要に応じて認証情報を設定してください。" -ForegroundColor Green
}

Write-Host "=== Setup complete ===" -ForegroundColor Cyan
Write-Host "Next steps:"
Write-Host "  1. .env.local を編集して必要な設定を入力"
Write-Host "  2. dotnet run --project BaseballGame を実行"
Write-Host "  3. ブラウザで https://localhost:5001 を開く"
