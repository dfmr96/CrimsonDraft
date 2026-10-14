# Soldier Appearance Variants — casco / anteojos / mochila / chaleco intercambiables

> Estado al 2026-10-14, rama `feat/Animations`. Variantes de equipo del soldado skinneadas al **mismo esqueleto** que `SoldierCharacterRigged.fbx`, sin retargeting ni cambios en clips, controllers ni Avatar.

## 1. Qué se hizo

| Pieza | Ubicación |
|---|---|
| Pipeline Blender 5.2 (`bpy`) | `Tools/Blender/soldier_variants.py` (raíz del repo, fuera de Unity) |
| Fuente editable de las variantes | `Tools/Blender/SoldierVariants.blend` |
| Preview de combinaciones en Blender | `Tools/Blender/preview_variants.py` |
| Reportes de rest pose | `Tools/Blender/reports/restpose_{original,variants,diff}.json` |
| FBX de variantes (solo armature + piezas, sin animaciones ni cuerpo) | `Assets/Art/Models/CharactersFBX/MarineRigged/Variants/SoldierCharacterRigged_Variants.fbx` |
| Runtime | `Assets/Scripts/Operators/Appearance/` — `SoldierAppearanceSlot`, `SoldierAppearanceProfile`, `SoldierAppearance` |
| Editor | `Assets/Scripts/Editor/SoldierAppearance/` — `SoldierVariantsImportSettings`, `SoldierVariantInstaller`, `SoldierAppearanceEditorWindow` |
| Prefab Variants | `Assets/Prefabs/Characters/Ethan_Combat_Appearance.prefab` (base `Ethan_Combat_FBX`) y `Player_Appearance.prefab` (base `Player`) |
| Profiles de ejemplo | `Assets/Data/Characters/Appearance/SoldierAppearance_Default.asset`, `SoldierAppearance_Recon.asset` |
| Campo nuevo | `OperatorData.AppearanceProfile` (opcional, sin asignar) |

**No se tocó**: `SoldierCharacterRigged.fbx` ni su `.meta` (verificado por hash contra el backup en `_backups/SoldierRigged_2026-10-14/`), `SoldierCharacter_animated.fbx`, los controllers (`Operator_Combat_Controller v2`, `Marine v1/v2/v3`), clips, Avatar, `Ethan_Combat_FBX.prefab`, `Player.prefab`, los `*_Data.asset`, ni el Zombie Texture Editor.

### Datos clave del rig (Fase 0)
- `SoldierCharacterRigged.fbx` lo exportó **Blender 5.2.1 FBX IO** con valores por defecto: cm, Y up, -Z forward, `FBX_SCALE_NONE`, leaf bones `_end` generados por el exporter.
- Unity: Generic, **sin Avatar** (`avatarSetup: 0`), 37 clips `Rig|Soldier_*`.
- Los prefabs **no** usan la mesh de ese FBX: usan `Assets/Art/Animations/Player/SoldierCharacter_animated.fbx` (Avatar `SoldierCharacter_animatedAvatar`). Los dos FBX tienen el **mismo esqueleto** (68 huesos, mismos nombres, padres y rest pose).
- Esqueleto: `Rig > Root > Hip > spine.001..003 > neck > head`, brazos, dedos, `Breast.L/R`, piernas, controles IK (`ElbowIK/HandIK/KneeIK/HeelIK`, las armas cuelgan de `HandIK.R_end`).
- Piezas originales (rígidas): `Helmet` → `head`, `Goggle` → `head` (material `Soldier1`), `Pack` → `spine.003`, `Vest` → `spine.001-003` (`Soldier2`).
- El cuerpo `Marine` está **completo** debajo del equipo (cabeza y torso enteros): sacar casco o chaleco no deja agujeros.

### Ampliación spec-ops (2026-10-14)

| Pieza | Ubicación |
|---|---|
| Accesorios spec-ops (blockouts por script) | `Tools/Blender/soldier_specops.py` (lo llama `blockouts`/`build`) |
| Generador de texturas | `Tools/Blender/soldier_textures.py` + layout compartido `soldier_gear_layout.py` |
| Preview texturizado de loadouts | `Tools/Blender/preview_specops.py` |
| Texturas | `Variants/Textures/Camo_<Nombre>.png` (7) y `Gear_Atlas.png` |
| Materiales | `Variants/Camo/Mat_Camo_<Nombre>.mat` (7), `Variants/Materials/Mat_Gear.mat` (copias de `Soldier2.mat`, shader `PSXMaster_URP`, `_MainTex`) |

- **Slot nuevo `Mask`** (máscaras combinables con casco y anteojos, como en los retratos de `Art/Sprites/UI/New/operators.png`). Su default es `None`.
- **Variantes nuevas (17)**:
  - Mask: `GasMask` (GP-5/M40 como el sprite: oculares con aro atornillado, válvula con rejilla, válvula de exhalación, mentonera, filtro con cuello roscado, aro y tapa perforada, arnés de 5 correas con hebillas y almohadilla trasera), `GasMaskDual` (M50: visor panorámico con marco, dos filtros con aro de bayoneta, válvula frontal y lateral, boquilla y tubo de bebida, arnés), `Respirator` (3M media cara: cuerpo moldeado gris, clip nasal, válvula de exhalación con rejilla, filtros pancake oliva con banda amarilla sobre bayonetas (no magenta como un P100 real: en Unity se confunde con el rosa de "shader/textura faltante"), correas de cabeza y cuello con hebilla), `Skull`, `Shemagh`. Las correas se proyectan sobre la superficie real de la cabeza (raycast). **Presupuesto low-poly**: ninguna máscara supera al cuerpo (`Marine`: 1050 v / 984 t en Unity): GasMask 472 v / 264 t, GasMaskDual 602 v / 328 t, Respirator 354 v / 184 t. Se logra con cáscaras de una cara (sin solidify), cilindros de 6–8 lados sin la tapa trasera oculta, discos de una cara para rejillas/tapas/lentes/hebillas, correas como cintas planas y el visor del M50 pintado sobre las caras de la cáscara. Al agregar piezas nuevas, usar `Builder.disc`, `Builder.ribbon` y `cylinder(..., back_cap=False)` para mantener ese presupuesto. Detalles pintados en las zonas `GRILLE`, `RIM`, `FILTER_CAP`, `STRAP`, `SEAL`, `RESP_BODY` y `P100` del `Gear_Atlas`.
  - Helmet: `Boonie`, `Beanie`, `Beret`, `Headset`, `FastNVG` (casco original + NVG levantado y contrapeso; 2 materiales).
  - Glasses: `NVGQuad` (4 tubos bajados), `NVGMono`.
  - Backpack: `Radio` (con antena), `Hydration`.
  - Vest: `PlateCarrier`, `ChestRig`, `Bandolier` (cartuchos rojos).
- **Camuflajes (7)**: `Woodland`, `Desert`, `Urban`, `Arctic`, `RangerGreen`, `Coyote`, `NightOps`. Se generan recoloreando `MarineTexture1.png` **sin mover un píxel del layout**: mismas UVs, nada se rompe. Quedan intactos la cara/balaclava (UVs de las caras de la cabeza), la lente de los goggles y los parches rojos; guantes/botas (zonas oscuras) solo se tiñen.
- **Camuflajes con patrón propio (10)**, generados por `PATTERNS` en `soldier_textures.py`: el patrón se pinta en espacio UV y se multiplica por el sombreado del atlas original (pliegues, bolsillos y costuras siguen visibles); mismas zonas protegidas que arriba.
  | Nombre | Estilo |
  |---|---|
  | `M81` | woodland clásico: manchas grandes en 4 capas |
  | `TigerStripe` / `TigerNight` | rayas horizontales tipo jungla (verde / nocturno azulado) |
  | `MARPAT` / `DesertDigital` / `UrbanDigital` | digital pixelado (cuadros de 4 px con quiebre de 2 px) |
  | `Flecktarn` | puntos en racimos (5 colores) |
  | `Splinter` | parches angulares + "lluvia" vertical (Splittertarn) |
  | `HexTyphon` | escamas hexagonales negras/grises (tipo Kryptek) |
  | `ChocolateChip` | desierto con manchas suaves y "piedras" negras con brillo blanco |
  Para crear otro: agregá una función `pat_<x>(rng, h, w)` que devuelva un índice de color por píxel y una entrada en `PATTERNS` (paleta + semilla).
- **Gear_Atlas.png**: atlas nuevo de 256×256 para el equipo (goma, oculares, verde NVG, metal, filtro, shemagh, calavera, boina, polímero, tela negra, radio). Las piezas spec-ops mapean sus UVs a esos rects, así que la textura siempre encaja. El camo **no** afecta a `Mat_Gear`.
- Piezas que siguen siendo camo-ables (usan el atlas del soldado): `Boonie`, el casco de `FastNVG`, `PlateCarrier`, `ChestRig`, más todas las originales.

## 2. Convención de nombres

- Objeto = `<Slot>_<Variant>`; slots: `Helmet`, `Glasses`, `Backpack`, `Vest`, `Mask`. Ej.: `Helmet_Hood`, `Vest_BeltPouches`, `Mask_GasMask`. `Mask` no necesita `_Default`.
- Materiales permitidos: `Soldier1`, `Soldier2` (atlas del soldado, camo-ables), `Mat_Gear` (equipo) o `Mat_<Slot>_<Variant>`.
- Camos: textura `Camo_<Nombre>.png` → material `Mat_Camo_<Nombre>.mat`; en la tool aparecen como `<Nombre>`.
- En Blender cada slot es una colección `Slot_<Slot>`. El cuerpo vive en `Reference` y **no se exporta**.
- `None` no tiene mesh: `SoldierAppearance` oculta todo el slot.
- Las piezas originales del prefab (`Helmet`, `Goggle`, `Pack`, `Vest`) se reconocen como `Default` de su slot (alias); el instalador **no** duplica los `*_Default` del FBX de variantes en esos prefabs.
- Material propio de una variante: `Mat_<Slot>_<Variant>`. Por defecto se reusa `Soldier1` / `Soldier2` (mismo atlas, sin draw calls extra).

Variantes actuales (blockouts generados por script, para refinar a mano): `Helmet_Hood`, `Glasses_Shades`, `Glasses_Visor`, `Backpack_Small`, `Backpack_Large`, `Vest_BeltPouches` (+ los cuatro `*_Default`).

## 3. Crear una variante nueva en Blender (paso a paso)

```bash
cd D:/UADE/Tesis/CrimsonDraft/Tools/Blender
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b SoldierVariants.blend --python soldier_variants.py -- new-variant --from Helmet_Default --name Beret
```

1. El comando duplica la variante origen (mesh, pesos, material, Armature modifier), la nombra `Helmet_Beret`, la mete en `Slot_Helmet` y guarda el `.blend`. También se puede hacer desde la consola Python de Blender: `sv.new_variant("Helmet_Default", "Beret")`.
2. Abrí `SoldierVariants.blend`, modelá **solo la forma** de la nueva pieza en Edit Mode. Reglas:
   - **No** toques el armature `Rig` (ni huesos, ni rest pose, ni transform).
   - No muevas/rotes/escales el **objeto**: editá vértices. (Si lo hiciste, `Ctrl+A > All Transforms`.)
   - Sin modifiers extra (aplicalos antes de exportar), sin shape keys.
   - UVs dentro del atlas de `MarineTexture.png` / `MarineTexture1.png`.
3. Pesos: si la forma se aleja de la pieza original (p. ej. un cinturón o una capucha que baja al cuello), repesá desde el cuerpo con Data Transfer (Vertex Groups, Nearest Face Interpolated, fuente `Marine`) limitado a los huesos del slot, normalizá y limitá a 4 influencias. Huesos permitidos: Helmet `head, neck` · Glasses `head` · Backpack `spine.001-003` · Vest `Hip, spine.001-003` (ver `SLOT_BONES` en el script; `transfer_weights()` hace exactamente esto).
4. Guardá y seguí con la exportación (sección 4).

Para regenerar todo desde el FBX original (pisa el `.blend`, perdés lo modelado a mano): `-- build --force`.

### Texturas nuevas
- **Otro camuflaje**: agregá una paleta (4 colores, oscuro → claro) a `CAMOS` en `soldier_textures.py` y corré
  `blender -b SoldierVariants.blend --python soldier_textures.py`. En Unity: `CrimsonDraft > Soldier Appearance > Create Or Update Variant Materials` y después `Update Appearance Prefab Variants`.
- **Pintar a mano**: editá `Camo_<Nombre>.png` o `Gear_Atlas.png` en cualquier editor **sin cambiar el tamaño (256×256) ni mover zonas**: los camos tienen el mismo layout que `MarineTexture1.png` y el atlas de equipo sigue `soldier_gear_layout.GEAR_RECTS`. Ojo: volver a correr el script pisa esos PNG.
- Una pieza nueva de equipo: construila con `soldier_specops.Builder` y pasale el rect de `GEAR_RECTS` (o el `uv_rect` de una pieza original para que use el atlas camo-able).

## 4. Exportar

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b SoldierVariants.blend --python soldier_variants.py -- export
```

`export` hace, en orden:
1. **validate**: falla con mensaje claro si alguna variante no tiene exactamente un Armature modifier a `Rig`, tiene vertex groups que no son huesos deform del rig, vértices sin peso, más de 4 influencias, transforms sin aplicar, shape keys, modifiers extra, sin UVs, nombre/colección fuera de convención, o si un slot no tiene `_Default`.
2. Exporta armature + variantes a `SoldierCharacterRigged_Variants.fbx` con los mismos settings que el original (`-Z`/`Y`, `FBX_SCALE_NONE`, leaf bones regenerados, sin animación, sin "only deform bones").
3. **Rest pose**: reimporta el original y el exportado y compara posición/rotación de cada hueso (tolerancia 0.1 mm / 0.01°). Resultado actual: **68 huesos, máx. 0.0005 mm / 0.0007°**. Si falla, el comando termina con error y deja `reports/restpose_diff.json`.

Después, en Unity el FBX se reimporta solo y conserva su `.meta`. Si agregaste variantes o camos nuevos: `CrimsonDraft > Soldier Appearance > Update Appearance Prefab Variants` (re-instala en `Ethan_Combat_Appearance` y `Player_Appearance` conservando la selección guardada). Si apareció un material nuevo en el FBX: `Reapply Variants FBX Import Settings`.

## 5. Unity

### Import del FBX de variantes
`SoldierVariantsImportSettings` (AssetPostprocessor, sólo cuando el `.meta` es nuevo; manual: `CrimsonDraft > Soldier Appearance > Reapply Variants FBX Import Settings`): Generic, **No Avatar** (igual que el original), sin animaciones/cámaras/luces, file scale, 4 bones/vértice, materiales remapeados a `Assets/Art/Animations/Player/Soldier1.mat` y `Soldier2.mat`. El FBX es solo fuente de meshes: nunca se instancia, así que no hay Animator duplicado.

### Rebind por nombre de hueso
`SoldierVariantInstaller.Install(character, variantsModel)`: por cada variante crea (o actualiza) un hijo con `SkinnedMeshRenderer` junto a las piezas originales, mapeando `bones` y `rootBone` **por path relativo a `Rig`** (fallback por nombre único) contra el esqueleto del prefab, copia `localBounds` y los settings de render de la pieza original. Si falta un hueso tira `InvalidOperationException` con la lista. Las variantes nuevas quedan desactivadas; las originales siguen como Default.

### Componente runtime
`SoldierAppearance` va en el objeto que tiene el Animator (raíz en combate; `SoldierCharacter_animated` dentro de `Player`).
- `Apply(SoldierAppearanceProfile)`, `SetVariant(slot, name)` (devuelve `false` y no cambia nada si no existe), `GetVariant(slot)`, `GetVariantNames(slot, list)`, `SetSlotLook(slot, material, tint)`.
- `RebuildVariantList()` (también por menú contextual) autopobla desde la convención de nombres.
- Sin `Update`: solo activa/desactiva GameObjects al aplicar → cero costo por frame. Si se asigna `profile` en el componente, se aplica en `Awake`.
- `SetCamo(name)` / `GetCamoNames(list)` / `Camo`: reemplaza `Soldier1`/`Soldier2` por `Mat_Camo_<Nombre>` en el cuerpo y en todas las piezas (orden de prioridad: override de material del slot > camo > material importado). `Mat_Gear` nunca cambia. El profile guarda el camo.
- El tint usa `MaterialPropertyBlock` (`_BaseColor`/`_Color`): se ve en preview pero no se serializa en el prefab; persiste vía profile.

### Tool de editor
Menú **`CrimsonDraft > Soldier Appearance Editor`** (mismo estilo que el Zombie Texture Editor: una fila por slot con `<` preview `>`).
1. Seleccioná una instancia en escena o abrí el prefab (`Ethan_Combat_Appearance` / `Player_Appearance`) en Prefab Stage. Si seleccionás el asset, la tool ofrece **Open Prefab**.
2. Fila **Camo** (preview de la textura, `<` `>` y dropdown) y una fila por slot (Helmet, Glasses, Backpack, Vest, Mask): flechas, dropdown, **None**, material override y tint. Arriba: **Random**, **Reset to default** (Mask vuelve a None y el camo a Default).
3. **Profile**: `Load` aplica un profile, `Save` lo sobrescribe con la selección actual, `Save As...` crea uno nuevo en `Assets/Data/Characters/Appearance/`.
4. **Apply to Prefab** (instancias de escena): aplica solo los overrides de las variantes y del componente. En Prefab Stage se guarda al guardar el prefab.
5. **Install / Update Variants**: re-ejecuta el rebind (tras exportar variantes nuevas). Si no hay prefabs de apariencia: **Create Appearance Prefab Variants** (no pisa los existentes).
Todos los cambios usan Undo y marcan dirty la escena/prefab.

## 6. Validaciones realizadas

- Ida y vuelta por Blender (import → export → reimport): huesos ~1e-5 en matrices y vértices < 0.5 µm. Rest pose de `SoldierCharacterRigged_Variants.fbx` vs original: 68/68 huesos dentro de 0.0005 mm / 0.0007°.
- Validación del script: 10 variantes OK.
- **Bindposes en Unity**: sobre `Ethan_Combat_FBX` animado con clips del controller (PistolIdle, PistolAim, PistolShoot, PistolReload, ShootgunReload, Death en 0/50/100 %), cada `*_Default` reenganchado coincide con la pieza original a **0.001 mm**. Ninguna variante queda en el origen ni explota (centroide ≤ 24 cm del hueso más cercano: la mochila grande).
- Verificación visual: meshes skinneadas horneadas desde Unity (`BakeMesh`) en Aim, Shoot, Reload y Death (combate) y Run (navegación, `Marine v3`) para las combinaciones default / hood+visor+small+belt / none+shades+large: todas las piezas siguen al cuerpo.
- Prefab Variants: un único Animator de personaje con su controller original; 10 variantes; ningún hueso nulo.
- `validate_script` sin errores en todos los scripts nuevos; consola sin errores ni warnings nuevos.

- Ampliación spec-ops: validación del script OK con 27 variantes; rest pose del FBX re-exportado igual que antes (68 huesos dentro de tolerancia). En Unity: 27 variantes por prefab, 0 huesos nulos; `Apply` con máscara + FastNVG + Woodland deja el cuerpo y el casco en `Mat_Camo_Woodland` y la máscara/NVG en `Mat_Gear`; `SetCamo("Default")` restaura `Soldier2`. Verificación visual de meshes horneadas desde Unity **con UVs y texturas** en 7 loadouts × Aim/Reload/Death: texturas sin cortes y piezas siguiendo la animación.

## 7. Pendientes / recomendaciones

- **Integración por operador (no implementada)**: los 4 `OperatorData` siguen apuntando a `Ethan_Combat_FBX`. Punto recomendado: en `BattlefieldView` (`Assets/Scripts/Combat/UI/BattlefieldView.cs`, donde hace `Instantiate(op.BattlefieldPrefab, ...)`) agregar `go.GetComponentInChildren<SoldierAppearance>()?.Apply(op.AppearanceProfile)` si el profile no es null, y apuntar `battlefieldPrefab` a `Ethan_Combat_Appearance`. En navegación, el equivalente es aplicar el profile del operador activo sobre `Player_Appearance` al spawnear / cambiar de operador. `OperatorNavCorpse` no tiene variantes (se puede instalar igual con la tool si hace falta que el cadáver se vea igual).
- Las escenas siguen usando `Player.prefab`; cambiar a `Player_Appearance` es manual.
- Los blockouts son placeholders: refinarlos en `SoldierVariants.blend` y re-exportar. Las UVs de hood/visor/belt son una proyección frontal dentro del rect del atlas de la pieza original.
- La UI de la ventana (clicks, Undo/Redo interactivo, Apply to Prefab sobre una instancia de escena) no se probó a mano; la lógica que llama (`SetVariant`, `Apply`, `Install`, creación de variants) sí se probó por código.
