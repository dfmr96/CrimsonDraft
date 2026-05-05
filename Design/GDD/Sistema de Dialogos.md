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
Interactuable llama StartDialogue(nodeName, variables, onComplete, comandos)
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
  → onComplete se invoca (si fue provisto)
```

El juego está **siempre pausado** mientras hay un diálogo activo. El input cambia a **UI map** para toda la duración.

El parámetro `onComplete` permite al interactuable ejecutar lógica después de que el diálogo cierra — por ejemplo, abrir una puerta tras mostrar el feedback de uso de llave.

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

La lógica de inventario se ejecuta en C# **antes** de iniciar el diálogo. Yarn solo muestra el resultado — nunca presenta opciones para puertas.

| Variable | Tipo | Significado |
|---|---|---|
| `$outcome` | string | `"opened"` / `"needs_key"` / `"locked"` |
| `$key_name` | string | Nombre de la llave (presente cuando `$outcome` es `"opened"` o `"needs_key"`) |

Flujo completo:

```
Jugador interactúa con puerta bloqueada
  → Si no requiere llave específica:
      StartDialogue(yarnNodeName, { $outcome: "locked" })
  → Si requiere llave y el jugador la tiene:
      TryUseKey() → éxito
      StartDialogue(yarnNodeName, { $outcome: "opened", $key_name: "..." },
                    onComplete: () → unlocked=true, onOpen.Invoke())
  → Si requiere llave y el jugador no la tiene:
      StartDialogue(yarnNodeName, { $outcome: "needs_key", $key_name: "..." })
```

La puerta abre **después** de que el diálogo cierra, via `onComplete`. El jugador lee el feedback y luego la puerta se activa.

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

Los comandos son llamadas desde el script `.yarn` hacia el juego. Se usan cuando la elección del jugador dentro del diálogo debe desencadenar lógica — por ejemplo, al seleccionar una opción en un prompt de ítems.

**Comandos por sesión** (registrados por el interactuable, eliminados al cerrar el diálogo):

| Comando | Registrado por | Efecto |
|---|---|---|
| `<<itemUsed>>` | Ítem con confirmación | Ejecuta la lógica de uso del ítem |

**Patrón de uso en `.yarn` (ítem con confirmación):**

```
title: item_uso_medkit
---
¿Usar el Medkit ahora?
-> Sí
    <<itemUsed>>
-> No
===
```

**Patrón de uso en `.yarn` (puerta — sin opciones):**

```
title: door_bodega_principal
---
<<if $outcome == "opened">>
Usaste {$key_name}.
<<elseif $outcome == "needs_key">>
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

Las puertas no piden confirmación — la llave se usa automáticamente, igual que en los referentes del género. El feedback de Yarn informa al jugador qué ocurrió, no le pide permiso. Las opciones Sí/No se reservan para ítems consumibles donde el jugador puede querer conservar el recurso para otro momento.

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
