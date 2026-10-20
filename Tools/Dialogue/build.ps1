# Arma dialogos.json a partir de personajes.json + conversaciones\*.json
#
#   .\build.ps1
#
# Las conversaciones se ordenan por nombre de archivo (de ahi los prefijos 01-, 02-, ...).
# Si falta "id" en un archivo, se usa el nombre del archivo como id (asi reimportar
# actualiza la conversacion en vez de duplicarla).
# Si existe retratos\<id-de-personaje>.png (o .jpg/.jpeg/.webp), se incrusta como retrato.

param([switch]$Cobertura)   # -Cobertura imprime las lineas que quedan en cada variante de caidos

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Get-Ids($v) { if ($null -eq $v) { return @() } return @($v | ForEach-Object { $_ } | Where-Object { $_ }) }

# Una condicion se cumple si TODOS los listados en alive estan vivos y TODOS los de dead, muertos.
function Test-Cond($cond, $deadSet) {
    if ($null -eq $cond) { return $true }
    foreach ($id in (Get-Ids $cond.alive)) { if ($deadSet -contains $id) { return $false } }
    foreach ($id in (Get-Ids $cond.dead))  { if ($deadSet -notcontains $id) { return $false } }
    return $true
}
function Resolve-Lines($lines, $deadSet) {
    $out = @()
    foreach ($l in $lines) {
        if ($l.speaker -and $deadSet -contains $l.speaker) { continue }
        if (-not (Test-Cond $l.'if' $deadSet)) { continue }
        $out += $l
    }
    return $out
}

function Read-Json($path) {
    $raw = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $raw = $raw.TrimStart([char]0xFEFF)
    try { return $raw | ConvertFrom-Json }
    catch { throw "JSON invalido en $(Split-Path $path -Leaf): $($_.Exception.Message)" }
}

# ---------- personajes ----------
$charsPath = Join-Path $root 'personajes.json'
if (-not (Test-Path $charsPath)) { throw "No existe personajes.json" }
# el pipe fuerza la enumeracion: en PS 5.1 ConvertFrom-Json emite el array como un solo objeto
$characters = @((Read-Json $charsPath) | ForEach-Object { $_ })

$retratosDir = Join-Path $root 'retratos'
$mime = @{ '.png' = 'image/png'; '.jpg' = 'image/jpeg'; '.jpeg' = 'image/jpeg'; '.webp' = 'image/webp' }

$outChars = @()
foreach ($ch in $characters) {
    if (-not $ch.id) { throw "Hay un personaje sin 'id' en personajes.json" }
    $portrait = ''
    if ($ch.PSObject.Properties['portrait'] -and $ch.portrait) { $portrait = $ch.portrait }
    if (-not $portrait -and (Test-Path $retratosDir)) {
        foreach ($ext in $mime.Keys) {
            $p = Join-Path $retratosDir ($ch.id + $ext)
            if (Test-Path $p) {
                $b64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($p))
                $portrait = "data:$($mime[$ext]);base64,$b64"
                Write-Host "  retrato: $($ch.id)$ext" -ForegroundColor DarkGray
                break
            }
        }
    }
    $color = '#5fd4a0'
    if ($ch.PSObject.Properties['color'] -and $ch.color) { $color = $ch.color }
    $outChars += [ordered]@{ id = $ch.id; name = $ch.name; color = $color; portrait = $portrait }
}
$charIds = $outChars | ForEach-Object { $_.id }

# ---------- conversaciones ----------
$convDir = Join-Path $root 'conversaciones'
if (-not (Test-Path $convDir)) { throw "No existe la carpeta conversaciones\" }
$files = @(Get-ChildItem -Path $convDir -Filter '*.json' | Sort-Object Name)
if (-not $files.Count) { Write-Host "  (todavia no hay conversaciones: solo personajes)" -ForegroundColor DarkGray }

$outConvs = @()
$warnings = @()
$seenIds = @{}

foreach ($f in $files) {
    $c = Read-Json $f.FullName
    $id = if ($c.PSObject.Properties['id'] -and $c.id) { $c.id } else { $f.BaseName }
    if ($seenIds.ContainsKey($id)) { throw "id duplicado '$id' ($($f.Name))" }
    $seenIds[$id] = $true

    $lines = @()
    $n = 0
    foreach ($l in @($c.lines | ForEach-Object { $_ })) {
        $n++
        $esBeat = $l.PSObject.Properties['beat'] -and $l.beat
        if ($esBeat) {
            if ('static', 'silence', 'pause', 'silencio', 'pausa' -notcontains $l.beat) { $warnings += "$($f.Name) linea $n`: beat '$($l.beat)' no reconocido (static / silence / pause)" }
            $out = [ordered]@{ beat = [string]$l.beat }
        }
        else {
            if (-not $l.speaker) { $warnings += "$($f.Name) linea $n`: sin 'speaker'" }
            elseif ($charIds -notcontains $l.speaker) { $warnings += "$($f.Name) linea $n`: speaker '$($l.speaker)' no existe en personajes.json" }
            $out = [ordered]@{ speaker = $l.speaker; text = [string]$l.text }
        }

        # condicion de caidos
        if ($l.PSObject.Properties['if'] -and $l.'if') {
            $cond = [ordered]@{}
            foreach ($k in 'alive', 'dead') {
                $v = Get-Ids $l.'if'.$k
                foreach ($id in $v) { if ($charIds -notcontains $id) { $warnings += "$($f.Name) linea $n`: if.$k '$id' no existe en personajes.json" } }
                if ($v.Count) { $cond[$k] = $v }
            }
            if ($cond.Count) { $out['if'] = $cond } else { $warnings += "$($f.Name) linea $n`: 'if' vacio" }
        }

        # textos alternativos segun quien este vivo
        if ($l.PSObject.Properties['alt'] -and $l.alt) {
            $alts = @()
            foreach ($a in @($l.alt | ForEach-Object { $_ })) {
                $one = [ordered]@{}
                foreach ($k in 'alive', 'dead') {
                    $v = Get-Ids $a.$k
                    foreach ($id in $v) { if ($charIds -notcontains $id) { $warnings += "$($f.Name) linea $n`: alt.$k '$id' no existe en personajes.json" } }
                    if ($v.Count) { $one[$k] = $v }
                }
                if (-not $one.Count) { $warnings += "$($f.Name) linea $n`: un 'alt' sin condicion (nunca se usaria o taparia al resto)"; continue }
                $one['text'] = [string]$a.text
                $alts += $one
            }
            if ($alts.Count) { $out['alt'] = $alts }
        }

        $lines += $out
    }
    if (-not $lines.Count) { $warnings += "$($f.Name): no tiene lineas" }

    # grupo del jugador (derecha). Acepta rightIds (array) o el viejo rightId (uno solo).
    $rightIds = @()
    if ($c.PSObject.Properties['rightIds']) { $rightIds = @($c.rightIds | ForEach-Object { $_ }) }
    elseif ($c.PSObject.Properties['rightId'] -and $c.rightId) { $rightIds = @([string]$c.rightId) }
    $rightIds = @($rightIds | Where-Object { $_ })
    if (-not $rightIds.Count) { $warnings += "$($f.Name): falta 'rightIds'" }
    foreach ($v in $rightIds) {
        if ($charIds -notcontains $v) { $warnings += "$($f.Name): rightIds '$v' no existe en personajes.json" }
    }
    if (-not $c.leftId) { $warnings += "$($f.Name): falta 'leftId'" }
    elseif ($charIds -notcontains $c.leftId) { $warnings += "$($f.Name): leftId '$($c.leftId)' no existe en personajes.json" }
    if ($rightIds -contains $c.leftId) { $warnings += "$($f.Name): leftId '$($c.leftId)' tambien esta en rightIds" }

    # un hablante que no esta ni en el grupo ni es el leftId igual se muestra a la izquierda
    $outConvs += [ordered]@{
        id       = $id
        title    = [string]$c.title
        freq     = [string]$c.freq
        rightIds = $rightIds
        leftId   = [string]$c.leftId
        lines    = $lines
    }
    Write-Host ("  {0,-34} {1,8}  {2} lineas" -f $f.Name, $c.freq, $lines.Count)

    # ---- cobertura: recorre todas las combinaciones de caidos del grupo ----
    # "variantes": false marca una escena donde nadie puede estar muerto (intro, obligatorias).
    $admiteMuertos = $true
    if ($c.PSObject.Properties['variantes'] -and $c.variantes -eq $false) { $admiteMuertos = $false }
    if (-not $admiteMuertos) {
        if ($Cobertura) { Write-Host "      (sin variantes: nadie puede morir en esta escena)" -ForegroundColor DarkGray }
        continue
    }
    $nameOf = @{}; foreach ($ch in $outChars) { $nameOf[$ch.id] = $ch.name }
    $k = $rightIds.Count
    for ($mask = 0; $mask -lt [Math]::Pow(2, $k); $mask++) {
        $deadSet = @()
        for ($b = 0; $b -lt $k; $b++) { if ($mask -band (1 -shl $b)) { $deadSet += $rightIds[$b] } }
        $res = @(Resolve-Lines $lines $deadSet)
        $vivos = @($rightIds | Where-Object { $deadSet -notcontains $_ })
        $hablan = @($res | Where-Object { $rightIds -contains $_.speaker } | ForEach-Object { $_.speaker } | Select-Object -Unique)

        $etiqueta = if ($deadSet.Count) { 'KIA: ' + (($deadSet | ForEach-Object { $nameOf[$_] }) -join ', ') } else { 'todos vivos' }

        if ($vivos.Count -and -not $res.Count) {
            $warnings += "$($f.Name) [$etiqueta]: la variante queda sin ninguna linea"
        }
        foreach ($v in $vivos) {
            if ($hablan -notcontains $v) {
                $warnings += "$($f.Name) [$etiqueta]: $($nameOf[$v]) esta vivo pero no dice nada en esta variante"
            }
        }
        if ($Cobertura) {
            Write-Host ("      {0,-30} {1,3} lineas   hablan: {2}" -f $etiqueta, $res.Count,
                (($hablan | ForEach-Object { $nameOf[$_] }) -join ', ')) -ForegroundColor DarkGray
        }
    }
}

# ---------- salida ----------
$db = [ordered]@{ characters = $outChars; conversations = $outConvs }
$json = $db | ConvertTo-Json -Depth 12
$outPath = Join-Path $root 'dialogos.json'
[System.IO.File]::WriteAllText($outPath, $json, (New-Object System.Text.UTF8Encoding $false))

Write-Host ""
if ($warnings.Count) {
    Write-Host "Avisos:" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host ""
}
Write-Host "OK -> dialogos.json  ($($outConvs.Count) conversaciones, $($outChars.Count) personajes)" -ForegroundColor Green
Write-Host "Importalo en la app con 'Importar (agregar)'."
