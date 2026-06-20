$conn = Get-NetTCPConnection -LocalPort 5198 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($conn) {
    $procId = $conn.OwningProcess
    Stop-Process -Id $procId -Force
    Write-Host "サーバーを停止しました (PID: $procId)" -ForegroundColor Yellow
} else {
    Write-Host "ポート5198でサーバーは起動していません" -ForegroundColor Gray
}
