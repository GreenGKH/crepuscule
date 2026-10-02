# Compile Crepuscule.exe avec le compilateur C# fourni avec Windows (aucune installation requise).
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$fw  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
$options = @(
    '/nologo', '/target:winexe', '/optimize+', '/codepage:65001', "/lib:$fw\WPF",
    '/r:PresentationFramework.dll', '/r:PresentationCore.dll', '/r:WindowsBase.dll',
    '/r:System.Xaml.dll', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll'
)

# 1) Compilation temporaire, qui sert a dessiner l'icone
$temp = Join-Path $env:TEMP 'crepuscule_icone.exe'
& $csc @options "/out:$temp" Crepuscule.cs
if ($LASTEXITCODE -ne 0) { throw 'Echec de la compilation.' }
Start-Process $temp -ArgumentList '--icone', "`"$PSScriptRoot\Crepuscule.ico`"" -Wait
Remove-Item $temp

# 2) Compilation finale, icone incluse
& $csc @options '/win32icon:Crepuscule.ico' '/out:Crepuscule.exe' Crepuscule.cs
if ($LASTEXITCODE -ne 0) { throw 'Echec de la compilation.' }
Write-Host 'Crepuscule.exe est pret.'
