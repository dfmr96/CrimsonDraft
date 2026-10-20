# Ejecutables: PC y PlayStation

## PC: `LeonU.exe` (GOG)

- PE de 32 bits, **base `0x400000`**, sin empaquetar.
- Hay dos ejecutables: `LeonU.exe` y `ClaireU.exe`. Solo se analizó el de Leon.
- Suele ejecutarse con **permisos de administrador** (lanzado desde GOG). Para leer su memoria, la herramienta también tiene que ir como administrador (error 5 si no).

| Sección | VA | Offset en el archivo |
|---|---|---|
| `.text` | `0x401000` | `0x600` |
| `.rdata` | `0x51E000` | `0x11C800` |
| `.data` | `0x522000` | `0x120400` |

Conversión en `.data`: `offset = VA − 0x400000 − 0x122000 + 0x120400`.

## PlayStation: PS-X EXE

| Offset | Contenido |
|---|---|
| `0x00` | `"PS-X EXE"` |
| `0x18` | Dirección de carga (`0x80010000` en RE2) |
| `0x1C` | Tamaño del código |
| `0x800` | Inicio del código |

Conversión: `offset = dirección − 0x80010000 + 0x800`.

| Versión | Ejecutable | Tamaño |
|---|---|---|
| Beta 2 | `PSX.EXE` (+ `MAIN.CPE`, `MAIN.SYM`) | 987 136 bytes |
| Comercial Dual Shock (USA) | `SLUS_007.48` | 987 136 bytes |
| Prototipo nov. 1996 | `PSX.EXE` | 718 848 bytes |

## Imágenes de disco

- `.bin`/`.cue` y la `.iso` de la Beta 2 usan **sectores raw de 2352 bytes** (aunque la extensión sea `.iso`).
- Datos de usuario: byte 16 del sector (modo 1) o 24 (modo 2).
- Al parchear raw, el **EDC/ECC** del sector deja de cuadrar. DuckStation no lo comprueba; una consola real sí podría.
