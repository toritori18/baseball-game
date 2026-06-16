$conn = Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($conn) {
    $procId = $conn.OwningProcess
    Stop-Process -Id $procId -Force
    Write-Host "Stopped dev server (PID: $procId)" -ForegroundColor Yellow
} else {
    Write-Host "No server running on port 3000" -ForegroundColor Gray
}
