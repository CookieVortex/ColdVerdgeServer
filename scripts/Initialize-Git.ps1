param(
    [string]$RemoteUrl = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git не найден. Установите Git и повторите команду."
}

$forbiddenTrackedPatterns = @(
    '^\.env$',
    '^\.env\.',
    'appsettings\.Production\.json$',
    '\.(pfx|p12|pem|key)$',
    '(^|/)(bin|obj)/'
)

$stagedOrTracked = git ls-files --cached 2>$null
foreach ($file in $stagedOrTracked) {
    foreach ($pattern in $forbiddenTrackedPatterns) {
        if ($file -match $pattern -and $file -ne '.env.example') {
            throw "Опасный или генерируемый файл уже отслеживается Git: $file"
        }
    }
}

$secretPatterns = @(
    'AKIA[0-9A-Z]{16}',
    'ASIA[0-9A-Z]{16}',
    'BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY',
    'POSTGRES_PASSWORD\s*=\s*(?!CHANGE_ME|replace_with|$).+'
)

$filesToScan = Get-ChildItem -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\(bin|obj|\.git|\.vs)\\' -and
    $_.Name -ne '.env' -and
    $_.Length -lt 2MB
}

foreach ($file in $filesToScan) {
    $content = Get-Content -Raw -ErrorAction SilentlyContinue $file.FullName
    if ($null -eq $content) { continue }

    foreach ($pattern in $secretPatterns) {
        if ($content -match $pattern) {
            throw "Найден возможный секрет в файле: $($file.FullName)"
        }
    }
}

if (-not (Test-Path .git)) {
    git init
}

git branch -M main
git add .

$ignoredEnv = git check-ignore .env 2>$null
if (-not $ignoredEnv) {
    throw ".env не игнорируется. Остановлено до исправления .gitignore."
}

if (git diff --cached --quiet) {
    Write-Host "Нет новых файлов для коммита."
} else {
    git commit -m "Initial ColdVerdge server"
}

if ($RemoteUrl) {
    $existingOrigin = git remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0) {
        git remote set-url origin $RemoteUrl
    } else {
        git remote add origin $RemoteUrl
    }

    git push -u origin main
}

Write-Host "Готово. Проверьте: git status"
