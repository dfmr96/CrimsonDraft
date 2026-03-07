---
estado: aprobado
ultima-revision: 2026-03-07
tags:
  - game-design
---

# Sistema de Inventario

El inventario es un pool compartido por todo el roster. Cada operador tiene un único slot de equipamiento: su arma principal.

---

## Diseño

### Estructura

El inventario es una **lista plana compartida** entre todos los operadores. No tiene límite de capacidad. No hay grillas, no hay tamaños físicos.

Cada **ítem** tiene: identificador, tipo, nombre, y calibre (solo si es arma o caja de balas).

### Tipos de ítems

| Tipo | Acciones disponibles |
|---|---|
| Arma | Equipar / Desequipar, Examinar |
| Caja de balas | Recargar, Examinar |
| Consumible | Usar, Examinar |

### Equipamiento

Cada operador tiene un único slot: **arma equipada** (puede estar vacío).

El arma equipada permanece en la lista compartida marcada con el nombre del operador que la lleva — `[Eq: García]`. Si un operador muere, su arma vuelve al pool sin dueño.

Equipar un arma a un operador que ya tiene otra: el arma anterior queda sin dueño en la lista. No se pierde.

### Pantalla de inventario

La pantalla tiene dos paneles:

**Panel izquierdo — lista de ítems (navegable):**
El jugador navega ítem por ítem con D-pad / flechas. Los ítems equipados muestran `[Eq: NombreOperador]`.

**Panel derecho — estado del roster (solo visual):**
Muestra cada operador con su arma equipada actual. No recibe foco de navegación — es referencia para el jugador.

```
┌──────────────────────────────────────────────────────┐
│  INVENTARIO                                          │
│                                                      │
│  ┌─────────────────────┐  ┌──────────────────────┐   │
│  │ > Benelli M4        │  │ García               │   │
│  │   Mk18 [Eq: García] │  │   Arma: Mk18         │   │
│  │   9mm Box ×32       │  │                      │   │
│  │   9mm Box ×18       │  │ Torres               │   │
│  │                     │  │   Arma: ---          │   │
│  └─────────────────────┘  └──────────────────────┘   │
│                                                      │
│  [A] Acción   [B] Cerrar                            │
└──────────────────────────────────────────────────────┘
```

### Controles

> El juego se juega exclusivamente con joystick / teclado. Sin input de mouse.

| Input | Acción |
|---|---|
| D-pad / Flechas | Navegar por la lista de ítems |
| A (confirmar) | Abrir menú contextual del ítem seleccionado |
| B | Cerrar inventario |
| Tab / Select | Abrir inventario (desde exploración) |

Al abrir el inventario, el input de movimiento del jugador se desactiva. Al cerrar, se restaura inmediatamente.

### Menú contextual

Al confirmar sobre un ítem aparece un menú vertical con las acciones disponibles para ese tipo.

#### Equipar / Desequipar (armas)

Submenú con la lista de operadores. El operador que ya lleva el arma seleccionada aparece con `✓`. Seleccionar un operador diferente transfiere el arma directamente.

#### Recargar (cajas de balas)

Submenú con los operadores que tienen un arma equipada del **calibre compatible** y ammo por debajo del máximo. Seleccionar un operador consume la caja y lleva su ammo al máximo.

Si no hay operadores compatibles: la acción aparece deshabilitada.

#### Usar (consumibles)

Comportamiento específico por consumible. En algunos casos: acción inmediata. En otros: submenú de operadores destino. TBD según consumible.

#### Examinar (cualquier ítem)

Overlay con la descripción completa del ítem. B para volver al inventario.

### Descarte

Los ítems no se descartan desde el inventario. Se depositan en zonas seguras del barco (sistema pendiente de diseño).

### Acceso

El inventario está disponible **solo durante exploración**. Queda bloqueado mientras el jugador está en combate.

---

## Intención

> El inventario compartido elimina la microgestión por personaje. El jugador decide qué lleva el equipo como unidad, no individualmente.

La lista es deliberadamente simple: sin grillas, sin tamaños, sin rotaciones. El peso de la decisión recae en qué ítems tiene el jugador, no en cómo los organiza espacialmente.

El slot único por operador hace que armar al roster sea una decisión táctica clara: con armas limitadas y varios operadores, el jugador elige quién va armado y quién no.

El arma de un operador muerto no se pierde — vuelve al pool. Esto evita que una muerte accidental destruya el progreso de equipo, pero sí obliga a volver a asignar en la siguiente oportunidad.

---

## Pendiente

- [ ] Máximo de ammo por arma / por operador (definir valor concreto)
- [ ] Sistema de zonas seguras para depositar ítems
- [ ] Comportamiento concreto de consumibles (Usar → qué efecto, sobre qué operador)
- [ ] Integración con [[Krokonil]] si existe como ítem consumible
- [ ] Decidir si las cajas de balas son stackeables en la lista o entradas separadas

---

Volver a [[Crimson Draft]] | Ver [[Diseño de Combate y Armas]] | Ver [[Sistema de Salud]]
