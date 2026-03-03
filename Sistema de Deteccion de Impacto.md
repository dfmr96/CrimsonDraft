---
estado: borrador
ultima-revision: 2026-03-03
tags:
  - game-design
---

# Sistema de Detección de Impacto

Determina si un disparo acertó al objetivo y qué zona del cuerpo fue impactada, leyendo el color del píxel en la posición final del disparo sobre la silueta del objetivo.

---

## Diseño

### La Silueta como Mapa de Impacto

El objetivo de combate se representa como un sprite de silueta. Este sprite **no es solo visual** — es la definición autoritativa de las zonas de impacto. Cada color en el sprite corresponde a una zona lógica. Si el sprite cambia, la detección cambia con él sin intervención adicional.

El **punto de impacto final** (output del [[Sistema de Dispersión y Apuntado]], después de aplicar las tres capas de dispersión) se proyecta sobre la silueta. El sistema lee el color del píxel en esa posición y lo resuelve contra una paleta de zonas definida por el diseñador.

### Paleta de Zonas

Cada entrada de la paleta define un par `color → ShotZone`. El sistema busca el color de paleta con menor distancia euclidiana RGB al píxel muestreado.

| Color en sprite | ShotZone | Descripción                       |
|-----------------|----------|-----------------------------------|
| Blanco `#FFFFFF`| Hit      | Zona válida — el disparo acertó   |
| Negro `#000000` | Miss     | Fuera de silueta — el disparo falló |

> Esta es la configuración mínima de la iteración actual. Las zonas anatómicas se añaden como nuevas entradas de paleta sin cambiar la arquitectura.

### Proceso de Detección

```
1. Obtener posición final del disparo en coordenadas de AimSpace
2. Transformar al espacio local de la silueta
3. Normalizar como UV dentro del rect de la silueta (u, v ∈ [0, 1])
4. Muestrear el píxel en (u × ancho_textura, v × alto_textura)
5. Comparar color del píxel contra cada entrada de la paleta
6. La entrada con menor distancia euclidiana RGB gana → ShotZone
7. Si ninguna entrada supera la tolerancia → ShotZone = Miss
```

### Zonas Anatómicas (iteración futura)

Cuando se añadan zonas de cuerpo, la paleta se expande con nuevos colores. La silueta del sprite se divide en regiones pintadas con el color correspondiente.

| Color          | ShotZone  | Multiplicador de daño | Prob. hemorragia |
|----------------|-----------|-----------------------|------------------|
| Rojo `#FF0000` | Head      | ×2.5                  | 80%              |
| Verde `#00FF00`| Torso     | ×1.0                  | 60%              |
| Azul `#0000FF` | Arms      | ×0.6                  | 40%              |
| Amarillo       | Legs      | ×0.7                  | 50%              |
| Negro `#000000`| Miss      | —                     | —                |

Los multiplicadores y probabilidades se toman del [[Sistema de Salud]].

### Flujo de Datos

```
AimView QTE
    → posición final (3 capas de dispersión)
    → detección de zona (paleta de colores)
    → ShotZone

ShotZone → cálculo de daño (Diseño de Combate y Armas)
         → probabilidad de hemorragia (Sistema de Salud)
```

### Tolerancia de Color

La paleta acepta una tolerancia de distancia RGB configurable (por defecto 0.1) para proteger contra artefactos de compresión. El sprite de silueta se distribuye sin compresión con pérdida para garantizar colores exactos — la tolerancia es solo un safety net.

---

## Intención

> El disparo es el recurso más escaso del juego. Que el jugador vea si acertó o falló no es feedback de UX — es la consecuencia de su decisión de gastar esa bala.

El sistema cierra el loop del QTE de apuntado: el jugador invirtió tiempo y tensión en el minijuego, y recibe un resultado claro. Un miss no es "sin efecto" — es una bala menos, irreversible.

La silueta como mapa de impacto conecta el diseño visual del enemigo con su mecánica de combate. Un enemigo con armadura en el torso se representa visualmente con esa protección; el mapa de impacto refleja automáticamente qué zonas son vulnerables. El jugador que lee bien la silueta toma mejores decisiones de apuntado — esto es exactamente el **Pilar 5: Información como recurso**.

Las zonas anatómicas futuras añaden la decisión de dónde apuntar: un headshot es más efectivo pero el QTE bidimensional hace que la cabeza sea más difícil de alcanzar. El sistema de dispersión garantiza que incluso con buen apuntado hay incertidumbre — el jugador nunca tiene control perfecto, solo control degradado.

---

## Pendiente

- [ ] Añadir zonas anatómicas (Head, Torso, Arms, Legs) al sprite y a la paleta
- [ ] Definir feedback visual de hit/miss (efecto en la silueta, sonido de impacto)
- [ ] Conectar ShotZone al cálculo de daño en [[Sistema de Salud]]
- [ ] Definir comportamiento cuando el disparo sale completamente fuera del AimSpace

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Dispersion y Apuntado]] | Ver [[Sistema de Salud]] | Ver [[Diseño de Combate y Armas]]
