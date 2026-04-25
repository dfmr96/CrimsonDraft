---
estado: borrador
ultima-revision: 2026-04-25
tags:
  - game-design
---

# Sistema de Diálogos

Todo el texto visible al jugador durante la exploración pasa por un motor de diálogos centralizado basado en **Yarn Spinner**. Ningún string de interfaz vive en código C#.

---

## Diseño

### Principio fundamental

> Todo texto que el jugador lee en pantalla está escrito en un archivo `.yarn`. El código solo decide qué nodo iniciar y qué variables pasar.

Este principio garantiza que la localización sea posible sin modificar código, y que el contenido sea editable por diseñadores sin tocar scripts.

---

### Estructura del motor

El motor de diálogos tiene tres capas:

| Capa | Responsabilidad |
|---|---|
| **YarnProject** | Compila todos los archivos `.yarn` de la escena de navegación en un único proyecto |
| **DialogueRunner** | Ejecuta nodos Yarn, gestiona opciones y envía líneas a las vistas |
| **IDialogueService** | Servicio del juego que encapsula al runner: pausa tiempo, cambia input map, pasa variables y registra comandos por sesión |

El `IDialogueService` es el único punto de entrada para los interactuables. Ningún interactuable llama al `DialogueRunner` directamente.

---

### Ciclo de vida de un diálogo

```
Interactuable llama StartDialogue(nodeName, variables, comandos)
  → IDialogueService pausa Time.timeScale
  → IDialogueService cambia input map a UI
  → Variables cargadas en el almacén de variables Yarn
  → Comandos registrados como handlers para esta sesión
  → DialogueRunner inicia el nodo
  → El jugador navega líneas y opciones con input UI
  → Nodo termina
  → IDialogueService restaura Time.timeScale
  → IDialogueService vuelve a input map Gameplay
  → Handlers de comandos de la sesión eliminados
```

El juego está **siempre pausado** mientras hay un diálogo activo. El input cambia a **UI map** para toda la duración.

---

### Nodos Yarn

Cada objeto interactuable que muestra texto tiene un campo `yarnNodeName` en su data asset o MonoBehaviour. Este campo contiene el nombre del nodo `.yarn` que se debe ejecutar.

**Convención de nombres de nodo:**

| Tipo | Formato | Ejemplo |
|---|---|---|
| POI | `poi_<identificador>` | `poi_rastro_de_sangre` |
| Puerta | `door_<identificador>` | `door_bodega_principal` |
| Socket | `socket_<identificador>` | `socket_panel_reactor` |
| Ítem (uso/confirmación) | `item_<identificador>` | `item_uso_medkit` |

Los archivos `.yarn` se organizan por sistema dentro de `Assets/Dialogues/`:

```
Assets/Dialogues/
  Navigation.yarnproject
  poi/
  doors/
  sockets/
  items/
```

---

### Variables Yarn por sistema

Las variables se pasan al iniciar el diálogo. El nodo Yarn las usa para ramificar o mostrar texto dinámico.

#### PoiInteractable

No recibe variables. El contenido del nodo es texto fijo (monólogo de examinación).

#### DoorInteractable

| Variable | Tipo | Significado |
|---|---|---|
| `$key_required` | bool | La puerta exige una llave específica |
| `$has_key` | bool | El jugador tiene la llave en este momento |
| `$key_name` | string | Nombre de la llave requerida |

El nodo de puerta rama según estas variables. Si `$has_key` es verdadero, presenta opciones Sí/No.

#### ItemSocketInteractable — Interact (estado del socket)

| Variable | Tipo | Significado |
|---|---|---|
| `$activated` | bool | El socket ya está completamente lleno |
| `$slots_filled` | number | Slots actualmente ocupados |
| `$slots_total` | number | Total de slots requeridos |

#### ItemSocketInteractable — TryInsert (feedback de inserción)

| Variable | Tipo | Significado |
|---|---|---|
| `$insert_result` | string | `"success"` o `"wrong_item"` |
| `$item_name` | string | Nombre del ítem que se intentó insertar |
| `$slots_filled` | number | Slots ocupados tras la inserción |
| `$slots_total` | number | Total de slots requeridos |

---

### Comandos Yarn

Los comandos son llamadas desde el script `.yarn` hacia el juego. Permiten que la elección del jugador desencadene lógica de juego.

**Comandos por sesión** (registrados por el interactuable, eliminados al cerrar el diálogo):

| Comando | Registrado por | Efecto |
|---|---|---|
| `<<doorConfirmed>>` | DoorInteractable | Consume la llave del inventario y ejecuta `onOpen` |

**Patrón de uso en `.yarn`:**

```
title: door_bodega_principal
---
<<if $has_key>>
La puerta está bloqueada. Tienes {$key_name}.
-> Desbloquear
    <<doorConfirmed>>
-> Cancelar
<<elseif $key_required>>
Necesitas {$key_name} para abrir esta puerta.
<<else>>
La puerta está bloqueada.
<<endif>>
===
```

Los comandos globales permanentes (sonidos, efectos) se registran una sola vez al inicializar el servicio.

---

### Data assets — campos de diálogo

| Asset | Campo nuevo |
|---|---|
| `PoiData` | `yarnNodeName: string` (reemplaza `lines: string[]`) |
| `DoorData` | `yarnNodeName: string` |
| `ItemSocketInteractable` | `yarnNodeName: string` (serializado en el MonoBehaviour) |

---

### Tabla de comportamiento actualizada

| Tipo | Pausa | Input | Motor |
|---|---|---|---|
| PickupInteractable | No | Gameplay | — |
| DocumentInteractable | Sí | UI map | InteractionReaderView (propio) |
| DoorInteractable | Sí* | UI map* | Yarn Spinner |
| PoiInteractable | Sí | UI map | Yarn Spinner |
| ContainerInteractable | Sí | UI map | ContainerView (propio) |
| ItemSocketInteractable | Sí* | UI map* | Yarn Spinner |

*Solo cuando hay texto que mostrar. Las puertas desbloqueadas y los sockets sin feedback no inician diálogo.

---

## Intención

> El jugador lee el mundo, no los tooltips del motor.

Mover todo el texto a Yarn Spinner resuelve un problema práctico —localización— pero también impone una disciplina de diseño: si quieres que el jugador vea un texto, tienes que escribirlo intencionalmente en un archivo de diálogo. No hay atajos de `Debug.Log` disfrazados de UI.

Las opciones Sí/No en puertas e ítems no son confirmaciones de seguridad. Son momentos de decisión: el jugador elige conscientemente consumir un recurso. Esa fricción es intencional en un survival horror donde cada llave y cada dosis importa.

> La información que el jugador recibe al examinar un POI debe justificar la pausa. Si el texto no agrega tensión, misterio o contexto narrativo, no debería existir.

---

## Pendiente

- [ ] Definir si los nodos de POI pueden tener opciones (conversación ramificada) o solo líneas lineales
- [ ] Definir convención de nombres para localización de string tables
- [ ] Confirmar rama `feature/yarn-spinner` como rama de desarrollo de este sistema
- [ ] Diseñar presentación visual de `LineView` y `OptionsListView` (fuente, colores, posición en pantalla)
- [ ] Documentar comandos globales permanentes cuando se definan

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Interactuables]] | Ver [[Sistema de Item Socket]] | Ver [[Sistema de Inventario]]
