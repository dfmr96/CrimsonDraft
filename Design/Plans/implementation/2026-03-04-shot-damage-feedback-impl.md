# Shot Damage Feedback (Floating Text) — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Mostrar feedback visual del disparo resuelto en forma de texto flotante anclado al `ShotMarker`: daño (`-N`) en impacto y `MISS` en fallo.

**Architecture:**
- `CombatMenuController` sigue siendo el orquestador del flujo: recibe `OnShotFired`, calcula daño, aplica daño al enemigo y delega la visualización del feedback al `IAimView`.
- `IAimView` se extiende con una operación explícita para pintar feedback de disparo.
- `AimViewController` instancia un prefab de texto UI en una capa de feedback, lo posiciona según la coordenada normalizada del disparo, y anima `float-up + fade`.
- El texto se crea al resolver disparo (no al crear marker), en línea con el GDD aprobado.

**Spec:** Implements [[Sistema de Feedback de Daño de Disparo]] (`Sistema de Feedback de Daño de Disparo.md`)

**Tech Stack:** Unity 6, uGUI + TextMeshProUGUI, DOTween, NUnit EditMode

---

## Task 1 — Extender contrato de `IAimView`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs`

### Step 1: Añadir método de feedback visual

Agregar al contrato:

```csharp
void ShowShotFeedback(Vector2 normalizedPos, int damage, bool isMiss);
```

Convenciones:
- `normalizedPos` está en rango `[0..1]` relativo a `aimSpace`.
- `damage` es el daño final post-multiplicadores (entero).
- `isMiss=true` obliga texto `MISS`.

### Step 2: Verificar compilación esperada

Esperar errores de compilación hasta implementar en `AimViewController` y fakes de tests.

---

## Task 2 — Implementar texto flotante en `AimViewController`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs`

### Step 1: Añadir campos serializados de feedback

Añadir campos para configurar visual y límites:

- `RectTransform feedbackRoot`
- `GameObject feedbackTextPrefab`
- `Vector2 feedbackOffset = new(0f, 24f)`
- `float feedbackDuration = 0.60f`
- `float feedbackFloatY = 18f`
- `int maxConcurrentFeedback = 3`
- `Color hitFeedbackColor`
- `Color missFeedbackColor`

### Step 2: Añadir cola/lista de feedback activos

Mantener lista privada de instancias activas.
Regla: si se supera `maxConcurrentFeedback`, destruir o reciclar primero la más antigua.

### Step 3: Implementar `ShowShotFeedback(...)`

Flujo:
1. Convertir `normalizedPos` a local de `aimSpace`.
2. Sumar `feedbackOffset`.
3. Instanciar `feedbackTextPrefab` en `feedbackRoot` (o fallback a `aimSpace.parent` si es null).
4. Resolver texto:
   - `isMiss == true` => `MISS`
   - caso contrario => `-<damage>` (incluye `-0` si damage es 0)
5. Resolver color:
   - miss => `missFeedbackColor`
   - hit => `hitFeedbackColor`
6. Animar con DOTween:
   - mover +Y `feedbackFloatY`
   - fade a 0 en `feedbackDuration`
   - destruir al completar.

### Step 4: Limpieza defensiva

En `Hide()`:
- mantener limpieza actual de `aimSpace`.
- limpiar también feedback activos que sigan vivos al cerrar vista.

### Step 5: Verificar compilación

Esperar: sin errores.

---

## Task 3 — Integrar feedback en `CombatMenuController`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`

### Step 1: Disparar feedback al resolver disparo

Dentro de `HandleShotFired(Vector2 normalizedPos, ShotZone zone)`:

1. Calcular `damage` como hoy.
2. Determinar `isMiss = zone == ShotZone.Miss`.
3. Invocar:

```csharp
this.aimView.ShowShotFeedback(normalizedPos, damage, isMiss);
```

4. Mantener el flujo actual de daño/victoria y reset de estado.

### Step 2: Orden recomendado de operaciones

Orden para máxima coherencia:
1. calcular/aplicar daño,
2. mostrar feedback,
3. evaluar victoria,
4. limpiar estado (`Hide Aim`, etc.).

### Step 3: Mantener logs editor-only

No cambiar política actual de logs (`#if UNITY_EDITOR`).

---

## Task 4 — Tests EditMode del controlador

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

### Step 1: Extender `FakeAimView`

Agregar implementación de `ShowShotFeedback(...)` con campos de captura:
- `bool ShowShotFeedbackCalled`
- `Vector2 LastFeedbackPos`
- `int LastFeedbackDamage`
- `bool LastFeedbackIsMiss`

### Step 2: Añadir tests de comportamiento

Agregar:
1. `ShotFired_hit_callsShowShotFeedback_withDamageAndMissFalse`
2. `ShotFired_miss_callsShowShotFeedback_withMissTrue`

Assert mínimo:
- método llamado exactamente una vez por disparo,
- `damage` coincide con `ComputeShotDamage(zone)`,
- `isMiss` correcto.

### Step 3: Ejecutar suite EditMode

Esperar:
- tests existentes siguen pasando,
- tests nuevos del feedback pasan.

---

## Task 5 — Wiring manual en Unity

**Assets/Scene wiring:**
- `Game/CrimsonDraft/Assets/Scenes/Combat.unity`
- Nuevo prefab de feedback (ruta sugerida): `Game/CrimsonDraft/Assets/Prefabs/UI/ShotFeedbackText.prefab`

### Step 1: Crear prefab `ShotFeedbackText`

Contenido mínimo:
- `RectTransform`
- `CanvasGroup` (para fade)
- `TextMeshProUGUI` (alineación centrada)

### Step 2: Asignar referencias en `AimViewController`

En el objeto de escena/prefab donde está `AimViewController`:
- `Feedback Root` -> contenedor UI válido dentro del Canvas de combate.
- `Feedback Text Prefab` -> `ShotFeedbackText.prefab`.
- Configurar colores y timings según plan.

### Step 3: Smoke test en Play Mode

Casos:
1. hit en torso -> aparece `-20`.
2. miss -> aparece `MISS`.
3. disparos consecutivos rápidos -> no más de 3 textos simultáneos.
4. cerrar combate en medio de animación -> no quedan textos huérfanos.

---

## Acceptance Criteria

1. El feedback aparece solo al resolver el disparo.
2. `ShotZone.Miss` muestra `MISS` (sin número).
3. Impacto muestra `-<daño_final>` como entero.
4. El feedback se posiciona relativo al punto del `ShotMarker` (coordenada normalizada del disparo).
5. La animación MVP (subida + fade) funciona y se limpia correctamente.
6. Máximo 3 textos simultáneos.
7. EditMode tests pasan incluyendo nuevos asserts de `ShowShotFeedback`.

---

## Commit Strategy

1. `feat(combat-ui): add IAimView shot feedback contract and AimView floating text implementation`
2. `feat(combat): trigger shot feedback from CombatMenuController on shot resolve`
3. `test(combat): verify shot feedback payload for hit and miss`
