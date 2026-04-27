---
estado: borrador
ultima-revision: 2026-04-25
tags:
  - game-design
---

# Sistema de Interactuables

Sistema que permite al jugador interactuar con objetos del entorno: recoger ítems, leer documentos, abrir puertas, examinar puntos de interés y registrar contenedores.

---

## Diseño

### Principio de detección

El jugador no recibe prompts visuales pasivos. La interacción es **intencional**: el jugador presiona el botón de interactuar (C / botón Sur del gamepad), y en ese momento el sistema dispara un **raycast** desde el personaje hacia adelante.

El raycast usa una **LayerMask exclusiva** llamada `Interactable`. Solo los objetos en esa capa son detectables. El raycast impacta en el **primer collider** que encuentra — si ese objeto es interactuable, se ejecuta la interacción. Si no, no ocurre nada.

```
Jugador presiona Interact
  → Raycast hacia adelante con LayerMask "Interactable"
  → Primer hit con IInteractable
  → Ejecuta Interact(InteractionContext)
```

No hay indicador de proximidad. No hay outline. El jugador debe posicionarse y orientarse hacia el objeto.

---

### Interfaz central

Todo objeto interactuable implementa **`IInteractable`**:

```
IInteractable
  Interact(InteractionContext context)
```

**`InteractionContext`** — empaqueta las dependencias que los interactuables pueden necesitar:

| Campo | Tipo | Uso |
|---|---|---|
| `InventoryService` | IInventoryService | Pickups y containers |
| `InputService` | IInputService | Cambio de input map en UI |
| `DialogueService` | IDialogueService | Texto al jugador vía [[Sistema de Diálogos\|Yarn Spinner]] |

El `PlayerInteractionCaster` construye el contexto y lo pasa al interactuable. No conoce el tipo concreto.

---

### Tipos de interactuables

#### PickupInteractable

Recoge un ítem del mundo y lo añade al inventario del jugador.

- Referencia a un `ItemData` existente (arma, caja de balas, consumible)
- Al interactuar: añade el ítem via `IInventoryService`, desactiva el GameObject
- No pausa el juego
- No abre ninguna UI

#### DocumentInteractable

Permite leer un documento a pantalla completa. Estilo Resident Evil / Metal Gear MSX.

- Datos en **`DocumentData`**: título (string), páginas (string[])
- Al interactuar:
  1. `Time.timeScale = 0`
  2. Cambia input a UI map
  3. Abre `InteractionReaderView` con el documento
- Navegación: Navigate ←→ avanza/retrocede páginas
- Cierre: Back / UIBack restaura `timeScale = 1` y vuelve a Gameplay map
- El documento puede configurarse como **recogible** (se añade a una colección) o **solo leíble** in-situ

#### DoorInteractable

Abre o bloquea el paso según configuración.

- Datos en **`DoorData`**: `bloqueada` (bool), `itemLlave` (ItemData?, nullable)
- **Puerta libre**: se activa inmediatamente al interactuar (animación o teleport configurado via UnityEvent)
- **Puerta bloqueada**: evalúa el inventario e inicia un nodo Yarn con variables de estado. Si el jugador tiene la llave, el nodo presenta opciones Sí/No. Ver [[Sistema de Diálogos]].

#### PoiInteractable

Muestra texto de examinación como diálogo en panel inferior, línea a línea.

- Datos en **`PoiData`**: `yarnNodeName` (string) — nombre del nodo Yarn a ejecutar
- Al interactuar: inicia el nodo Yarn correspondiente vía [[Sistema de Diálogos|IDialogueService]]
- El sistema de diálogos gestiona la pausa, el input y la progresión de líneas

#### ItemSocketInteractable

Requiere uno o más ítems de tipo [[Sistema de Item Socket|SocketItem]] para activarse. Ver [[Sistema de Item Socket]] para el diseño completo.

- Datos en campos serializados directamente en el MonoBehaviour: `requiredItems` (SocketItemData[]), `onActivated` (UnityEvent)
- Al presionar Interact sin ítem activo: muestra estado actual del socket vía [[Sistema de Diálogos|IDialogueService]] con variables `$slots_filled` y `$slots_total`
- Al usar un SocketItem desde el inventario: el socket valida por `itemId`, consume el ítem si coincide
- Cuando todos los slots están satisfechos: dispara `onActivated`
- No pausa el juego

#### ContainerInteractable

Abre un sub-inventario junto al inventario del jugador para transferir ítems.

- Datos en **`ContainerData`**: ítems iniciales (ItemData[]), flag `vaciado` (bool)
- Al interactuar:
  1. `Time.timeScale = 0`
  2. Cambia input a UI map
  3. Abre `ContainerView` como panel lateral simultáneo al inventario existente
- El jugador navega el contenido del container y transfiere con Confirm
- La `InventoryView` existente **no se modifica**
- El flag `vaciado` persiste: un container ya registrado no vuelve a tener ítems

---

### Tabla resumen de comportamiento

| Tipo | Pausa | Input | UI |
|---|---|---|---|
| PickupInteractable | No | Gameplay | Ninguna |
| DocumentInteractable | Sí | UI map | Pantalla completa, paginado |
| DoorInteractable | Sí* | UI map* | Nodo Yarn via [[Sistema de Diálogos]] |
| PoiInteractable | Sí | UI map | Nodo Yarn via [[Sistema de Diálogos]] |
| ContainerInteractable | Sí | UI map | Panel lateral junto al inventario |
| ItemSocketInteractable | Sí* | UI map* | Nodo Yarn via [[Sistema de Diálogos]] |

---

### ScriptableObjects de datos

| ScriptableObject | Campos |
|---|---|
| `DocumentData` | título: string, páginas: string[] |
| `PoiData` | yarnNodeName: string |
| `DoorData` | bloqueada: bool, itemLlave: ItemData?, yarnNodeName: string |
| `ContainerData` | ítems: ItemData[], vaciado: bool |

`PickupInteractable` referencia `ItemData` directamente — no requiere ScriptableObject propio.

---

### Vistas de UI

| Vista | Descripción |
|---|---|
| `InteractionReaderView` | Canvas pantalla completa. Título + texto paginado. Usado por DocumentInteractable. |
| `LineView` + `OptionsListView` | Vistas de Yarn Spinner. Panel inferior con línea actual y opciones. Usadas por POI, Door, Socket. Ver [[Sistema de Diálogos]]. |
| `ContainerView` | Canvas panel lateral. Lista de ítems del container. Usado por ContainerInteractable. |

---

### Escena de muestra

La escena Navigation incluye un ejemplo funcional de cada tipo:

| Objeto en escena | Tipo |
|---|---|
| Arma en el suelo | PickupInteractable (referencia a un ItemData de arma existente) |
| FILE 01 del marinero | DocumentInteractable (DocumentData con título y texto del FILE 01) |
| Puerta libre | DoorInteractable (bloqueada: false) |
| Puerta con llave | DoorInteractable (bloqueada: true, itemLlave: referencia a ItemData "Llave") |
| Rastro de sangre (POI) | PoiInteractable (3 líneas de examinación) |
| Caja de suministros | ContainerInteractable (2 ítems iniciales) |

---

## Intención

> El jugador interactúa con el mundo porque quiere, no porque el juego le indica que debe hacerlo.

La ausencia de prompts visuales es una decisión deliberada. En un survival horror, la tensión proviene de la incertidumbre. Un outline brillante o un ícono flotante destruye esa atmósfera: le dice al jugador "aquí hay algo seguro". Que el jugador tenga que acercarse, orientarse y presionar el botón hace que cada interacción sea un acto consciente.

El raycast intencional también evita interacciones accidentales — el jugador nunca recoge algo que no quería recoger.

Los documentos pausan el juego para que el jugador pueda leer sin presión. El lore de Crimson Draft está escrito para quien quiere encontrarlo — el sistema no penaliza al que decide leer.

Los POIs mantienen la tensión de movimiento: el jugador sigue presente en el mundo mientras recibe la información ambiental, pero con el juego pausado no hay riesgo de ser sorprendido.

---

## Pendiente

- [ ] Definir distancia máxima del raycast
- [ ] Definir si el FLAG `vaciado` del ContainerData se persiste entre sesiones (guardado)
- [ ] Diseñar `ContainerView` en detalle (controles, layout, transferencia parcial vs total)
- [ ] Decidir si DocumentInteractable añade el documento a una colección visible desde el inventario
- [ ] Confirmar si la llave se consume al usar DoorInteractable o permanece en inventario
- [ ] Integrar POIs del [[Acto I - Diseño Detallado]] con PoiInteractable

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Diálogos]] | Ver [[Sistema de Inventario]] | Ver [[Sistema de Item Socket]] | Ver [[Documentos del Marinera]] | Ver [[Acto I - Diseño Detallado]]
