# Colagem das fichas do catálogo de uma família, lado a lado (uma imagem PNG).
#
#   powershell -File Tools/shots/summon_sheet.ps1 lich
#   powershell -File Tools/shots/summon_sheet.ps1 wizard -Out C:\temp\mago.png
#
# Abre o jogo com o save `debugger` (offline, nunca o save de verdade), abre a ficha de cada variante nas
# estrelas naturais (nível 1, sem sigilos) e salva a colagem. O jogo fecha sozinho.
param(
    [Parameter(Mandatory = $true)][string]$Family,
    [string]$Out = "summon_sheet_$Family.png",
    [string]$Godot = "C:\Godot_v4.7.1\Godot_v4.7.1-stable_mono_win64_console.exe"
)
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$target = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path (Get-Location) $Out }
& $Godot --path $root -- --save=debugger "--summon-sheet=$Family" "--out=$target"
if (Test-Path $target) { Write-Host "Pronto: $target" } else { Write-Error "A colagem não foi gerada." }
