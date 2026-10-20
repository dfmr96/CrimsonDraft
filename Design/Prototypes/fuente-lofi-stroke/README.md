# Lo-Fi Stroke

Efecto de fuente de Crimson Draft: glifos de píxel con **relleno texturizado, suavizado en las esquinas y un contorno de 1 px**, coloreados con **una rampa de 8 tonos** por color de texto. Da el aspecto del texto de las consolas de 32 bits sobre una fuente de píxeles moderna (`Assets/Resources/Fonts/Silver.ttf`).

## Capas de cada glifo

Cada píxel del glifo guarda un **índice**, no un color:

| Índice | Capa | Regla |
|---|---|---|
| 0 | Fondo | Transparente |
| 1–3 | Relleno | Tres tonos repartidos de forma irregular, fijos por carácter: ≈28 % claro, 47 % medio, 25 % oscuro. El borde superior izquierdo de cada trazo tiende a ser más claro |
| 4–6 | Suavizado | Huecos en esquinas interiores (trazo a un lado y también arriba o abajo) |
| 8 | Contorno | 1 px alrededor del trazo, también en diagonal |

Los índices 1–8 son **una sola rampa de claro a oscuro**. El color del texto cambia la rampa entera, contorno incluido.

Origen de las reglas de capas y colores: [investigación de referencia](../../Reference/RE2/texto/render-de-texto.md).

## Implementación en Unity

| Pieza | Ruta |
|---|---|
| Shader | `Assets/Art/Shaders/UI/TMP_LoFiStroke.shader` → `CrimsonDraft/UI/TMP Lo-Fi Stroke` |
| Horneador | `Assets/Scripts/Editor/LoFiStrokeFontBaker.cs` → menú `CrimsonDraft/Fonts/Lo-Fi Stroke Baker` y `CrimsonDraft/Fonts/Bake Silver Lo-Fi Stroke` |
| Font asset | `Assets/Resources/Fonts/Silver LoFi Stroke.asset` |
| Escena de prueba | `Assets/Scenes/Test/FontLoFiStroke_Test.unity` |

- **Horneado:** TMP en modo RASTER a 19 px (tamaño nativo de Silver: 100 unidades = 1 píxel) y población estática. Cada glifo se convierte en mapa de índices (alfa = índice / 15) y se suma **1 px al avance** para que los contornos de letras vecinas no se fundan. Al volver a hornear se conserva el GUID.
- **Shader:** busca cada índice en la rampa del material (`_Ramp1`…`_Ramp8`) y lo multiplica por el color del vértice (**tinte**). Las etiquetas `<color>` y la paleta de Yarn (`[item]`, `[place]`…) tiñen toda la rampa.
- **Uso:** tamaño 19 (o múltiplos) en Canvas con escala entera (640 × 360 → ×3 en 1080p), sin auto-tamaño, negrita sintética ni *Extra Padding*.

Pendiente: **rampas exactas** (una tabla de paletas con color clave por fila, con el tinte como respaldo).

## Imágenes

| Imagen | Qué muestra |
|---|---|
| [unity-escena-prueba-1080p.png](unity-escena-prueba-1080p.png) | La escena de prueba renderizada en Unity a 1080p (los bloques de "Silver SDF" son un defecto de la captura) |
| [unity-tmp-tinte-x3.png](unity-tmp-tinte-x3.png) | Primera prueba en TextMesh Pro con tinte |
| [espaciado-antes-despues.png](espaciado-antes-despues.png) | Antes y después de sumar 1 px al avance: antes los contornos de letras vecinas se fundían |
| [silver-lofi-paletas.png](silver-lofi-paletas.png) | Prototipo en Python con las rampas de 8 tonos |
| [silver-lofi-trazo-1px.png](silver-lofi-trazo-1px.png) | Primera versión: colores inventados y contorno púrpura fijo (descartada) |
| [silver-lofi-trazo-2px.png](silver-lofi-trazo-2px.png) | Trazo engrosado 1 px: tapa los huecos de "m" y "a" (descartada) |
| [silver-original.png](silver-original.png) | Silver sin efecto |

## Prototipo en Python

```
python -I lofi_stroke_font.py <ruta a Silver.ttf> <salida.png> [escala] [bold]
```

Necesita Pillow. Los textos y las paletas están al principio del script.
