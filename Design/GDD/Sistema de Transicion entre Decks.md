---
estado: borrador
ultima-revision: 2026-06-09
tags:
  - game-design
---

# Sistema de Transición entre Decks

Permite al jugador moverse entre Deck B y Deck C a través de escaleras o puertas que conectan escenas Unity distintas, preservando el estado de desbloqueo entre transiciones.

---

## Diseño

### Principio general

Las habitaciones dentro de un mismo Deck viven en una sola escena Unity. El paso entre Decks cruza el límite de escena: Deck B y Deck C son escenas separadas que se cargan y descargan en tiempo de ejecución.

La transición entre Decks usa la misma animación de puerta que las transiciones intra-Deck. El jugador no distingue visualmente si está cambiando de habitación o de Deck — la experiencia es idéntica.

---

### Interactuable de transición entre Decks

Un objeto en escena — una puerta, una escalera o cualquier punto de paso entre Decks — funciona como [[Sistema de Interactuables|interactuable]] especializado del tipo **Interactuable de Deck**. Se configura con:

| Campo | Descripción |
|---|---|
| `doorId` | Identificador único de esta puerta en todo el juego (ej: `"deckb_port_stairs_upper"`) |
| `Destino` | Nombre de la escena Unity a cargar |
| `Punto de entrada` | ID del punto de llegada en la escena destino |
| `Bloqueada` | Si requiere llave para usarse por primera vez |
| `Ítem llave` | Referencia al ítem que la desbloquea (opcional) |
| `Animación de puerta` | Prefab de la animación de transición |

Las reglas de bloqueo y desbloqueo son idénticas a las de la [[Sistema de Interactuables#DoorInteractable|DoorInteractable]] estándar: el nodo Yarn presenta opciones al jugador, y una vez desbloqueada la puerta queda abierta permanentemente.

---

### Conexiones unidireccionales

Cada **Puerta de Deck** es unidireccional. La vuelta requiere un objeto independiente en la escena destino con la dirección inversa. Esto permite asimetría narrativa: una puerta puede estar bloqueada en un sentido pero libre en el otro.

```
Deck B — Escalera de Popa
  → destino: "Deck_C"
  → punto de entrada: "deckb_port_entry"

Deck C — Escalera de Popa (planta baja)
  → destino: "FIX_Deck_B"
  → punto de entrada: "deckc_port_entry"
```

---

### Persistencia del estado de desbloqueo

El estado de cada puerta — bloqueada o desbloqueada — sobrevive a los cambios de escena. Un registro central en memoria mantiene un mapa de `doorId → desbloqueada` durante toda la sesión de juego.

Cuando una escena carga, todas las puertas en ella consultan el registro y actualizan su estado local. Si el jugador desbloqueó la puerta en una sesión anterior de la misma escena, aparecerá desbloqueada al volver.

Este registro está diseñado para integrarse con el sistema de guardado cuando esté disponible.

---

### Puntos de entrada por escena

Cada escena que puede recibirse como destino de otra escena tiene uno o más **Puntos de entrada**. Cada punto define:

| Campo | Descripción |
|---|---|
| `ID de entrada` | Clave única que la escena de origen indica al iniciar la transición |
| `Habitación inicial` | La habitación de esta escena que se activa al llegar |
| `Posición y rotación` | Transform exacto donde aparece el jugador |
| `Cámara` | Cámara Cinemachine a activar en el momento de llegada (opcional) |

Si no hay ningún punto de entrada pendiente al cargar una escena — por ejemplo, al iniciar el juego desde el menú principal — se usa la habitación inicial configurada por defecto.

---

### Flujo de una transición

```
Jugador interactúa con Escalera de Popa (Deck B)
  → Si bloqueada: diálogo Yarn; si tiene llave: desbloqueo + registro
  → Si libre:
      1. Pausa input del jugador
      2. Marca punto de entrada destino: "deckb_port_entry"
      3. Carga escena "DoorTransition" (animación de puerta)
      4. Espera fin de animación
      5. Descarga la escena de Deck B
      6. Carga la escena de Deck C
          → Deck C inicializa y busca punto de entrada marcado
          → Activa habitación correspondiente
          → Coloca jugador en el transform del punto
      7. Descarga escena "DoorTransition"
      8. Restaura input del jugador
```

---

### Caché de puertas en editor

Para evitar búsquedas en tiempo de ejecución, las puertas de cada escena se registran manualmente en el editor mediante un botón **"Cache Scene Doors"** en el inspector del componente de gestión de la escena. El resultado queda serializado y disponible sin costo en runtime.

Este mismo registro también aplica a las [[Sistema de Interactuables#DoorInteractable|puertas intra-Deck]], centralizando la gestión de estado de todas las puertas del juego.

---

## Intención

> La separación entre Decks no debe sentirse como una carga técnica — debe sentirse como bajar unas escaleras.

Deck B y Deck C son mundos distintos en tono, densidad narrativa y peligro. La escalera es el umbral. El jugador debe notar que algo cambia al cruzarla, pero no debe notar que el juego está cargando una escena nueva.

La persistencia del estado de puertas cierra un contrato implícito con el jugador: lo que hizo importa. Si consiguió una llave y abrió una puerta, esa puerta sigue abierta. El mundo recuerda sus acciones aunque el motor no lo haga por defecto.

La unidireccionalidad de las conexiones abre espacio para diseño asimétrico: una escalera puede estar bloqueada al bajar pero libre al subir, o dar a habitaciones distintas según la dirección de llegada. Esto amplía las posibilidades del diseño de niveles sin añadir complejidad mecánica.

---

## Pendiente

- [ ] Definir los IDs de todos los puntos de entrada entre Deck B y Deck C
- [ ] Definir si las escaleras internas del Marinera son del tipo intra-Deck o inter-Deck
- [ ] Confirmar si la animación de puerta es la misma que para puertas de habitación o una variante (escalera)
- [ ] Integrar con el sistema de guardado cuando esté disponible

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Interactuables]] | Ver [[Acto I - Diseño Detallado]]
