# Herramientas construidas

> **Código:** [`../tools/`](../tools/): `scripts/` (análisis y parches) y `re2tool/` (visor y overlay), con la misma estructura con la que se escribieron (los imports relativos dependen de ella).
> **No se incluyen datos del juego**: ni `PSX.EXE`, `MAIN.SYM`, RDT, TIM ni volcados de RAM, ni el `data.json` del visor (se genera con `build.py` desde tu instalación de RE2). Los scripts los leen de tu copia del juego o de un disco extraído con `psx_extract.py`.
> Muchos scripts de `scripts/` son pruebas de un solo uso; los principales son los de las tablas de abajo.
> Todas son **de solo lectura** sobre el juego y el emulador. Las que parchean la ISO solo escriben en copias con "(EN" en el nombre.
> Python ejecutado con `-I` (modo aislado). Dependencias en un venv: `capstone` 5.0.3 y `pillow` 11.1.0.

## Visor de salas y overlay (`re2tool/`)

| Archivo | Uso |
|---|---|
| `re2data.py` | Lee de los RDT de PC: cámaras (RID), zonas (RVD), AOT del SCD y mensajes en inglés |
| `build.py` | `python -I build.py "<carpeta de RE2>"` → genera `data.json` (escenarios de Leon y Claire) |
| `viewer.html` | Visor en el navegador: planta de cada sala con cámaras, conos de FOV, zonas y AOT |
| `live.py` | `python -I live.py "<carpeta de RE2>"` → sirve el visor en `http://127.0.0.1:8765` y, si `LeonU.exe` está abierto, muestra la posición, la sala y la cámara en vivo |
| `overlay.py` | Overlay transparente sobre la ventana del juego, con zonas, área de cámara y AOT proyectadas con la cámara del juego |
| `overlay.py --beta --mem` | Igual sobre DuckStation (Beta 2) por GDB, con regla, visor de IA y visor de memoria |
| `memviewer.py` | Visor de memoria de la Beta 2 con nombres de `MAIN.SYM` y entidades decodificadas campo a campo |
| `gdbmem.py` | Cliente GDB mínimo (solo paquete `m`, lectura) |

### Teclas del overlay (globales)

| Tecla | Acción |
|---|---|
| F5 / F6 | Espejo en X / Y (ajuste de convención) |
| F7 / F8 | Mostrar u ocultar AOT / zonas |
| F9 | Ocultar el overlay |
| F10 | Ajuste 4:3 o estirado |
| F11 | Salir |
| Num1 / Num2 / Num0 | Regla: punto A / punto B / borrar (MONOSASHI) |
| Num5 | Ocultar el visor de IA |
| Num7 | Cambiar convención de dirección de los conos (`sin/cos`, `sin/-cos`, `-sin/cos`, `cos/sin`) |

### Problemas conocidos

- **Error 5 al abrir el proceso (PC):** el juego de GOG se ejecuta como administrador. Hay que lanzar `live.py` / `overlay.py` también como administrador.
- **Foco:** se probó un arreglo para que el overlay no robara el foco (tecla Acción), pero se revirtió a la versión anterior, que era la que funcionaba.
- La convención correcta de dirección de los conos no está confirmada.
- Las zonas de cámara se solapan; el overlay no imita el antiparpadeo (no se estudió).

## Análisis (`scripts/`)

| Script | Para qué |
|---|---|
| `re2dis.py` | Desensamblador x86 de `LeonU.exe` (Capstone) con índice de referencias cruzadas: `build`, `xref`, `at`, `imm` |
| `re2scd.py` | Desensamblador de scripts SCD de sala |
| `re2items.py`, `re2pickups.py` | Atributos, combinaciones y pickups por sala |
| `re2dmg_dump.py`, `re2dmg_table.py` | Volcado de la matriz de daño |
| `re2cams.py`, `re2fov.py`, `re2rvd.py`, `re2doorcam.py` | Cámaras, FOV, zonas y verificación de cámara por puerta |
| `tim2png.py` | TIM → PNG (todas las paletas) |
| `psx_extract.py` | Lector ISO9660 (sectores de 2048 o raw de 2352) |
| `psyq_sym.py` | Lector de `MAIN.SYM` (Psy-Q MND v1) |
| `psyq_structs.py` | Estructuras de `MAIN.SYM` con sus campos y offsets (así salieron los nombres de los grupos de flags y del bloque de guardado) |
| `mipsdis.py` | Desensamblador MIPS (Capstone con `skipdata`) con símbolos |
| `psx_funcmatch.py` | Busca funciones de la Beta en otras versiones con inmediatos enmascarados |
| `mipsdis2.py` | Desensambla una función de la Beta por nombre, con nombres de `MAIN.SYM` en las llamadas |
| `beta_callers.py` | Quién llama a una función de la Beta (con la función que contiene la llamada) |
| `beta_table.py` | Imprime una tabla de punteros a función con nombres |
| `ramsnap.py` | Foto de los 2 MB de RAM por GDB (solo lectura). Comparar fotos antes y después de una acción localiza variables (así se encontró la escalera empujada) |
| `re2sca.py` | Vuelca la colisión SCA de un RDT con el nombre de cada forma |
| `beta_english.py`, `beta_item_names.py`, `beta_menu_tim.py`, `beta_menu_copies.py`, `fix_st0_final.py` | Parches de traducción → [traducción](../beta2/traduccion.md) |

Datos extraídos de la Beta en `psx/beta2/` (`PSX.EXE`, `MAIN.SYM`, `symbols.txt`, `rdt/`, `tim/`).
