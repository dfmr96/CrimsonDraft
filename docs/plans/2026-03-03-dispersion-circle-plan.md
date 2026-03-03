# Dispersion Circle — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Reemplazar el SpawnMarker exacto en `AimViewController` por un sistema de dispersión invisible donde el shot marker aparece en una posición aleatoria redondeada a pixel dentro de un radio configurable.

**Architecture:** Todo el cambio vive en `AimViewController.cs`. La fase `HorizontalAiming` deja de spawnear el marker y en cambio guarda la intersección en espacio local. La fase `WaitingDismiss` calcula la posición aleatoria (polar + round), spawna el marker ahí y dispara el evento. El gizmo es editor-only.

**Tech Stack:** Unity 6, C# 9, DOTween, `#nullable enable`, `#if UNITY_EDITOR`

---

### Task 1: Actualizar campos de la clase

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs`

**Step 1: Eliminar campos obsoletos y agregar los nuevos**

En la región `#region Fields`, hacer los siguientes cambios:

Eliminar estas tres líneas:
```csharp
private float    confirmedY;
private float    confirmedWorldY;
private Vector2  pendingShot;
```

Agregar estas dos líneas después de `private AimPhase phase;`:
```csharp
[SerializeField] private int     dispersionRadius;
private                  Vector2 confirmedLocalPos;
```

El bloque de campos privados queda así:
```csharp
private AimPhase phase;
private Vector2  confirmedLocalPos;
```

Y el SerializeField de `shotMarkerPrefab` sigue igual. El nuevo `dispersionRadius` va junto a los otros SerializeFields:
```csharp
[SerializeField] private float         speed              = 0.8f;
[SerializeField] private float         dimmingAlpha       = 0.3f;
[SerializeField] private int           dispersionRadius   = 10;
```

**Step 2: Compilar y verificar zero warnings**

En Unity Editor, verificar que la consola no tiene errores de compilación.
Puede haber warnings de campos no usados por los eliminados — van a desaparecer en los siguientes tasks.

---

### Task 2: Reescribir `Confirm()` — fase HorizontalAiming

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs:64-78`

**Step 1: Reemplazar el bloque `HorizontalAiming`**

El bloque actual (líneas 64–78 aprox.) es:
```csharp
else if (this.phase == AimPhase.HorizontalAiming)
{
    this.horizontalSelector.rectTransform.DOKill();
    var hLocal = this.horizontalSelector.rectTransform.localPosition;
    hLocal.x   = Mathf.Round(hLocal.x);
    this.horizontalSelector.rectTransform.localPosition = hLocal;

    float halfW      = this.horizontalSpace.rect.width / 2f;
    float confirmedX = (hLocal.x + halfW) / (halfW * 2f);
    float confirmedWorldX = this.horizontalSelector.rectTransform.position.x;

    this.horizontalSelector.DOFade(this.dimmingAlpha, 0.15f);
    this.pendingShot = new Vector2(confirmedX, this.confirmedY);
    this.SpawnMarker(confirmedWorldX, this.confirmedWorldY);
    this.phase = AimPhase.WaitingDismiss;
}
```

Reemplazar por:
```csharp
else if (this.phase == AimPhase.HorizontalAiming)
{
    this.horizontalSelector.rectTransform.DOKill();
    var hLocal = this.horizontalSelector.rectTransform.localPosition;
    hLocal.x   = Mathf.Round(hLocal.x);
    this.horizontalSelector.rectTransform.localPosition = hLocal;

    this.horizontalSelector.DOFade(this.dimmingAlpha, 0.15f);

    var worldIntersection = new Vector3(
        this.horizontalSelector.rectTransform.position.x,
        this.verticalSelector.rectTransform.position.y,
        this.aimSpace.position.z);
    this.confirmedLocalPos = this.aimSpace.InverseTransformPoint(worldIntersection);

    this.phase = AimPhase.WaitingDismiss;
}
```

**Step 2: Verificar compilación**

Habrá un warning/error por `confirmedX`, `confirmedWorldX`, `confirmedWorldY` ya no usados — están eliminados. Verificar zero errores en consola Unity.

---

### Task 3: Reescribir `Confirm()` — fase VerticalAiming

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs:49-63`

**Step 1: Reemplazar el bloque `VerticalAiming`**

El bloque actual calcula `confirmedY` y `confirmedWorldY` que ya no existen. Reemplazarlo por:

```csharp
if (this.phase == AimPhase.VerticalAiming)
{
    this.verticalSelector.rectTransform.DOKill();
    var vLocal = this.verticalSelector.rectTransform.localPosition;
    vLocal.y   = Mathf.Round(vLocal.y);
    this.verticalSelector.rectTransform.localPosition = vLocal;

    this.verticalSelector.DOFade(this.dimmingAlpha, 0.15f);
    this.StartHorizontalOscillation();
    this.phase = AimPhase.HorizontalAiming;
}
```

(Se eliminan las dos líneas que asignaban `confirmedY` y `confirmedWorldY`.)

**Step 2: Verificar compilación — zero warnings/errors**

---

### Task 4: Reescribir `Confirm()` — fase WaitingDismiss

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs:80-83`

**Step 1: Reemplazar el bloque `WaitingDismiss`**

El bloque actual:
```csharp
else if (this.phase == AimPhase.WaitingDismiss)
{
    this.OnShotFired?.Invoke(this.pendingShot);
}
```

Reemplazar por:
```csharp
else if (this.phase == AimPhase.WaitingDismiss)
{
    var shotLocal = this.ComputeRandomShotLocal();
    this.SpawnMarker(shotLocal);
    this.OnShotFired?.Invoke(this.NormalizeShotLocal(shotLocal));
}
```

**Step 2: Verificar compilación**

`ComputeRandomShotLocal`, `SpawnMarker(Vector2)` y `NormalizeShotLocal` aún no existen — habrá errores de compilación. Se resuelven en los siguientes tasks.

---

### Task 5: Reescribir `SpawnMarker` con firma local

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs:129-138`

**Step 1: Reemplazar `SpawnMarker(float worldX, float worldY)` por `SpawnMarker(Vector2 localPos)`**

Método actual:
```csharp
private void SpawnMarker(float worldX, float worldY)
{
    var worldPos = new Vector3(worldX, worldY, this.aimSpace.position.z);
    var localPos = this.aimSpace.InverseTransformPoint(worldPos);
    var marker   = Instantiate(this.shotMarkerPrefab, this.aimSpace);
    ((RectTransform)marker.transform).localPosition = new Vector3(
        Mathf.Round(localPos.x),
        Mathf.Round(localPos.y),
        0f);
}
```

Reemplazar por:
```csharp
private void SpawnMarker(Vector2 localPos)
{
    var marker = Instantiate(this.shotMarkerPrefab, this.aimSpace);
    ((RectTransform)marker.transform).localPosition = new Vector3(
        Mathf.Round(localPos.x),
        Mathf.Round(localPos.y),
        0f);
}
```

La conversión world→local ya no es necesaria porque `confirmedLocalPos` y el resultado de `ComputeRandomShotLocal` ya están en espacio local de `aimSpace`.

**Step 2: Verificar compilación — solo deben quedar errores por los dos métodos aún sin crear**

---

### Task 6: Agregar `ComputeRandomShotLocal` y `NormalizeShotLocal`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs` — dentro de `#region Private`, después de `SpawnMarker`

**Step 1: Agregar los dos métodos**

```csharp
private Vector2 ComputeRandomShotLocal()
{
    float angle  = Random.value * Mathf.PI * 2f;
    float r      = this.dispersionRadius * Mathf.Sqrt(Random.value);
    return new Vector2(
        Mathf.Round(this.confirmedLocalPos.x + r * Mathf.Cos(angle)),
        Mathf.Round(this.confirmedLocalPos.y + r * Mathf.Sin(angle)));
}

private Vector2 NormalizeShotLocal(Vector2 localPos)
{
    float halfW = this.aimSpace.rect.width  / 2f;
    float halfH = this.aimSpace.rect.height / 2f;
    return new Vector2(
        Mathf.Clamp01((localPos.x + halfW) / (halfW * 2f)),
        Mathf.Clamp01((localPos.y + halfH) / (halfH * 2f)));
}
```

**Nota sobre la fórmula:** `r = radius * √(Random.value)` garantiza distribución uniforme dentro del círculo (sin concentración en el centro). Extraído directamente del prototipo Python `apply_three_layer_dispersion`.

**Step 2: Verificar compilación — zero errores**

**Step 3: Test en Play Mode**

1. Entrar en Play Mode
2. Activar el AimView (según el flujo de la escena de combate)
3. Confirmar V → confirmar H → verificar que NO aparece nada todavía
4. Confirmar disparo → verificar que el shot marker aparece en una posición cercana (pero no exacta) a la intersección de selectores
5. Repetir varias veces — el marker debe caer en posiciones distintas

---

### Task 7: Agregar Gizmo editor-only

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs` — al final de `#region Private`, antes del `#endregion`

**Step 1: Agregar el método gizmo**

```csharp
#if UNITY_EDITOR
private void OnDrawGizmosSelected()
{
    if (this.aimSpace == null) return;

    int   r      = this.dispersionRadius;
    var   center = this.aimSpace.position;
    float scale  = this.aimSpace.lossyScale.x;
    float cube   = scale * 0.8f;

    Gizmos.color = new Color(1f, 1f, 0.2f, 0.5f);
    for (int y = -r; y <= r; y++)
        for (int x = -r; x <= r; x++)
            if (x * x + y * y <= r * r)
                Gizmos.DrawCube(
                    center + new Vector3(x * scale, y * scale, 0f),
                    Vector3.one * cube);
}
#endif
```

**Step 2: Verificar en Scene View**

1. Salir de Play Mode
2. Seleccionar el GameObject que tiene `AimViewController`
3. En Scene View debe aparecer una nube de cubitos amarillos en forma de círculo, centrada en el `aimSpace`
4. Cambiar `dispersionRadius` en el Inspector → los cubitos deben actualizarse en tiempo real

**Step 3: Verificar que el Gizmo NO existe en build**

El `#if UNITY_EDITOR` lo garantiza. No hay acción adicional.

---

### Task 8: Commit final

**Step 1: Verificar estado**

```bash
cd "D:/Proyectos Unity/CrimsonDraft/CrimsonDraft"
git diff Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs
```

Confirmar que solo hay cambios en `AimViewController.cs` y el nuevo doc.

**Step 2: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs
git add docs/plans/2026-03-03-dispersion-circle-design.md
git add docs/plans/2026-03-03-dispersion-circle-plan.md
git commit -m "feat(combat-ui): replace exact shot marker with pixel-rounded dispersion radius"
```
