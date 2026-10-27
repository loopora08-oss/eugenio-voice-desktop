param(
  [Parameter(Mandatory)][string]$Path,
  [switch]$RequireSignature,
  [string]$UnsignedPath,
  [string]$ExpectedSignerSubject,
  [string]$MetadataOutput
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Path).Path
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$owned = @(Get-Content -LiteralPath (Join-Path $repositoryRoot 'signing/owned-binaries.json') -Raw | ConvertFrom-Json)
if ($owned.Count -ne 9 -or @($owned | Select-Object -Unique).Count -ne 9) { throw 'Elenco dei binari propri assente o duplicato.' }
[xml]$configuration = Get-Content -LiteralPath (Join-Path $repositoryRoot 'signing/artifact-configuration.xml') -Raw
$namespaces = [Xml.XmlNamespaceManager]::new($configuration.NameTable)
$namespaces.AddNamespace('s', 'http://signpath.io/artifact-configuration/v1')
$includes = @($configuration.SelectNodes('//s:pe-file-set/s:include', $namespaces) | ForEach-Object { $_.GetAttribute('path') })
if (@(Compare-Object $owned $includes).Count -ne 0) { throw 'La configurazione SignPath non coincide con i binari propri.' }
if ($RequireSignature -and ([string]::IsNullOrWhiteSpace($UnsignedPath) -or [string]::IsNullOrWhiteSpace($ExpectedSignerSubject))) {
  throw 'Per verificare la firma servono pacchetto originale ed editore atteso.'
}
$versions = @()
foreach ($relative in $owned) {
  if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\/])\.\.([\/]|$)') { throw 'Percorso non ammesso nel perimetro dei binari.' }
  $file = Join-Path $root $relative
  if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Binario proprio mancante: $relative" }
  $metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($file)
  if ($metadata.ProductName -ne 'Eugenio') { throw "Nome prodotto errato: $relative" }
  if ($metadata.ProductVersion -notmatch '^\d+\.\d+\.\d+[A-Za-z0-9.+-]*$' -or $metadata.FileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Versione non valida: $relative" }
  $versions += [pscustomobject]@{ path=$relative; productVersion=$metadata.ProductVersion; fileVersion=$metadata.FileVersion }
  if ($RequireSignature) {
    $signature = Get-AuthenticodeSignature -LiteralPath $file
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -ne $ExpectedSignerSubject) { throw "Firma assente, non valida o editore inatteso: $relative" }
  }
}
if (@($versions.productVersion | Select-Object -Unique).Count -ne 1 -or @($versions.fileVersion | Select-Object -Unique).Count -ne 1) { throw 'Le versioni dei binari propri non coincidono.' }
if ($UnsignedPath) {
  $unsignedRoot = (Resolve-Path -LiteralPath $UnsignedPath).Path
  $before = @(Get-ChildItem -LiteralPath $unsignedRoot -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($unsignedRoot,$_.FullName).Replace('\','/') })
  $after = @(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/') })
  if (@(Compare-Object $before $after).Count -ne 0) { throw 'Il servizio ha aggiunto o rimosso file dal pacchetto.' }
  foreach ($relative in $before) {
    if ($relative -in $owned) { continue }
    $sourceHash = (Get-FileHash -LiteralPath (Join-Path $unsignedRoot $relative) -Algorithm SHA256).Hash
    $resultHash = (Get-FileHash -LiteralPath (Join-Path $root $relative) -Algorithm SHA256).Hash
    if ($sourceHash -ne $resultHash) { throw "File estraneo alla firma modificato: $relative" }
  }
}
$report = [ordered]@{product='Eugenio';productVersion=$versions[0].productVersion;fileVersion=$versions[0].fileVersion;ownedBinaryCount=$owned.Count;signatureVerified=[bool]$RequireSignature}
if ($MetadataOutput) { $report | ConvertTo-Json | Set-Content -LiteralPath $MetadataOutput -Encoding utf8 }
if ($env:GITHUB_OUTPUT) { "product-version=$($report.productVersion)" | Add-Content -LiteralPath $env:GITHUB_OUTPUT; "file-version=$($report.fileVersion)" | Add-Content -LiteralPath $env:GITHUB_OUTPUT }
$report | ConvertTo-Json
