# Sistema de Inventario

**Fecha:** 2026-02-18
**Estado:** Diseño aprobado

---

## Estructura General

Cada operador tiene una **grilla de inventario propia de 4×4** (16 slots). El inventario es **por personaje**: si el operador muere, todo lo que llevaba se pierde permanentemente.

Los items tienen **dimensiones físicas** que ocupan múltiples slots y se pueden **rotar 90°** para optimizar el espacio.

---

## Tamaños de Items

| Item | Tamaño | Stackeable | Notas |
|---|---|---|---|
| Pistola (9mm) | 2×1 | No | P229 |
| SMG | 3×1 | No | MP5 |
| AR | 3×1 | No | Mk18 |
| Escopeta | 4×1 | No | Benelli M4 |
| Cargador | 1×2 | No | Por calibre |
| Caja balas 9mm / 12ga | 1×1 | Sí (máx. TBD) | Recarga de cargadores |
| Caja metálica AR (5.56) | 2×2 | Sí (máx. TBD) | Recarga de cargadores |
| Puñal | 2×1 | No | Ítem táctico defensivo |
| Granada | 1×1 | No | Ítem táctico defensivo |
| Queroseno | 2×1 | No | Eliminación permanente post-combate |
| Encendedor | 1×1 | No | Requerido para usar queroseno |
| Dosis Krokonil | TBD | TBD | Pendiente de diseño |

---

## Equipamiento

El item **actualmente equipado** (activo en mano) muestra una **"E" en la esquina** de su slot en la grilla.

Solo puede haber un item activo por categoría:
- **Arma principal** (la que se usa en combate QTE)
- **Ítem táctico** (puñal o granada, usado en Acción Defenderse)

---

## Tensión de Espacio

La grilla 4×4 genera decisiones reales de qué llevar. Ejemplo de configuración típica:

```
[ Benelli M4 4×1         ]  — fila completa
[ Mk18 3×1    ] [  1×1  ]  — AR + 1 slot libre
[ Carg ] [ Carg ] [ G ] [ E ]  — 2 cargadores, 1 granada, 1 encendedor
[ BalaBox ] [ BalaBox ] [ Q 2×1 ]  — cajas balas + queroseno
```

Una escopeta (4×1) más una AR (3×1) ya consume 7 de 16 slots. Llevar dos armas largas deja poco espacio para suministros: cada arma adicional es un tradeoff explícito contra munición y consumibles.

---

## Relación con Otros Sistemas

- **Acción Defenderse**: Requiere tener puñal o granada en el inventario. Si el slot táctico está vacío, la acción no está disponible cuando el enemigo telegrafía el ataque.
- **Eliminación Permanente**: Requiere tener queroseno **y** encendedor simultáneamente. Perder uno de los dos inutiliza el otro.
- **Ammo system**: Los cargadores se recargan consumiendo cajas de balas del mismo calibre. Sin caja de balas, no se puede recargar el cargador vacío.

---

## Acceso desde Exploración

El inventario se abre **en cualquier momento durante la exploración** sin interrumpir la escena — el mapa de navegación permanece visible y activo en segundo plano.

| Input | Acción |
|---|---|
| **Tab / Select** | Abre el inventario |
| **Botón B / Esc** | Cierra el inventario y vuelve a exploración |

Al abrir el inventario el input de movimiento se desactiva automáticamente — el jugador no puede moverse mientras navega el inventario. Al cerrar, el control vuelve inmediatamente.

El inventario **no está disponible durante el combate** — la escena de combate usa su propio sistema de input y el inventario queda bloqueado hasta volver a exploración.

---

## Controles e Interacción

> El juego se juega **exclusivamente con joystick / teclado. Sin input de mouse.**

### Navegación normal

El cursor salta **de ítem en ítem** — nunca queda en una celda vacía.

| Input | Acción |
|---|---|
| D-pad / Flechas | Saltar al ítem siguiente en esa dirección |
| **Botón A** (confirmar) | Abrir menú contextual sobre el ítem seleccionado |
| **Botón X** (agarrar) | Entrar en modo reordenar |

### Modo reordenar (ítem agarrado)

Al agarrar un ítem, el cursor cambia a movimiento **celda por celda**. El ítem sigue al cursor con preview verde (válido) / rojo (inválido).

| Input | Acción |
|---|---|
| D-pad / Flechas | Mover celda por celda |
| **Botón Y** | Rotar ítem 90° |
| **Botón A** | Colocar ítem (si la posición es válida) |
| **Botón B** | Cancelar — ítem vuelve a su posición original |

**Transferir entre operadores:** cruzar a la fila de otro operador en modo reordenar y soltar el ítem = transferencia directa. No hay acción de menú para esto.

### Menú contextual

Aparece al confirmar sobre un ítem. Las opciones disponibles dependen del tipo:

| Acción | Disponible para | Descripción |
|---|---|---|
| **Equipar / Desequipar** | Armas, tácticos | Activa el ítem en su slot (Holster o Sling) |
| **Recargar** | Cargadores | Consume una caja de balas del mismo calibre |
| **Examinar** | Cualquier ítem | Muestra descripción completa |

#### Detalle: Recargar

- **9mm:** muestra las variantes disponibles con su cantidad — `RIP (×32) / FMJ (×18)` — el jugador elige cuál cargar.
- **Otros calibres** (12ga, 5.56): consume automáticamente la caja compatible. Sin submenú.
- Si no hay balas del calibre requerido: acción deshabilitada (grayed out).

#### Sin "Descartar"

Los ítems no se descartan desde el inventario. Se depositan en **zonas seguras** del barco (sistema pendiente de diseño).

---

## Pendiente

- Máximo de stack para cajas de balas (9mm, 12ga, 5.56)
- Tamaño y stackeabilidad de dosis Krokonil (si existe como ítem)
- Sistema de zonas seguras / almacenamiento en el barco
