# Reglas de uso de ítems (resumen)

> Resumen. El detalle completo (arquetipos, catálogo de ítems, tablas de preguntas Sí/No) está en
> [re2-interaction-rules.md](../../re2-interaction-rules.md).

## Regla de resolución implícita en RE2

```
1. ¿No necesita ítem?                     → sin ítem; Sí/No si es irreversible o mueve al jugador
2. ¿Es una puerta con llave?              → automática
3. ¿Elegir el ítem ES el puzzle o es una pieza mecánica?
                                          → elegida desde el inventario (V_USED_ITEM)
4. Resto (herramienta, llave de mueble)   → confirmada: detecta (keep_item_ck) + Sí/No
```

## Arquetipos

| Arquetipo | Resolución | Ejemplos |
|---|---|---|
| Examinable | Sin ítem | Mensajes de sala |
| Acción ambiental | Sí/No | Interruptores, escaleras, mapas |
| Recogible | Confirmada | Ítems, documentos |
| Cerradura de puerta | **Automática** | Spade, Diamond, Heart, Club… (14 de ~310 puertas) |
| Cerradura de herramienta | **Confirmada** | Lockpick, Small Key, Special Key, Weapon Box Key, Master Key |
| Receptáculo | **Desde el inventario** | Medallas, piezas de ajedrez, Valve Handle, fusibles |
| Transformador | Desde el inventario | Rollos de película, Fuse Case |
| Punto de guardado | Confirmada | Máquina de escribir + Ink Ribbon |
| Consumible personal | Desde el inventario | Curativos, munición |
| Recurso del entorno | Sí/No | Plantas de hierba azul |

## Casos especiales

- **Pareja (Joint Plugs):** usar una coloca ambas si llevas las dos.
- **Precondición (Red Card):** sin energía no se gasta y un mensaje lo explica.
- **Prioridad (Small Key vs Lockpick):** se gasta antes el consumible.
- **Doble entrada (Master Key en el panel):** acepta interactuar con pregunta o usar desde el inventario.
- **Herramientas reutilizables:** la Valve Handle y el Lighter se eligen desde el inventario pero no se consumen.
- **La cantidad de una llave = número de cerraduras que abre** (Club Key 3, Diamond Key 2).
- **El nombre no define el arquetipo:** las Up/Down Key van en un panel (receptáculo).
- **El motor pregunta "Discard?"** (texto en el ejecutable), probablemente con las llaves agotadas.
