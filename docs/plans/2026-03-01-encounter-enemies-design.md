# Diseño: Encuentros de combate con slots de enemigos

**Fecha:** 2026-03-01
**Estado:** Aprobado

## Objetivo

Al entrar en un encuentro de combate, pasar la lista de enemigos al CombatScope para renderizarlos
en sus slots del campo de batalla. Habilitar la selección de enemigo al elegir Shoot antes del QTE.

## Layout del campo de batalla

```
ENEMIGOS (6 slots)          JUGADORES (4 slots)

   [0] [1]                      [0]
  [2] [3]                      [1] [2]
   [4] [5]                      [3]
```

Posiciones fijas en world space, definidas en el prefab del campo de batalla.
Slots vacíos (null en EncounterData) no instancian sprite.

## Modelo de datos (ScriptableObjects)

```
EnemyData
├── string enemyId
└── Sprite sprite

OperatorData
├── string operatorId
└── Sprite sprite

EncounterData
├── string encounterId
├── EnemyData?[6] slots      // null = slot vacío
└── OperatorData[4] operators

EncounterDatabase
├── EncounterData[] encounters
└── EncounterData? GetById(string id)
```

## Flujo de datos

```
CombatTrigger.OnTriggerEnter2D(encounterId)
  → SceneTransitionService.StartCombatAsync(encounterId)
      → EncounterDatabase.GetById(encounterId)
      → EncounterContext.Set(encounterData)          ← nuevo paso
      → SceneManager.LoadSceneAsync("Combat", Additive)
          → CombatScope inicializa
              → BattlefieldPresenter.Initialize()
                  → lee IEncounterContext.CurrentEncounter
                  → llama BattlefieldView.Populate(encounter)
                      → instancia sprites de enemigos en slots ocupados
                      → instancia sprites de operadores en slots de jugador
```

## Servicios nuevos

### IEncounterContext / EncounterContext (singleton, GameLifetimeScope)

```csharp
public interface IEncounterContext
{
    EncounterData? CurrentEncounter { get; }
}
```

`EncounterContext` expone internamente `void Set(EncounterData data)` para que
`SceneTransitionService` lo popule antes de cargar la escena.

### BattlefieldPresenter (scoped, CombatScope, IInitializable)

Inyecta `IEncounterContext` + `BattlefieldView`. En `Initialize()` llama
`view.Populate(CurrentEncounter)`.

### BattlefieldView (MonoBehaviour, CombatScope)

```
Transform[] enemySlotTransforms     // 6 posiciones
Transform[] playerSlotTransforms    // 4 posiciones

void Populate(EncounterData encounter)
void SetOperatorIndicator(int slotIndex)   // mueve indicador + estado Focused
void DimOperatorIndicator()                // atenúa sin mover (estado CommandPanel)
void ResetOperatorIndicator()              // limpia indicador al volver a OperatorSelection
void SetEnemyTargetIndicator(int slotIndex)
void HideEnemyTargetIndicator()
```

## Indicador de operador activo

Al navegar entre operadores en el menú UI, un indicador sobre el sprite del operador
en el campo de batalla sigue la selección.

Estados visuales del indicador:
- **Focused**: brillante, sigue la navegación (estado OperatorSelection)
- **Dimmed**: atenuado, fijo en el slot confirmado (estados CommandPanel / SubPanel / Aiming)

Comunicación: `CombatMenuController` publica `OperatorFocusedEvent` al navegar,
llama `DimOperatorIndicator()` al confirmar operador y `ResetOperatorIndicator()` al cancelar.

## Estado TargetSelection (nuevo en CombatMenuController)

```
OperatorSelection → CommandPanel → TargetSelection → Aiming
                                 ↑ Cancel           ↑ Confirm
```

- Activa al elegir Shoot en CommandPanel
- Solo slots ocupados (no-null) son navegables
- `CombatNavigate` salta entre slots ocupados, omite vacíos
- Indicador de apuntado se mueve entre sprites de enemigo en BattlefieldView
- **Confirm**: guarda slotIndex → entra en Aiming
- **Cancel**: vuelve a CommandPanel, oculta indicador de enemigo

## Eventos MessagePipe (cambios)

| Evento | Campos nuevos |
|---|---|
| `OperatorFocusedEvent` | `int SlotIndex` (nuevo) |
| `QTEStartedEvent` | + `int EnemySlotIndex` (extensión) |

## Componentes modificados

| Script | Cambio |
|---|---|
| `GameLifetimeScope` | Registra `EncounterDatabase` (RegisterInstance) + `EncounterContext` (singleton), nuevo broker `OperatorFocusedEvent` |
| `SceneTransitionService` | Inyecta `IEncounterDatabase` + `IEncounterContext`; llama `Set()` antes de cargar la escena |
| `CombatMenuController` | Añade estado `TargetSelection`; publica `OperatorFocusedEvent` al navegar; llama `Dim/Reset` en transiciones |
| `GameEvents.cs` | Añade `OperatorFocusedEvent`, extiende `QTEStartedEvent` |
| `CombatScope` | Registra `BattlefieldPresenter` + `BattlefieldView` |
