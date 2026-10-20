# Beta 2: menú de depuración

> Descubierto en el juego por el usuario (Select del mando 1) y analizado con el código y `MAIN.SYM`.

## Cómo se abre

**Select en el mando 1** durante el juego. (Primero se pensó que era Start del mando 2 por `Padd2_menu`, pero era falso.)

## Opciones

| Fila | Opción | Estado en la Beta 2 |
|---|---|---|
| 0 | UTILITY MENU | Stub vacío (`0x80013B10`) |
| 1 | **JUMP** | **Funciona** (`0x800136A4`): saltar a cualquier sala |
| 2 | MEMORY VIEWER | Stub vacío |
| 3 | XA PLAY TEST | Stub vacío |
| 4 | OBJ ATARI DISP | Stub vacío (dibujar colisiones de objetos) |
| 5 | SHORI CHECK DISP | Stub vacío (*shori* = procesamiento: medidor de carga de CPU) |
| 6 | MONOSASHI | Stub vacío (*monosashi* = regla: medir distancias) |

Las opciones vacías llamaban a los overlays `T_*` del kit de 8 MB, que no están en el disco → [disco y símbolos](disco-y-simbolos.md).

## Reconstrucción

La regla (MONOSASHI), el visor de memoria y un visor de IA se reconstruyeron por fuera, leyendo la RAM por GDB y dibujando encima del emulador → [herramientas](../herramientas/herramientas.md).

## `Padd2_menu`

- Función de depuración con el mando 2, condicionada por el bit `0x800` de una palabra de sistema.
- **Los códigos que se probaron no hicieron nada.** No se encontró cómo activarla en la Beta.

→ Comparación con otras versiones en [versiones](versiones.md).
