# Beta 2: traducción al inglés (parches)

> Probado en DuckStation. **Solo se parchea una copia**: `Biohazard 2 [Beta 2] (EN test).iso`. Los scripts se niegan a escribir en archivos sin "(EN" en el nombre. La ISO original no se toca.

## 1. Mensajes de las salas (`beta_english.py`)

- Muchos RDT de la Beta **ya traen una tabla de mensajes en inglés sin usar** (tabla 14) junto a la japonesa (tabla 13).
- El parche **intercambia los punteros** de las tablas 13 y 14 en la cabecera del RDT → [RDT](../formatos/rdt.md).
- **Reajuste de líneas:** la fuente japonesa da a cada letra el ancho de un carácter japonés, así que el texto se salía de pantalla. Se reparten de nuevo las líneas a **18 caracteres visibles**, cambiando solo separadores (espacio `00` ↔ salto `FC`): **el tamaño de cada mensaje no cambia**, no hay que mover nada.
- Filtro `is_english`: se descartaron **14 salas** cuya tabla 14 no era inglés real.
- Resultado: **26 salas** con mensajes en inglés.
- Funciona con sectores raw de 2352 bytes: `disc_write` reparte los datos que cruzan sectores, escribiendo solo en la zona de datos de usuario de cada uno. **No recalcula EDC/ECC**; DuckStation carga la copia igualmente.

## 2. Nombres de ítems (`beta_item_names.py`)

- Se toman los nombres oficiales en inglés de `LeonU.exe` y se escriben en la tabla de la Beta (`0x80093C20`, offsets en `0x800940F6`, 136 entradas) → [atributos](../items/atributos.md).
- Problema: los nombres ingleses ocupaban **1601 bytes**, y el hueco es de **1233**.
- Solución:
  - Nombres repetidos que comparten la misma cadena.
  - **Sufijos compartidos**: un nombre que es final de otro apunta dentro de él (la tabla de offsets lo permite).
  - 15 títulos de documentos acortados ("Police memorandum" → "Police memo", "Sewer manager diary" → "Sewer diary"…).
  - Resultado: **1232 bytes**.

## 3. Menú de comandos (`beta_menu_tim.py`, `beta_menu_copies.py`, `fix_st0_final.py`)

- Se redibujaron las palabras EQUIP / USE / CHECK / COMBINE / PARTNER en las celdas de 48×16 de `ST_MOJI.TIM`, usando la misma rampa de suavizado (índices 6–12, fondo 14) con una fuente del sistema → [menú de comandos](../texto/menu-comandos.md).
- **Seguía en japonés**: `ST0.TIM` y `ST1.TIM` llevan copias de esas filas y se suben después a VRAM.
  - `ST1`: sustitución de filas completas.
  - `ST0`: stride de 128 bytes, solo 4 celdas (96 bytes por fila) y solo los bytes cambiados. Antes se restauró `ST0` desde la original, porque un primer intento pudo escribir en un sitio equivocado (en la fila 68 `ST0` tiene un byte propio distinto).

## Errores durante el proceso

- El primer parche "no funcionaba": DuckStation tenía cargada **la ISO original**. Se confirmó leyendo la RAM por GDB.
- `PermissionError` al escribir: DuckStation tenía abierta la ISO.

## Sin traducir

- Mensajes del sistema en el ejecutable (`0x80094206`+): la pregunta de recoger ítems, examinar, etc.
- Textos de los documentos.
- Kanji de las fuentes por etapa.
