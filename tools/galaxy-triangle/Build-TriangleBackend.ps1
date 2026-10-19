param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [switch]$WithTestFixtures
)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path -LiteralPath $SourceDirectory).Path
$pinned = @{
    'triangle.c' = 'F8164D23C7F4CECB58CA84784558E5BADEFD39CEF9A9AC142C752C482A238065'
    'triangle.h' = '4E945DCC26CD892BFBB0C71B2CBADC5882625291AF7C798F645E5E86DAD7C594'
}
foreach ($name in $pinned.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $sourceRoot $name) -Algorithm SHA256).Hash -ne $pinned[$name]) {
        throw "Неверный SHA256 official Triangle1.6: $name"
    }
}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsRoot = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vsRoot) { throw 'Требуется уже установленный MSVC x64; script ничего не устанавливает.' }
$vcvars = Join-Path $vsRoot 'VC/Auxiliary/Build/vcvars64.bat'
# Единственный cmd используется для штатной настройки compiler environment, без file mutations.
$nativeEnvironment = & $env:ComSpec /d /s /c "call `"$vcvars`" >nul && set"
if ($LASTEXITCODE -ne 0) { throw 'Не удалось получить MSVC environment.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$buildRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
$upstream = [IO.File]::ReadAllText((Join-Path $sourceRoot 'triangle.c'))
# LLP64 port: только address casts/alignment и соответствующий diagnostic format.
# randomseed/randomnation остаются unsigned long32; REAL/predicates/algorithm неизменны.
if ([regex]::Matches($upstream, '\(unsigned long\)').Count -ne 61 -or
    [regex]::Matches($upstream, 'unsigned long alignptr;').Count -ne 7 -or
    [regex]::Matches($upstream, '%lx').Count -ne 19) { throw 'Неожиданный pointer port surface.' }
$ported = "/* Windows x64 LLP64 port: address casts/alignment -> uintptr_t; diagnostic %lx -> %llx. */`n#include <stdint.h>`n" +
    $upstream.Replace('(unsigned long)', '(uintptr_t)').Replace('unsigned long alignptr;', 'uintptr_t alignptr;').Replace('%lx', '%llx')
$portedPath = Join-Path $buildRoot 'triangle-windows-x64.c'
[IO.File]::WriteAllText($portedPath, $ported, [Text.UTF8Encoding]::new($false))
$compiler = [Diagnostics.ProcessStartInfo]::new('cl.exe')
$compiler.UseShellExecute = $false
$compiler.WorkingDirectory = $buildRoot
foreach ($line in $nativeEnvironment) {
    $separator = $line.IndexOf('=')
    if ($separator -gt 0) { $compiler.Environment[$line.Substring(0, $separator)] = $line.Substring($separator + 1) }
}
# Upstream immutable; compiled copy содержит только явный LLP64 port выше.
# SINGLE обязателен и для predicates, и для координат/центров.
foreach ($argument in @('/nologo', '/TC', '/O2', '/fp:strict', '/std:c11', '/w', '/DTRILIBRARY', '/DSINGLE', '/DNO_TIMER',
    '/DANSI_DECLARATORS', '/D_CRT_SECURE_NO_WARNINGS', "/I$sourceRoot", $portedPath,
    (Join-Path $PSScriptRoot 'Worker.c'), '/Fe:stellaris-triangle-worker.exe', '/link', '/Brepro', '/INCREMENTAL:NO')) {
    $compiler.ArgumentList.Add($argument)
}
# Process.Start не использует PATH из child Environment при поиске executable.
$compiler.FileName = Join-Path ($compiler.Environment['VCToolsInstallDir']) 'bin/Hostx64/x64/cl.exe'
$process = [Diagnostics.Process]::Start($compiler)
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Triangle build failed: $($process.ExitCode)" }
$worker = Join-Path $buildRoot 'stellaris-triangle-worker.exe'
[ordered]@{
    SourceSha256 = $pinned
    PortedSourceSha256 = (Get-FileHash -LiteralPath $portedPath -Algorithm SHA256).Hash
    WorkerSha256 = (Get-FileHash -LiteralPath $worker -Algorithm SHA256).Hash
    Compiler = $compiler.FileName
    Flags = @($compiler.ArgumentList)
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $buildRoot 'build-manifest.json') -Encoding utf8
Get-FileHash -LiteralPath $worker -Algorithm SHA256
if ($WithTestFixtures) {
    $compiler.ArgumentList.Clear()
    foreach ($argument in @('/nologo', '/TC', '/O2', '/W4', '/WX', '/std:c11',
        (Join-Path $PSScriptRoot '../../tests/Stellaris.GalaxyGenerationChecks/TriangleFailureWorker.c'),
        '/Fe:triangle-failure-worker.exe', '/link', '/Brepro', '/INCREMENTAL:NO')) { $compiler.ArgumentList.Add($argument) }
    $fixtureProcess = [Diagnostics.Process]::Start($compiler)
    $fixtureProcess.WaitForExit()
    if ($fixtureProcess.ExitCode -ne 0) { throw 'Failure fixture build failed.' }
}
