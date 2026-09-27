param(
    [Parameter(Mandatory)][string]$PublishedDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ModelsDirectory
)
$ErrorActionPreference = 'Stop'
$PublishedDirectory = (Resolve-Path -LiteralPath $PublishedDirectory).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$stage = Join-Path $OutputDirectory 'Eugenio-Intune'
if (Test-Path -LiteralPath $stage) {
    $expectedParent = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if (-not $resolvedStage.StartsWith($expectedParent + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Staging path escaped output directory.' }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
New-Item -ItemType Directory -Path (Join-Path $stage 'payload') -Force | Out-Null
Get-ChildItem -LiteralPath $PublishedDirectory -Force | Copy-Item -Destination (Join-Path $stage 'payload') -Recurse -Force
if (-not (Test-Path -LiteralPath (Join-Path $stage 'payload\VoceLocale.exe'))) { throw 'VoceLocale.exe missing from publish output.' }
# The desktop app rewrites through local Ollama now; the legacy bundled GGUF worker is not required.
$legacyWorker = Join-Path $stage 'payload\engines\RewriteWorker'
if (Test-Path -LiteralPath $legacyWorker) {
    $payloadRoot = [IO.Path]::GetFullPath((Join-Path $stage 'payload')).TrimEnd('\') + '\'
    $resolvedWorker = [IO.Path]::GetFullPath($legacyWorker)
    if (-not $resolvedWorker.StartsWith($payloadRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Legacy worker path escaped the staged payload.' }
    Remove-Item -LiteralPath $resolvedWorker -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination (Join-Path $stage 'payload')
if ($ModelsDirectory) {
    $ModelsDirectory = (Resolve-Path -LiteralPath $ModelsDirectory).Path
    New-Item -ItemType Directory -Path (Join-Path $stage 'models') -Force | Out-Null
    foreach ($file in @('ggml-small.bin', 'ggml-base.bin')) {
        $candidate = Join-Path $ModelsDirectory $file
        if (Test-Path -LiteralPath $candidate) { Copy-Item -LiteralPath $candidate -Destination (Join-Path $stage 'models') }
    }
}
$hashes = Get-ChildItem -LiteralPath $stage -File -Recurse | ForEach-Object {
    [pscustomobject]@{ Path = $_.FullName.Substring($stage.Length + 1).Replace('\','/'); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$hashes | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $stage 'package-files.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'detect.ps1') -Destination $stage
Write-Host "Intune content staged at $stage"
Write-Host 'Use IntuneWinAppUtil.exe to wrap this directory as a Win32 .intunewin package.'
