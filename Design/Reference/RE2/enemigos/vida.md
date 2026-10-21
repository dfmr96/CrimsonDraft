# Vida de los enemigos

> Verificado en `LeonU.exe`. La vida está en `+0x156` (→ [estructura](estructura-entidad.md)); se asigna en el estado 0 ("aparecer") de cada enemigo.

## Zombi (`0x449590`)

- La vida se elige **al azar** de una tabla de 16 valores (`0x52EFB8`):
  `80, 94, 128, 75, 60, 95, 58, 75, 50, 83, 79, 66, 80, 65, 82, 65` (entre 50 y 128).
- Índice: **`(rnd >> (rnd & 3)) & 15`**. El desplazamiento previo **sesga la distribución hacia las primeras entradas**: no es uniforme.
- Con el flag `0x20` del estado de juego (probablemente el modo fácil) usa otra tabla (`0x52EFD8`):
  `54, 28, 54, 54, 70, 40, 54, 100, 40, 54, 70, 54, 54, 70, 54, 40`.
- Sin ese flag, si una variable de progreso (`0x9944EB`) es menor que 4, el zombi recibe **+15**.
- **Patrón impar** (zombis que se arrastran): empiezan con **la mitad de vida**.
- Un tipo de aparición concreto recibe `FFFF`: inmortal (probablemente decorado).
- En el modo del flag `0x6942A0` (el mismo que bloquea los desbloqueos; probablemente Extreme Battle): vida ×5, o ×3 según una variable de progreso. Otros enemigos usan ×1,5 o ×2 en ese modo.

## Otros enemigos

Tienen sus propias tablas de vida aleatoria, por ejemplo `0x52FE18`, `0x530538` y `0x532270`. A veces la reducen según flags de la historia. No se catalogaron una por una.

## Lectura de diseño

- Variabilidad sin aleatoriedad uniforme: casi siempre salen los valores "de diseño" (primeras entradas) y de vez en cuando uno raro.
- Con la pistola (16/15/14 de daño) un zombi aguanta entre 4 disparos (50 HP) y 10 (128 + 15 HP) → [daño](danio.md).
