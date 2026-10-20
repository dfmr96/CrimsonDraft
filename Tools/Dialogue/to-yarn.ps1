# Convierte conversaciones\*.json en nodos Yarn para el juego.
#
#   .\to-yarn.ps1                      # todas las conversaciones
#   .\to-yarn.ps1 -Only 01-adrift      # una sola (nombre de archivo sin .json)
#
# Salida: <Assets>\Dialogues\radio\radio_<id>.yarn  +  radio_vars.yarn (declaraciones $dead_*)
#
# Convencion de los nodos generados (la lee RadioLinePresenter en Unity):
#   - cabeceras:  freq / left / right      (metadatos, documentacion)
#   - <<radio_open "freq" "left" "right,ids">>   primer comando del nodo: configura la radio
#   - linea:      "deer: texto"            (el hablante es el id de personajes.json)
#   - beat:       "_beat: silence"         (silence | pause | static)
#   - salto de linea dentro de un texto: el caracter  ¶  (el runtime lo vuelve \n)
#   - caidos:     $dead_<id> (bool). Un hablante muerto no habla; 'if' y 'alt' se
#                 traducen a <<if>>/<<elseif>>/<<else>> con esas variables.

param(
    [string]$Only,
    [string]$OutDir = (Join-Path $PSScriptRoot '..\..\Game\CrimsonDraft\Assets\Dialogues\radio')
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$NL = [string][char]0x00B6   # ¶

function Get-Ids($v) { if ($null -eq $v) { return @() } return @($v | ForEach-Object { $_ } | Where-Object { $_ }) }

function Read-Json($path) {
    $raw = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8).TrimStart([char]0xFEFF)
    try { return $raw | ConvertFrom-Json }
    catch { throw "JSON invalido en $(Split-Path $path -Leaf): $($_.Exception.Message)" }
}

# Escapa lo que Yarn interpretaria como sintaxis y pasa los saltos de linea al marcador.
function ConvertTo-YarnText([string]$t, [string]$where) {
    if ($t -match '//') { Write-Warning "$where`: el texto contiene '//' (Yarn lo lee como comentario)" }
    $t = $t -replace "`r", ''
    $t = $t -replace '\\', '\\'
    $t = $t -replace '([{}\[\]<>#])', '\$1'
    return ($t -replace "`n", $NL)
}

function Get-CondExpr($cond) {
    if ($null -eq $cond) { return $null }
    $parts = @()
    foreach ($id in (Get-Ids $cond.alive)) { $parts += "not `$dead_$id" }
    foreach ($id in (Get-Ids $cond.dead))  { $parts += "`$dead_$id" }
    if (-not $parts.Count) { return $null }
    return ($parts -join ' and ')
}

$chars = @((Read-Json (Join-Path $root 'personajes.json')) | ForEach-Object { $_ })
$charIds = $chars | ForEach-Object { $_.id }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$utf8 = New-Object System.Text.UTF8Encoding $false

# ---- variables compartidas: un solo <<declare>> por variable en TODO el proyecto Yarn ----
$vars = New-Object System.Collections.Generic.List[string]
$vars.Add('title: radio_vars')
$vars.Add('---')
foreach ($id in $charIds) { $vars.Add("<<declare `$dead_$id = false as bool>>") }
$vars.Add('===')
[System.IO.File]::WriteAllText((Join-Path $OutDir 'radio_vars.yarn'), (($vars -join "`n") + "`n"), $utf8)
Write-Host "  radio_vars.yarn  ($($charIds.Count) variables)"

$files = @(Get-ChildItem -Path (Join-Path $root 'conversaciones') -Filter '*.json' | Sort-Object Name)
if ($Only) { $files = @($files | Where-Object { $_.BaseName -eq $Only }) }
if (-not $files.Count) { throw 'No hay conversaciones que convertir.' }

foreach ($f in $files) {
    $c = Read-Json $f.FullName
    $id = if ($c.PSObject.Properties['id'] -and $c.id) { [string]$c.id } else { $f.BaseName }
    $node = 'radio_' + ($id -replace '[^A-Za-z0-9]', '_')

    $right = @(); if ($c.PSObject.Properties['rightIds']) { $right = Get-Ids $c.rightIds }
    # "variantes": false = nadie puede estar muerto en esta escena: no se generan guardas
    $admiteMuertos = -not ($c.PSObject.Properties['variantes'] -and $c.variantes -eq $false)
    $out = New-Object System.Collections.Generic.List[string]
    $out.Add("title: $node")
    $out.Add("freq: $($c.freq)")
    $out.Add("left: $($c.leftId)")
    $out.Add("right: $($right -join ',')")
    $out.Add("name: $($c.title)")
    $out.Add('---')
    # el runtime lee los metadatos con este comando (corre antes de la primera linea)
    $out.Add("<<radio_open `"$($c.freq)`" `"$($c.leftId)`" `"$($right -join ',')`">>")

    $n = 0
    foreach ($l in @($c.lines | ForEach-Object { $_ })) {
        $n++
        $where = "$($f.Name) linea $n"
        $isBeat = $l.PSObject.Properties['beat'] -and $l.beat
        if (-not $isBeat -and $charIds -notcontains $l.speaker) { Write-Warning "$where`: speaker '$($l.speaker)' no existe"; continue }

        # lista de (condicion, texto) de la linea
        $variants = @()
        if ($isBeat) { $emit = { param($t) "_beat: $($l.beat)" } }
        else {
            $sp = [string]$l.speaker
            $emit = { param($t) "${sp}: $t" }
        }

        # guardas: el hablante vivo + el 'if' de la linea
        $guards = @()
        # solo el grupo del jugador puede caer; el resto (p.ej. Vulture) habla siempre
        if (-not $isBeat -and $admiteMuertos -and $right -contains $l.speaker) { $guards += "not `$dead_$($l.speaker)" }
        $ifExpr = $null; if ($admiteMuertos -and $l.PSObject.Properties['if']) { $ifExpr = Get-CondExpr $l.'if' }
        if ($ifExpr) { $guards += $ifExpr }

        $alts = @(); if ($admiteMuertos -and $l.PSObject.Properties['alt'] -and $l.alt) { $alts = @($l.alt | ForEach-Object { $_ }) }

        $ind = ''
        if ($guards.Count) { $out.Add("<<if $($guards -join ' and ')>>"); $ind = '    ' }

        if ($isBeat) { $out.Add("$ind$(& $emit '')") }
        elseif (-not $alts.Count) { $out.Add("$ind$(& $emit (ConvertTo-YarnText ([string]$l.text) $where))")  }
        else {
            $first = $true
            foreach ($a in $alts) {
                $e = Get-CondExpr $a
                if (-not $e) { Write-Warning "$where`: 'alt' sin condicion, se ignora"; continue }
                $out.Add("$ind$(if ($first) { '<<if' } else { '<<elseif' }) $e>>")
                $out.Add("$ind    $(& $emit (ConvertTo-YarnText ([string]$a.text) $where))")
                $first = $false
            }
            if ($first) { $out.Add("$ind$(& $emit (ConvertTo-YarnText ([string]$l.text) $where))") }
            else {
                $out.Add("$ind<<else>>")
                $out.Add("$ind    $(& $emit (ConvertTo-YarnText ([string]$l.text) $where))")
                $out.Add("$ind<<endif>>")
            }
        }

        if ($guards.Count) { $out.Add('<<endif>>') }
    }
    $out.Add('===')

    $path = Join-Path $OutDir "$node.yarn"
    [System.IO.File]::WriteAllText($path, (($out -join "`n") + "`n"), $utf8)
    Write-Host ("  {0,-34} {1} lineas" -f "$node.yarn", $n)
}
Write-Host "OK -> $OutDir" -ForegroundColor Green
