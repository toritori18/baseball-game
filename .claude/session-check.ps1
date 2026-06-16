# セッション開始時チェック：必須項目が未入力の場合にClaudeへ通知する

$missing = @()

# GOAL.md チェック
if (-not (Test-Path "GOAL.md")) {
    $missing += "・GOAL.md が未設定です。/goal <目標> で今セッションのゴールを設定してください。"
}

# README.md チェック
if (-not (Test-Path "README.md")) {
    $missing += "・README.md が存在しません。プロジェクト概要を記載してください。"
} elseif (Select-String -Path "README.md" -Pattern "\{\{" -Quiet) {
    $missing += "・README.md にプレースホルダーが残っています。プロジェクト情報を入力してください。"
}

# 技術スタック チェック
if (-not (Test-Path "docs/tech-stack.md")) {
    $missing += "・docs/tech-stack.md が存在しません。技術スタックを記載してください。"
} elseif (Select-String -Path "docs/tech-stack.md" -Pattern "\{\{例:" -Quiet) {
    $missing += "・docs/tech-stack.md の技術スタックが未入力です。プレースホルダーを実際の技術に書き換えてください。"
}

if ($missing.Count -gt 0) {
    $list = $missing -join "\n"
    $message = "作業を始める前に以下を設定してください:\n$list"
    $json = [PSCustomObject]@{
        hookSpecificOutput = [PSCustomObject]@{
            hookEventName   = "SessionStart"
            additionalContext = $message
        }
    } | ConvertTo-Json -Compress
    Write-Output $json
}
