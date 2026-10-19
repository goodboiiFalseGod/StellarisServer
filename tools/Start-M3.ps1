param(
    [Parameter(Mandatory = $true)][string]$GameRoot,
    [Parameter(Mandatory = $true)][string]$TriangleWorker,
    [ValidateRange(1024, 65535)][int]$Port = 5089,
    [string]$JoinCode = 'M3-local-test-code'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$workerPath = (Resolve-Path -LiteralPath $TriangleWorker).Path
$gamePath = (Resolve-Path -LiteralPath $GameRoot).Path
if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) {
    throw "Порт $Port уже занят. Выберите другой -Port."
}
$outputPath = Join-Path $projectRoot "artifacts/local-m3-$Port"
& dotnet publish (Join-Path $projectRoot 'src/Stellaris.Server') -c Release -o $outputPath -p:BuildInParallel=false
if ($LASTEXITCODE -ne 0) { throw 'Сборка не выполнена. При отсутствии зависимостей выполните pnpm --dir tools/asset-probe install --frozen-lockfile --ignore-scripts.' }
$oldWorker = $env:STELLARIS_TRIANGLE_WORKER
$oldHash = $env:STELLARIS_TRIANGLE_WORKER_SHA256
try {
    $env:STELLARIS_TRIANGLE_WORKER = $workerPath
    $env:STELLARIS_TRIANGLE_WORKER_SHA256 = (Get-FileHash -LiteralPath $workerPath -Algorithm SHA256).Hash
    Write-Host "Клиент: http://127.0.0.1:$Port/m3.html"
    Write-Host 'Остановка: Ctrl+C. Перезапуск завершает текущую партию.'
    & dotnet (Join-Path $outputPath 'Stellaris.Server.dll') --mode m3 --game-root $gamePath --urls "http://127.0.0.1:$Port" --join-code $JoinCode
    if ($LASTEXITCODE -ne 0) { throw 'Сервер завершился с ошибкой.' }
}
finally {
    $env:STELLARIS_TRIANGLE_WORKER = $oldWorker
    $env:STELLARIS_TRIANGLE_WORKER_SHA256 = $oldHash
}
