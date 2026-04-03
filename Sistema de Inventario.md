---
estado: aprobado
ultima-revision: 2026-04-03
tags:
  - game-design
---

# Sistema de Inventario

El inventario es una grilla compartida por todo el roster. Cada operador tiene su propio bloque de slots dentro de la grilla global — los ítems se mueven libremente entre bloques, pero cada operador solo puede usar los ítems que están en sus propios slots.

---

## Diseño

### Estructura

El inventario es una **grilla única** de `rosterCount × 4` slots. Visualmente se presenta como bloques 2×2 contiguos, uno por operador:

```
[ Op1 ][ Op1 ] | [ Op2 ][ Op2 ] | [ Op3 ][ Op3 ] | ...
[ Op1 ][ Op1 ] | [ Op2 ][ Op2 ] | [ Op3 ][ Op3 ] | ...
```

El índice global de un slot determina su dueño: `slotIndex / 4` = operador, `slotIndex % 4` = posición dentro del bloque 2×2.

### `InventorySlot`

Cada slot tiene tres estados:

| Estado | Descripción |
|---|---|
| Vacío | `Item = null`, `Quantity = 0` |
| Ocupado | `Item = <ítem>`, `Quantity = 1` |
| Apilado | `Item = <ítem>`, `Quantity = N` — solo si `ItemData.Stackable = true` |

El slot siempre existe aunque esté vacío. No hay slots nulos.

### Tipos de ítems y stackability

| Tipo | Stackable por defecto | Acciones disponibles |
|---|---|---|
| Arma | No | Equipar / Desequipar, Examinar |
| Caja de balas | Sí | Recargar, Examinar |
| Consumible | No | Usar, Examinar |

`ItemData` expone un campo `Stackable`. Las cajas de balas del mismo tipo que ya están en el bloque del operador destino apilan cantidad en lugar de ocupar un slot nuevo.

### Reglas de uso vs. movimiento

| Acción | Restricción |
|---|---|
| Mover ítem (reorder) | Libre — cualquier slot de la grilla completa |
| Equipar / Desequipar | Solo desde slots del operador dueño |
| Recargar | Solo desde slots del operador dueño |
| Usar consumible | Solo desde slots del operador dueño |

### Inventario lleno

Si todos los slots del operador destino están ocupados y el ítem no puede apilarse, `AddItem` devuelve `false`. El interactable de pickup muestra: `"No tienes espacio para: {nombre}."` sin recoger el ítem.

---

### Pantalla de inventario

La pantalla muestra la grilla completa. Cada bloque 2×2 tiene el nombre del operador como cabecera.

```
┌──────────────────────────────────────────────────────────────┐
│  INVENTARIO                                                  │
│                                                              │
│  García          Torres                                      │
│  ┌─────┬─────┐   ┌─────┬─────┐                              │
│  │Mk18 │9mm×32│  │     │     │                              │
│  │     │     │   │Bnli │     │                              │
│  └─────┴─────┘   └─────┴─────┘                              │
│                                                              │
│  [A] Acción   [Y] Mover   [B] Cerrar                        │
└──────────────────────────────────────────────────────────────┘
```

El cursor se mueve en 2D sobre la grilla completa. Cruzar la frontera entre bloques de operador es navegación normal.

### Controles

> El juego se juega exclusivamente con joystick / teclado. Sin input de mouse.

| Input | Estado | Acción |
|---|---|---|
| D-pad / Flechas | List | Mover cursor por la grilla |
| A (confirmar) | List | Abrir menú contextual (solo si slot tiene ítem) |
| Y (reorder) | List | Levantar ítem del slot actual (solo si tiene ítem) |
| A (confirmar) | Reorder | Soltar ítem — swap si destino tiene ítem, mover si está vacío |
| B | List | Cerrar inventario |
| B | Reorder | Cancelar — devolver ítem al slot origen |

### Menú contextual

Al confirmar sobre un slot con ítem aparece el menú contextual. Las acciones disponibles dependen del tipo de ítem **y** de si el slot pertenece al operador en turno.

> Si el ítem está en un slot ajeno, el menú contextual solo muestra **Examinar** (no se puede usar, equipar ni recargar desde slots de otro operador).

#### Equipar / Desequipar (armas)
Acción directa — equipar un arma del slot de Op1 la equipa a Op1 inmediatamente, sin submenu de selección de operador. Si Op1 ya tenía otra arma equipada, la anterior queda sin dueño en su slot original (sigue ocupando el slot).

#### Recargar (cajas de balas)
Disponible solo desde slots del operador dueño. Si no hay operador compatible (calibre + ammo por debajo del máximo): acción deshabilitada.

#### Usar (consumibles)
Comportamiento específico por consumible. TBD según tipo.

#### Examinar (cualquier ítem)
Overlay con descripción completa. B para volver.

---

## Intención

> La grilla sectorial hace que el inventario sea una decisión táctica de asignación de recursos, no solo de transporte.

El jugador decide conscientemente qué lleva cada operador. Mover ítems entre secciones es posible pero requiere acción deliberada — el slot "pertenece" a alguien. Esto crea tensión: si García tiene el único botiquín, ¿lo movemos a Torres para esta misión o lo dejamos donde está?

Los 4 slots por operador son intencionalmente restrictivos. El jugador no puede llevarse todo — tiene que priorizar por operador.

La grilla única detrás de escenas garantiza que reorganizar el equipo entre misiones sea fluido, sin fricciones técnicas entre "inventarios separados".

---

## Pendiente

- [ ] Comportamiento concreto de consumibles (Usar → qué efecto, sobre qué operador)
- [ ] Integración con [[Krokonil]] si existe como ítem consumible
- [ ] Sistema de zonas seguras para depositar ítems
- [ ] Máximo de ammo por arma / por operador (definir valor concreto)
- [ ] Decidir si las cajas de balas del mismo calibre apilan automáticamente al recoger o requieren mover manual
- [ ] Definir a qué bloque de operador va un ítem cuando se recoge durante exploración (¿primer slot libre del primer operador? ¿operador activo?)

---

Volver a [[Crimson Draft]] | Ver [[Diseño de Combate y Armas]] | Ver [[Sistema de Salud]]
