$ErrorActionPreference = 'Stop'
$installRoot = Join-Path $env:ProgramFiles 'Eugenio'
$expected = [IO.Path]::GetFullPath($installRoot)
if ($expected -ne [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'Eugenio'))) { throw 'Unexpected install path.' }
Get-Process -Name 'VoceLocale' -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path -LiteralPath $expected) { Remove-Item -LiteralPath $expected -Recurse -Force }
Remove-Item -LiteralPath 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Eugenio' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Eugenio.lnk') -Force -ErrorAction SilentlyContinue
# Per-user settings/models and the IT-managed shared model cache are retained intentionally.
Write-Host 'Eugenio application files removed.'
