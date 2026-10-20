# Beta 2: contenido del disco y símbolos de depuración

> Verificado extrayendo la ISO (`psx_extract.py`, ISO9660 en sectores raw de 2352 bytes) y leyendo los símbolos con `psyq_sym.py`.

## Archivos de desarrollo que vienen en el disco

| Archivo | Contenido |
|---|---|
| `PSX.EXE` | Ejecutable PS-X EXE, cargado en `0x80010000` |
| **`MAIN.SYM`** | Símbolos de **Psy-Q** (formato MND v1): nombres de funciones y variables |
| `MAIN.CPE` | Ejecutable en formato CPE del kit de desarrollo (todo por debajo de 2 MB) |
| `FILECODE.H` / `FILE_LOC.H` | Cabeceras C con el código y la posición de cada archivo del disco (generadas desde `BIO_MAP.TXT`, ©1994) |

## `MAIN.SYM`

- **4135 funciones** con nombre.
- **105 archivos fuente**, con rutas del tipo `D:\Bio2\prog\...`.
- Registros `0x98` / `0x9A`: definiciones de **overlays**.
- Nombres útiles: `mess_trans`, `Member_set`, `St_disp_num`, `print_num`, `St_disp_menu0/1`, `Cut_chg_main`, `Padd2_menu`, `Debug_menu`, `GetClut`, `AddPrim`…

## Overlays de herramientas (`T_*`)

Los símbolos declaran overlays de herramientas internas en `0x80200004` y en `0x807xxxxx`. Esas direcciones **solo existen en el kit de desarrollo de 8 MB** (la consola comercial tiene 2 MB). **Su código no está en el disco**: solo quedan los nombres.

| Símbolo | Probable función (por el nombre) |
|---|---|
| `Disp_atari` | Dibujar colisiones (*atari* = colisión) |
| `Sc_hit` | Colisión del escenario |
| `Rview` | Visor de salas |
| `Tool_sce_at_set` | Editor de AOT |
| `Tool_em_set` | Colocar enemigos |
| `Enemy_move_tool` | Mover enemigos |
| `Enemy_st_view` | Ver el estado de los enemigos |

Las opciones del [menú de depuración](menu-debug.md) que llaman a estas herramientas no hacen nada en la Beta.

## Texturas extraídas

`COMMON/DATA`: 28 TIM (fuentes por etapa, `ST_MOJI`, `ST0`/`ST1`, `TEX`, selección, título…) → [TIM](../formatos/tim.md).
