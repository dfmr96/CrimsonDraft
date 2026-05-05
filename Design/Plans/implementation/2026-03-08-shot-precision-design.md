# Shot Precision — Diseño

**Date:** 2026-03-08
**Scope:** combat-ui — ShotZoneDefinition, AimViewController, CombatMenuController

---

## Problema

El sistema de detección de impactos actual mapea color del sprite → `ShotZone` anatómica
(Head, Torso, Arms, Legs) y calcula daño usando un multiplicador fijo por zona.
No existe distinción de precisión dentro de una misma zona: un tiro que roza el borde
de la cabeza hace el mismo daño que uno al centro.

---

## Objetivo

Introducir un sistema de **precisión de disparo** data-driven que permita al diseñador
definir, por cada color en el mask sprite, tanto la zona anatómica como un modificador
de precisión con su multiplicador numérico configurable desde el Inspector.

La fórmula de daño pasa a ser:

```
damage = BaseDamage × ZoneMult(zone) × precisionEntry.multiplier
```

---

## Tipos Nuevos

### `ShotPrecision` (enum)

Categoría semántica usada para feedback en HUD y como label en el Inspector.
No determina el multiplicador directamente — ese dato vive en `ShotPrecisionEntry`.

```
Normal    = 0   // impacto estándar
Graze     = 1   // tiro rozando el borde de la silueta
WeakPoint = 2   // punto débil (cabeza centro, corazón, etc.)
```

El enum puede extenderse en el futuro (Armored, Critical, etc.) sin cambios en la
lógica de daño.

### `ShotPrecisionEntry` (struct serializable)

Struct anidado dentro de `ShotZoneDefinition`. Agrupa el label semántico y el
multiplicador configurable:

```csharp
[Serializable]
public struct ShotPrecisionEntry
{
    public ShotPrecision precision;   // dropdown en Inspector (label/categoría)
    public float         multiplier;  // multiplicador real, editable por el diseñador
}
```

### `ShotZoneDefinition` (actualización)

Se agrega `precisionEntry` al struct existente:

```csharp
[Serializable]
public struct ShotZoneDefinition
{
    public Color              color;          // color en el mask sprite
    public ShotZone           zone;           // zona anatómica
    public ShotPrecisionEntry precisionEntry; // precisión + multiplicador
}
```

El campo `precisionEntry.multiplier` tiene valor por defecto `1.0` para compatibilidad
con definiciones existentes que no usen precisión.

---

## Sprite: modelo de degradado por zona

El artista pinta el mask sprite con múltiples tonalidades por zona. Cada tonalidad
se registra como una entrada en `zoneDefinitions` del `AimHitMaskProfile`.

Ejemplo con Head y Torso (mínimo para esta iteración):

| Color | zone | precision | multiplier | Descripción |
|-------|------|-----------|------------|-------------|
| `#660000` | Head | Graze | 0.5 | Borde de cabeza |
| `#FF0000` | Head | Normal | 1.0 | Cabeza estándar |
| `#FF8888` | Head | WeakPoint | 2.0 | Centro de cabeza |
| `#006600` | Torso | Graze | 0.5 | Borde de torso |
| `#00FF00` | Torso | Normal | 1.0 | Torso estándar |
| `#88FF88` | Torso | WeakPoint | 2.0 | Corazón |

El número de tonalidades por zona es ilimitado — agregar más entradas en
`zoneDefinitions` sin tocar código.

---

## Cambios en `ResolvedShot`

Agrega `ShotPrecision Precision` para que el HUD pueda mostrar el label correcto:

```csharp
public readonly struct ResolvedShot
{
    public int           Index;
    public Vector2       NormalizedPos;
    public ShotZone      Zone;
    public ShotPrecision Precision;   // NUEVO
    public int           Damage;
}
```

---

## Cambios en `AimViewController`

### `ResolveZone` → devuelve `ShotZoneDefinition?`

Actualmente devuelve `ShotZone`. Pasa a devolver la definición completa para que
`BuildResolvedShots` tenga acceso al `precisionEntry.multiplier`.

```csharp
internal static ShotZoneDefinition? ResolveZone(
    Color pixel,
    ShotZoneDefinition[] definitions,
    float tolerance)
```

Devuelve `null` si ningún color coincide dentro de la tolerancia (= Miss).

### `SampleSilhouette` → devuelve `ShotZoneDefinition?`

Pasa el resultado de `ResolveZone` directamente.

### `BuildResolvedShots`

Llama `SampleSilhouette` → obtiene `ShotZoneDefinition?`.
- Si `null`: zona = Miss, precision = Normal, damage = 0.
- Si no nulo: extrae `zone`, `precisionEntry.precision` y calcula damage.

```csharp
int damage = CombatMenuController.ComputeShotDamage(
    def.zone,
    def.precisionEntry.multiplier);
```

---

## Cambios en `CombatMenuController`

### `ComputeShotDamage` actualizado

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

La firma cambia de `ComputeShotDamage(ShotZone)` a
`ComputeShotDamage(ShotZone, float)`.

---

## Compatibilidad con definiciones existentes

Los `AimHitMaskProfile` assets existentes que usen `ShotZoneDefinition` sin
`precisionEntry` serializado obtendrán `precision = Normal (0)` y `multiplier = 0`
por default de C#. Para garantizar `multiplier = 1.0` como fallback, se debe
serializar el valor explícitamente en los assets al abrirlos en el Editor, o
bien aplicar un `Mathf.Max(1f, multiplier)` en `ComputeShotDamage`.

Decisión: usar `Mathf.Max(1f, multiplier)` como safety net para assets no migrados.

---

## Sin cambios

- `AimHitMaskProfile` — solo agrega entradas a su array existente
- `ShotZone` enum — no se agregan valores
- Flujo de estados del combat (AimingState, TargetSelectionState, etc.)
- `IBattlefieldView`, `IAimView`, `IOperatorRoster`
