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

**Hoy:** blanco = Hit, negro = Miss.
**Futuro (body zones):** rojo = Head, verde = Torso, azul = Arms, negro = Miss.
No hay cambio de arquitectura cuando se añadan zonas — solo se expande el enum
y se actualiza la paleta.

---

## Tipos Nuevos

### `ShotZone` (enum)

```
Miss  = 0   ← disparo fuera de la silueta
Hit   = 1   ← disparo en zona válida (por ahora, toda la silueta)
```

Extensible: añadir `Head`, `Torso`, `LeftArm`, `RightArm`, `Legs` sin cambiar
la interfaz ni la lógica de detección.

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
6. pixel         = sprite.texture.GetPixel(Round(u * tex.width), Round(v * tex.height))
7. zone          = ResolveZone(pixel)
8. OnShotFired?.Invoke(NormalizeShotLocal(shotLocal), zone)
```

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

## Configuración del Inspector (iteración actual)

```
zoneDefinitions:
  [0] color: #FFFFFF   zone: Hit
  [1] color: #000000   zone: Miss
colorTolerance: 0.1
```

---

## Sin cambios

- Lógica de fases del AimViewController (`VerticalAiming`, `HorizontalAiming`,
  `WaitingDismiss`)
- `SpawnMarker`, `ComputeRandomShotLocal`, `NormalizeShotLocal`, `SpawnDispersionCircle`
- `Hide()`, `Show()`
- `CombatScope` — no se agregan ni quitan registros
