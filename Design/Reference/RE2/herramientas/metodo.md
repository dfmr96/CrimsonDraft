# Método de trabajo

## Principios

1. **Solo lectura** sobre el juego original, el emulador y la ISO original. Los parches van a copias.
2. Separar **verificado** de **deducido** en cada afirmación.
3. **Validar con el juego en ejecución** (capturas, VRAM, RAM por GDB) antes de dar algo por cierto: varias hipótesis razonables resultaron falsas (ver abajo).

## PC (`LeonU.exe`)

- Barrido lineal de `.text` con **Capstone** (~360 000 instrucciones) e **índice de referencias cruzadas** a direcciones de datos (`re2dis.py`).
- Punto de partida: una **tabla conocida** (por ejemplo, la de atributos de ítems, encontrada por la firma de capacidades de cargador). Después: listar el código que la lee o escribe y seguir las variables globales.
- Búsqueda de **inmediatos** (constantes como `0xF00140`, `400`, `0x3FF`) para encontrar funciones.
- Formatos de sala: [biohazard-utils de IntelOrca](https://github.com/IntelOrca/biohazard-utils) como base, verificados contra el ejecutable.
- Validación estadística: proyectar las 552 puertas con la cámara deducida (97,6–97,7% de aciertos).

## PlayStation (Beta 2)

- Extraer el disco y leer **`MAIN.SYM`**: con nombres de funciones, el desensamblado MIPS es casi legible.
- Capstone MIPS con **`skipdata`** (si no, se para en los datos mezclados con el código).
- **Comparar versiones**: buscar cada función de la Beta en otra versión con los inmediatos y saltos enmascarados (`psx_funcmatch.py`).
- Texturas: convertir TIM a PNG con todas sus paletas y compararlas con capturas del juego.

## Hipótesis que resultaron falsas (lecciones)

| Hipótesis | Realidad | Cómo se descubrió |
|---|---|---|
| La tabla 13 de mensajes de PC era inglés corrupto | Era japonés; el inglés está en la 14 | Decodificar con la tabla de kana |
| Las zonas de cámara tienen histéresis | Se solapan; no hay histéresis | Leer el bucle |
| `F9` tiñe con la GPU / usa paletas de `ST_MOJI` | Usa `TEX.TIM`, fila 480 + 2p | Comparar con captura |
| `ST_MOJI` está en la fila 480 (cabecera) | Está en la 488 | Visor de VRAM |
| `Padd2_menu` = Start en el mando 2 | El menú es Select en el mando 1 | Probándolo en el juego (lo descubrió el usuario) |
| `Cprint` / `Keyboard` eran depuración | Código del personaje | Leer las funciones |
| Funciones sin llamadas = código muerto | Se llaman por punteros (`Game_loop`) | Contraejemplo |
| El parche no funciona | Estaba cargada la ISO original | Leer RAM por GDB |
| Basta con parchear `ST_MOJI` | `ST0`/`ST1` llevan copias | El menú seguía en japonés |
| Los colores de los números son paleta = color | Paleta = color + 2 | VRAM + corrección del usuario |
