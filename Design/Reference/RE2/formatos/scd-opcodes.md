# Scripts SCD y opcodes

> Los scripts de sala (bloques 16 y 17 del RDT) son bytecode. Tamaños y firmas de los opcodes:
> `Bio2ConstantTable.cs` de IntelOrca/biohazard-utils.

## Tabla de manejadores en el ejecutable

- **PC (`LeonU.exe`)**: **143 punteros en `0x544DE8`**, uno por opcode (verificado).
- Cada manejador recibe el contexto del script; `[ctx+0x1C]` es el puntero de instrucción.

## Codificación de parámetros (firmas)

- Minúscula o dígito = 1 byte. Mayúscula, `~` o `@` = 2 bytes (`I` con signo).
- `s` = tipo SCE, `a` = flags SAT, `t`/`T` = ítem o llave, `p` = procedimiento, `3` = mensaje, `0` = id de AOT, `v` = variable.

## Opcodes relevantes

| Op | Nombre | Uso | Verificado |
|---|---|---|---|
| `0x1C` | `break_point` | **No hace nada** en PC (mismo manejador que `nop`) | Sí |
| `0x18` | `gosub` | Llamar a un procedimiento | |
| `0x21` | `ck` | Comprobar un flag (grupo, bit, valor) → [hitos](../jugador/hitos.md) | Sí (`0x4E8320`) |
| `0x22` | `set` | Cambiar un flag (0 borrar, 1 activar, 7 alternar) | Sí (`0x4E8370`) |
| `0x23` | `cmp` | Comparar variable (`V_USED_ITEM` = variable 2) | Sí |
| `0x29` | `cut_chg` | Cambiar de cámara y **bloquear el cambio automático** | Sí (`0x4E86C0`) |
| `0x2A` | `cut_old` | Volver a la cámara anterior | Sí (`0x4E8720`) |
| `0x2B` | `message_on` | Mostrar un mensaje | |
| `0x2C` | `aot_set` | Declarar AOT rectangular | |
| `0x3B` | `door_aot_se` | Declarar puerta | Sí (`0x4E8990`) |
| `0x3C` | `cut_auto` | Activar o desactivar el cambio automático | Sí (`0x4E8790`) |
| `0x46` | `aot_reset` | Reescribir una AOT existente | |
| `0x47` | `aot_on` | **Disparar** una AOT desde el script | Sí (por uso) |
| `0x4B` | `cut_replace` | Intercambiar dos cámaras en todas las zonas | Sí (`0x4E87D0`) |
| `0x4E` | `item_aot_set` | Declarar pickup | |
| `0x5E` | `keep_item_ck` | ¿Lleva el jugador el ítem? | |
| `0x61` | `cut_be_set` | Activar o desactivar una zona de cámara | Sí (`0x4E8860`) |
| `0x62` | `sce_item_lost` | Quitar un ítem | |
| `0x67`–`0x69` | `*_4p` | Variantes de AOT con cuadrilátero | |
| `0x83` | `heal` | Vida al máximo y quitar veneno | Sí (`0x4EC78B`) |
| `0x88` | `sce_item_ck_lost` | Quitar N unidades si las lleva | |

Opcodes sin implementación (mismo manejador que `nop`, `0x4E7AF0`): `0x00`, `0x1C`, `0x1E`, `0x1F`, `0x20`, `0x63`.

## Firmas de los opcodes de AOT

| Opcode | Firma | Parámetros |
|---|---|---|
| `aot_set` | `0sauuIIIIuuuuuu` | id, SCE, SAT, planta, super, x, z, ancho, fondo, datos ×6 |
| `door_aot_se` | `0sauuIIIIIIIIuuuuuuuutu` | id, SCE, SAT, planta, super, x, z, ancho, fondo, llegada x/y/z, dirección, **NextStage, NextRoom, NextCamera, NextFloor**, textura, animación, sonido, cerradura, llave, libre |
| `item_aot_set` | `0sauuIIUUTUU1n` | id, SCE, SAT, planta, super, x, z, ancho, fondo, ítem, cantidad, flag, modelo, flags |

Datos de `aot_set` según el tipo:
- `MESSAGE`: dato 0 = número de mensaje.
- `EVENT`: datos 2–3 = `0x18` (gosub) + procedimiento.
- `FLAG_CHG` con dato 0 = 10 (`FG_USE`): dato 2 = **ítem que se puede usar en esa zona**.

## Variables destacadas

| Índice | Nombre | Significado |
|---|---|---|
| 2 | `V_USED_ITEM` | Ítem usado desde el inventario (receptáculos) |
| 24 / 25 / 26 | `V_STAGE` / `V_ROOM` / `V_CUT` | Etapa, sala, cámara |
