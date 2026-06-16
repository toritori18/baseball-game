# Initial setup script

Write-Host "=== Setup ===" -ForegroundColor Cyan

# Check Node.js
Write-Host "[1/3] Checking Node.js version..."
node --version
if (-not $?) {
    Write-Host "ERROR: Node.js is not installed. Please install from https://nodejs.org" -ForegroundColor Red
    exit 1
}

# Install dependencies
Write-Host "[2/3] Installing dependencies..."
npm install
if (-not $?) {
    Write-Host "ERROR: npm install failed." -ForegroundColor Red
    exit 1
}

# Create .env.local
Write-Host "[3/3] Creating .env.local..."
if (Test-Path ".env.local") {
    Write-Host ".env.local already exists. Skipping." -ForegroundColor Yellow
} else {
    Copy-Item ".env.example" ".env.local"
    Write-Host ".env.local created. Please set your credentials." -ForegroundColor Green
}

Write-Host "=== Setup complete ===" -ForegroundColor Cyan
Write-Host "Next steps:"
Write-Host "  1. Edit .env.local and set credentials"
Write-Host "  2. Run: npm run dev"
Write-Host "  3. Open: http://localhost:3000"
