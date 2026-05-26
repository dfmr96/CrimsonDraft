# // MapPinner

Herramienta para colocar y explorar pins anotados sobre cualquier imagen de mapa. Funciona completamente en el navegador, sin servidor ni instalación. Todo se guarda en `localStorage`.

---

## Estructura de archivos

```
mappinner/
├── index.html
├── css/
│   └── style.css
└── js/
    ├── app.js        — lógica principal, eventos, modos
    ├── map.js        — carga de imagen, zoom, paneo
    ├── pins.js       — tipos de pin, render, SVG
    ├── presets.js    — presets DEV y vistas Explore
    └── storage.js    — localStorage, export/import
```

Para usarla basta con abrir `index.html` en un navegador. No requiere conexión ni dependencias locales (la tipografía IBM Plex Mono se carga desde Google Fonts).

---

## Pantalla de inicio

Al abrir la app aparece una pantalla de selección. Elegís el modo antes de entrar a la herramienta. El modo puede cambiarse en cualquier momento desde el switcher en el header.

---

## Modos

### DEV

Modo de creación y edición. Pensado para quien arma el mapa.

**Cargar un mapa**
Hacé click en la caja central o arrastrá cualquier imagen (PNG, JPG, WebP, etc.). El mapa queda guardado automáticamente en localStorage.

**Navegar el mapa**
Click y arrastre para mover. Rueda del mouse para zoom.

**Crear un pin**
Click derecho sobre el mapa abre el panel de creación. Seleccioná una categoría, completá el nombre, descripción e imágenes opcionales (máx. 2), y confirmá.

**Editar un pin existente**
Click sobre el pin → botón Editar en el modal. Se abre el mismo panel con los datos precargados.

**Eliminar un pin**
Click sobre el pin → botón Eliminar en el modal.

**Ocultar un pin temporalmente**
Click sobre el pin → botón Hide/Unhide. El pin queda semitransparente en lugar de desaparecer. Este estado se guarda.

**Presets DEV**
Guardado de estados completos: mapa + pins. Sirven para tener distintas versiones del proyecto. Se crean y cargan desde el panel derecho (sección PRESETS).

**Panel de acciones (izquierda, botón ☰)**
- Exportar .json — guarda el estado actual (mapa + pins) como archivo JSON.
- Importar .json — carga un JSON previamente exportado, reemplazando el estado actual.
- Cambiar mapa — reemplaza la imagen del mapa sin borrar los pins.
- Publicar → Explore — genera el bundle que se usa en modo Explore.
- Limpiar todo — elimina todos los pins del mapa actual.

---

### EXPLORE

Modo de navegación. No permite crear ni eliminar pins. Pensado para quien usa el mapa ya armado.

**Cargar el mapa**
Al entrar sin bundle cargado aparece una caja en el centro. Arrastrá o hacé click para cargar el bundle `.json` generado desde DEV. También se puede cargar desde el panel de acciones.

Una vez cargado, el bundle queda guardado en localStorage y se carga automáticamente la próxima vez que se entre en Explore.

**Navegar**
Igual que en DEV: arrastre para mover, rueda para zoom.

**Ver un pin**
Click sobre cualquier pin abre el modal con nombre, categoría, descripción e imágenes si las tiene.

**Ocultar un pin**
En el modal, botón Hide/Unhide. Igual que en DEV, el pin queda semitransparente.

**Vistas**
Una vista guarda el estado actual de filtros (qué categorías están visibles) y qué pins están en hide. Se crean y cargan desde el panel derecho (sección VISTAS). Son independientes del mapa y los pins.

**Panel de acciones (izquierda, botón ☰)**

Sección MAPA:
- Importar bundle — carga un nuevo bundle (reemplaza el actual).

Sección VISTAS:
- Exportar vistas — guarda la lista completa de vistas como archivo JSON liviano (sin mapa ni pins).
- Importar vistas — carga vistas desde un JSON y las agrega a las existentes sin reemplazar.

---

## Categorías de pins

Los pins se agrupan en tres categorías con un color por grupo.

**Locations** (azul)
Save Zone, Door, Transit, Environment, Interactable

**Loot** (dorado)
Item, Key Item, Note

**Enemies** (rojo)
Boss, Consumed

Los colores, íconos y nombres de cada tipo se pueden modificar directamente en `js/pins.js` dentro del objeto `PIN_TYPES`. El color de cada grupo se define en `GROUP_COLORS`.

---

## Panel derecho

Siempre visible (colapsable con el botón ◀).

La mitad superior muestra los **filtros**: dos botones globales (Todo / Ninguno) y una grilla de categorías agrupadas. Hacer click en una categoría la oculta o muestra en el mapa. El estado de los filtros no se guarda automáticamente — para persistirlo hay que guardarlo en un preset (DEV) o en una vista (Explore).

La mitad inferior muestra **Presets** en modo DEV o **Vistas** en modo Explore.

---

## Flujo típico DEV → Explore

1. En DEV: cargar el mapa, colocar todos los pins con sus categorías, nombres, descripciones e imágenes.
2. Desde el panel de acciones: Publicar → Explore. Se descarga un archivo `mappinner-bundle-*.json`.
3. Compartir ese archivo con quien va a usar el mapa.
4. En Explore: cargar el bundle desde la caja central o desde el panel de acciones.

El bundle contiene el mapa embebido como base64, así que funciona en cualquier PC sin archivos adicionales. El tamaño del archivo depende del peso de la imagen del mapa y de las imágenes adjuntas a los pins.

---

## Formato de los archivos JSON

**Bundle (DEV → Explore)**
```json
{
  "version": 2,
  "projectBundle": true,
  "exportedAt": "...",
  "map": "data:image/png;base64,...",
  "pins": [ ... ]
}
```

**Export DEV**
```json
{
  "version": 1,
  "exportedAt": "...",
  "map": "data:image/png;base64,...",
  "pins": [ ... ]
}
```

**Vistas Explore**
```json
{
  "version": 1,
  "exploreViews": true,
  "exportedAt": "...",
  "sets": [
    {
      "name": "Solo jefes",
      "hiddenTypes": ["item", "note", "door"],
      "hiddenPins": ["1234567.89", "9876543.21"]
    }
  ]
}
```

---

## Personalización

**Agregar o cambiar categorías de pins**
Editar `js/pins.js`. Cada entrada en `PIN_TYPES` define `label`, `group` e `icon`. El color se hereda del grupo definido en `GROUP_COLORS`. El `icon` acepta cualquier emoji o símbolo Unicode.

**Cambiar colores de grupos**
Editar `GROUP_COLORS` en `js/pins.js`.

**Agregar un grupo nuevo**
Agregar la entrada en `GROUP_COLORS`, agregar los tipos correspondientes en `PIN_TYPES` con el `group` correcto, y agregar el grupo al array `PIN_GROUPS` con su `key` y `label`.
