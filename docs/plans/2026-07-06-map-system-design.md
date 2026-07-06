# Sistema de Mapa In-Game — Diseño (Brainstorm aprobado)

> **Fecha**: 2026-07-06
> **Referencia**: [Horror Engine Map System](../../Design/Reference/horror-engine-map-system.md)
> **Fase**: 1/4 del pipeline de diseño (brainstorm → GDD → plan → ejecución)

## Resumen

Pantalla de mapa a pantalla completa (estilo Resident Evil) para CrimsonDraft, adaptando
el patrón de Horror Engine a la arquitectura existente: `RoomController`/`RoomOrchestrator`,
puertas interactables con `doorId`, registries globales en `GameLifetimeScope` y eventos
MessagePipe.

## Decisiones tomadas (con el usuario)

| Tema | Decisión |
|---|---|
| Formato | Pantalla completa estilo RE (sin minimapa) |
| Fog of war | Habitaciones visitadas siempre; ítem de mapa revela las no visitadas |
| Autoría de siluetas | Polígonos con editor custom |
| Render | RenderTexture + cámara ortográfica (fiel a Horror Engine) |
| Estados de puerta | 3 estados: Unknown / Locked / Unlocked |
| Multi-deck | Un `MapData` por deck + selector de decks conocidos |
| Posición del jugador | Se resalta la habitación actual (sin icono posicional exacto) |
| Estado de habitación | Incluye `Completed` (todos los pickups recogidos), derivado |
| Arquitectura | A: autoría en escena + bakeo automático a ScriptableObject |
| Herramienta de edición | Ventana 2D con grilla **más** calco de polígonos en SceneView |

## 1. Modelo de datos

**`MapData`** (ScriptableObject, uno por deck/escena):
- `SceneName` — escena a la que corresponde.
- `DisplayName`, `Abbreviation` — nombre visible del deck.
- `MapItemId` — ID del ítem de inventario que revela este mapa.
- `Rooms: List<MapRoomData>`: `RoomId` (copiado del `RoomController`), puntos del
  polígono (espacio local del room), transform de mapa (Offset/Rotation/Scale/ZOrder),
  `DoorIds` linkeadas, `PickupIds` contenidos.
- `Doors: List<MapDoorData>`: `DoorId` (copiado del interactable), transform de mapa,
  tamaño del rectángulo.
- Parámetros de grilla: `Size`, `CellSize`, `GlobalScale`.

**`MapDataSet`** (ScriptableObject único): array ordenado de todos los `MapData` del
juego — alimenta el selector de decks.

Principio (de Horror Engine): el asset guarda **geometría estática + IDs**; el estado
dinámico vive en registries. Se reutilizan los `roomId`/`doorId`/`pickupId` existentes —
no se crea un sistema de IDs nuevo.

## 2. Autoría y bakeo (editor)

- **`MapRoomShape`** (componente hijo del `RoomController`): polígono en espacio local,
  editado en **SceneView** con Handles sobre el piso real (calco). Botón "Trace from
  bounds" para generar el rectángulo inicial. Guarda además su transform de mapa,
  inicializado desde la posición de mundo × `GlobalScale`.
- **`MapDoorMarker`** (componente en la puerta interactable o hijo): posición/tamaño de
  la puerta en el mapa. El `doorId` se toma automáticamente del interactable.
- **`MapEditorWindow`** (ventana 2D con grilla): dibuja rooms y puertas del deck en
  espacio de mapa; seleccionar (sync con selección de Unity), mover, rotar, escalar —
  edita el transform de mapa, desacoplado del transform 3D (como `MapElement` de HE).
- **`MapBaker`** (estático, `[InitializeOnLoad]`): al guardar la escena o entrar a Play
  Mode, recorre `RoomController`s → shapes, markers y `PickupInteractable`s hijos →
  escribe al `MapData` de la escena (referenciado desde un `MapSceneConfig` en la raíz).
  Valida: shapes con <3 puntos, rooms sin `roomId`, puertas sin marker, IDs vacíos →
  warnings clickeables.
- `RoomGraphWindow` existente no se modifica.

## 3. Estado en runtime

Registries en `GameLifetimeScope` (sobreviven transiciones de escena):

- **`DoorStateRegistry` (extendido)**: bool → enum `DoorMapState { Unknown, Locked,
  Unlocked }`. Se mantiene `IsUnlocked()` por compatibilidad. Nuevos: `MarkLocked(id)`
  (solo pisa `Unknown`, nunca degrada `Unlocked`) y `MarkUnlocked(id)`. Los interactables
  agregan `MarkLocked` en el camino "bloqueada sin llave" y `MarkUnlocked` al cruzar
  una puerta abierta.
- **`RoomStateRegistry` (nuevo)**: `roomId → { Unknown, Visited }` monotónico.
- **`MapStateTracker` (nuevo, `IInitializable` en `NavigationScope`)**: se suscribe a
  `RoomTransitionedEvent` y marca `Visited`; marca también la habitación inicial.
- **`KnownMapsRegistry` (nuevo)**: set de mapas en posesión del jugador; el pickup del
  ítem de mapa lo registra.
- **Estados derivados al renderizar** (no almacenados):
  - `NotVisited` = room `Unknown` + mapa conocido.
  - `Completed` = `Visited` + todos los `PickupIds` bakeados ∈ `PickupRegistry.CollectedIds`.
- Todos los registries exponen `GetState()/LoadState()` para el save system futuro.

## 4. Presentación (UI)

- **`MapScreenController`/`MapScreenView`** siguiendo el patrón Controller/View del
  proyecto, registrados vía VContainer. Abre desde input/menú y pausa la navegación
  (mismo mecanismo que el inventario).
- **`MapRenderer`**: genera mallas en una layer oculta (rooms triangulados, paredes de
  contorno, puertas como quads), cámara ortográfica → `RenderTexture` → `RawImage`.
  Materiales por estado; la habitación actual pulsa (solo si el mapa mostrado es el del
  deck actual, según la room activa del `RoomOrchestrator`).
- **Fog of war**: room `Unknown` sin mapa → no se dibuja; con mapa → estilo `NotVisited`.
  Puertas: solo si alguna room dibujada las linkea.
- **Selector de decks**: desde `MapDataSet`, solo decks conocidos (alguna room `Visited`
  o mapa en posesión). Paneo con clamping al área del mapa.

## 5. Errores y testing

- Bakeo y runtime defensivos con warnings contextuales; registry vacío = todo `Unknown`.
- **Tests EditMode** (patrón de fakes del proyecto): transiciones monotónicas de
  `DoorStateRegistry` extendido y compat de `IsUnlocked`; `RoomStateRegistry`;
  `MapStateTracker` con fakes; derivación de estados en un **`MapStateResolver`** puro
  (sin Unity) que concentra la lógica `NotVisited`/`Completed`/visibilidad.

## Fuera de alcance (esta iteración)

- Minimapa en HUD.
- Icono posicional exacto del jugador (transformación mundo→mapa por room).
- Integración con save system a disco (los registries ya quedan listos).
- Marcadores de objetivos/POIs en el mapa.
