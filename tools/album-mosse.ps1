# Album of every move (phase 2.9): one Move Lab capture per move at its top level, with its name, icon, type and
# archetype, laid out twelve to a page, to check at a glance that every move is recognisable (Milestone B).
#
#   powershell -ExecutionPolicy Bypass -File tools\album-mosse.ps1
#
# Build the solution first (Debug). Pages go to previews\milestone-b\album-mosse-N.png.
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$exe = Join-Path $Root 'src\PokeRogue.Game\bin\Debug\net10.0\PokeRogue.exe'
$out = Join-Path $Root 'previews\milestone-b'
$work = Join-Path $out 'album-catture'
New-Item -ItemType Directory -Force $work | Out-Null

# Moves in the order of moves.json (the base moves, then the evolutions), with their archetype.
$json = Get-Content (Join-Path $Root 'content\data\moves.json') -Raw -Encoding UTF8
$moves = [regex]::Matches($json, '"id":\s*"([^"]+)",(?:\s*"catalogKey":\s*"[^"]+",)?\s*"archetype":\s*"([^"]+)"') |
    ForEach-Object { [pscustomobject]@{ Id = $_.Groups[1].Value; Archetype = $_.Groups[2].Value } }

# The first use comes on the first tick; capture each kind of effect where it shows best: bolts at once, slashes as they
# sweep, shots in flight, rocks while falling, rings as they spread, trails once Pikachu has walked a little.
$waits = @{ Arc = 3; Slash = 4; Aura = 6; Projectile = 12; Beam = 12; Homing = 14; Strike = 18; Boomerang = 22; Ring = 22; Orbit = 30; Zone = 40 }
$trails = @('flame-wheel', 'flare-blitz')

# Pietrataglio's stones only burst out once the warning is over.
$custom = @{ 'stone-edge' = 26 }

# The Move Lab centre: Pikachu, the move around it, and the caption with the name and icon above.
$crop = '150,56,340,250'
$index = 0
foreach ($move in $moves) {
    $index++
    $file = Join-Path $work ('{0:D2}-{1}.png' -f $index, $move.Id)
    $wait = if ($custom.ContainsKey($move.Id)) { $custom[$move.Id] } elseif ($trails -contains $move.Id) { 60 } else { $waits[$move.Archetype] }
    & $exe --preview movelab --reel $move.Id --every 100000 --level 8 --hitbox 0 --capture $file --wait $wait --crop $crop | Out-Null
}

$files = Get-ChildItem $work -Filter '*.png' | Sort-Object Name
$cols = 4; $rows = 3; $cellW = 340; $cellH = 250; $gap = 8; $header = 30
$perPage = $cols * $rows
$pages = [math]::Ceiling($files.Count / $perPage)
for ($page = 0; $page -lt $pages; $page++) {
    $bitmap = New-Object System.Drawing.Bitmap ($cols * ($cellW + $gap) + $gap), ($rows * ($cellH + $gap) + $gap + $header)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::FromArgb(24, 20, 40))
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $font = New-Object System.Drawing.Font 'Segoe UI', 12
    $graphics.DrawString("Album delle mosse  -  pagina $($page + 1) di $pages", $font, [System.Drawing.Brushes]::Gainsboro, $gap, 6)

    for ($i = 0; $i -lt $perPage; $i++) {
        $n = $page * $perPage + $i
        if ($n -ge $files.Count) { break }

        $image = [System.Drawing.Image]::FromFile($files[$n].FullName)
        $x = $gap + ($i % $cols) * ($cellW + $gap)
        $y = $header + $gap + [math]::Floor($i / $cols) * ($cellH + $gap)
        $graphics.DrawImage($image, $x, $y, $cellW, $cellH)
        $image.Dispose()
    }

    $bitmap.Save((Join-Path $out ("album-mosse-{0}.png" -f ($page + 1))), [System.Drawing.Imaging.ImageFormat]::Png)
    $font.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}

Write-Output "Album: $($files.Count) mosse in $pages pagine in $out"
