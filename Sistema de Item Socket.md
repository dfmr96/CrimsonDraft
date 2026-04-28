---
estado: borrador
ultima-revision: 2026-04-24
tags:
  - game-design
---

# Sistema de Item Socket

Un **Item Socket** es un objeto del mundo que requiere uno o más ítems específicos para activarse. Los ítems se insertan desde el inventario mediante el comando **Usar**, y el socket dispara un evento al recibir todos sus requerimientos.

---

## Diseño

### SocketItem — tipo de ítem

El tipo **SocketItem** agrupa los ítems destinados exclusivamente a ser insertados en Item Sockets. No tiene propiedades adicionales respecto a [[Sistema de Inventario#Tipos de ítems y stackability|la base]]: su `ItemId` es suficiente para la lógica de inserción.

| Campo | Tipo | Descripción |
|---|---|---|
| `ItemId` | string | Identificador único. El socket lo usa para comparar. |
| `DisplayName` | string | Nombre mostrado en el feedback de inserción. |
| `Icon` | Sprite | Ícono en la grilla de inventario. |

Acciones disponibles en el menú contextual del inventario:

| Acción | Disponible |
|---|---|
| Usar | Sí — inserta el ítem en el socket apuntado por el raycast. |
| Combinar | Sí |
| Examinar | Sí |

> El tipo Consumible también muestra **Usar**, pero su comportamiento es distinto: los consumibles se aplican sobre el operador (curación, efectos). Un SocketItem solo puede usarse sobre un Item Socket en el mundo.

### ItemSocketInteractable

MonoBehaviour en el mundo que define los requerimientos y mantiene el estado del socket. Implementa [[Sistema de Interactuables#Interfaz central|IInteractable]].

**Campos serializados:**

| Campo | Tipo | Descripción |
|---|---|---|
| `requiredItems` | SocketItemData[] | Ítems necesarios para activar el socket. Puede repetirse el mismo tipo. |
| `onActivated` | UnityEvent | Evento que se dispara cuando todos los slots están satisfechos. |

**Estado runtime** (no persiste entre sesiones):

| Campo | Tipo | Descripción |
|---|---|---|
| `inserted` | bool[] | Paralelo a `requiredItems`. Indica qué slots ya fueron satisfechos. |
| `activated` | bool | Verdadero una vez activado. Impide re-activación. |

### Flujo de uso

El jugador usa un SocketItem desde el inventario. El sistema reutiliza el mismo raycast que [[Sistema de Interactuables#Principio de detección|Interact normal]] — misma distancia, misma LayerMask.

```
Jugador abre inventario
  → Selecciona un SocketItem
  → Menú contextual → Usar
    → Raycast hacia adelante
        NO hay hit con ItemSocketInteractable
          → no ocurre nada, ítem permanece en inventario
        SÍ hay hit con ItemSocketInteractable
          → socket.TryInsert(item)
              ¿Coincide con algún slot no satisfecho?
                SÍ → inserted[i] = true
                     Feedback: "Insertado: {nombre}."
                     ítem eliminado del inventario
                     ¿Todos los slots satisfechos?
                       SÍ → onActivated.Invoke()
                       NO → socket espera próxima inserción
                NO → Feedback: "No se puede usar {nombre} aquí."
                     ítem permanece en inventario
```

El ítem se consume completamente al insertarse con éxito — independientemente de cualquier campo de usos múltiples.

### Interact normal sobre el socket

Cuando el jugador presiona Interact frente al socket sin estar usando un ítem, el socket muestra su estado actual como checklist:

```
[ ] Battery
[✓] Keycard
```

Si el socket ya fue activado, muestra `"Ya activado."`.

### Feedback

| Situación | Mensaje (PoiController) |
|---|---|
| Inserción exitosa | "Insertado: {nombre}." |
| Ítem no compatible | "No se puede usar {nombre} aquí." |
| Interact normal, activado | "Ya activado." |
| Interact normal, pendiente | Lista de slots con estado [✓] / [ ] |

### Tabla de comportamiento

| Propiedad | Valor |
|---|---|
| Pausa el juego | No |
| UI adicional | Ninguna (solo líneas de feedback via PoiController) |
| Estado persiste entre sesiones | No — solo runtime |
| Orden de inserción requerido | No — cualquier orden válido |
| Mismo ítem requerido múltiples veces | Sí — si aparece más de una vez en `requiredItems` |

---

## Intención

> El Item Socket convierte la gestión del inventario en parte de la exploración del entorno, no solo del combate.

La mayoría de los puzzles ambientales del género muestran el objeto requerido de forma explícita. En Crimson Draft, el jugador descubre qué necesita el socket al interactuar con él — o leyendo los documentos del entorno. La información está en el mundo, no en un prompt de UI.

El mecanismo de inserción desde el inventario refuerza la agencia: el jugador decide cuándo acercarse, cuándo usar el ítem, con qué operador. No hay cutscene de inserción ni interrupción del flujo de juego. La consecuencia — el evento activado — es inmediata y visible.

Los sockets con múltiples requerimientos generan tensión de inventario: ¿tenemos todos los ítems necesarios, o hay que explorar más antes de poder activarlo?

---

## Pendiente

- [ ] Decidir si el estado del socket (`inserted[]`) persiste al cambiar de escena
- [ ] Definir feedback visual en el mundo (luz, animación) para socket parcialmente satisfecho vs. completamente activado
- [ ] Definir si un SocketItem puede combinarse con otro para cumplir un requerimiento distinto al original

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Inventario]] | Ver [[Sistema de Interactuables]]
