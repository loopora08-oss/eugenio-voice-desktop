$installRoot = Join-Path $env:ProgramFiles 'Eugenio'
$exe = Join-Path $installRoot 'VoceLocale.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { exit 1 }
try {
    $version = [version](Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    if ($version -lt [version]'1.2.0.0') { exit 1 }
    Write-Output "Eugenio $version detected"
    exit 0
} catch { exit 1 }
