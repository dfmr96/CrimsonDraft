# ATB Speed System

## Campo `Speed` en `OperatorData`

Definido en `Assets/Scripts/Operators/OperatorData.cs`:

```csharp
[SerializeField, Range(1, 99)] private int speed = 50;
```

- Rango: **1 – 99**
- Default: **50**
- Configurado por Inspector en cada ScriptableObject de Operator.

---

## Conversión a tasa de gauge

Al iniciar combate, `CombatOrchestrator.BuildATBConfigs()` transforma `Speed` en `GaugePerSecond`:

```csharp
// CombatOrchestrator.cs
[SerializeField] private float atbGaugeDivisor = 100f;

int speed = roster[i].Data?.Speed ?? 50;
configs.Add(new ATBActorConfig(i, ATBActorKind.Operator, speed / divisor));
```

**Fórmula:**

```
GaugePerSecond = Speed / atbGaugeDivisor
               = Speed / 100
```

---

## Tick del gauge

Cada frame, `ATBActorState.Tick()` acumula el gauge:

```csharp
// ATBActorState.cs
this.Gauge = Math.Min(1.0, this.Gauge + deltaTime * this.gaugePerSecond);
```

El gauge va de `0` a `1`. Cuando `Gauge >= 1` → `IsReady == true` → el actor puede actuar.

**Tiempo hasta actuar:**

```
Segundos = 1 / GaugePerSecond = 100 / Speed
```

---

## Tabla de referencia

| Speed | GaugePerSecond | Segundos hasta actuar |
|------:|---------------:|----------------------:|
| 25    | 0.25           | 4.00 s                |
| 50    | 0.50           | 2.00 s                |
| 75    | 0.75           | 1.33 s                |
| 99    | 0.99           | 1.01 s                |

---

## Enemigos (comparación)

Los enemigos **no usan `Speed`**. Su tasa se define con `EnemyData.AttackBaseSec`:

```csharp
float gps = data.AttackBaseSec > 0f ? 1f / data.AttackBaseSec : 1f;
```

Un enemigo con `AttackBaseSec = 2f` tiene `GaugePerSecond = 0.5`, equivalente a un Operator con Speed 50.

---

## Archivos relevantes

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Operators/OperatorData.cs` | Define el campo `Speed` |
| `Assets/Scripts/Combat/CombatOrchestrator.cs` | Convierte `Speed` → `GaugePerSecond` |
| `Assets/Scripts/Combat/ATBActorState.cs` | Acumula el gauge cada frame |
| `Assets/Scripts/Combat/ATBSystem.cs` | Orquesta el tick de todos los actores |
