# Feature: Persistent OperatorRoster + Single Initialization Ownership

## Context

El roster de operadores debe persistir entre encuentros. No existe sistema de guardado/carga todavía, así que se necesita un único punto de inicialización en runtime de sesión, fuera de combate.

**Regla de negocio acordada:**

1. `OperatorRoster` se inicializa una sola vez por sesión de juego.
2. Entre encuentros no se reinicia ni se rehidrata.
3. Combate nunca inicializa el roster.
4. Cuando exista save/load, esa misma inicialización única vendrá de datos cargados.

---

## Decisión de arquitectura

1. Extraer tipos de operadores a un assembly compartido `CrimsonDraft.Operators`.
2. Registrar `OperatorRoster` en `NavigationScope` (lifetime scoped de navegación).
3. Crear un bootstrap en navegación para forzar inicialización temprana una sola vez.
4. Remover cualquier inicialización desde `EnemyAttackController` y `CombatSessionController`.

---

## Nuevos archivos

### `Scripts/Operators/CrimsonDraft.Operators.asmdef`
```json
{
  "name": "CrimsonDraft.Operators",
  "rootNamespace": "CrimsonDraft.Operators",
  "references": ["VContainer", "VContainer.Unity", "UniTask"],
  "autoReferenced": false
}
```

### `Scripts/Operators/AssemblyInfo.cs`
```csharp
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("CrimsonDraft.Tests.EditMode")]
```

### `Scripts/Operators/IOperatorRosterSeedProvider.cs`
Contrato para el origen de datos inicial del roster (por ahora party default de runtime, luego save/load).

### `Scripts/Navigation/OperatorRosterBootstrap.cs`
`IStartable` o `IInitializable` en Navigation que invoca `roster.EnsureInitialized()` exactamente una vez al levantar navegación.

---

## Archivos a mover (de `Scripts/Combat/` -> `Scripts/Operators/`)

Cambiar namespace `CrimsonDraft.Combat` -> `CrimsonDraft.Operators` en cada archivo:

| Origen | Destino |
|--------|---------|
| `Scripts/Combat/Data/OperatorData.cs` | `Scripts/Operators/OperatorData.cs` |
| `Scripts/Combat/OperatorRuntime.cs` | `Scripts/Operators/OperatorRuntime.cs` |
| `Scripts/Combat/OperatorDamageResult.cs` | `Scripts/Operators/OperatorDamageResult.cs` |
| `Scripts/Combat/IOperatorRoster.cs` | `Scripts/Operators/IOperatorRoster.cs` |
| `Scripts/Combat/OperatorRoster.cs` | `Scripts/Operators/OperatorRoster.cs` |

`OperatorRoster` debe quedar auto-contenido y dueño de su inicialización:

```csharp
public interface IOperatorRoster
{
    bool IsInitialized { get; }
    int Count { get; }
    OperatorRuntime this[int slotIndex] { get; }
    IReadOnlyList<int> GetAliveSlots();
    void EnsureInitialized();
}
```

```csharp
public sealed class OperatorRoster : IOperatorRoster
{
    public bool IsInitialized { get; private set; }

    public void EnsureInitialized()
    {
        if (this.IsInitialized) return;

        // seedProvider provee operadores iniciales y defaults de HP/ammo
        // construir slots aquí
        this.IsInitialized = true;
    }
}
```

---

## Archivos a modificar

### asmdef
- `CrimsonDraft.Combat.asmdef` -> añadir `"CrimsonDraft.Operators"`
- `CrimsonDraft.Navigation.asmdef` -> añadir `"CrimsonDraft.Operators"`
- `CrimsonDraft.Tests.EditMode.asmdef` -> añadir `"CrimsonDraft.Operators"`

### `Scripts/Navigation/NavigationScope.cs`
```csharp
using CrimsonDraft.Operators;
// ...
builder.Register<OperatorRoster>(Lifetime.Scoped).AsSelf().As<IOperatorRoster>();
builder.Register<DefaultOperatorRosterSeedProvider>(Lifetime.Scoped).As<IOperatorRosterSeedProvider>();
builder.Register<OperatorRosterBootstrap>(Lifetime.Scoped).AsImplementedInterfaces();
```

### `Scripts/Combat/CombatScope.cs`
- Eliminar registro de `OperatorRoster`.

### `Scripts/Combat/CombatSessionController.cs`
- No inicializa roster.
- Mantener solo responsabilidades de sesión de combate (`EndCombat` y publicación de evento).

### `Scripts/Combat/EnemyAttackController.cs`
- Eliminar `this.roster.Initialize(...)` en `Start()`.
- Eliminar `[SerializeField] private int defaultOperatorHp`.
- Añadir `using CrimsonDraft.Operators;`.

### Archivos Combat con tipos movidos
Añadir `using CrimsonDraft.Operators;` donde se usen tipos migrados:
- `States/OperatorSelectionState.cs`, `CommandPanelState.cs`, `ShotCountSelectionState.cs`, `AimingState.cs`
- `Commands/ShootCommand.cs`, `ReloadCommand.cs`
- `Data/EncounterData.cs` (usa `OperatorData?[]`)
- `UI/CombatMenuController.cs`

### Tests
Actualizar rutas reales:
- `Assets/Tests/EditMode/OperatorRosterTests.cs`
- `Assets/Tests/EditMode/CombatMenuControllerTests.cs`

Cambios:
- `using CrimsonDraft.Combat` -> `using CrimsonDraft.Operators` para tipos movidos.
- Ajustar referencias totalmente calificadas (`CrimsonDraft.Combat.OperatorData`, etc.).
- `FakeOperatorRoster` implementa `IsInitialized` + `EnsureInitialized()` en vez de `Initialize(...)`.

---

## Criterios de aceptación

1. `OperatorRoster` se construye en Navigation y `IsInitialized` pasa a `true` exactamente una vez.
2. `EnemyAttackController` y `CombatSessionController` no inicializan roster.
3. Daño/ammo aplicados en un encuentro se preservan al salir y entrar a otro encuentro.
4. No hay reset implícito del roster al cambiar de encounter.
5. Unity compila sin errores de asmdef/namespace.
6. EditMode tests pasan con el contrato nuevo (`EnsureInitialized`).

---

## Verificación manual

1. Entrar a combate A, recibir daño/gastar ammo.
2. Salir a navegación.
3. Entrar a combate B.
4. Confirmar que HP/ammo continúan desde estado previo (sin reinicio).
5. Confirmar en logs/debug que `EnsureInitialized()` solo ejecutó la rama de inicialización una vez en toda la sesión.
