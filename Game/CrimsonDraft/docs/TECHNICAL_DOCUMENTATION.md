# Crimson Draft — Documentación Técnica

> Generado a partir de un relevamiento del código en `Assets/Scripts`, `Assets/Tests` y assets de configuración (Input, Animator) al 2026-06-28, rama `Animations`. Documenta el estado actual del proyecto y deja registradas oportunidades de mejora concretas.

## 1. Visión general

Crimson Draft es un juego con dos modos alternantes, cargados como escenas separadas (aditivas):

- **Navegación/Exploración**: movimiento en tercera persona por "Rooms", detección de enemigos con IA de patrulla, interactuables, diálogos (Yarn Spawner), inventario.
- **Combate**: sistema táctico por turnos con gauge ATB, menú de comandos (Shoot/Reload/Items/Defend) y un minijuego de apuntado (QTE).

Stack: **VContainer** (DI), **MessagePipe** (eventos pub/sub), **UniTask** (async), **Yarn Spawner** (diálogos), **Input System** (nuevo), Cinemachine (cámaras).

## 2. Estructura de `Assets/Scripts`

| Carpeta | Responsabilidad |
|---|---|
| `Infrastructure/` | Composition root global, DI, servicios cross-cutting (input, cámaras, fade, transición de escenas, registries de persistencia, eventos globales) |
| `Navigation/` | Todo el modo exploración: player, rooms, puertas, interactuables, enemigos, diálogos |
| `Combat/` | Modo combate: orquestador, ATB, máquina de estados de menú, datos de encuentro, vistas de batalla |
| `Operators/` | Datos y runtime de los personajes jugables ("Operators") y su roster |
| `Inventory/` | Items, slots, equipamiento de armas, combinaciones, persistencia |
| `Audio/` | Footsteps y tipos de superficie |
| `Editor/` | Herramientas de editor (validación, ventanas custom) |
| `UI/` | Vistas/controladores de UI globales |
| `Health/` | Carpeta reservada, actualmente sin contenido |

## 3. Infraestructura / Dependency Injection

### Composition root: `GameLifetimeScope`
`Assets/Scripts/Infrastructure/GameLifetimeScope.cs` — `DontDestroyOnLoad`, raíz de todos los LifetimeScope de VContainer.

Registra como **singleton global**:
- `IInputService` → `InputService`
- `ICameraService` → `CameraService`
- `ScreenFader` (AsImplementedInterfaces)
- `ISceneTransitionService` → `SceneTransitionService`
- `IEncounterContext` → `EncounterContext`
- Registries de persistencia: `DoorStateRegistry`, `PickupRegistry`, `InventoryStateRegistry`, `EnemyStateRegistry`
- MessagePipe: brokers para `CombatStartedEvent`, `CombatEndedEvent`, `ShootConfigurationRequestedEvent`

### `NavigationScope` (hijo de `GameLifetimeScope`)
Cargado junto con la escena de exploración. Registra `OperatorRoster`, `InventoryService`, `RoomOrchestrator`, `DialogueService`/`PickupDialogueService`, `FloorTransitionService`, y bootstraps (`EnemyBootstrap`, `OperatorRosterBootstrap`, `InventoryBootstrap`, `DoorBootstrap`, etc.). Reutiliza las `MessagePipeOptions` del padre para compartir el bus de eventos.

### `CombatScope` (hijo de `NavigationScope`)
Cargado al entrar en combate. Registra `CombatSessionController`, `ATBSystem`, `CombatActionQueue`, `CombatOrchestrator`, `CombatMenuController`, vistas (`BattlefieldView`, `CommandPanelView`, `AimViewController`, etc.) y presentadores.

## 4. Sistema de Input

`IInputService` / `InputService.cs` (`Assets/Scripts/Infrastructure/Input/`), respaldado por `Assets/Input/CrimsonDraftControls.inputactions`.

Action maps:

| Map | Acciones |
|---|---|
| `Gameplay` | Move, Interact, OpenInventory, OpenMap, Aim, **Shoot**, Pause, Sprint |
| `Combat` | UseItem (navegación/confirmación reutiliza el map `UI`) |
| `UI` | Navigate, Submit, Cancel, UIBack |
| `Dialogue` | AdvanceLine, CancelDialogue |
| `DoorTransition` | Skip |
| `PickupPrompt` | Navigate, Confirm |
| `Inventory` | Navigate, Confirm, Pickup, Cancel, NextTab, PrevTab |

El cambio de contexto se hace con `SwitchToX()`, que llama `DisableAll()` y habilita un único map. `Shoot` se agregó recientemente (clic izq. / gatillo derecho de gamepad) para disparo en exploración.

## 5. Modo Navegación / Exploración

### Player
`PlayerController.cs` (`Navigation/Player/`): movimiento 8-way (teclado) o analógico (gamepad), Walk 4 m/s / Run 7 m/s. Sincroniza animator (`Speed`, `Armed`, `Aiming`). Maneja `Aim` (sólo si hay arma activa) y `Shoot` (raycast frontal contra `enemyLayer`; si impacta un `EnemyNavAgent`, dispara combate vía `NotifyShot()`).

### Rooms
- `RoomController`: activa/desactiva el GameObject de una room.
- `RoomOrchestrator` / `IRoomOrchestrator`: resuelve la room inicial, orquesta transición entre rooms cargando una escena `DoorTransition` aditiva, mueve al jugador, pausa audio, espera skip/timeout, publica `RoomTransitionStartedEvent`/`RoomTransitionedEvent`.
- `FloorTransitionService`: igual que el anterior pero entre **escenas** completas, usando `SceneEntryContext` para pasar el punto de entrada.
- `SceneSpawnPoint`, `RoomTransitionContext`, `SceneEntryContext`: datos de apoyo para las transiciones.

### Interactables
Patrón: `IInteractable.Interact(InteractionContext)` + `IInteractionCaster` (`PlayerInteractionCaster`, raycast frontal por capa `Interactable`).

`InteractionContext` agrega: `InventoryService`, `InputService`, `DialogueService`, `DocumentController`, `ContainerController`, `PickupDialogueService`, `PuzzleViewController`.

Implementaciones: `PickupInteractable`, `ContainerInteractable`, `DoorInteractable`, `RoomDoorInteractable`, `SceneDoorInteractable`, `DocumentInteractable`, `ItemSocketInteractable`, `BarredDoorInteriorInteractable`/`BarredDoorExteriorInteractable`, `PuzzleInteractable`, `PoiInteractable`, `Lookable`.

Flag opcional `InteractCrouchFlag` (agregado recientemente): si está presente y `RequiresCrouch = true`, `PlayerInteractionCaster` setea `InteractType = 2` (agachado) en el Animator antes de invocar `Interact()`; si no está presente, usa `InteractType = 0` (parado).

### Enemigos
- `NavigationEnemyData` (ScriptableObject): velocidades, radios de detección por proximidad (con histéresis), por sonido (walk/run) y visual (FOV + raycast de obstrucción).
- `EnemyDetectionSensor.Evaluate(...)`: combina las 3 detecciones.
- `EnemyNavAgent`: máquina de estados `Patrol → Suspicious (opcional) → Alert`, persigue al jugador con `NavMeshAgent`, dispara combate por `catchRadius` (colisión) o por `NotifyShot()` (disparo del jugador en exploración) — ambos caminos reutilizan el mismo `TriggerCombat()` → `ISceneTransitionService.StartCombatAsync`.
- `EnemyBootstrap`: inyecta dependencias manualmente a cada `EnemyNavAgent`/`CombatTrigger` en `Initialize()`, respeta `EnemyStateRegistry` (enemigos derrotados quedan desactivados).
- `CombatTrigger`: variante de disparo de combate por colisión física con el jugador (zonas, no atadas a un enemigo concreto).

### Diálogos
`IDialogueService`/`DialogueService` envuelve `Yarn.Unity.DialogueRunner`: registra comandos custom, publica `DialogueActiveChangedEvent`, cambia el input map a `Dialogue` mientras corre.

## 6. Modo Combate

### Orquestación
`CombatOrchestrator`/`ICombatOrchestrator` (`Combat/CombatOrchestrator.cs`): en `Initialize()` lee la `EncounterData` actual desde `IEncounterContext` e inicializa `ATBSystem`. En `Update()`: tick del ATB, sincroniza enemigos muertos contra `BattlefieldView`, marca operadores listos, encola ataques de enemigos listos, procesa la cabeza de la `CombatActionQueue`.

### ATB
`ATBSystem` + `ATBActorState`: gauge `[0,1]` por actor (operador o enemigo); `IsReady` cuando `Gauge >= 1`. Tasa de operador = `Speed/100`; de enemigo = `1/AttackBaseSec`.

### Máquina de estados de menú
`CombatMenuController` orquesta: `OperatorSelectionState → CommandPanelState → (ShotCountSelectionState → TargetSelectionState → AimingState) | SubPanelState (Reload/Items) | Defend`.

### Acciones y datos
- `PendingAction` (struct) + `CombatActionQueue` (FIFO).
- `EncounterData`/`EnemyData`/`OperatorData` (ScriptableObjects) definen slots de la batalla.
- `AimHitMaskProfile`, `ShotPrecision*`, `ShotZone*`: datos del minijuego de apuntado.
- `Commands/` (`IOperatorCommand`, `ShootCommand`, `ReloadCommand`, `UseItemCommand`): patrón Command parcialmente implementado (ver §8).

### Transición Navegación ↔ Combate
`SceneTransitionService.StartCombatAsync`: fija `IsInCombat`, guarda el encuentro en `IEncounterContext`, cambia input a Combat, fade out, carga escena `Combat` aditiva, cambia cámara, fade in. El cierre (`CombatEndedEvent.Victory`) revierte el proceso y descarga la escena.

## 7. Operadores (personajes jugables)

- `OperatorRuntime`: estado en memoria de un operador (HP, arma activa primaria/secundaria, evento `ActiveWeaponChanged`).
- `IOperatorRoster`/`OperatorRoster`: roster lazy-inicializado desde un `IOperatorRosterSeedProvider` (`StartingLoadoutRosterSeedProvider` lee el SO `StartingLoadout`).
- `OperatorData` (SO): id, nombre, prefab de batalla, retrato, `Speed` (alimenta el ATB).
- `IWeaponSlot`/`WeaponItem`: munición actual/máxima, calibre (`Caliber`).

No es el mismo concepto que "enemigo": los `Operators` son siempre los personajes controlables del roster del jugador; los enemigos se modelan vía `EnemyData`/`EnemyNavAgent` en exploración y como slots de `EncounterData` en combate.

## 8. Inventario

`IInventoryService`/`InventoryService`: grid de slots (4 por operador), soporta stacking, mover/equipar/desequipar armas, recarga (`ReloadOperator`, consume `AmmoBoxItem`), combinación de items (`ICombineService`/`CombineRecipeLibrary`), uso de llaves (`TryUseKey` → `KeyUseOutcome`), y persistencia vía `LoadState`/`GetRawSlots` + `InventoryStateRegistry`.

Jerarquía de items: `ItemData` (base) → `WeaponData`/`WeaponItem`, `AmmoBoxData`/`AmmoBoxItem`, `ConsumableData`/`ConsumableItem`, `KeyItemData`/`KeyItem`, `SocketItemData`/`SocketItem`.

`InventoryBootstrap`: al iniciar, restaura desde `InventoryStateRegistry` si hay save, o puebla desde `StartingLoadout` y equipa armas por defecto; al destruirse, persiste el estado.

## 9. Eventos (MessagePipe)

**Globales** (`Infrastructure/Events/GameEvents.cs`): `CombatStartedEvent`, `CombatEndedEvent`, `CharacterDamagedEvent`, `CharacterDiedEvent`, `QTEStartedEvent`, `QTECompletedEvent`, `ItemUsedEvent`, `WeaponReloadedEvent`, `KrokonilDoseAppliedEvent`, `ShootConfigurationRequestedEvent`, `GuardAlertChangedEvent`, `NoteCollectedEvent`. Enums: `AmmoType`, `GuardAlertState`.

**De Navegación** (`Navigation/NavigationEvents.cs`): `RoomTransitionStartedEvent`, `RoomTransitionedEvent`, `DialogueActiveChangedEvent`.

## 10. Tests (`Assets/Tests/EditMode`)

23 archivos EditMode, cobertura sólida en lógica core:
- Combate: `ATBSystemTests`, `CombatActionQueueTests`, `CombatMenuControllerTests`, `AimViewControllerTests`.
- Navegación: `EnemyDetectionSensorTests`, `RoomOrchestratorInitTests`, `RoomControllerTests`, `RoomDoorInteractableTests`.
- Inventario: `InventoryServiceTests`, `InventoryStateRegistryTests`, `CombineServiceTests`.
- Interactuables: `DoorInteractableTests`, `DoorStateRegistryTests`, `ItemSocketInteractableTests`, `LookableTests`/`LookableSelectorTests`.
- Operadores: `OperatorRosterTests`.
- Otros: `DocumentMarkupFormatterTests`, `MorseDecoderTests`.

Sin cobertura relevante en PlayMode ni en vistas/UI (`BattlefieldView`, `CombatActionMenuView`, `InventoryView`, etc.).

## 11. Convenciones observadas

- `#nullable enable` en casi todos los archivos; `Type?` / `Type!` usados consistentemente.
- Clases predominantemente `sealed`.
- DI: `[Inject]` en métodos `Construct(...)` de MonoBehaviours; `[Preserve]` en constructores de servicios (protege de AOT stripping); registro interfaz-first en los LifetimeScope.
- Async: UniTask, `.Forget()` para fire-and-forget, `IInitializable.Initialize()` como hook post-DI en vez de `Awake`/`Start` para servicios.
- Eventos: `struct readonly` con `{ get; init; }`, sin lógica.
- Patrones: Registry (persistencia: `DoorStateRegistry`, `PickupRegistry`, `EnemyStateRegistry`, `InventoryStateRegistry`), Context (`InteractionContext`, `EncounterContext`, `RoomTransitionContext`), Bootstrap (`IInitializable` por feature).

## 12. Oportunidades de mejora

| # | Área | Problema | Sugerencia |
|---|---|---|---|
| 1 | `Combat/Commands/*` | `IOperatorCommand` existe pero `ReloadCommand`/`UseItemCommand.Execute()` están vacíos; la lógica real vive en los States/Orchestrator | Completar el patrón Command o eliminarlo y mover toda la lógica a los States, para no mantener una abstracción sin uso |
| 2 | `CombatOrchestrator` | Una sola clase mezcla tick de ATB, sync de enemigos muertos, aplicación de daño, animation locks y disparo de eventos de input (~340 líneas) | Extraer `EnemyDeadSynchronizer`, `DamageApplier`, `AnimationLockController` como colaboradores inyectados |
| 3 | `CombatOrchestrator.ResolveEcgFeedback` | Usa `FindObjectsOfType<MonoBehaviour>(true)` en runtime para hallar `IOperatorEcgFeedback` | Registrar la implementación en `CombatScope` e inyectarla directamente |
| 4 | `RoomDoorInteractable` vs `SceneDoorInteractable` | Lógica de unlock-por-llave y diálogo duplicada entre ambos | Extraer una clase base `DoorInteractableBase` con la parte común |
| 5 | Animator params (`Speed`, `Armed`, `Aiming`, `InteractType`) | Se referencian por `Animator.StringToHash("...")` sin validación; un rename en el Animator rompe silenciosamente el wiring | Editor script de validación que compare los parámetros esperados por código contra los del `.controller` asignado |
| 6 | `SceneTransitionService` / `FloorTransitionService` | `await SceneManager.LoadSceneAsync(...)` sin try/catch; un fallo de carga deja `isInCombat`/flags de transición inconsistentes | Envolver en try/finally con rollback de estado en caso de excepción |
| 7 | Persistencia | `InventoryStateRegistry` persiste items, pero `OperatorRuntime` (HP, munición cargada) no persiste si se recarga la escena de navegación | Agregar snapshot/restore de `OperatorRuntime` análogo al de inventario, si el flujo de juego lo requiere |
| 8 | Tests | 23 tests EditMode, ninguno cubre vistas/UI (`BattlefieldView`, `CombatActionMenuView`, `InventoryView`) ni flujos PlayMode end-to-end | Agregar PlayMode tests para las transiciones de UI de combate y para el flujo disparo→combate agregado recientemente |
| 9 | Inicialización | Mezcla de patrones: `IInitializable` (CombatOrchestrator), `Construct()` manual post-DI (PlayerController, EnemyNavAgent vía EnemyBootstrap), lazy init (InventoryService) | Estandarizar un único patrón de inicialización por capa para reducir la carga cognitiva de "¿cuándo está listo este servicio?" |
| 10 | `RoomTransitionContext.SkipAction` | Un ScriptableObject retiene una referencia a `InputAction`, que no es serializable de forma estable y puede quedar colgada si `InputService` cambia | Pasar la `InputAction` como parámetro de método en vez de almacenarla en el SO |
| 11 | Capas físicas | No existe una layer "Enemy" dedicada; el raycast de disparo del jugador (`PlayerController.enemyLayer`) depende de que se configure manualmente en el Inspector | Definir una layer física `Enemy` explícita en `TagManager` para evitar mal-configuraciones futuras |
| 12 | `BattlefieldView` fallback | Si un `EnemyData`/`OperatorData` no tiene `BattlefieldPrefab`, cae a una cápsula primitiva sin warning | Loggear un warning/error claro cuando se use el fallback, para detectar datos incompletos antes de build |

## 13. Resumen ejecutivo

**Fortalezas**: DI bien estructurado en capas (Game → Navigation → Combat), separación clara entre modos de juego con escenas aditivas, inventario maduro con persistencia, IA de enemigos con detección multi-capa realista, input modular por contexto, buena cobertura de tests en lógica core.

**Debilidades principales**: patrón Command sin terminar, `CombatOrchestrator` con demasiadas responsabilidades, duplicación entre interactuables de puerta, falta de tests de UI/PlayMode, y algunos puntos de configuración manual (layers, animator params) sin red de seguridad en editor.

**Riesgo general**: bajo-medio — la deuda es de mantenibilidad, no de bugs críticos conocidos.
