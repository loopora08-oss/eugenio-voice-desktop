param([string]$Dotnet = 'dotnet', [string]$Destination = (Join-Path $PSScriptRoot '../VoceLocale'))
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$Destination = [System.IO.Path]::GetFullPath($Destination)
$projects = @(
  @{ Project='App/VoceLocale.csproj'; Target=$Destination },
  @{ Project='WhisperWorker/WhisperWorker.csproj'; Target=(Join-Path $Destination 'engines/WhisperWorker') },
  @{ Project='RewriteWorker/RewriteWorker.csproj'; Target=(Join-Path $Destination 'engines/RewriteWorker') }
)
foreach ($item in $projects) {
  & $Dotnet publish (Join-Path $PSScriptRoot $item.Project) -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:RestoreLockedMode=true -o $item.Target
  if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($item.Project)" }
}
$testOutput = Join-Path $PSScriptRoot 'Tests/bin/package-checks'
& $Dotnet build (Join-Path $PSScriptRoot 'Tests/Tests.csproj') -c Release -o $testOutput
if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
& $Dotnet (Join-Path $testOutput 'Tests.dll')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $Destination 'LEGGIMI.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY.md') -Destination $Destination
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VERIFICA.md') -Destination $Destination
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $Destination -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $Destination -Recurse -Force
