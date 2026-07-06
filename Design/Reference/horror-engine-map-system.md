# Horror Engine — Sistema de Mapa (Referencia)

> **Fuente**: template *Survival Horror* (Horror Engine) ubicado en
> `Game/SH_E/Survival Horror Template/Assets/HorrorEngine/`.
> Este documento describe cómo el template crea, edita, persiste y renderiza el mapa
> in-game, con foco en cómo guarda el estado de las puertas y lo refleja en la UI.
> Sirve como referencia para implementar un sistema equivalente en CrimsonDraft.

---

## Visión general

El sistema tiene cuatro capas, cada una con una responsabilidad única:

| Capa | Componentes | Responsabilidad |
|------|-------------|-----------------|
| **Autoría (editor)** | `NewMapWizard`, `MappingEditorWindow`, `MapController` + hijos en escena | El diseñador dibuja el mapa como GameObjects en la escena |
| **Bakeo (editor)** | `MapSerializer` | Al guardar la escena, vuelca la autoría al asset `MapData` (ScriptableObject) |
| **Persistencia (runtime)** | `ObjectStateManager`, `SavableObjectState`, `ObjectUniqueId` | Diccionario central de estados por ID único; es lo que serializa el save game |
| **Presentación (runtime)** | `UIMap`, `MapRenderer` | Genera mallas 3D del mapa, las filma a un RenderTexture y lo muestra en la UI |

La idea central: **la UI nunca consulta las puertas 3D directamente**. Consulta el
diccionario `ObjectStateManager` usando IDs únicos que el editor bakeó dentro del
`MapData`. Las puertas reales *empujan* su estado a ese diccionario mediante eventos
en el momento en que se abren o bloquean.

```
  AUTORÍA (escena)                 BAKEO                    RUNTIME
┌──────────────────┐      ┌────────────────────┐    ┌─────────────────────┐
│ MapController    │      │ MapSerializer      │    │ UIMap               │
│ ├─ MapRoom (x N) │ ───► │ (al guardar escena │    │  └─► MapRenderer    │
│ │   └─ Shape     │      │  o entrar a Play)  │    │       .GenerateAll()│
│ └─ MapDoor (x N) │      │ escribe en MapData │    │       lee estados de│
└──────────────────┘      └────────────────────┘    │  ObjectStateManager │
        ▲                                           └─────────▲───────────┘
        │ ObjectUniqueId vincula todo               eventos   │
        └───────────────────────────────── DoorBase.OnOpened/OnLocked
```

---

## 1. Autoría: creación y edición del mapa

### NewMapWizard
`Scripts/Editor/NewMapWizard.cs` — menú **Horror Engine → Wizards → New Map**.

1. Crea un asset `MapData` (ScriptableObject) en la carpeta elegida y le genera un ID.
2. Opcionalmente instancia el prefab `MapController` en la escena actual y le asigna el asset.
3. Abre la `MappingEditorWindow` con ese controller seleccionado.

### Estructura en escena
El mapa se autorea como jerarquía de GameObjects:

```
MapController (+ ObjectUniqueId + SavableObjectState)
├─ MapRoom "Hall"        (+ Shape, ObjectUniqueId, SavableObjectState)
│   ├─ MapDetailingShape (+ Shape, MapElementSavable opcional)
│   └─ MapImage          (+ MapElementSavable opcional)
├─ MapRoom "Pasillo"
├─ MapDoor "Puerta_Hall" (+ ObjectUniqueId, SavableObjectState)
└─ MapDoor "Puerta_B1"
```

- Todos los elementos heredan de `MapElement` (`Scripts/Mapping/MapDoor.cs`, clase base
  abstracta): exponen `Offset` (Vector2), `Rotation`, `Scale` y `ZOrder` — sus
  coordenadas en el espacio 2D del mapa, independientes del transform 3D.
- `MapRoom` requiere un componente `Shape`: un polígono de puntos que define la silueta
  de la habitación. Ese mismo polígono se usa en runtime para detectar en qué habitación
  está el jugador (`ContainsWorldPosition`, point-in-polygon sobre el plano XZ).
- `MapRoom.LinkedElements`: lista de elementos (puertas, detalles) que "pertenecen" a la
  habitación. Con `AutoLinkChildren` los hijos se agregan solos en `Awake()`. **Una puerta
  solo se dibuja en el mapa si alguna habitación visible la tiene linkeada.**
- `MapDoor.Door`: referencia a la puerta jugable real (`DoorBase`). Es el vínculo entre
  el elemento de mapa y el gameplay.

### MappingEditorWindow
`Scripts/Editor/MappingEditorWindow.cs` — ventana **HE Map Editor**.

Editor 2D con grilla (dimensiones `MapData.Size` × `CellSize`). Permite:
- Ver todas las `MapRoom` y `MapDoor` del `MapController` dibujadas sobre la grilla.
- Seleccionar un elemento (sincronizado con la selección de Unity), moverlo y rotarlo
  con el teclado, y editar sus propiedades en un inspector lateral.
- Crear habitaciones, puertas, líneas de detalle, formas e imágenes desde prefabs
  configurados en la propia ventana.

No hay ningún paso manual de "exportar": la edición queda en la escena y el bakeo es automático (ver abajo).

---

## 2. Bakeo: MapSerializer

`Scripts/Editor/MapSerializer.cs` — clase estática `[InitializeOnLoad]`.

Se suscribe a dos eventos del editor:
- `EditorSceneManager.sceneSaved` → serializa.
- `EditorApplication.playModeStateChanged` (al **entrar** a Play Mode) → serializa.

`SerializeAndSaveMap(MapController)` recorre la jerarquía y vuelca todo al asset:

| De la escena | Al MapData |
|---|---|
| `MapController` + su `ObjectUniqueId` | `MapData.ControllerUniqueId` |
| Cada `MapDoor` (nombre, tamaño, transform 2D, `ObjectUniqueId`) | `MapData.Doors : List<MapDoorSerializedData>` |
| Cada `MapRoom` (nombre, shapes, transform 2D, `LinkedElements` como IDs, detalles, imágenes, `ObjectUniqueId`) | `MapData.Rooms : List<MapRoomSerializedData>` |

**Punto clave**: lo que se guarda de cada elemento es su **`ObjectUniqueId`**. En runtime,
ese ID es la clave para buscar el estado actual en `ObjectStateManager`. El asset `MapData`
contiene la *geometría* (estática); el *estado* (dinámico) vive en el save system.

`MapData` (`Scripts/Mapping/MapData.cs`) además define: `Size`, `CellSize`,
`GlobalScale` (escala mundo→mapa), `Name`/`Abbreviation` localizables, y un `MapSet`
opcional (`MapDataSet`: array de mapas, usado para pisos de un mismo edificio).

---

## 3. Persistencia: estados por ID único

### El backbone genérico

- **`ObjectUniqueId`**: componente que da a cada GameObject un ID string estable.
- **`SavableObjectState`** (`Scripts/SaveSystem/SavableObjectState.cs`): serializa el
  objeto a un `ObjectStateSaveDataEntry` (transform local, `activeSelf`, y los datos de
  todos los componentes `ISavableObjectStateExtra` del mismo GameObject, guardados como
  pares *tipo → string*). En `Start()` aplica el estado guardado si existe.
- **`ObjectStateManager`** (`Scripts/SaveSystem/ObjectStateManager.cs`): singleton con un
  `Dictionary<string, ObjectStateSaveDataEntry>` indexado por `ObjectUniqueId`.
  `SetState()` upserta una entrada; `GetState(id)` la consulta. `CaptureStates()` /
  `ApplyStates()` recorren la escena completa (los usa el flujo de save/load y las
  transiciones de escena). Este diccionario es lo que el save game escribe a disco.

### Estado de puertas: MapDoor

`Scripts/Mapping/MapDoor.cs`. Estados posibles (`MapDoorState`):

| Estado | Significado | Material en el mapa |
|---|---|---|
| `Unknown` (default) | El jugador nunca interactuó con la puerta | `DoorUnknownMaterial` |
| `Locked` | Intentó abrirla y estaba cerrada con llave | `DoorLockedMaterial` |
| `Unlocked` | La abrió al menos una vez | `DoorUnlockedMaterial` |

Flujo *push* (dirigido por eventos, no por polling):

1. En `Start()`, `MapDoor` se suscribe a `Door.OnLocked` y `Door.OnOpened` de su
   `DoorBase`. Si la puerta es una `SceneDoor` con `Exit` (puerta espejo en la otra
   escena), también se suscribe a los eventos de la salida — así el estado se comparte
   entre ambos lados.
2. Al disparar un evento → `MarkAs(state)`: actualiza `State` y llama
   `m_Savable.SaveState()` **inmediatamente**, escribiendo la entrada en
   `ObjectStateManager` sin esperar al próximo save.
3. `MapDoor` implementa `ISavableObjectStateExtra`: `GetSavableData()` devuelve
   `State.ToString()`; `SetFromSavedData()` lo parsea de vuelta. Por eso el estado
   sobrevive save/load y cambios de escena.

En el lado de lectura, `MapDoorSerializedData.GetState()` (`MapData.cs:107`) hace la
consulta inversa: busca la entrada por `UniqueId` en `ObjectStateManager` y extrae el
dato del componente `MapDoor` con `GetComponentData<MapDoor>()`. Si no hay entrada,
devuelve `Unknown`.

### Estado de habitaciones: MapRoom

Mismo mecanismo (`ISavableObjectStateExtra`), con estados **ordenados por progreso**
(`MapRoomState`): `Unknown < NotVisited < Visited < Completed`.
`TryMarkAs()` solo permite avanzar, nunca retroceder.

`MapController` (`Scripts/Mapping/MapController.cs`) mantiene los estados al día:
- Escucha `SceneTransitionPostMessage`, `DoorTransitionEndMessage` y
  `MapStepCompletedMessage` (bus estático `MessageBuffer<T>`).
- En cada evento, `UpdateContent()`: marca `Visited` la habitación que contiene al
  jugador (point-in-polygon con el `Shape`), promueve a `Completed` las habitaciones
  cuyos `MapRoomCompletionStep` estén todos cumplidos (p. ej. recoger un ítem o vaciar
  un contenedor: `CompleteMapRoomStepOnPickup`, `CompleteMapRoomStepOnContainerEmpty`),
  y si el jugador posee el mapa en inventario, promueve `Unknown → NotVisited`.
- También persiste su propio flag `Visited` (para saber si el jugador estuvo en ese nivel).

### Detalles e imágenes: MapElementSavable

`MapDetailingShape` y `MapImage` pueden llevar un `MapElementSavable` con métodos
`ActivateOnMap()` / `DeactivateOnMap()` pensados para UnityEvents de gameplay
(p. ej. revelar un pasadizo en el mapa al descubrirlo). Activan/desactivan el
GameObject y fuerzan `SaveState()`; el renderer luego lee `savedState.Active`.

---

## 4. Presentación: UIMap + MapRenderer

### Cómo se obtiene un mapa
`PickupMap` (`Scripts/Pickups/PickupMap.cs`): pickup que agrega el `MapData` (o todo su
`MapDataSet`, p. ej. todos los pisos) a `GameManager.Instance.Inventory.Maps`, y
opcionalmente abre el mapa al recogerlo. `MapData.IsKnownByPlayer()` =
*visitó el nivel* **o** *tiene el mapa en inventario*.

### UIMap
`Scripts/UI/UIMap.cs`. Pantalla de mapa con:
- `UIMapList`: selector de mapas conocidos.
- Panel de pisos (si el mapa pertenece a un `MapDataSet`), coloreando el piso actual.
- `RawImage` que muestra el `RenderTexture` del `MapRenderer`.

Al seleccionar un mapa: `MapRenderer.GenerateAll(map)`; si es el mapa del nivel actual,
`MapController.UpdateContent()` + posiciona el ícono del jugador
(`GetTransformInRoom` convierte posición mundial → espacio de mapa con la matriz TRS
de la habitación) y permite panear la cámara con el stick.

### MapRenderer
`Scripts/Mapping/MapRenderer.cs` — singleton. **No dibuja con Canvas**: genera geometría
3D real en una layer oculta y la filma con una cámara ortográfica hacia un
`RenderTexture`.

`GenerateAll(MapData)` reconstruye todo desde cero en cada apertura:
1. Destruye el contenido anterior y genera la grilla (`GridGenerator`).
2. **Habitaciones**: por cada `MapRoomSerializedData`, `GetState()` decide:
   - `Unknown` sin mapa en inventario → **no se dibuja** (fog of war).
   - `Unknown` con mapa → se trata como `NotVisited`.
   - Según estado usa un `ShapeCreationProcess` distinto (`RoomNotVisitedSP`,
     `RoomIncompletedSP`, `RoomCompletedSP`) + paredes (`WallsSP`).
   - Dibuja sus detalles e imágenes respetando `GetState().Active`.
   - Registra sus `LinkedElements` como visibles.
3. **Puertas**: solo las que estén en el set de elementos visibles. Genera un quad con
   `Size` de la puerta y asigna el material según `MapDoorSerializedData.GetState()`
   (Unknown/Locked/Unlocked). **Aquí es donde el estado guardado se convierte en color
   en la UI.**

Los elementos se separan por altura Y (grid −1, detalles 1, paredes 2, puertas 3,
jugador 5) para controlar el orden de dibujado con una sola cámara ortográfica.

Con varios `MapController` cargados (pisos), `MapController.GetCurrent()` elige el que
tiene una habitación conteniendo al jugador con menor distancia vertical.

---

## Secuencias resumidas

**El jugador abre una puerta cerrada con llave (sin la llave):**
```
DoorLock → DoorBase.OnLocked
         → MapDoor.OnDoorLocked() → MarkAs(Locked)
         → SavableObjectState.SaveState()
         → ObjectStateManager[id] = { ..., MapDoor: "Locked" }
```

**El jugador abre el mapa en la UI:**
```
UIMap.Show → MapRenderer.GenerateAll(mapData)
           → por cada room/door bakeada en MapData:
               GetState() → ObjectStateManager.GetState(UniqueId)
           → malla + material según estado → cámara orto → RenderTexture → RawImage
```

**Save / Load:**
```
Save: ObjectStateManager.GetSavableData() → lista de entradas → disco
Load: SetFromSavedData() reconstruye el diccionario
      → al cargar escena, SavableObjectState.Start() reaplica cada estado
      → MapDoor.SetFromSavedData("Locked") restaura State
```

---

## Ideas clave para portar a CrimsonDraft

1. **Desacople por ID**: la UI del mapa no conoce las puertas; solo conoce IDs bakeados
   y un diccionario de estados. Equivale a nuestro patrón de eventos: las puertas
   publicarían su estado (MessagePipe) y un servicio de estado central lo retendría.
2. **Push por eventos, no polling**: el estado se escribe en el momento del cambio
   (`OnOpened`/`OnLocked` → save inmediato), así el mapa siempre está al día sin
   escanear la escena.
3. **Geometría estática vs. estado dinámico**: el ScriptableObject solo guarda la
   forma del mapa; el progreso vive en el save system. Encaja con nuestra convención
   de ScriptableObjects como datos de configuración.
4. **Bakeo automático en editor**: serializar al guardar la escena elimina el paso
   manual de exportación y evita mapas desincronizados.
5. **Estados monotónicos de habitación** (`TryMarkAs` solo avanza) simplifican el
   razonamiento sobre progresión y evitan regresiones visuales en el mapa.
