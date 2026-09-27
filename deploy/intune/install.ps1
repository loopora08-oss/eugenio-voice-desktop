$ErrorActionPreference = 'Stop'
$version = '1.4.0.0'
$installRoot = Join-Path $env:ProgramFiles 'Eugenio'
$sharedRoot = Join-Path $env:ProgramData 'Eugenio'
$source = Join-Path $PSScriptRoot 'payload'
$manifestPath = Join-Path $PSScriptRoot 'package-files.json'
if (-not (Test-Path -LiteralPath (Join-Path $source 'VoceLocale.exe'))) { throw 'Eugenio payload is missing.' }
if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'Package hash manifest is missing.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
foreach ($entry in $manifest) {
    $filePath = Join-Path $PSScriptRoot ($entry.Path.Replace('/','\'))
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { throw "Package file missing: $($entry.Path)" }
    $actual = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.SHA256) { throw "Package hash mismatch: $($entry.Path)" }
}
$process = Get-Process -Name 'VoceLocale' -ErrorAction SilentlyContinue
if ($process) { Write-Host 'Close Eugenio before updating; open meeting notes may be unsaved.'; exit 1618 }
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $installRoot -Recurse -Force
$installKey = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Eugenio'
New-Item -Path $installKey -Force | Out-Null
New-ItemProperty -Path $installKey -Name DisplayName -Value 'Eugenio' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $installKey -Name DisplayVersion -Value $version -PropertyType String -Force | Out-Null
New-ItemProperty -Path $installKey -Name Publisher -Value 'Eugenio' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $installKey -Name InstallLocation -Value $installRoot -PropertyType String -Force | Out-Null
New-ItemProperty -Path $installKey -Name UninstallString -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$installRoot\uninstall.ps1`"" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $installKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $installKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
$startMenu = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'
New-Item -ItemType Directory -Path $startMenu -Force | Out-Null
$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $startMenu 'Eugenio.lnk'))
$shortcut.TargetPath = Join-Path $installRoot 'VoceLocale.exe'
$shortcut.WorkingDirectory = $installRoot
$shortcut.Description = 'Dettatura locale Eugenio'
$shortcut.Save()
$notetakerShortcut = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $startMenu 'Eugenio Notetaker.lnk'))
$notetakerShortcut.TargetPath = Join-Path $installRoot 'VoceLocale.exe'
$notetakerShortcut.Arguments = '--meeting'
$notetakerShortcut.WorkingDirectory = $installRoot
$notetakerShortcut.Description = 'Meeting e appunti locali Eugenio'
$notetakerShortcut.Save()
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'models')) {
    New-Item -ItemType Directory -Path (Join-Path $sharedRoot 'models') -Force | Out-Null
    $acl = Get-Acl -LiteralPath $sharedRoot
    $acl.SetAccessRuleProtection($true, $false)
    $acl.Access | ForEach-Object { [void]$acl.RemoveAccessRule($_) }
    $system = New-Object Security.AccessControl.FileSystemAccessRule('SYSTEM','FullControl','ContainerInherit,ObjectInherit','None','Allow')
    $admins = New-Object Security.AccessControl.FileSystemAccessRule('BUILTIN\Administrators','FullControl','ContainerInherit,ObjectInherit','None','Allow')
    $users = New-Object Security.AccessControl.FileSystemAccessRule('BUILTIN\Users','ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow')
    $acl.SetAccessRule($system); $acl.AddAccessRule($admins); $acl.AddAccessRule($users)
    Set-Acl -LiteralPath $sharedRoot -AclObject $acl
    $modelManifest = @{
        'ggml-small.bin' = @{ Bytes = 487601967L; SHA256 = '1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b' }
        'ggml-base.bin' = @{ Bytes = 147951465L; SHA256 = '60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe' }
    }
    foreach ($name in $modelManifest.Keys) {
        $modelSource = Join-Path (Join-Path $PSScriptRoot 'models') $name
        if (-not (Test-Path -LiteralPath $modelSource)) { continue }
        $info = Get-Item -LiteralPath $modelSource
        $hash = (Get-FileHash -LiteralPath $modelSource -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($info.Length -ne $modelManifest[$name].Bytes -or $hash -ne $modelManifest[$name].SHA256) { throw "Model verification failed: $name" }
        Copy-Item -LiteralPath $modelSource -Destination (Join-Path $sharedRoot 'models') -Force
    }
}
Write-Host "Eugenio $version installed successfully."
