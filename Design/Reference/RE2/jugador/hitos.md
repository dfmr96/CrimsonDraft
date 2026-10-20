# Hitos y progreso guardado (flags)

> Verificado en `LeonU.exe` (código), en partidas guardadas reales de la versión de GOG y en los símbolos de la Beta 2 (`MAIN.SYM`, nombres de los campos).
> Los nombres de los grupos salen de la Beta 2. En PC el orden y las direcciones coinciden (verificado), pero que cada grupo signifique lo mismo es deducido.

## Idea general

RE2 no tiene "misiones" ni lista de hitos: **todo el progreso es un conjunto de bits**. Los scripts de sala comprueban un bit con `ck` y lo cambian con `set`. Un hito ("se abrió la puerta del ala este", "ya se vio la escena de Ada") es un bit que algún script activa y otro comprueba.

**Guardar es copiar un bloque fijo de memoria al disco**, sin serializar nada. Todos los bits que pertenecen a ese bloque se guardan; los que viven fuera se pierden al cargar.

## Opcodes `ck` / `set`

- `ck grupo, bit, valor` (`0x21`, manejador `0x4E8320`): verdadero si el bit vale `valor`.
- `set grupo, bit, op` (`0x22`, manejador `0x4E8370`): `op` 0 = borrar, 1 = activar, 7 = alternar.
- Cada grupo es un array de palabras de 32 bits. El bit `n` está en la palabra `n >> 5`, y **se numera desde el bit alto** (`0x80000000 >> (n & 31)`).
- El grupo se busca en una tabla de punteros: **`0x5451F0`** en PC (40 grupos) y `Flg_addr` (`0x8009AAD0`) en la Beta 2 (35 grupos).

## Grupos de flags

"Bloque" = dentro del bloque de guardado (`0x998F9C`, ver abajo); se guarda. "Fuera" = no se guarda.

| Grupo | Nombre (Beta 2) | PC | Bits | ¿Se guarda? | Uso (deducido salvo indicación) |
|---|---|---|---|---|---|
| 0 | `System_flg` | `0x99446C` | 32 | No | Estado del sistema (pausa, modos) |
| 1 | `Status_flg` | `0x9944D0` | 32 | No | Estado de juego de este momento (cámara bloqueada, etc., ver [mapa PC](../direcciones/pc-leonu.md)) |
| 2 | `Stop_flg` | `0x9944D4` | 32 | No | Pausas de sistemas |
| 3 | **`Scenario_flg`** | `0x99912C` | 256 | **Sí** | **Hitos de la historia** del escenario actual |
| 4 | **`Common_flg`** | `0x99914C` | 256 | **Sí** | Hitos compartidos (puertas abiertas, puzzles resueltos…) |
| 5 | `Room_flg` | `0x99916C` | 64 | Sí | Estado temporal de la sala actual: en las partidas guardadas casi siempre está a 0 |
| 6 | `Enemy_flg` | `0x999174` | 256 | Sí | Enemigos eliminados (no reaparecen) |
| 7 | `Enemy_flg2` | `0x999194` | 256 | Sí | Segundo bloque de enemigos |
| 8 | **`Item_flg`** | `0x9991B4` | 256 | **Sí** | **Ítems recogidos** (el pickup no vuelve) |
| 9 | `Map_flg` | `0x999204` | 128+ | Sí | Salas visitadas / mapa |
| 10 | `Use_flg` | `0x9944D8` | 128 | No | Uso de ítems en la sala actual |
| 11 | ? | `0x98F64C` | ? | No | Sin símbolo en la Beta |
| 12 | `Room_enemy_flg` | `0x9944EC` | 16 | No | Enemigos de la sala actual |
| 13–28 | `Pri_be_flg[0..15]` | `0x999220` + 16·i | 128 c/u | Sí | 16 grupos (uno por zona o etapa) |
| 29 | `Zapping_flg` | `0x999324` | 64 | Sí | "Zapping": lo que el escenario A deja hecho para el B |
| 30 | `Rbj_set_flg` | `0x994468` | 32 | No | Objetos de la sala |
| 31 | **`Key_flg`** | `0x99932C` | 64 | **Sí** | **Cerraduras ya abiertas** |
| 32 | `Map_c_flg` | `0x999218` | — | Sí | Mapa (en PC desplazado 4 bytes respecto a la Beta) |
| 33 | `Map_i_flg` | `0x99921C` | 32 | Sí | Mapa |
| 34 | `Item_flg2` | `0x9991D4` | 128 | Sí | Segundo bloque de ítems |
| 35–39 | Solo PC | `0x999200`, `0x999480`, `0x9994A0`, `0x9994B0`, `0x9994D0` | — | Sí | Añadidos de PC, dentro del bloque ampliado |

## Qué hay en `Scenario_flg` (escenario de Leon, PC)

> Verificado: todos los `ck`/`set` del grupo 3 en los 250 RDT de `Pl0/Rdu`, desensamblados con `re2scd.py`. La interpretación de cada bit sale del script que lo rodea (deducido).

Se usa poco: **28 bits y 100 referencias**, frente a **895 de `Common_flg`**, que es donde va el grueso del progreso. `Scenario_flg` guarda **eventos de un solo uso del escenario actual**, casi siempre activados y comprobados en la misma sala o en una vecina:

| Bits | Sala | Qué marca |
|---|---|---|
| 33 | `2160` (aparcamiento) | Evento de entrada ya visto (`proc_07`); evita que se repita y que vuelva a aparecer su enemigo |
| 37 | `2080` (cuarto oscuro) / `4100` | Secuencia de "an old gun here" ya hecha |
| 64–68 | `3010` (celdas) | Muerte de Ben por fases: qué enemigos y objetos se colocan, y el diálogo ("He's not talking") una sola vez |
| 65–67, 98 | `2190` | Cadena con `3010`: 66 y 67 desbloquean el evento de la sala vecina |
| 101–104 | `3030` | Las cuatro piezas de un panel (efectos visuales); **se borran al entrar** |
| 128–131 | `3030` / `3050` / `30B0` (alcantarillas) | Encuentro en fases: qué enemigos y qué munición aparecen |
| 36, 160–162, 106, 193 | `60C0`, `6100`, `6110`, `6020`, `6070` (laboratorio) | Persianas, luces, cuadro eléctrico y ascensor ya activados |
| 192 | `5000` (teleférico) | Evento del teleférico ya visto |

Dos usos distintos dentro del mismo grupo:
- **Hitos de verdad:** se activan una vez y no se borran, como la escena de Ben o el teleférico.
- **Variables de sala:** se borran con `set … 0` al principio del script de la sala (101–104, 97, 99, 106, 107, 130) y solo viven mientras estás ahí. Funcionan como `Room_flg`, pero en otro grupo.

Los bits se agrupan de 32 en 32 por zona: 32–63 comisaría, 64–159 sótano y alcantarillas, 160–191 laboratorio, 192+ teleférico y ascensores. Que esa agrupación sea deliberada es deducido.

## Bloque de guardado

- **Beta 2**: estructura `Save_data_head` … `Zero_clr_end` dentro de `G`, **`0x4EC` bytes**.
- **PC**: el mismo bloque ampliado a **`0x598` bytes** en **`0x998F9C`** (verificado: `memcpy(0x9A6EC0, 0x998F9C, 0x598)` en `0x4AF2EC`). Ya conocíamos tres campos: contador de guardados (`+0x20` → `0x998FBC`), armas infinitas (`+0x28` → `0x998FC4`) y etapa / sala / cámara (`+0x178` → `0x999114`).

Contenido (offsets de la Beta, iguales en PC hasta `+0x268`):

| Offset | Campo | Qué es |
|---|---|---|
| `+0x000` / `+0x004` | `Game_play_time` / `Front_play_time` | Tiempo de juego |
| `+0x00A` | `Pl_id` | Personaje |
| `+0x018` / `+0x019` | `Equip_id` / `Equip_no` | Arma equipada |
| `+0x01A` | `Pl_life` | Vida |
| `+0x01C` / `+0x01E` / `+0x020` | `Zonbi_cnt` / `Cure_cnt` / `Save_cnt` | Contadores para el [rango](rango-y-desbloqueos.md) |
| `+0x022` | `Pl_pos_x/y/z` | Posición del jugador |
| `+0x028` | `Weapon_mugen` | Armas infinitas |
| `+0x02C` | `Bgm_tbl[142]` | Estado de la música por sala |
| `+0x150` | `Sce_work[8]`, `DSce_work[4]` | **Variables del script** (las que lee `cmp`) |
| `+0x178` | `Stage_no`, `Room_no`, `Cut_no` | Dónde está el jugador |
| `+0x190` → `+0x390` | Grupos de flags 3–9, 13–34 | Ver tabla de arriba |
| `+0x398` | `Item_work[11]` | Inventario |
| `+0x3C4` | `Item_boxwork[64]` | Baúl |
| `+0x4C4` | `File_tag_bk[24]` | Archivos (documentos) leídos |

## Archivo en disco (PC)

- Imagen en memoria: **`0x998D9C`** = cabecera de `0x200` (la de una tarjeta de memoria de PS1: título e icono; estructura de `0x200` bytes en la Beta) + el bloque de `0x598`.
- **Guardar** (`0x4C6264`): `WriteFile` de **`0x800` bytes** desde `0x998D9C` (`0x510300`: `CreateFileA` con escritura, crear siempre). Extensión `.RESIDENT2`, en la carpeta `Saves`.
- **Cargar** (`0x4C5DAC`): copia `0x798` bytes de vuelta a `0x998D9C`. El estado entero vuelve tal cual.
- Comprobado leyendo las partidas reales de Claire A (de `_01` a `_10`):
  - `Save_cnt` va de 1 a 10, igual que el número del nombre.
  - Los bits activos de `Scenario_flg`, `Common_flg`, `Item_flg`, `Map_flg` y `Key_flg` solo crecen. Por ejemplo, `Item_flg` pasa de 9 a 104 y `Scenario_flg` de 0 a 27.
  - `Room_flg` vale 0 en casi todas.

## Lectura de diseño

- **Un hito = un bit con nombre.** El script de la sala lo comprueba para decidir qué hay (puerta abierta, enemigo presente, escena ya vista). No hay un sistema de "estado de la sala" aparte.
- **Los grupos separan los hitos por vida útil:** los de historia (`Scenario`), los compartidos (`Common`), los de una sola sala que se pierden al salir (`Room`), y los de este frame (`Status`, `Use`), que no se guardan.
- **Los ítems recogidos, los enemigos muertos y las cerraduras abiertas son también bits**, no listas de IDs: cada pickup, enemigo y cerradura tiene su número de bit fijo en el script.
- **Guardar es trivial:** un bloque fijo de 1,4 KB. A cambio, los números de bit son globales y hay que asignarlos a mano.
