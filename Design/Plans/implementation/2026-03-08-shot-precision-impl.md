# Shot Precision Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Introducir precisión de disparo data-driven: cada color del mask sprite lleva zona anatómica + multiplicador de precisión configurable, dando `damage = BaseDamage × zoneMult × precisionMult`.

**Architecture:** Se añaden `ShotPrecision` (enum de label) y `ShotPrecisionEntry` (struct con enum + float). `ShotZoneDefinition` incorpora `ShotPrecisionEntry`. `ResolveZone` devuelve `ShotZoneDefinition?` en lugar de `ShotZone`. `ResolvedShot` expone `Precision`. `ComputeShotDamage` recibe zona + multiplicador float.

**Tech Stack:** Unity 6, C# 9, NUnit (Unity Test Runner EditMode), VContainer. Tests corren via MCP `run_tests` o Unity Test Runner window.

---

## Task 1: Crear ShotPrecision enum y ShotPrecisionEntry struct

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotPrecision.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotPrecisionEntry.cs`

No hay tests para estos tipos — son data pura sin lógica. Se verifica compilación en Task 2.

**Step 1: Crear ShotPrecision.cs**

```csharp
namespace CrimsonDraft.Combat
{
    public enum ShotPrecision
    {
        Normal    = 0,
        Graze     = 1,
        WeakPoint = 2,
    }
}
```

**Step 2: Crear ShotPrecisionEntry.cs**

```csharp
using System;

namespace CrimsonDraft.Combat
{
    [Serializable]
    public struct ShotPrecisionEntry
    {
        public ShotPrecision precision;
        public float         multiplier;
    }
}
```

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotPrecision.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotPrecisionEntry.cs
git commit -m "feat(combat): add ShotPrecision enum and ShotPrecisionEntry struct"
```

---

## Task 2: Actualizar ShotZoneDefinition para incluir precisionEntry

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotZoneDefinition.cs`

**Step 1: Actualizar el struct**

Reemplazar el contenido completo:

```csharp
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    [Serializable]
    public struct ShotZoneDefinition
    {
        public Color              color;
        public ShotZone           zone;
        public ShotPrecisionEntry precisionEntry;
    }
}
```

**Step 2: Verificar compilación**

Abre Unity — debe compilar sin errores. Los assets existentes de `AimHitMaskProfile` tendrán `precisionEntry.multiplier = 0` y `precisionEntry.precision = Normal` por defecto de serialización. El safety net `Mathf.Max(1f, ...)` se añade en Task 5.

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotZoneDefinition.cs
git commit -m "feat(combat): add precisionEntry to ShotZoneDefinition"
```

---

## Task 3: Actualizar ResolvedShot para exponer Precision

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ResolvedShot.cs`

**Step 1: Actualizar el struct**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public readonly struct ResolvedShot
    {
        public int           Index         { get; }
        public Vector2       NormalizedPos { get; }
        public ShotZone      Zone          { get; }
        public ShotPrecision Precision     { get; }
        public int           Damage        { get; }

        public ResolvedShot(int index, Vector2 normalizedPos, ShotZone zone, ShotPrecision precision, int damage)
        {
            this.Index         = index;
            this.NormalizedPos = normalizedPos;
            this.Zone          = zone;
            this.Precision     = precision;
            this.Damage        = damage;
        }
    }
}
```

**Step 2: Verificar que los call sites del constructor fallen (expected)**

Tras guardar, Unity mostrará errores en:
- `AimViewController.BuildResolvedShots` (construye `ResolvedShot`)
- Tests en `CombatMenuControllerTests` que construyan `ResolvedShot`

Son errores esperados — se resuelven en Tasks 4 y 5.

**Step 3: Commit (aunque haya errores de compilación temporales — se resuelven en tasks siguientes)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/ResolvedShot.cs
git commit -m "feat(combat): add Precision field to ResolvedShot"
```

---

## Task 4: Actualizar ResolveZone → devuelve ShotZoneDefinition?

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/AimViewControllerTests.cs`

`ResolveZone` es `internal static` y está cubierta por tests. Primero actualizamos los tests para la nueva firma, luego la implementación.

**Step 1: Actualizar tests de ResolveZone en AimViewControllerTests.cs**

Reemplazar el helper `StandardPalette()` y todos los tests de `ResolveZone`:

```csharp
private static ShotZoneDefinition[] StandardPalette() => new[]
{
    new ShotZoneDefinition
    {
        color          = Color.white,
        zone           = ShotZone.Hit,
        precisionEntry = new ShotPrecisionEntry { precision = ShotPrecision.Normal, multiplier = 1f }
    },
    new ShotZoneDefinition
    {
        color          = Color.black,
        zone           = ShotZone.Miss,
        precisionEntry = new ShotPrecisionEntry { precision = ShotPrecision.Normal, multiplier = 0f }
    },
};

[Test]
public void ResolveZone_exactWhite_returnsHitDefinition()
{
    var result = AimViewController.ResolveZone(Color.white, StandardPalette(), 0.1f);
    Assert.IsTrue(result.HasValue);
    Assert.AreEqual(ShotZone.Hit, result!.Value.zone);
    Assert.AreEqual(ShotPrecision.Normal, result.Value.precisionEntry.precision);
}

[Test]
public void ResolveZone_exactBlack_returnsMissDefinition()
{
    var result = AimViewController.ResolveZone(Color.black, StandardPalette(), 0.1f);
    Assert.IsTrue(result.HasValue);
    Assert.AreEqual(ShotZone.Miss, result!.Value.zone);
}

[Test]
public void ResolveZone_nearWhite_withinTolerance_returnsHit()
{
    var nearWhite = new Color(0.95f, 0.95f, 0.95f);
    var result = AimViewController.ResolveZone(nearWhite, StandardPalette(), 0.1f);
    Assert.IsTrue(result.HasValue);
    Assert.AreEqual(ShotZone.Hit, result!.Value.zone);
}

[Test]
public void ResolveZone_unknownColor_outsideTolerance_returnsNull()
{
    var result = AimViewController.ResolveZone(Color.red, StandardPalette(), 0.1f);
    Assert.IsFalse(result.HasValue);
}

[Test]
public void ResolveZone_emptyPalette_returnsNull()
{
    var result = AimViewController.ResolveZone(Color.white, new ShotZoneDefinition[0], 0.1f);
    Assert.IsFalse(result.HasValue);
}

[Test]
public void ResolveZone_preservesPrecisionEntry()
{
    var palette = new[]
    {
        new ShotZoneDefinition
        {
            color          = Color.red,
            zone           = ShotZone.Head,
            precisionEntry = new ShotPrecisionEntry { precision = ShotPrecision.WeakPoint, multiplier = 2f }
        }
    };
    var result = AimViewController.ResolveZone(Color.red, palette, 0.1f);
    Assert.IsTrue(result.HasValue);
    Assert.AreEqual(ShotPrecision.WeakPoint, result!.Value.precisionEntry.precision);
    Assert.AreEqual(2f, result.Value.precisionEntry.multiplier);
}
```

**Step 2: Correr tests — deben fallar (firma vieja devuelve ShotZone)**

Usar MCP `run_tests` o Unity Test Runner > EditMode > AimViewControllerTests. Esperado: error de compilación o fallo de assertion.

**Step 3: Actualizar ResolveZone en AimViewController.cs**

Localizar `internal static ShotZone ResolveZone(...)` (línea ~443) y reemplazar:

```csharp
internal static ShotZoneDefinition? ResolveZone(Color pixel, ShotZoneDefinition[] definitions, float tolerance)
{
    if (definitions == null || definitions.Length == 0)
        return null;

    float bestDistSq = float.MaxValue;
    int   bestIdx    = -1;

    for (int i = 0; i < definitions.Length; i++)
    {
        float dr     = pixel.r - definitions[i].color.r;
        float dg     = pixel.g - definitions[i].color.g;
        float db     = pixel.b - definitions[i].color.b;
        float distSq = dr * dr + dg * dg + db * db;
        if (distSq < bestDistSq)
        {
            bestDistSq = distSq;
            bestIdx    = i;
        }
    }

    return (bestIdx >= 0 && bestDistSq <= tolerance * tolerance)
        ? definitions[bestIdx]
        : null;
}
```

**Step 4: Correr tests — deben pasar**

Esperado: todos los tests de `ResolveZone` en verde.

**Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/AimViewControllerTests.cs
git commit -m "refactor(combat): ResolveZone returns ShotZoneDefinition? instead of ShotZone"
```

---

## Task 5: Actualizar SampleSilhouette y BuildResolvedShots en AimViewController

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs`

Estos métodos son privados — no tienen tests directos. Se ajustan para usar la nueva firma de `ResolveZone`.

**Step 1: Actualizar SampleSilhouette**

Localizar `private ShotZone SampleSilhouette(Vector2 shotLocal)` y cambiar firma + return a `ShotZoneDefinition?`:

```csharp
private ShotZoneDefinition? SampleSilhouette(Vector2 shotLocal)
{
    if (this.silhouetteImage == null)
        return null;
    if (this.activeZoneMaskSprite == null)
    {
        if (!this.warnedMissingMaskConfig)
        {
            Debug.LogWarning("[AimView] Missing hit mask profile. Returning null until ConfigureHitMask(...) is provided.");
            this.warnedMissingMaskConfig = true;
        }
        return null;
    }

    var worldPos   = this.aimSpace.TransformPoint(new Vector3(shotLocal.x, shotLocal.y, 0f));
    var silRt      = this.silhouetteImage.rectTransform;
    var localInSil = silRt.InverseTransformPoint(worldPos);
    var rect       = silRt.rect;
    if (rect.width <= 0f || rect.height <= 0f)
        return null;

    float u = Mathf.Clamp01((localInSil.x - rect.xMin) / rect.width);
    float v = Mathf.Clamp01((localInSil.y - rect.yMin) / rect.height);

    var sprite     = this.activeZoneMaskSprite;
    var tex        = sprite.texture;
    var pixelCoord = MapUvToTexturePixel(sprite, u, v);
    int px = pixelCoord.x;
    int py = pixelCoord.y;
    var texRect = sprite.textureRect;
    var pixel   = tex.GetPixel(px, py);
    var def     = ResolveZone(pixel, this.activeZoneDefinitions, this.activeColorTolerance);
    string hex  = $"#{ColorUtility.ToHtmlStringRGB(pixel)}";
    string spriteName  = sprite.name;
    string textureName = tex.name;
    Debug.Log(
        $"[AimView] Sampled sprite='{spriteName}' texture='{textureName}' px=({px},{py}) color={hex} ({pixel}) -> Zone: {def?.zone} Precision: {def?.precisionEntry.precision}");
#if UNITY_EDITOR
    float uCenter = Mathf.Clamp01(((px - texRect.xMin) + 0.5f) / texRect.width);
    float vCenter = Mathf.Clamp01(((py - texRect.yMin) + 0.5f) / texRect.height);
    float sampleX = Mathf.Lerp(rect.xMin, rect.xMax, uCenter);
    float sampleY = Mathf.Lerp(rect.yMin, rect.yMax, vCenter);
    this.hasLastSample      = true;
    this.lastSampleWorldPos = silRt.TransformPoint(new Vector3(sampleX, sampleY, 0f));
    this.lastSamplePixel    = new Vector2Int(px, py);
    this.lastSampleColor    = pixel;
    this.lastSampleHex      = hex;
#endif
    return def;
}
```

**Step 2: Actualizar BuildResolvedShots**

Localizar `private ResolvedShot[] BuildResolvedShots(...)` y reemplazar el cuerpo del loop:

```csharp
private ResolvedShot[] BuildResolvedShots(Vector2 firstShotLocal, int count)
{
    int clampedCount = Mathf.Max(1, count);
    var resolved = new ResolvedShot[clampedCount];
    for (int i = 0; i < clampedCount; i++)
    {
        Vector2           shotLocal  = ComputeBulletLocalFromPrimary(firstShotLocal, i, this.perBulletYOffset);
        Vector2           normalized = this.NormalizeShotLocal(shotLocal);
        ShotZoneDefinition? def      = this.SampleSilhouette(shotLocal);
        ShotZone          zone       = def?.zone ?? ShotZone.Miss;
        ShotPrecision     precision  = def?.precisionEntry.precision ?? ShotPrecision.Normal;
        float             precMult   = def.HasValue ? Mathf.Max(1f, def.Value.precisionEntry.multiplier) : 0f;
        int               damage     = CombatMenuController.ComputeShotDamage(zone, precMult);
        resolved[i] = new ResolvedShot(i, normalized, zone, precision, damage);
    }
    return resolved;
}
```

> Nota: el `Mathf.Max(1f, ...)` se aplica solo cuando `def.HasValue` — si es Miss, el multiplicador es 0 (sin daño).

**Step 3: Verificar compilación en Unity**

Puede haber un error en `AimingState` o en otros sitios que lean `ResolvedShot.Zone`. El campo `Damage` ya existe, `Precision` es nuevo y aditivo. Revisar la consola.

**Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs
git commit -m "refactor(combat): SampleSilhouette and BuildResolvedShots use ShotZoneDefinition?"
```

---

## Task 6: Actualizar ComputeShotDamage y sus tests

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Step 1: Actualizar tests de ComputeShotDamage en CombatMenuControllerTests.cs**

Reemplazar los 5 tests existentes de `ComputeShotDamage_*`:

```csharp
[Test]
public void ComputeShotDamage_head_normalPrecision_returns40()
{
    Assert.AreEqual(40, CombatMenuController.ComputeShotDamage(ShotZone.Head, 1f));
}

[Test]
public void ComputeShotDamage_head_graze_returns20()
{
    Assert.AreEqual(20, CombatMenuController.ComputeShotDamage(ShotZone.Head, 0.5f));
}

[Test]
public void ComputeShotDamage_head_weakPoint_returns80()
{
    Assert.AreEqual(80, CombatMenuController.ComputeShotDamage(ShotZone.Head, 2f));
}

[Test]
public void ComputeShotDamage_torso_normalPrecision_returns20()
{
    Assert.AreEqual(20, CombatMenuController.ComputeShotDamage(ShotZone.Torso, 1f));
}

[Test]
public void ComputeShotDamage_torso_graze_returns10()
{
    Assert.AreEqual(10, CombatMenuController.ComputeShotDamage(ShotZone.Torso, 0.5f));
}

[Test]
public void ComputeShotDamage_arms_normalPrecision_returns14()
{
    Assert.AreEqual(14, CombatMenuController.ComputeShotDamage(ShotZone.Arms, 1f));
}

[Test]
public void ComputeShotDamage_legs_normalPrecision_returns16()
{
    Assert.AreEqual(16, CombatMenuController.ComputeShotDamage(ShotZone.Legs, 1f));
}

[Test]
public void ComputeShotDamage_miss_returns0()
{
    Assert.AreEqual(0, CombatMenuController.ComputeShotDamage(ShotZone.Miss, 1f));
}
```

**Step 2: Correr tests — deben fallar (firma vieja)**

Esperado: error de compilación porque `ComputeShotDamage(ShotZone)` (1 arg) no acepta 2 args.

**Step 3: Actualizar ComputeShotDamage en CombatMenuController.cs**

Localizar `internal static int ComputeShotDamage(ShotZone zone)` (línea ~168) y reemplazar:

```csharp
internal static int ComputeShotDamage(ShotZone zone, float precisionMultiplier)
{
    float zoneMult = zone switch
    {
        ShotZone.Head  => 2.0f,
        ShotZone.Torso => 1.0f,
        ShotZone.Arms  => 0.7f,
        ShotZone.Legs  => 0.8f,
        ShotZone.Hit   => 1.0f,
        _              => 0.0f,
    };
    return Mathf.RoundToInt(BaseDamage * zoneMult * precisionMultiplier);
}
```

**Step 4: Correr todos los tests EditMode**

Usar MCP `run_tests` o Unity Test Runner > EditMode. Esperado: todos en verde.

**Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): ComputeShotDamage accepts precision multiplier, tests updated"
```

---

## Task 7: Commit final de assets .meta y verificación

**Step 1: Verificar nuevos .meta**

Unity genera `.meta` para los dos archivos nuevos. Incluirlos:

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotPrecision.cs.meta
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/ShotPrecisionEntry.cs.meta
git status   # verificar que no queden cambios sin commitear
```

**Step 2: Commit si hay .meta pendientes**

```bash
git commit -m "chore(assets): add .meta files for ShotPrecision and ShotPrecisionEntry"
```

**Step 3: Smoke test en Play Mode**

Entrar a una escena de combate, seleccionar operador con arma, seleccionar Shoot, completar el QTE. Verificar en Console:
- `[AimView] Sampled ... Zone: Head Precision: Normal` (o Graze/WeakPoint)
- El daño aplicado al enemigo es correcto según la zona y precisión

---

## Referencia rápida de daños con el sistema completo

| Zona | precMult | Daño (BaseDamage=20) |
|------|----------|----------------------|
| Head - Graze | 0.5 | 20 |
| Head - Normal | 1.0 | 40 |
| Head - WeakPoint | 2.0 | 80 |
| Torso - Graze | 0.5 | 10 |
| Torso - Normal | 1.0 | 20 |
| Torso - WeakPoint | 2.0 | 40 |
| Arms - Normal | 1.0 | 14 |
| Legs - Normal | 1.0 | 16 |
| Miss | 0.0 | 0 |
