# Brainstorm: Sistema de Inventario MVP

**Fecha:** 2026-03-07
**Estado:** Aprobado

---

## Contexto

El diseño original (`Sistema de Inventario.md`) especificaba una grilla 4×4 por operador con tamaños físicos, rotación de ítems y transferencia por arrastre. Para el MVP se simplifica a:

- Inventario **compartido** para todo el roster (lista plana, sin límite)
- Representación como **lista** en lugar de grilla
- Un solo slot de equipamiento por operador: **arma principal**
- Sin cargadores — solo cajas de balas
- Sin ítem táctico

---

## Decisiones de diseño

| Aspecto | Decisión |
|---|---|
| Inventario | Compartido para todo el roster |
| Capacidad | Ilimitada |
| Representación | Lista plana |
| Slot por operador | Solo arma principal (armaEquipada) |
| Ítem táctico | Eliminado del MVP |
| Cargadores | Eliminados — solo cajas de balas |
| Acceso en combate | Bloqueado |
| Input | Joystick/teclado, sin mouse |

---

## Estructura de datos

**Ítem:** `{ id, tipo, nombre, calibre? }`

**Tipos de ítem:**
| Tipo | Acciones disponibles |
|---|---|
| `Arma` | Equipar / Desequipar, Examinar |
| `CajaBalas` | Recargar, Examinar |
| `Consumible` | Usar, Examinar |

**Slot de operador:** cada operador tiene `armaEquipada` (puede ser nula). El arma equipada permanece en la lista compartida con `equipadoPor: operadorId`. Si el operador muere, el arma vuelve al pool sin dueño.

---

## Layout de pantalla (Opción A aprobada)

```
┌──────────────────────────────────────────────────────┐
│  INVENTARIO                                          │
│                                                      │
│  ┌─────────────────────┐  ┌──────────────────────┐   │
│  │ Lista de ítems      │  │ Operadores           │   │
│  │                     │  │                      │   │
│  │ > Benelli M4        │  │ ● García             │   │
│  │   Mk18 [Eq: García] │  │   Arma: Benelli M4   │   │
│  │   9mm Box ×32       │  │                      │   │
│  │   9mm Box ×18       │  │ ● Torres             │   │
│  │   Granada           │  │   Arma: ---          │   │
│  │                     │  │                      │   │
│  └─────────────────────┘  └──────────────────────┘   │
│                                                      │
│  [A] Acción   [B] Cerrar                            │
└──────────────────────────────────────────────────────┘
```

Panel derecho: solo visual, sin foco de navegación.

---

## Flujo de navegación

- **D-pad / flechas:** navegar por la lista de ítems
- **A (confirmar):** abrir menú contextual del ítem seleccionado
- **B:** cerrar inventario → volver a exploración
- **Tab / Select:** abrir inventario desde exploración

---

## Menú contextual por tipo

### Arma → Equipar / Desequipar

Submenú con lista de operadores. El operador que ya lleva esa arma muestra `✓`. Seleccionar otro operador transfiere directamente. Si el operador destino ya tenía otra arma, esa arma queda sin dueño en la lista.

### CajaBalas → Recargar

Submenú con operadores que tienen arma equipada del calibre compatible y ammo < máximo. Seleccionar un operador consume la caja y lleva su ammo al máximo. Si no hay operadores compatibles: acción deshabilitada (grayed out).

### Consumible → Usar

Acción inmediata o submenú de operadores. Comportamiento específico TBD por consumible.

### Cualquier ítem → Examinar

Overlay con descripción. B para volver.

---

## Acceso

- Solo durante **exploración**. Bloqueado durante combate.
- Al abrir: input de movimiento desactivado.
- Al cerrar: input de movimiento restaurado inmediatamente.
