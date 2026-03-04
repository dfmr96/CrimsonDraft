# Hit Detection — Diseño

**Date:** 2026-03-03
**Scope:** combat-ui — AimViewController, IAimView, CombatMenuController

---

## Problema

El `AimViewController` ya produce una posición de disparo (normalizada 0–1 dentro de
`aimSpace`) y la emite via `OnShotFired(Vector2)`. El `CombatMenuController` la recibe
pero la descarta (`_`). El juego no sabe si el disparo acertó o falló.

La silueta del objetivo (sprite pixel art, blanco sobre negro) está en el `aimSpace`.
Necesitamos saber si la posición del disparo cae en un píxel válido del sprite.

---

## Enfoque: Paleta de Zonas Indexada por Color

El sprite de silueta es la fuente de verdad de las zonas de impacto. Cada color
del sprite mapea a una `ShotZone` lógica. La detección lee el píxel en las
coordenadas del disparo y busca el color más cercano en una paleta definida por
el diseñador en el inspector.

**Estado actual del enum:** `Miss`, `Hit`, `Head`, `Torso`, `Arms`, `Legs`.
La arquitectura soporta paletas por color para zonas anatómicas y variantes por borde.

---

## Tipos Nuevos

### `ShotZone` (enum)

```
Miss  = 0   ← disparo fuera de la silueta
Hit   = 1   ← disparo en zona válida (por ahora, toda la silueta)
Head  = 2   ← zona de cabeza
Torso = 3   ← zona de torso
Arms  = 4   ← zona de brazos
Legs  = 5   ← zona de piernas
```

`Hit` se mantiene por compatibilidad con iteraciones anteriores.

### `ShotZoneDefinition` (struct serializable)

```
[Serializable]
struct ShotZoneDefinition
    Color    color    ← color exacto en el sprite
    ShotZone zone     ← zona que representa ese color
```

---

## Cambios en `IAimView`

```csharp
event Action<Vector2, ShotZone>? OnShotFired;   // antes: Action<Vector2>
```

El segundo parámetro es la zona resuelta. `Miss` indica disparo fallado.
`CombatMenuController` recibe la zona y la usará para calcular daño (futuro).

---

## Cambios en `AimViewController`

### Campos nuevos en el inspector

| Campo             | Tipo                   | Descripción                                      |
|-------------------|------------------------|--------------------------------------------------|
| `silhouetteImage` | `Image`                | La imagen de silueta del objetivo en `aimSpace`  |
| `zoneDefinitions` | `ShotZoneDefinition[]` | Paleta: color del sprite → ShotZone              |

### Algoritmo de detección (al disparar)

```
1. shotLocal     = ComputeRandomShotLocal()           (ya existe)
2. worldPos      = aimSpace.TransformPoint(shotLocal)
3. localInSil    = silhouetteRect.InverseTransformPoint(worldPos)
4. u = clamp((localInSil.x - rect.xMin) / rect.width,  0, 1)
5. v = clamp((localInSil.y - rect.yMin) / rect.height, 0, 1)
6. texRect        = sprite.textureRect    // sub-rect real del sprite dentro de la textura/atlas
7. px             = Round(texRect.xMin + u * (texRect.width  - 1))
8. py             = Round(texRect.yMin + v * (texRect.height - 1))
9. pixel          = sprite.texture.GetPixel(px, py)
10. zone          = ResolveZone(pixel)
11. OnShotFired?.Invoke(NormalizeShotLocal(shotLocal), zone)
```

### Notas técnicas de muestreo

#### ¿Qué es `textureRect` (sub-rect)?

`sprite.textureRect` es el rectángulo `(x, y, width, height)` que indica qué zona
de la textura pertenece al sprite.

- Si el sprite está en un atlas/spritesheet, la textura puede contener varios sprites.
- `textureRect` evita muestrear píxeles de otros sprites.
- Muestrear contra `texture.width/height` completos puede devolver colores incorrectos.

#### ¿Qué devuelve `GetPixel`?

`GetPixel(x, y)` devuelve un `Color` con componentes `r,g,b,a` en rango `0..1`.

Ejemplo:
- `RGBA(0.718, 0.851, 0.455, 1.000)` corresponde a `#B7D974`.

#### ¿Cómo funciona la distancia RGB?

Se compara el color muestreado contra cada color configurado en `zoneDefinitions`:

```
dr = pixel.r - def.color.r
dg = pixel.g - def.color.g
db = pixel.b - def.color.b
distSq = dr*dr + dg*dg + db*db
```

- Se elige la definición con menor `distSq`.
- Si la mejor distancia supera `colorTolerance^2`, el resultado es `Miss`.
- `alpha` no participa en la distancia (solo RGB).

### `ResolveZone(Color pixel) → ShotZone`

```
Por cada ShotZoneDefinition en zoneDefinitions:
    distSq = (pixel.r - def.color.r)² + (pixel.g - def.color.g)² + (pixel.b - def.color.b)²
    if distSq < bestDistSq → guardar como mejor candidato

if bestDistSq > toleranceSq → return Miss   (ningún color coincide)
return best.zone
```

Campo inspector adicional:
| `colorTolerance` | `float` | Distancia máxima RGB aceptable (default 0.1) |

La tolerancia protege contra artefactos de compresión. Para pixel art sin
compresión con pérdida, el valor exacto es 0 y la tolerancia es solo un safety net.

---

## Requisito de Asset

La textura de la silueta debe tener **Read/Write enabled** en sus import settings
(Texture Import Settings → Advanced → Read/Write). Sin esto, `GetPixel` lanza
excepción en runtime.

---

## Cambios en `CombatMenuController`

```csharp
private void HandleShotFired(Vector2 normalizedPos, ShotZone zone)
{
    // zone disponible para calcular daño — por ahora ignorado
    this.aimView.OnShotFired -= this.HandleShotFired;
    ...resto igual...
}
```

La firma del delegate cambia de `Action<Vector2>` a `Action<Vector2, ShotZone>`.

---

## Configuración de Paleta (actual y recomendada)

```
zoneDefinitions:
  [0] color: #FF0000   zone: Head
  [1] color: #00FF00   zone: Torso
  [2] color: #0000FF   zone: Arms
  [3] color: #FFFF00   zone: Legs
  [4] color: #000000   zone: Miss
colorTolerance: 0.1
```

### Variante opcional para bordes (daño reducido futuro)

Para distinguir borde vs centro del mismo miembro (sin agregar enums nuevos), se puede usar
un tono más oscuro y mapearlo al mismo `ShotZone`:

| Zona  | Centro   | Borde    |
|-------|----------|----------|
| Head  | `#FF0000` | `#B30000` |
| Torso | `#00FF00` | `#00B300` |
| Arms  | `#0000FF` | `#0000B3` |
| Legs  | `#FFFF00` | `#B3B300` |
| Miss  | `#000000` | `#000000` |

---

## Sin cambios

- Lógica de fases del AimViewController (`VerticalAiming`, `HorizontalAiming`,
  `WaitingDismiss`)
- `SpawnMarker`, `ComputeRandomShotLocal`, `NormalizeShotLocal`, `SpawnDispersionCircle`
- `Hide()`, `Show()`
- `CombatScope` — no se agregan ni quitan registros
