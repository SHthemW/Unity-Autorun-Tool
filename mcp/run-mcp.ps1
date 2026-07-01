$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolRoot = Split-Path -Parent $scriptDir
$projectPath = Join-Path $toolRoot "mcp~\UnityAutorun.Mcp\UnityAutorun.Mcp.csproj"
$runId = "{0}-{1}" -f [DateTime]::UtcNow.ToString("yyyyMMddHHmmssfff"), $PID
$outputRoot = Join-Path ([IO.Path]::GetTempPath()) "UnityAutorun.Mcp"
$outputDir = Join-Path $outputRoot $runId

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$buildOutput = & dotnet build $projectPath -p:UseAppHost=false -o $outputDir --nologo -v:q 2>&1
if ($LASTEXITCODE -ne 0)
{
    [Console]::Error.WriteLine(($buildOutput -join [Environment]::NewLine))
    exit $LASTEXITCODE
}

try
{
    & dotnet (Join-Path $outputDir "UnityAutorun.Mcp.dll") mcp
    exit $LASTEXITCODE
}
finally
{
    Remove-Item -LiteralPath $outputDir -Recurse -Force -ErrorAction SilentlyContinue
}
