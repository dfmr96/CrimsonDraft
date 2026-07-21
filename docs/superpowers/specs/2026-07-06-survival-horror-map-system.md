# Survival Horror Template: sistema de mapa

Este documento resume cómo funciona el mapa en el template `Survival Horror` de `Game/SH_E/Survival Horror Template`.

## Resumen rápido

- El mapa vive en un `MapController` de escena.
- El contenido visible del mapa se guarda en un asset `MapData`.
- El editor `MappingEditorWindow` permite crear, mover, rotar y guardar rooms, doors, detalles e imágenes.
- El estado de puertas y rooms no se guarda dentro del UI del mapa, sino en el sistema global de estados (`ObjectStateManager`).
- La UI del mapa solo lee ese estado y lo renderiza con materiales distintos.

## Creación del mapa

1. `NewMapWizard` crea un asset `MapData`.
2. Opcionalmente instancia el prefab de `MapController` en la escena.
3. Asigna el `MapData` al `MapController`.
4. Abre el editor de mapa.

Referencias:

- [`NewMapWizard.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Editor/NewMapWizard.cs)
- [`MapControllerEditor.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Editor/MapControllerEditor.cs)

## Edición del mapa

`MappingEditorWindow` es la herramienta principal de edición.

Funciones principales:

- Dibuja la grilla del mapa.
- Lista los `MapController` de la escena.
- Lista rooms y doors.
- Permite agregar rooms y doors nuevos.
- Permite mover con `G` y rotar con `R`.
- Permite editar propiedades del room/door seleccionado.
- Permite guardar el mapa con un botón `Save`.

Referencias:

- [`MappingEditorWindow.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Editor/MappingEditorWindow.cs)

## Serialización del mapa

Cuando se guarda la escena, o al entrar en Play Mode en el editor, `MapSerializer` vuelve a serializar todos los `MapController` presentes.

Lo que guarda:

- `MapData.ControllerUniqueId`
- `MapData.Rooms`
- `MapData.Doors`
- Shapes de cada room
- Linked elements
- Details
- Images

Referencias:

- [`MapSerializer.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Editor/MapSerializer.cs)
- [`MapData.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Mapping/MapData.cs)

## Estado de rooms y puertas

### Rooms

- `MapRoom` implementa `ISavableObjectStateExtra`.
- Su estado puede ser `Unknown`, `NotVisited`, `Visited` o `Completed`.
- Cuando el jugador entra en una room, `MapController` la marca como `Visited`.
- Si se cumplen sus `CompletionSteps`, pasa a `Completed`.

### Puertas

- `MapDoor` también implementa `ISavableObjectStateExtra`.
- Se conecta a los eventos de la puerta real:
  - `DoorBase.OnLocked`
  - `DoorBase.OnOpened`
- Cuando la puerta se bloquea, el mapa guarda `Locked`.
- Cuando la puerta se abre, el mapa guarda `Unlocked`.

Referencias:

- [`MapController.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Mapping/MapController.cs)
- [`MapRoom.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Mapping/MapRoom.cs)
- [`MapDoor.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Mapping/MapDoor.cs)

## Cómo se persiste el estado

`SavableObjectState` captura el estado de cada objeto con `ObjectUniqueId`.

Cada objeto guarda:

- posición local
- escala local
- rotación local
- activo/inactivo
- datos extra de cada componente que implemente `ISavableObjectStateExtra`

Eso termina en `ObjectStateManager`, que mantiene un diccionario por `UniqueId`.

Flujo:

1. La puerta real cambia de estado.
2. `DoorLock` o `SceneDoor` disparan eventos.
3. `MapDoor` recibe el evento y llama `SaveState()`.
4. `SavableObjectState` serializa el estado.
5. `ObjectStateManager` lo almacena.
6. `MapRenderer` lee ese estado para pintar el mapa.

Referencias:

- [`SavableObjectState.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/SaveSystem/SavableObjectState.cs)
- [`ObjectStateManager.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/SaveSystem/ObjectStateManager.cs)
- [`DoorLock.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Doors/DoorLock.cs)
- [`SceneDoor.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Doors/SceneDoor.cs)

## Cómo lo muestra la UI

`UIMap` abre el mapa, elige el mapa actual y le pide a `MapRenderer` que genere la vista.

`MapRenderer`:

- dibuja rooms según su estado
- dibuja detalles e imágenes según estado activo
- dibuja puertas con material distinto según `Unknown`, `Locked` o `Unlocked`
- posiciona el jugador y el target de la cámara del mapa

Referencias:

- [`UIMap.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/UI/UIMap.cs)
- [`UIMapList.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/UI/UIMapList.cs)
- [`MapRenderer.cs`](../../../Game/SH_E/Survival%20Horror%20Template/Assets/HorrorEngine/Scripts/Mapping/MapRenderer.cs)

## Respuesta corta a tu duda

Sí, el template puede guardar el estado de las puertas.

Pero no hay una “edición manual” de puertas dentro de la UI del mapa. La UI solo refleja el estado persistido que viene de la lógica de la puerta real en escena.

Si quieres cambiar ese estado desde una UI propia, tendrías que llamar a la lógica de la puerta o al lock, y eso ya dispararía el guardado automáticamente.
