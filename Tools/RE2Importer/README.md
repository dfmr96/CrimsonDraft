# RE2 / RE3 Importer (Blender)

Importa a Blender los personajes jugables de **Resident Evil 2 (PC, 1998)** y **Resident Evil 3: Nemesis (PC, 1999)**, versiones de GOG. Trae malla, textura, esqueleto y todas sus animaciones, para estudiar cómo están construidos.

> Es una herramienta de **referencia**. Los modelos y animaciones son propiedad de Capcom: no se copian al repo ni se usan en CrimsonDraft. El repo solo guarda los scripts; los datos se leen de la instalación del juego (o de una carpeta temporal fuera del repo, en el caso de RE3).

## Archivos

| Archivo | Qué hace |
|---|---|
| `re2pld.py` | Parser puro (sin `bpy`) de los PLD/PLW de RE2: tabla de animaciones (EDD), esqueleto y keyframes (EMR), malla (MD1) y textura (TIM). Se puede usar fuera de Blender. |
| `re3pld.py` | Parser de los PLD/PLW de RE3. Reutiliza `sections`/`parse_tim` de `re2pld` y añade la tabla de animaciones, el keyframe y la malla (MD2) de RE3, más el skinning aproximado (`skin`). |
| `rofs.py` | Lista y extrae archivos de los `Rofs*.dat` de RE3 (descifrado + descompresión). Port a Python de `reevengi-tools/src/rofs.c` © 2010 Patrice Mandin, **GPL v2**: este archivo hereda esa licencia. |
| `re2_blender_import.py` | Construye la escena en Blender: armature, malla con UVs y textura, y una Action por animación. Elige el formato con el parámetro `fmt`. |

## Uso (RE2)

Probado con Blender 4.2 LTS. En la consola de Python de Blender (o en el Text Editor):

```python
SCRIPT_DIR = r'D:\Proyectos Unity\CrimsonDraft\CrimsonDraft\Tools\RE2Importer'
g = {'SCRIPT_DIR': SCRIPT_DIR, '__name__': 're2imp'}
exec(open(SCRIPT_DIR + r'\re2_blender_import.py').read(), g)

# Leon con las animaciones base + las de todas sus armas
arm, mesh, actions = g['build'](plws=g['LEON_WEAPONS'])
```

Parámetros de `build()`:

| Parámetro | Por defecto | Notas |
|---|---|---|
| `pld` | `'PL00.PLD'` | Modelo base. `PL01.PLD` es otra variante de Leon; los `...CH.PLD` también existen. |
| `plws` | `()` | Lista de PLW a cargar: nombres de archivo o tuplas `(archivo, etiqueta)`. |
| `name` | `'Leon'` | Prefijo de los objetos creados (`Leon`, `Leon_Rig`). |
| `game_dir` | `...\Resident Evil 2\Pl0\pld` | Para Claire: `...\Pl1\PLD` (y sus propios `PL10Wxx.PLW`). |
| `fmt` | `re2pld` | Módulo de formato: `re2pld` o `re3pld` (en el snippet: `g['re3pld']`). |
| `root_motion` | `False` | Si es `True`, suma el desplazamiento de cada frame a la raíz: el clip se desplaza en vez de reproducirse en el sitio. Útil para ver hacia dónde se mueve. |

Resultado:

- `Leon_Rig`: armature de 15 huesos (`bone_00`…`bone_14`).
- `Leon`: malla única, hija del rig, con un vertex group por hueso y modificador Armature.
- Actions `PLD_00`…`PLD_09` y `Wxx_<Arma>_NN`, todas con fake user y su propio rango de frames.

Para ver una animación: seleccionar el **armature** (`Leon_Rig`, no la malla) → Dope Sheet → Action Editor → elegir la acción. Ver [Problemas conocidos](#problemas-conocidos-en-blender) antes de asignar acciones.

## Uso (RE3)

En RE3 los archivos no están sueltos: van dentro de `Rofs*.dat`, cifrados y comprimidos. Primero se extraen (fuera del repo) y luego se importan.

**1. Extraer** (Python normal, no hace falta Blender). Jill está en `Rofs5.dat` (`DATA/PLD`):

```bash
python rofs.py "C:/Program Files (x86)/GOG Galaxy/Games/Resident Evil 3/Rofs5.dat" <carpeta_salida> PL00.PLD PL00W00.PLW PL00W02.PLW
```

Sin nombres de archivo al final, extrae todo el `.dat`. La carpeta de salida tiene que existir.

**2. Importar** en Blender:

```python
SCRIPT_DIR = r'D:\Proyectos Unity\CrimsonDraft\CrimsonDraft\Tools\RE2Importer'
g = {'SCRIPT_DIR': SCRIPT_DIR, '__name__': 're2imp'}
exec(open(SCRIPT_DIR + r'\re2_blender_import.py').read(), g)

arm, mesh, actions = g['build'](pld='PL00.PLD', plws=['PL00W00.PLW', 'PL00W02.PLW'], name='Jill',
                                game_dir=r'<carpeta_salida>', fmt=g['re3pld'], root_motion=True)
```

Contenido de los `Rofs*.dat` (carpeta interna):

| Archivo | Carpeta | Contenido |
|---|---|---|
| `Rofs5.dat` | `DATA/PLD` | Personajes jugables: `PL00` Jill (+ `PL00Wxx.PLW`, W00–W14), `PL08`–`PL0A` con sus PLW, otras variantes `PL01`–`PL0F` |
| `Rofs6.dat` | `DATA_A/PLD` | Dos TIM sueltos (`PL000.TIM`, `PL006.TIM`) |
| `Rofs9.dat` / `Rofs10.dat` | `ROOM/EMD`, `ROOM/EMD08` | Enemigos (`EMxx.EMD` + `.TIM`) |
| `Rofs12.dat` / `Rofs13.dat` | `DATA_AJ/RDT`, `DATA_S/RDT` | Salas |
| `Rofs7.dat`, `Rofs14.dat`, `Rofs15.dat` | `SOUND`, `VOICE` | Audio |
| Resto | `DOOR`, `ETC`, `BSS`, `RBJ`… | Puertas, UI, fondos, etc. |

## Contenido del juego (RE2)

Ruta (GOG): `C:\Program Files (x86)\GOG Galaxy\Games\Resident Evil 2\`

- `Pl0\pld\PL00.PLD`: Leon. Malla, textura, esqueleto y animaciones comunes a todas las armas.
- `Pl0\pld\PL00Wxx.PLW`: animaciones por arma. `xx` es el ID del arma en el juego:

| PLW | Arma | PLW | Arma |
|---|---|---|---|
| W00 | Desarmado | W08 | Custom Shotgun |
| W01 | Cuchillo | W0D | Colt SAA |
| W02 | VP70 | W0F | Subfusil |
| W04 | Custom Handgun | W10 | Lanzallamas |
| W05 | Magnum | W11 | Lanzacohetes |
| W06 | Custom Magnum | W12 | Gatling |
| W07 | Escopeta | | |

W03, W09–W0C y W0E existen pero están vacíos (3436 bytes): son armas solo de Claire.

Cada PLW repite 9 slots de locomoción (`_00`–`_08`: caminar, correr, idle, daño…) seguidos de las animaciones propias del arma (apuntar, disparar, recargar). Las de 1 frame son poses estáticas de apuntado (arriba/frente/abajo).

Animaciones identificadas hasta ahora:

| Action | Contenido |
|---|---|
| `PLD_00` | Caminar |
| `PLD_02` | Muerte (se arrodilla y cae) |
| `W07_Escopeta_10` | Disparo con bombeo |

## Formato (RE2)

Todo en little-endian. Base de conocimiento: documentación de la comunidad (proyecto *reevengi*, wikis de modding de RE clásico), verificada contra los datos.

### Contenedor PLD / PLW

```
u32 dir_offset, u32 count     ; en offset 0
u32 section_offset[count]      ; en dir_offset
```

Secciones (4 en ambos tipos): `0` EDD, `1` EMR, `2` MD1 (malla), `3` TIM (textura). En los PLW, la malla y la textura son las del arma.

### EDD: tabla de animaciones

```
{ u16 frame_count, u16 offset }[n]   ; n = offset_de_la_primera / 4
u32 frame[frame_count]               ; en cada offset
```

Cada `frame`: bits 0–11 = índice de keyframe en el EMR; bits 12+ = flags (sin decodificar). Varias animaciones pueden apuntar al mismo keyframe.

### EMR: esqueleto + keyframes

```
u16 armature_offset, u16 keyframe_offset, u16 bone_count, u16 keyframe_size
s16 relpos[bone_count][3]                      ; en +8: posición del hueso relativa al padre
{ u16 child_count, u16 child_offset }[bone_count]  ; en armature_offset
u8  child_index[...]                           ; relativo a armature_offset
```

Keyframe (`keyframe_size` = 80 bytes con 15 huesos):

```
s16 pos[3]       ; posición de la raíz
s16 speed[3]     ; desplazamiento en el mundo de este frame (root motion)
u8  angles[...]  ; bone_count * 3 ángulos de 12 bits, dos ángulos cada 3 bytes
```

Ángulo de 12 bits: `0..4095` = `0..360°`. Desempaquetado:
`a0 = b0 | (b1 & 0xF) << 8`, `a1 = (b1 >> 4) | b2 << 4`.

Orden de rotación: X·Y·Z (en Blender, modo Euler `ZYX`). Comprobado visualmente: con otro orden, las poses compuestas salen retorcidas.

Jerarquía de Leon: `0` torso (raíz) → `1` pelvis → piernas `2-3-4` / `5-6-7`; `8` cabeza; brazos `9-10-11` / `12-13-14`.

### MD1: malla

```
u32 length, u32 unknown, u32 object_count     ; object_count = huesos * 2
{ u32 vtx_ofs, vtx_count, nrm_ofs, nrm_count, face_ofs, face_count, uv_ofs }[object_count]
```

Offsets relativos a `+12`. Por cada hueso hay dos objetos (triángulos, quads) que comparten la misma lista de vértices.

- Vértice / normal: `s16 x, y, z, pad`, en espacio local del hueso.
- Triángulo: `u16 n0, v0, n1, v1, n2, v2`. UV: `u8 u0, v0; u16 clut; u8 u1, v1; u16 page; u8 u2, v2; u16 pad`.
- Quad: igual, con 4 vértices. Orden PSX en "Z": en Blender se reordena a `0, 1, 3, 2`.
- `page & 0xF` = página de textura de 128 px de ancho; `clut` elige la fila de paleta (en Leon, página *n* ↔ fila *n*).

### TIM: textura

Formato estándar de PlayStation. Leon: 8 bpp, 384×256, 3 paletas de 256 colores (RGB 5:5:5). El color `0x0000` es transparente.

## Formato (RE3)

Misma familia que RE2: contenedor PLD/PLW, ángulos de 12 bits empaquetados igual, mismo orden de rotación y TIM. Cambian el archivo contenedor, la tabla de animaciones, el keyframe y la malla.

### Rofs*.dat

```
0x00   u32 3, u32 1, u32 4, u8[4]
0x15   char dir_level1[] (ej. "DATA")
       u32 ofs, u32 len, char dir_level2[] (ej. "PLD")
0x1000 u32 file_count
       { u32 offset/8, u32 size, char name[] }[file_count]
```

Cada archivo empieza con una cabecera de cifrado:

```
u16 header_size, u16 block_count, u32 decompressed_size
u8  ident[8]                 ; "Hi_Comp\0" XOR ident[7] -> comprimido
u32 key[block_count]
u32 block_length[block_count]
```

Cada bloque se descifra con un flujo XOR pseudoaleatorio. La semilla es `key`, con LCG `key = key * 0x5d588b65 + 0x8000000b`, byte alto. Las rachas tienen longitud variable según una tabla de 64 entradas. Después se descomprime con un LZSS de ventana de 4 KB ("Hi_Comp"). Detalles en `rofs.py`.

### PLD / PLW

PLD con 5 secciones: `0` EDD, `1` EMR (con esqueleto), `2` MD2 (malla), `3` desconocida (probablemente la tabla de skinning), `4` TIM.

PLW con 9 secciones:

| Sección | Contenido |
|---|---|
| `0`–`1` | EDD/EMR de cuerpo entero (solo keyframes; el esqueleto viene del PLD) |
| `2`–`5` | Capas parciales: EDD/EMR de 7 huesos (tren superior: apuntar/disparar) |
| `6`–`8` | Arma: EMR, MD2 y TIM |

### EDD (RE3)

```
{ u16 frame_count, u16 offset, u32 first_keyframe }[n]   ; n = offset_de_la_primera / 8
u16 frame[frame_count]
```

Cada `frame`: byte bajo = keyframe relativo a `first_keyframe`; bits altos = flags (sin decodificar).

### EMR (RE3)

Cabecera igual que en RE2. En los PLW, `keyframe_offset == 8` indica que no hay esqueleto, solo keyframes.

Keyframe (76 bytes con 15 huesos):

```
s16 offset[3]    ; desplazamiento acumulado de la raíz (root motion)
s16 root_y       ; altura de la raíz
u8  angles[...]  ; 15 * 3 ángulos de 12 bits, empaquetado idéntico a RE2
```

Comparado con RE2: desaparecen `pos.x`/`pos.z` (siempre 0) y el keyframe ocupa 4 bytes menos.

Jerarquía de Jill: `0` torso (raíz) → `1` cabeza; brazos `2-3-4` / `5-6-7`; `8` pelvis → piernas `9-10-11` / `12-13-14`.

### MD2: malla

```
u32 length, u32 object_count     ; 21 en Jill: 15 huesos + 6 objetos extra (sin usar)
{ u32 vtx_ofs, nrm_ofs, vtx_count, tri_ofs, quad_ofs; u16 tri_count, quad_count }[object_count]
```

Offsets relativos a `+8`. Vértices y normales: `s16 x, y, z, pad`.

- Triángulo (12 bytes): `u0 v0, u16 clut, u1 v1, u8 page, u8 i0, u2 v2, u8 i1, u8 i2`.
- Quad (16 bytes): `u0 v0, u16 clut, u1 v1, u16 page, u2 v2, u8 i0, u8 i1, u3 v3, u8 i2, u8 i3`.

El layout del quad se eligió midiendo la longitud media de las aristas: 150 contra 245–263 de las alternativas, y 126 en los triángulos.

### Skinning: la gran diferencia con RE2

En RE2 cada pieza es local a su hueso. En RE3 no:

- La mayoría de las piezas están en el **espacio de la raíz, en pose de reposo**: torso, cabeza, brazo completo, cadera+muslos, pierna+bota.
- Las manos sí son locales a su hueso.
- Los huesos intermedios (antebrazo, muslo, pie) tienen como malla un **dummy de 3 vértices**. Su geometría vive dentro de la pieza del padre y se deforma con skinning por vértice.

Los vértices no llevan índice de hueso. La asignación real probablemente está en la sección 3 del PLD, sin decodificar. `re3pld.skin()` la aproxima así:

1. Para cada pieza decide si es local o de raíz, según cuál deja sus vértices más cerca de sus huesos.
2. A cada vértice le asigna el hueso más cercano entre el de la pieza y sus descendientes dummy.

Funciona bien en general. En poses con la rodilla muy doblada (arrodillarse, rodar, apuntar agachada), algunos vértices de la rodilla quedan del lado equivocado de la articulación y se hunden 8–21 cm en el suelo. El esqueleto sí está bien: los tobillos quedan apoyados.

## Contenido del juego (RE3)

### Jill (`PL00.PLD`, 22 animaciones)

Las marcadas con *(datos)* se dedujeron solo del desplazamiento, la altura y el giro de la raíz, sin verlas; el resto se vio en Blender.

| Action | Contenido |
|---|---|
| `PLD_00` | Caminar *(datos: equivale a `PLD_00` de RE2)* |
| `PLD_01` | Muerte *(datos: 119 frames, termina en el suelo)* |
| `PLD_02` / `PLD_03` | Recibir daño de frente / por detrás (equivalen a `PLD_04`/`PLD_03` de RE2) |
| `PLD_06` | Giro brusco hacia un lado, agachándose (10 frames) |
| `PLD_08` | Paso de pie a muy bajo, ¿trepar/levantarse? *(datos)* |
| `PLD_10` | Knockback: sale despedida hacia atrás |
| `PLD_14` | Salto agachada hacia atrás |
| `PLD_15` / `PLD_16` | Esquive: paso lateral a un lado / al otro *(datos)* |
| `PLD_17`, `PLD_18` | Reacciones agachada / tropiezo |
| `PLD_19` / `PLD_20` | Esquive con rodada lateral *(datos)* |
| `PLD_21` | Idle herida: mano en la cabeza (84 frames) |

### PLW (`PL00W00`–`W14`, 18 animaciones cada uno)

Todos tienen las mismas 18 ranuras. Las de las armas difieren solo en detalles de los tiempos.

| Ranuras | Contenido |
|---|---|
| `_00`–`_08` | Locomoción (caminar, correr, idle, daño…), igual que RE2. `_00` (caminar) visto |
| `_09`, `_10` | Caminar hacia atrás *(datos)* |
| `_11` | Idle con la mano en la cadera |
| `_12` | Idle *(datos)* |
| `_13`–`_17` | Poses agachadas / de apuntado bajo *(datos)* |

### El quick turn (180°)

**No hay una animación de 180°.** Al medir la rotación de la raíz en las 22 animaciones del PLD y en las 18 de cuerpo entero de cada uno de los 21 PLW, ninguna supera los 116°. Las que se acercan son daño y rodadas. Tampoco hay ningún clip de giro en el sitio: casi sin desplazamiento, a altura de pie y con los pies dando pasos.

El manual original dice que se hace pulsando Correr mientras se camina hacia atrás. Lo más probable es que el código gire la orientación 180° en ~0,3–0,5 s mientras suena un clip de locomoción. Qué clip usa durante el giro lo decide el ejecutable, no los datos. Para confirmarlo habría que buscar en `ResidentEvil3.exe` la rutina que suma 2048 unidades (180°) a la orientación del jugador.

## Problemas conocidos en Blender

- **Asignar una acción a un objeto que no es el rig la deja inservible.** Al evaluar la acción sobre un objeto sin esos huesos (la malla, un objeto de referencia…), Blender marca sus curvas como inválidas (`FCurve.is_valid = False`). El flag se guarda en la acción y, aunque después se ponga en el rig, esas curvas se saltan: el hueso se queda congelado con el valor de otro clip (por ejemplo, el personaje "hundido" en el suelo). Arreglo:

  ```python
  for a in bpy.data.actions:
      for c in a.fcurves: c.is_valid = True
  ```

  Para prevenirlo: seleccionar siempre el armature antes de elegir acción. Los objetos de referencia (suelo, flechas) se marcan como no seleccionables (`hide_select = True`).
- **Rango de la línea de tiempo.** Blender no cambia el rango de la escena al cambiar de acción. Los .blend de estudio traen un módulo de texto, `sync_action_range.py`, que lo ajusta con `bpy.msgbus` cuando se cambia la acción del rig desde el Action Editor. Al abrir el archivo Blender puede pedir permiso para ejecutar scripts (Auto Run). No usar un handler de `depsgraph_update_post` para esto: modificar la escena durante la evaluación es frágil.
- **Medir desde Python.** Tras cambiar la acción por código, la pose no se actualiza hasta que cambia el frame: hay que llamar a `scene.frame_set()` a otro frame y volver.

## Decisiones del importador

- **Huesos con orientación identidad.** Todos apuntan a +Y sin roll, así que los ángulos del archivo se meten tal cual en `rotation_euler` del pose bone, sin convertir matrices. Por eso en el viewport los huesos apuntan todos en la misma dirección: es solo visual.
- **Espacio PSX dentro del armature.** Los datos van en coordenadas originales (Y hacia abajo). El objeto `Leon_Rig` lleva la conversión: rotación `(-90°, 0, -90°)` (Z arriba, mirando a -Y) y escala `0.00064` (~1.9 m de alto).
- **Ángulos desenrollados.** Antes de crear los keyframes se pasan a rango con signo y se ajustan frame a frame (±4096), para que la interpolación no gire 360° al cruzar el 0.
- **Piel rígida.** Cada vértice tiene peso 1 en su hueso, igual que en el original.
- **Textura compuesta.** Se genera una sola imagen aplicando a cada página de 128 px su paleta. Interpolación `Closest` y alpha clip.
- **30 fps, interpolación lineal.** El juego guarda una pose completa por frame.

## Limitaciones / pendiente

- **Root motion opcional:** con `root_motion=True` se aplica el desplazamiento de cada frame; por defecto las animaciones van en el sitio.
- **Armas no importadas:** la malla y la textura del arma (RE2: secciones 2–3 del PLW; RE3: 6–8) se ignoran; el personaje apunta con la mano vacía.
- **Capas parciales de RE3 sin importar:** las secciones 2–5 de los PLW (tren superior, 7 huesos) no se cargan.
- **Skinning de RE3 aproximado:** ver arriba. La sección 3 del PLD sin decodificar.
- **Flags de los frames sin decodificar** (RE2: bits 12+; RE3: byte alto).
- **Normales del archivo ignoradas:** Blender recalcula las suyas.
