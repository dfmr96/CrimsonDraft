# Restos de depuración en la versión de PC

> Búsqueda en `LeonU.exe` (GOG). **No hay modo de depuración accesible**: se eliminó antes de publicar.

| Resto | Detalle |
|---|---|
| **Textos** | Ninguna cadena de menú de depuración, ni en ASCII ni en las dos codificaciones del juego (mensajes y menús = ASCII − `0x24`) |
| **Logs de Marni** | Mensajes de error de la librería gráfica del port de PC (`Direct3D::ChangeMode`, `TMDObject::In`…). Sirven para poner nombre a funciones del motor |
| **Opcode `break_point` (`0x1C`)** | Existe en la tabla de opcodes, pero su manejador es `nop` (`0x4E7AF0`). Igual `0x1E`, `0x1F`, `0x20` y `0x63`. Probablemente detenían el script en desarrollo |
| **Trucos** | Solo uno confirmado: [munición infinita](../items/municion-infinita.md) (`0x5092E0`) |
| **"Discard?"** | El motor puede preguntar si descartar un objeto (seguramente llaves agotadas). No se vio cuándo aparece |

## Falsos positivos descartados

- `Cprint` y `Keyboard` parecían depuración, pero son código del personaje.
- La búsqueda automática de "código muerto" (funciones sin llamadas) no es fiable: marcaba como muertas funciones como `Game_loop`, que se llaman por punteros.

Para ver zonas de cámara y AOT en PC hay que reconstruirlas por fuera → [herramientas](../herramientas/herramientas.md).
