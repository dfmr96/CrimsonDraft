---
estado: aprobado
ultima-revision: 2026-04-23
tags:
  - game-design
---

# Sistema de Combinación de Ítems

La combinación permite al jugador fusionar dos ítems del inventario para producir un ítem nuevo. Las combinaciones son recetas predefinidas — no hay experimentación libre.

---

## Diseño

### Recetas

Una **receta de combinación** define dos ítems de entrada y un ítem de salida:

| Campo | Tipo | Descripción |
|---|---|---|
| Entrada A | `ItemData` | Primer ítem requerido |
| Entrada B | `ItemData` | Segundo ítem requerido |
| Resultado | `ItemData` | Ítem generado al combinar |

Las recetas son **simétricas**: combinar A con B produce el mismo resultado que combinar B con A, sin importar el orden de selección.

Las recetas se definen como datos de producción (`CombineRecipeLibrary`) y son fijas por build. No hay recetas desbloqueables ni dinámicas.

### Resultado de la combinación

Al ejecutar una combinación válida:

- Ambos ítems de entrada se eliminan del inventario
- El ítem resultado ocupa el **primer slot disponible** de la grilla global (recorre operadores en orden)

Dado que dos slots se liberan y uno se ocupa, siempre hay espacio para el resultado.

### Reglas de uso

| Regla | Comportamiento |
|---|---|
| Simetría | A+B = B+A — el orden de selección no afecta el resultado |
| Slots de origen | Libre — se puede combinar desde slots de cualquier operador |
| Consumo | Ambos ítems de entrada se consumen siempre |
| Sin receta válida | No ocurre nada — el modo Combinar permanece activo |
| Cancelar | B vuelve al estado Normal sin consumir ítems |

Combinar no respeta la restricción de slots por operador que aplica a Equipar / Recargar / Usar. Los ítems pueden estar en bloques de operadores distintos.

### Flujo de combinación

```
1. Jugador abre menú contextual sobre ítem A → selecciona "Combinar"
2. El inventario entra en Modo Combinar:
   - Cursor cambia de color (amarillo / ámbar)
   - El slot fuente (ítem A) se resalta con un color secundario
3. Jugador navega con D-pad hacia ítem B → confirma con A
   3a. Si existe receta A+B:
       - Se eliminan los slots de A y B
       - El ítem resultado aparece en el primer slot disponible
       - Inventario vuelve al estado Normal
   3b. Si no existe receta:
       - No ocurre nada
       - Modo Combinar permanece activo
       - El jugador puede seleccionar otro ítem o cancelar con B
```

### Controles en Modo Combinar

| Input | Acción |
|---|---|
| D-pad / Flechas | Mover cursor |
| A sobre ítem distinto al fuente | Intentar combinación |
| A sobre slot vacío | Ignorado |
| A sobre slot fuente | Ignorado |
| B | Cancelar — volver al estado Normal |

En Modo Combinar, A no abre el menú contextual — intenta la combinación directamente.

### Estados visuales

| Estado del inventario | Color de cursor | Slot fuente |
|---|---|---|
| Normal | Color estándar | — |
| Modo Combinar | Amarillo / ámbar | Resaltado con color secundario |

No hay feedback de audio ni texto en intentos de combinación sin receta válida.

---

## Intención

> El jugador nunca combina por accidente. Cada combinación es deliberada — requiere seleccionar dos ítems en secuencia con intención.

La ausencia de feedback en combinaciones inválidas es intencional. No hay lista de recetas expuesta. El jugador descubre las combinaciones por exploración narrativa: encontrar una llave cerca de un maletín es la pista suficiente.

El sistema mantiene la tensión del inventario ajustado: combinar consume dos slots y produce uno. El resultado no es siempre mejor que los ingredientes — a veces es la única forma de avanzar en la narrativa.

---

## Pendiente

- [ ] Definir lista completa de recetas del Acto I
- [ ] Decidir si los ítems resultado pueden ser de un tipo nuevo (Key Item) o siempre son tipos estándar

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Inventario]] | Ver [[Acto I - Diseño Detallado]]
