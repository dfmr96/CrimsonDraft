# Operator ECG HP+BPM MVP — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implementar un widget ECG dinámico en Canvas para 1 operador, usando solo `HP + BPM`, con estética pixelada y API simple de integración.

**Architecture:**
- Crear un componente de render UI (`MaskableGraphic`) que dibuje la onda ECG en runtime a partir de `hpRatio`, `bpm` e `isActive`.
- Separar funciones puras de cálculo (muestra de onda, color, amplitud, clamp BPM) en un helper testable para TDD en EditMode.
- Agregar un `Widget` de alto nivel para conectar onda + texto BPM y un `DebugDriver` temporal para validar sin depender aún del sistema final de salud.

**Spec:** Implements [[Sistema ECG de Operadores]] (`Sistema ECG de Operadores.md`)

**Tech Stack:** Unity 6, C# 9, uGUI (`MaskableGraphic`), TextMeshProUGUI, NUnit EditMode

---

## Task 1 — Preparar base de tests para UI assembly

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CrimsonDraft.Tests.EditMode.asmdef`
- Create: `Game/CrimsonDraft/Assets/Scripts/UI/AssemblyInfo.cs`

### Step 1: Write the failing test (assembly reference)

Crear `Game/CrimsonDraft/Assets/Tests/EditMode/OperatorEcgMathTests.cs` con un test mínimo que referencie `CrimsonDraft.UI.HUD.OperatorEcgMath` (aún no existe) para forzar wiring de assemblies.

```csharp
[Test]
public void Placeholder_compilesAgainstUiAssembly()
{
    Assert.Pass();
}
```

### Step 2: Run test to verify it fails

Run: Unity Test Runner (EditMode), filtro `OperatorEcgMathTests`.
Expected: FAIL de compilación por referencia faltante a `CrimsonDraft.UI` y/o tipo inexistente.

### Step 3: Write minimal implementation for assembly wiring

1. En `CrimsonDraft.Tests.EditMode.asmdef`, agregar referencia a `CrimsonDraft.UI`.
2. Crear `AssemblyInfo.cs` para exponer internos al assembly de tests:

```csharp
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("CrimsonDraft.Tests.EditMode")]
```

### Step 4: Run test to verify compile path

Run: Unity Test Runner (EditMode), filtro `OperatorEcgMathTests`.
Expected: ya no falla por referencia de assembly (puede seguir fallando por métodos aún no implementados).

### Step 5: Commit

```bash
git add Game/CrimsonDraft/Assets/Tests/EditMode/CrimsonDraft.Tests.EditMode.asmdef Game/CrimsonDraft/Assets/Scripts/UI/AssemblyInfo.cs Game/CrimsonDraft/Assets/Tests/EditMode/OperatorEcgMathTests.cs
git commit -m "test(ui): wire editmode tests to crimsondraft ui assembly"
```

---

## Task 2 — Implementar funciones puras de ECG con TDD

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/OperatorEcgMathTests.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgMath.cs`

### Step 1: Write the failing tests

Agregar tests para:
1. `ClampBpm(40)` => `60`, `ClampBpm(200)` => `160`.
2. `ComputeAmplitude(height:100, hpRatio:1)` => `42`.
3. `ComputeAmplitude(height:100, hpRatio:0)` => `12`.
4. `ComputeEcgColor(0.8f)` devuelve dominante verde.
5. `ComputeEcgColor(0.2f)` devuelve dominante roja.
6. `EcgSample(0.16f)` produce pico positivo notable.
7. `EcgSample(0.05f)` produce línea base `0`.

### Step 2: Run tests to verify they fail

Run: Unity Test Runner (EditMode), filtro `OperatorEcgMathTests`.
Expected: FAIL por métodos no implementados.

### Step 3: Write minimal implementation

Crear `OperatorEcgMath` (`internal static`) con métodos:
- `ClampBpm(int bpm)`
- `ClampHpRatio(float hpRatio)`
- `ComputeAmplitude(float contentHeight, float hpRatio)`
- `ComputeEcgColor(float hpRatio)`
- `EcgSample(float phase01)`

Usar exactamente las reglas del GDD/prototipo:
- BPM clamp `60..160`
- `amp = h * (0.12f + 0.30f * hpRatio)`
- umbrales de color `>0.6`, `0.3..0.6`, `<=0.3`
- forma de onda por ventanas de fase.

### Step 4: Run tests to verify they pass

Run: Unity Test Runner (EditMode), filtro `OperatorEcgMathTests`.
Expected: PASS completo.

### Step 5: Commit

```bash
git add Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgMath.cs Game/CrimsonDraft/Assets/Tests/EditMode/OperatorEcgMathTests.cs
git commit -m "feat(ui-hud): add ecg math model for hp+bpm rendering"
```

---

## Task 3 — Crear `OperatorEcgWaveGraphic` (render dinámico)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWaveGraphic.cs`
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/OperatorEcgWaveGraphicTests.cs`

### Step 1: Write the failing tests

Agregar tests para:
1. `SetVitals` clampa `hpRatio` y `bpm`.
2. `SetVitals(..., isActive:false)` deja `IsActive` en false.
3. `SetPixelStyle(2,2)` aplica `PixelStep=2`, `LineThickness=2`.
4. `BuildNormalizedSamples(width:10)` devuelve cantidad esperada de muestras según `pixelStep`.

Nota: si `OnPopulateMesh` no es testeable directo, validar helpers internos expuestos como `internal`.

### Step 2: Run tests to verify they fail

Run: Unity Test Runner (EditMode), filtro `OperatorEcgWaveGraphicTests`.
Expected: FAIL por clase/métodos inexistentes.

### Step 3: Write minimal implementation

Implementar `OperatorEcgWaveGraphic : MaskableGraphic` con:
- Estado serializado: `pixelStep`, `lineThickness`, `visibleBeats=2.5f`, colores fallback.
- API:
  - `SetVitals(float hpRatio, int bpm, bool isActive)`
  - `SetPixelStyle(int stepPx, int thicknessPx)`
- Render:
  - calcula `beatMs = 60000f / bpm`
  - genera muestras a lo ancho del rect
  - cuantiza X/Y por `pixelStep`
  - dibuja tira de línea con grosor entero (sin AA)
  - si `!isActive`, no dibuja trazado.

### Step 4: Run tests to verify they pass

Run: Unity Test Runner (EditMode), filtro `OperatorEcgWaveGraphicTests`.
Expected: PASS completo.

### Step 5: Commit

```bash
git add Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWaveGraphic.cs Game/CrimsonDraft/Assets/Tests/EditMode/OperatorEcgWaveGraphicTests.cs
git commit -m "feat(ui-hud): add dynamic operator ecg wave graphic"
```

---

## Task 4 — Crear `OperatorEcgWidget` + driver de debug

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWidget.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgDebugDriver.cs`
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/OperatorEcgWidgetTests.cs`

### Step 1: Write the failing tests

Agregar tests para `OperatorEcgWidget`:
1. `SetVitals(0.75,72,true)` actualiza texto BPM a `72`.
2. Reenvía datos al `OperatorEcgWaveGraphic` (`HpRatio`, `Bpm`, `IsActive`).
3. `SetPixelStyle` reenvía estilo pixelado al gráfico.

### Step 2: Run tests to verify they fail

Run: Unity Test Runner (EditMode), filtro `OperatorEcgWidgetTests`.
Expected: FAIL por clase/métodos inexistentes.

### Step 3: Write minimal implementation

- `OperatorEcgWidget`:
  - Referencias serializadas: `OperatorEcgWaveGraphic waveGraphic`, `TextMeshProUGUI bpmValueText`, `TextMeshProUGUI bpmLabelText`.
  - Métodos públicos:
    - `SetVitals(float hpRatio, int bpm, bool isActive)`
    - `SetPixelStyle(int stepPx, int thicknessPx)`
  - Convención visual: valor BPM en número grande + label `BPM`.
- `OperatorEcgDebugDriver`:
  - Campos serializados para `hpRatio`, `bpm`, `isActive`, `applyEachFrame`.
  - Opción para testear en PlayMode sin backend de salud.

### Step 4: Run tests to verify they pass

Run: Unity Test Runner (EditMode), filtro `OperatorEcg*Tests`.
Expected: PASS completo para math + wave + widget.

### Step 5: Commit

```bash
git add Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWidget.cs Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgDebugDriver.cs Game/CrimsonDraft/Assets/Tests/EditMode/OperatorEcgWidgetTests.cs
git commit -m "feat(ui-hud): add operator ecg widget and debug driver"
```

---

## Task 5 — Wiring de escena y smoke test manual

**Files (assets):**
- Modify: `Game/CrimsonDraft/Assets/Scenes/Combat.unity` (o escena de prueba UI)
- Create: `Game/CrimsonDraft/Assets/Prefabs/UI/OperatorEcgWidget.prefab`

### Step 1: Crear prefab de widget

Estructura mínima:
1. `OperatorEcgWidget` (root RectTransform)
2. Child `Wave` con `OperatorEcgWaveGraphic`
3. Child `BpmValue` (`TextMeshProUGUI`)
4. Child `BpmLabel` (`TextMeshProUGUI` = `BPM`)

### Step 2: Conectar `OperatorEcgDebugDriver`

En escena de prueba:
- Instanciar prefab dentro del Canvas.
- Asignar referencias del widget.
- Ajustes iniciales:
  - `pixelStep = 2`
  - `lineThickness = 2`
  - `bpm = 72`
  - `hpRatio = 1.0`

### Step 3: Smoke test PlayMode

Validar manualmente:
1. `HP=1.0, BPM=72` => verde, amplitud alta.
2. `HP=0.45, BPM=110` => amarillo, amplitud media.
3. `HP=0.15, BPM=150` => rojo, amplitud baja, ritmo rápido.
4. `isActive=false` => panel apagado sin trazo.
5. Pixelado visible y consistente en distintas resoluciones del Canvas.

### Step 4: Commit

```bash
git add Game/CrimsonDraft/Assets/Prefabs/UI/OperatorEcgWidget.prefab Game/CrimsonDraft/Assets/Scenes/Combat.unity
git commit -m "feat(ui-hud): wire operator ecg widget into canvas for mvp validation"
```

---

## Acceptance Criteria

1. Existe un widget ECG funcional para 1 operador dentro del Canvas.
2. El trazo se genera dinámicamente y se mueve según BPM (`60..160`).
3. Color y amplitud responden a HP con los umbrales definidos.
4. Estado inactivo apaga la línea ECG.
5. Estética pixelada visible (`pixelStep=2`, grosor entero sin AA).
6. EditMode tests nuevos pasan (`OperatorEcgMathTests`, `OperatorEcgWaveGraphicTests`, `OperatorEcgWidgetTests`).

---

## Commit Strategy

1. `test(ui): wire editmode tests to crimsondraft ui assembly`
2. `feat(ui-hud): add ecg math model for hp+bpm rendering`
3. `feat(ui-hud): add dynamic operator ecg wave graphic`
4. `feat(ui-hud): add operator ecg widget and debug driver`
5. `feat(ui-hud): wire operator ecg widget into canvas for mvp validation`
