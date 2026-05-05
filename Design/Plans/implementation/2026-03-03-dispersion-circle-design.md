# Dispersion Circle — AimView Design

**Date:** 2026-03-03
**Scope:** `AimViewController.cs` — ningún otro archivo cambia

---

## Contexto

El `AimViewController` implementa un QTE de apuntado en dos fases (vertical → horizontal). Al confirmar ambos ejes, spawneaba un `shotMarkerPrefab` en la intersección exacta y luego disparaba el evento `OnShotFired`.

El nuevo sistema introduce dispersión aleatoria invisible para el jugador: el marker aparece en una posición aleatorizada dentro de un radio, redondeada a pixel.

---

## Comportamiento por fase

| Fase | Confirm | Visual |
|---|---|---|
| VerticalAiming | Bloquea Y, dimea selector vertical | Sin cambio |
| HorizontalAiming | Bloquea X, dimea selector horizontal, guarda intersección | **Nada nuevo** — el punto exacto no se muestra |
| WaitingDismiss | Calcula random, spawna marker, dispara evento | Shot marker (sprite círculo pixel art) en posición aleatoria |

El jugador nunca ve el radio de dispersión ni el punto exacto de intersección. Solo ve el marker al momento del disparo.

---

## Cambios en campos

**Eliminados:**
- `confirmedY` (float)
- `confirmedWorldY` (float)
- `pendingShot` (Vector2)

**Agregados:**
- `[SerializeField] private int dispersionRadius` — radio en unidades UI (int para precisión pixel)
- `private Vector2 confirmedLocalPos` — intersección de selectores en espacio local de `aimSpace`

**Sin cambios:** todos los campos de selectores, `aimSpace`, `shotMarkerPrefab`, `speed`, `dimmingAlpha`

---

## Algoritmo de dispersión

Fórmula polar con distribución uniforme dentro del círculo (idéntica al prototipo Python):

```
angle  = Random.value × 2π
r      = dispersionRadius × √(Random.value)
offset = (Round(r × cos(angle)),  Round(r × sin(angle)))
shotLocal = Round(confirmedLocalPos) + offset
```

El `√(Random.value)` garantiza distribución uniforme (sin concentración en el centro).
El `Round` en offset **y** en `confirmedLocalPos` asegura posicionado pixel-perfect, igual que los selectores.

---

## Métodos nuevos / modificados

### `Confirm()` — HorizontalAiming
```
hLocal.x = Round(hLocal.x)
confirmedLocalPos = aimSpace.InverseTransformPoint(worldIntersection)
worldIntersection = (horizontal.position.x, vertical.position.y, aimSpace.position.z)
→ transiciona a WaitingDismiss (sin spawn)
```

### `Confirm()` — WaitingDismiss
```
shotLocal = ComputeRandomShotLocal()
SpawnMarker(shotLocal)
OnShotFired?.Invoke(NormalizeShotLocal(shotLocal))
```

### `SpawnMarker(Vector2 localPos)`
Recibe posición local en `aimSpace` (en vez de worldX/worldY anteriores).

### `ComputeRandomShotLocal() → Vector2`
Aplica la fórmula polar descrita arriba. Retorna posición local en `aimSpace`.

### `NormalizeShotLocal(Vector2 local) → Vector2`
Normaliza respecto a `aimSpace.rect` (0..1 con clamp), para el payload de `OnShotFired`.

---

## Gizmo (editor only)

`OnDrawGizmosSelected()` — solo compila con `#if UNITY_EDITOR`.

Dibuja un cubo por cada pixel `(x, y)` donde `x² + y² ≤ dispersionRadius²`, centrado en `aimSpace.position`, escalado por `aimSpace.lossyScale.x`.

**Propósito:** permite al diseñador ver exactamente qué píxeles conforman el radio antes de crear el sprite asset.

```csharp
for (int y = -r; y <= r; y++)
    for (int x = -r; x <= r; x++)
        if (x*x + y*y <= r*r)
            Gizmos.DrawCube(center + new Vector3(x, y, 0) * scale, Vector3.one * cube);
```

---

## Sin cambios

- `IAimView` — interfaz sin modificar
- `VerticalAiming` confirm — lógica idéntica
- `StartVerticalOscillation()` / `StartHorizontalOscillation()` — sin cambios
- `Hide()` — sin cambios
