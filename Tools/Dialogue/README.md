# Crimson Dialogues

Tester de diálogos estilo códec. Abrí `index.html` en el navegador; los datos se guardan en el
`localStorage` del navegador.

## Flujo de trabajo

1. Las conversaciones viven como archivos sueltos en `conversaciones/`, una por archivo.
2. Los personajes viven en `personajes.json` (compartidos por todas las conversaciones).
3. `.\build.ps1` fusiona todo en `dialogos.json` y avisa si algún `speaker` no existe.
4. En la app: **Importar (agregar)** → elegís `dialogos.json`.

**Importar (agregar)** suma las conversaciones nuevas, actualiza las que ya existen (por `id`) y
conserva los retratos que hayas subido desde el editor. **Importar (reemplazar)** borra todo y
deja solo el contenido del archivo — usalo solo para restaurar un backup completo.

## Idioma

Los **diálogos y los títulos de escena van en inglés**, para pasarlos directo al juego. La
documentación (este README, `contexto.md`, las fichas de `personajes/`) va en castellano: es
material de trabajo. Sterna sigue mezclando idiomas — ver su ficha.

## Personajes

Las fichas (bio + sección **Voz**, que son las reglas con las que se escriben sus líneas) están en
`personajes/`. `personajes.json` solo tiene lo que la app necesita: `id`, alias en pantalla y color.

| id | Alias | Nombre | Rol |
|---|---|---|---|
| `vulture` | VULTURE | Darius Mercer | Team Leader (SEALs) |
| `deer` | DEER | Ethan Miller | Boarding Specialist (MSRT) |
| `sterna` | STERNA | Lilou Vance | Recon Sniper (SEALs) |
| `bear` | BEAR | Marcus Hale | Combat Engineer (SEALs) |

Hoja de referencia rápida con las cuatro voces comparadas y el mapa de tensiones:
[personajes/equipo.md](personajes/equipo.md).

## Formato de una conversación

`conversaciones/03-mi-conversacion.json`

```json
{
  "title": "Mi conversación",
  "freq": "38.75",
  "rightIds": ["deer", "sterna", "bear"],
  "leftId": "vulture",
  "lines": [
    { "speaker": "vulture", "text": "Primera línea." },
    { "speaker": "deer", "text": "Respuesta con\ndos renglones." }
  ]
}
```

- `rightIds`: el **grupo del jugador**. Todos comparten el retrato de la derecha y se alternan
  según quién habla (con estática al relevarse). Acepta uno o varios.
- `leftId`: retrato de la izquierda al abrir la radio.
- `speaker`: **id** de un personaje de `personajes.json`, no el nombre.
- Si habla alguien que no está en `rightIds`, su retrato aparece a la izquierda con un efecto de
  estática. Así se mete un personaje a mitad de conversación sin configurar nada más.
- `\n` en `text` = salto de línea en pantalla.
- `"variantes": false`: marca una escena donde nadie puede estar muerto (la intro, las
  obligatorias). El build saltea el reporte de variantes para ese archivo.
- `id`: opcional. Si no está, se usa el nombre del archivo. Mantenerlo estable hace que
  reimportar **actualice** la conversación en lugar de duplicarla.
- El orden en el menú sale del orden alfabético de los nombres de archivo (de ahí los prefijos
  `01-`, `02-`, ...).

## Beats (silencios sin hablante)

Una línea sin `speaker`, solo con `beat`, es una pausa dramática: la radio se queda sin texto y el
click del jugador *es* el silencio.

```json
{ "beat": "silence" }
{ "beat": "pause" }
{ "beat": "static" }
```

`silence` y `pause` son lo mismo visualmente (el nombre es para leer el guion); `static` además
mete interferencia en los dos retratos. Las `[ESTÁTICA]` de apertura y cierre del guion **no hacen
falta**: la app ya abre y cierra el canal con esa animación.

## Variantes según quién esté vivo

Cada conversación es **un solo archivo** que cubre las 7 combinaciones de caídos del grupo. No hay
archivos duplicados por variante.

**Regla base, gratis:** una línea cuyo `speaker` está muerto **desaparece sola**. Eso resuelve la
mayoría de los casos.

Para el resto, dos campos opcionales por línea:

### `if` — la línea existe solo en ciertas variantes

```json
{ "speaker": "vulture", "text": "Ahora sos el cuarto de este equipo.", "if": { "alive": ["deer"] } }
{ "speaker": "vulture", "text": "...No tenemos ojos arriba.",          "if": { "dead":  ["sterna"] } }
```

Se cumple si **todos** los de `alive` están vivos **y todos** los de `dead` están muertos. Se
pueden combinar: `{ "alive": ["deer"], "dead": ["bear"] }`.

Esto es imprescindible para las líneas de Vulture: él no muere, así que sus líneas dirigidas a
alguien necesitan la condición a mano.

### `alt` — mismo momento, otro texto

```json
{ "speaker": "bear", "text": "Bear. Entero. Estoy con Deer.",
  "alt": [ { "dead": ["deer"], "text": "Bear. Entero.\nSolo." } ] }
```

Gana el **primer** `alt` que cumpla; si ninguno cumple, se usa el `text` base. Conviene escribir el
`text` base como la versión reducida y poner el detalle en un `alt` con `"alive": [los tres]`: así
una sola alternativa cubre la variante completa y la base sirve para todas las demás.

### El recurso bueno: hablarle a un muerto

Sale solo del sistema. Vulture pregunta sin condición, la respuesta se cae porque el que respondía
está muerto, y dos líneas condicionadas recogen el silencio:

```json
{ "speaker": "vulture", "text": "Sterna. Qué ves." },
{ "speaker": "sterna",  "text": "Un... deux... trois... siete en cubierta." },
{ "speaker": "vulture", "text": "Sterna.",                   "if": { "dead": ["sterna"] } },
{ "speaker": "vulture", "text": "...Copiado. Sin ojos arriba.", "if": { "dead": ["sterna"] } }
```

### Probar las variantes

- En la app, **Simular caídos** en el menú: botones por integrante del grupo. El detalle muestra la
  variante resuelta, con las líneas descartadas tachadas, y la radio reproduce solo esa variante.
- `.\build.ps1 -Cobertura` imprime las 8 combinaciones con cuántas líneas quedan y quién habla.
- El build **avisa** si una variante queda sin líneas o si alguien vivo no dice nada. **No puede
  detectar** una pregunta que quedó sin respuesta porque murió el que contestaba — eso hay que
  leerlo.

En el editor, el botón `⚑` de cada línea abre la condición (indistinto / vivo / muerto por
integrante). Los `alt` se editan en el JSON; el editor los preserva y los muestra como `altN`.

## Retratos

Dos opciones:

- Subirlos desde el editor de la app (click en el cuadrado del personaje). Se recortan a 256×256
  y quedan en el navegador; `Importar (agregar)` no los pisa.
- Poner `retratos/<id>.png` (o `.jpg`, `.jpeg`, `.webp`) y `build.ps1` los incrusta en
  `dialogos.json`. Conviene que sean cuadrados y chicos: el `localStorage` tiene ~5 MB.
