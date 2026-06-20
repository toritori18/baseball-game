$conn = Get-NetTCPConnection -LocalPort 5198 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($conn) {
    $procId = $conn.OwningProcess
    Stop-Process -Id $procId -Force
    Write-Host "Stopped existing server (PID: $procId)" -ForegroundColor Yellow
}

$dll = "BaseballGame\bin\Debug\net9.0\BaseballGame.dll"
$needsBuild = $true

if (Test-Path $dll) {
    $dllTime = (Get-Item $dll).LastWriteTime
    $srcFiles = Get-ChildItem -Path "BaseballGame" -Recurse -Include "*.cs","*.razor","*.csproj" |
        Where-Object { $_.LastWriteTime -gt $dllTime }
    if ($srcFiles.Count -eq 0) {
        $needsBuild = $false
        Write-Host "No changes detected -- skipping build" -ForegroundColor Cyan
    } else {
        Write-Host "Changes detected ($($srcFiles.Count) files) -- building first" -ForegroundColor Cyan
    }
}

if ($needsBuild) {
    dotnet run --project BaseballGame
} else {
    dotnet run --project BaseballGame --no-build
}
