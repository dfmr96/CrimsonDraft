# Hit Detection Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Detectar hit/miss al muestrear el color del píxel en la posición del disparo sobre la silueta del objetivo, y propagarlo a través del evento `OnShotFired`.

**Architecture:** Se añaden dos tipos nuevos (`ShotZone`, `ShotZoneDefinition`), se cambia la firma de `IAimView.OnShotFired` de `Action<Vector2>` a `Action<Vector2, ShotZone>`, y `AimViewController` adquiere la lógica de muestreo (`SampleSilhouette`) y resolución de zona (`ResolveZone`). `ResolveZone` es `internal static` para poder testearse directamente en EditMode.

**Spec:** Implements [[Sistema de Deteccion de Impacto]] — `docs/plans/2026-03-03-hit-detection-design.md`

**Tech Stack:** Unity 2D UI (Image, RectTransform), Unity EditMode Tests (NUnit), DOTween (sin cambios)

---

### Task 1: Añadir `ShotZone` y `ShotZoneDefinition`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotZone.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotZoneDefinition.cs`

**Step 1: Crear `ShotZone.cs`**

```csharp
namespace CrimsonDraft.Combat
{
    public enum ShotZone
    {
        Miss = 0,
        Hit  = 1,
    }
}
```

**Step 2: Crear `ShotZoneDefinition.cs`**

```csharp
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    [Serializable]
    public struct ShotZoneDefinition
    {
        public Color    color;
        public ShotZone zone;
    }
}
```

**Step 3: Verificar compilación**

Usar `read_console` filtrando por Error. Esperar: sin errores de compilación.

**Step 4: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotZone.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotZoneDefinition.cs
git commit -m "feat(combat-ui): add ShotZone enum and ShotZoneDefinition struct"
```

---

### Task 2: Tests fallidos para `ResolveZone`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/AimViewControllerTests.cs`

**Step 1: Crear el archivo de tests**

```csharp
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class AimViewControllerTests
    {
        private static ShotZoneDefinition[] StandardPalette() => new[]
        {
            new ShotZoneDefinition { color = Color.white, zone = ShotZone.Hit  },
            new ShotZoneDefinition { color = Color.black, zone = ShotZone.Miss },
        };

        [Test]
        public void ResolveZone_exactWhite_returnsHit()
        {
            var result = AimViewController.ResolveZone(Color.white, StandardPalette(), 0.1f);
            Assert.AreEqual(ShotZone.Hit, result);
        }

        [Test]
        public void ResolveZone_exactBlack_returnsMiss()
        {
            var result = AimViewController.ResolveZone(Color.black, StandardPalette(), 0.1f);
            Assert.AreEqual(ShotZone.Miss, result);
        }

        [Test]
        public void ResolveZone_nearWhite_withinTolerance_returnsHit()
        {
            var nearWhite = new Color(0.95f, 0.95f, 0.95f);
            var result = AimViewController.ResolveZone(nearWhite, StandardPalette(), 0.1f);
            Assert.AreEqual(ShotZone.Hit, result);
        }

        [Test]
        public void ResolveZone_unknownColor_outsideTolerance_returnsMiss()
        {
            // Color.red tiene distancia euclidiana ~1.41 de blanco y negro → fuera de tolerancia 0.1
            var result = AimViewController.ResolveZone(Color.red, StandardPalette(), 0.1f);
            Assert.AreEqual(ShotZone.Miss, result);
        }

        [Test]
        public void ResolveZone_emptyPalette_returnsMiss()
        {
            var result = AimViewController.ResolveZone(Color.white, new ShotZoneDefinition[0], 0.1f);
            Assert.AreEqual(ShotZone.Miss, result);
        }
    }
}
```

**Step 2: Correr los tests — verificar que fallan**

Usar `run_tests` con mode=EditMode. Esperar: 5 tests fallan con
`"ResolveZone" does not exist in "AimViewController"` o error de compilación equivalente.

---

### Task 3: Implementar `ResolveZone` en `AimViewController`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs`

El método es `internal static` para que `CrimsonDraft.Tests.EditMode` pueda llamarlo
(ya tiene `[assembly: InternalsVisibleTo("CrimsonDraft.Tests.EditMode")]` en `AssemblyInfo.cs`).

**Step 1: Añadir el método al final de la región `#region Private` (antes del `#endif` del gizmo)**

```csharp
internal static ShotZone ResolveZone(Color pixel, ShotZoneDefinition[] definitions, float tolerance)
{
    float bestDistSq = float.MaxValue;
    ShotZone bestZone = ShotZone.Miss;
    bool found = false;

    foreach (var def in definitions)
    {
        float dr = pixel.r - def.color.r;
        float dg = pixel.g - def.color.g;
        float db = pixel.b - def.color.b;
        float distSq = dr * dr + dg * dg + db * db;
        if (distSq < bestDistSq)
        {
            bestDistSq = distSq;
            bestZone   = def.zone;
            found      = true;
        }
    }

    return (found && bestDistSq <= tolerance * tolerance) ? bestZone : ShotZone.Miss;
}
```

**Step 2: Correr los tests — verificar que pasan**

Usar `run_tests` con mode=EditMode.
Esperar: 5 tests en `AimViewControllerTests` pasan. Los tests de `CombatMenuControllerTests` también pasan (sin cambios).

**Step 3: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/AimViewControllerTests.cs
git commit -m "feat(combat-ui): implement ResolveZone — palette-based color matching"
```

---

### Task 4: Actualizar firma de `IAimView` y todos sus consumidores

Este task cambia 4 archivos a la vez para evitar estado de compilación roto.
Todos los cambios son mecánicos (firma de evento/método). No hay lógica nueva excepto
los campos de inspector y `SampleSilhouette` en `AimViewController`.

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

#### 4a — `IAimView.cs`

Cambiar la línea del evento:

```csharp
// ANTES:
event Action<Vector2>? OnShotFired;

// DESPUÉS:
event Action<Vector2, ShotZone>? OnShotFired;
```

#### 4b — `AimViewController.cs`

**Campos nuevos** (en la región `#region Fields`, junto a los otros `[SerializeField]`):

```csharp
[SerializeField] private Image                silhouetteImage = null!;
[SerializeField] private ShotZoneDefinition[] zoneDefinitions = Array.Empty<ShotZoneDefinition>();
[SerializeField] private float                colorTolerance  = 0.1f;
```

**Campo nuevo en variables privadas** (junto a `confirmedLocalPos` y `pendingNormalizedShot`):

```csharp
private ShotZone pendingZone;
```

**Actualizar el evento** (en la región `#region Events`):

```csharp
// ANTES:
public event Action<Vector2>? OnShotFired;

// DESPUÉS:
public event Action<Vector2, ShotZone>? OnShotFired;
```

**Actualizar `Confirm()` en la rama `HorizontalAiming`** — añadir el cálculo de zona
justo antes de `this.phase = AimPhase.WaitingDismiss`:

```csharp
// Añadir estas dos líneas antes de "this.phase = AimPhase.WaitingDismiss":
var shotLocal = this.ComputeRandomShotLocal();
this.SpawnDispersionCircle(this.confirmedLocalPos);
this.SpawnMarker(shotLocal);
this.pendingNormalizedShot = this.NormalizeShotLocal(shotLocal);
this.pendingZone           = this.SampleSilhouette(shotLocal);   // ← nueva línea
this.phase = AimPhase.WaitingDismiss;
```

**Actualizar `Confirm()` en la rama `WaitingDismiss`**:

```csharp
// ANTES:
this.OnShotFired?.Invoke(this.pendingNormalizedShot);

// DESPUÉS:
this.OnShotFired?.Invoke(this.pendingNormalizedShot, this.pendingZone);
```

**Añadir método `SampleSilhouette`** en la región `#region Private`
(junto a `ResolveZone`, antes del gizmo):

```csharp
private ShotZone SampleSilhouette(Vector2 shotLocal)
{
    if (this.silhouetteImage == null || this.silhouetteImage.sprite == null)
        return ShotZone.Miss;

    var worldPos   = this.aimSpace.TransformPoint(new Vector3(shotLocal.x, shotLocal.y, 0f));
    var silRt      = this.silhouetteImage.rectTransform;
    var localInSil = silRt.InverseTransformPoint(worldPos);
    var rect       = silRt.rect;

    float u = Mathf.Clamp01((localInSil.x - rect.xMin) / rect.width);
    float v = Mathf.Clamp01((localInSil.y - rect.yMin) / rect.height);

    var tex   = this.silhouetteImage.sprite.texture;
    var pixel = tex.GetPixel(
        Mathf.RoundToInt(u * (tex.width  - 1)),
        Mathf.RoundToInt(v * (tex.height - 1)));

    return ResolveZone(pixel, this.zoneDefinitions, this.colorTolerance);
}
```

#### 4c — `CombatMenuController.cs`

Actualizar `HandleShotFired`:

```csharp
// ANTES:
private void HandleShotFired(Vector2 _)

// DESPUÉS:
private void HandleShotFired(Vector2 normalizedPos, ShotZone zone)
```

El cuerpo del método no cambia. `zone` queda disponible para uso futuro.

#### 4d — `CombatMenuControllerTests.cs`

Actualizar `FakeAimView`:

```csharp
// ANTES:
public event Action<Vector2>? OnShotFired;
// ...
public void FireShot(Vector2 pos) => this.OnShotFired?.Invoke(pos);

// DESPUÉS:
public event Action<Vector2, ShotZone>? OnShotFired;
// ...
public void FireShot(Vector2 pos, ShotZone zone = ShotZone.Miss) =>
    this.OnShotFired?.Invoke(pos, zone);
```

Las llamadas existentes a `this.aimView.FireShot(Vector2.zero)` no necesitan
cambiar porque `zone` tiene valor default.

**Step 1: Aplicar todos los cambios de 4a–4d**

**Step 2: Verificar compilación**

Usar `read_console` filtrando por Error. Esperar: sin errores.

**Step 3: Correr todos los tests**

Usar `run_tests` con mode=EditMode.
Esperar: todos los tests pasan (los 5 de `AimViewControllerTests` + todos los de `CombatMenuControllerTests`).

**Step 4: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat-ui): wire hit detection into OnShotFired — ShotZone payload"
```

---

### Task 5: Configurar Unity Editor

Estos son pasos manuales en el Editor — no se pueden automatizar vía MCP.

**Step 1: Habilitar Read/Write en la textura de la silueta**

1. En el Project window, localizar el sprite de la silueta del objetivo
2. En el Inspector → Texture Import Settings → sección Advanced
3. Marcar **Read/Write** (checkbox)
4. Click **Apply**

Sin este paso, `tex.GetPixel()` lanzará `UnityException` en runtime.

**Step 2: Asignar los campos nuevos en el AimView prefab/escena**

En el GameObject que tiene `AimViewController`:
- `Silhouette Image` → asignar el `Image` component de la silueta
- `Zone Definitions` → añadir 2 entradas:
  - Entry 0: Color = blanco (#FFFFFF), Zone = Hit
  - Entry 1: Color = negro (#000000), Zone = Miss
- `Color Tolerance` → 0.1 (default)

**Step 3: Verificar en Play Mode**

Ejecutar un disparo. En el log de Unity (si hay Debug.Log temporal) o en el
inspector durante runtime, verificar que `pendingZone` resuelve `Hit` cuando el
marker cae en la zona blanca y `Miss` cuando cae fuera.
