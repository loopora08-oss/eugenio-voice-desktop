param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Path).Path
$manifest = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object Name -ne 'BINARY-HASHES.json' | Sort-Object FullName | ForEach-Object {
  @{ path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'BINARY-HASHES.json') -Encoding utf8
Write-Output "Manifesto aggiornato dopo la firma: $($manifest.Count) file. Non sostituisce la verifica Authenticode."
