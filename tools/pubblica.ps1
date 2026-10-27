# Phase 8.5: the game as a folder that runs on its own (no .NET needed), in publish\PokeRogue.
# Usage: powershell -ExecutionPolicy Bypass -File tools\pubblica.ps1 [-Destinazione cartella]
param([string]$Destinazione = (Join-Path $PSScriptRoot '..\publish\PokeRogue'))

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$out = [IO.Path]::GetFullPath($Destinazione)

dotnet publish (Join-Path $root 'src\PokeRogue.Game\PokeRogue.Game.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish non riuscito' }

# The content folder goes next to the exe (ContentPaths finds it there); the download cache stays behind.
robocopy (Join-Path $root 'content') (Join-Path $out 'content') /MIR /XD cache /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'copia di content non riuscita' }

$size = (Get-ChildItem $out -Recurse | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Pronto: {0}\PokeRogue.exe ({1:N0} MB)" -f $out, $size)
