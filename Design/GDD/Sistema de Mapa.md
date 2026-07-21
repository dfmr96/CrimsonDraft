---
estado: borrador
ultima-revision: 2026-07-06
tags:
  - game-design
---

# Sistema de Mapa

Pantalla de mapa a pantalla completa que muestra el plano del deck actual: habitaciones descubiertas, estado de las puertas y la habitación donde está el jugador. El conocimiento del mapa se construye explorando o encontrando el plano del deck como ítem.

---

## Diseño

### Principio general

Cada Deck del Marinera tiene su propio plano. El jugador abre el mapa desde el menú y ve el plano del deck donde está parado; un selector permite consultar los planos de otros decks ya conocidos.

El mapa nunca muestra información que el jugador no haya ganado: las habitaciones se revelan al visitarlas o al encontrar el plano del deck, y las puertas solo muestran su estado cuando el jugador interactuó con ellas. El mapa es un registro de lo aprendido, no un oráculo.

---

### Estados de habitación

Cada habitación del plano está en exactamente uno de estos estados:

| Estado | Cómo se alcanza | Apariencia en el mapa |
|---|---|---|
| `Desconocida` | Estado inicial | No se dibuja |
| `NoVisitada` | El jugador tiene el plano del deck pero no entró | Silueta atenuada, sin detalle interior |
| `Visitada` | El jugador entró al menos una vez | Silueta completa, color estándar |
| `Completada` | Visitada y sin ítems por recoger | Silueta completa, color de completado |

Reglas:

- Los estados persistidos son monotónicos: una habitación nunca retrocede de `Visitada` a `Desconocida`.
- `NoVisitada` y `Completada` son **estados derivados** — se calculan al dibujar el mapa, no se almacenan:
	- `NoVisitada` = la habitación es `Desconocida` **y** el jugador posee el plano del deck.
	- `Completada` = la habitación es `Visitada` **y** todos los ítems recogibles asociados a ella figuran como recogidos en el registro de pickups del juego.
- La habitación donde está el jugador se marca `Visitada` al entrar — tanto al llegar por una transición de puerta como al aparecer en ella al cargar el deck.

---

### Estados de puerta

Cada puerta del plano está en exactamente uno de estos estados:

| Estado | Cómo se alcanza | Apariencia en el mapa |
|---|---|---|
| `Desconocida` | Estado inicial — el jugador nunca interactuó | Marca neutra |
| `Bloqueada` | Intentó abrirla sin la llave necesaria | Marca de bloqueo (rojo) |
| `Abierta` | La cruzó, o la desbloqueó con su llave | Marca de paso libre (verde) |

Reglas:

- `Bloqueada` solo reemplaza a `Desconocida`. Una puerta `Abierta` nunca vuelve a mostrarse bloqueada.
- Cruzar una puerta que no estaba bloqueada también la marca `Abierta`: el jugador ya sabe que ese paso funciona.
- Estas reglas aplican por igual a puertas entre habitaciones y a [[Sistema de Transicion entre Decks|puertas entre decks]] (escaleras, pasos de escena). Las puertas entre decks aparecen en el plano de ambos decks con el mismo estado.
- Una puerta se dibuja en el mapa solo si al menos una de las habitaciones que la contienen se está dibujando.

Este sistema extiende el registro central de puertas ya definido en [[Sistema de Transicion entre Decks#Persistencia del estado de desbloqueo|Transición entre Decks]]: donde hoy se recuerda solo "desbloqueada", pasa a recordarse el estado de tres valores. La regla de compatibilidad: todo lo que hoy se considera desbloqueado equivale a `Abierta`.

---

### Revelado del mapa (fog of war)

Dos fuentes de conocimiento, acumulativas:

1. **Exploración**: cada habitación visitada queda registrada para siempre.
2. **El plano del deck como ítem**: cada deck tiene un ítem de mapa único escondido en el mundo (estilo Resident Evil). Al recogerlo, todas las habitaciones de ese deck pasan a dibujarse — las no visitadas con estilo atenuado.

Poseer el plano no revela el estado de las puertas: eso sigue exigiendo interacción directa.

---

### El plano de un deck

El plano de cada deck es un documento de datos estático que contiene:

| Dato | Descripción |
|---|---|
| Deck | A qué escena/deck corresponde |
| Nombre y abreviatura | Texto mostrado en la pantalla de mapa (ej: "Cubierta B" / "B") |
| Ítem de plano | Qué ítem del inventario revela este deck |
| Habitaciones | Por cada una: identificador, silueta poligonal, ubicación en el plano, puertas asociadas, ítems recogibles contenidos |
| Puertas | Por cada una: identificador, posición y tamaño de la marca en el plano |
| Grilla | Dimensiones del plano y tamaño de celda |

Los identificadores de habitación, puerta e ítem son los mismos que ya usan los sistemas de [[Sistema de Interactuables|interactuables]] y [[Sistema de Transicion entre Decks|transición]] — el mapa no introduce identificadores propios.

La ubicación de cada habitación en el plano es independiente de su posición real en el mundo 3D: el diseñador puede acomodar el layout del plano para que se lea bien, sin alterar el juego.

---

### Pantalla de mapa

El mapa vive como una pestaña más del menú de inventario (junto a Ítems y Notas/Documentos), no como una pantalla independiente con su propio atajo. Abrir el inventario y navegar a la pestaña Mapa es el único camino de acceso.

```
Jugador abre el inventario y navega a la pestaña Mapa
  → El juego de navegación se pausa (mismo tratamiento que el resto del inventario)
  → Se muestra el plano del deck actual
      → Por cada habitación: calcular estado → decidir si se dibuja y con qué estilo
      → Por cada puerta de habitación dibujada: dibujar marca según estado
      → Resaltar (pulso) la habitación donde está el jugador
  → Paneo libre dentro de los límites del plano
  → Selector de decks: lista solo los decks conocidos
      → Deck conocido = alguna habitación visitada, o plano en posesión
      → Al elegir otro deck: mismo dibujado, sin resaltado de jugador
  → Cambiar a otra pestaña o cerrar el inventario → se reanuda la navegación
```

La posición del jugador se comunica **resaltando la habitación actual**, no con un icono posicional. El sistema de habitaciones ya sabe cuál está activa; no se rastrea la posición exacta dentro de ella.

---

### Actualización de estados durante el juego

El mapa no vigila nada por su cuenta: los sistemas existentes le informan. Cada actualización se registra en el momento en que ocurre, de modo que el mapa siempre está al día al abrirse.

| Momento del juego | Efecto en el mapa |
|---|---|
| El jugador entra a una habitación (transición de puerta) | Habitación → `Visitada` |
| El jugador aparece en una habitación (carga de deck) | Habitación → `Visitada` |
| Intento de abrir puerta con llave faltante | Puerta → `Bloqueada` (si era `Desconocida`) |
| Desbloqueo con llave | Puerta → `Abierta` |
| Cruce de puerta no bloqueada | Puerta → `Abierta` |
| Recoger el ítem de plano de un deck | Deck → conocido (revela sus habitaciones) |
| Recoger un ítem del mundo | Puede volver `Completada` a su habitación (derivado) |

---

### Persistencia

Los tres registros del mapa — estado de habitaciones, estado de puertas (extendido), planos en posesión — viven en memoria a nivel de sesión, sobreviven a los cambios de escena entre decks, y exponen su contenido completo para integrarse con el sistema de guardado cuando exista, igual que el registro de puertas actual.

Con registros vacíos (partida nueva), todo el mapa está en estado inicial: nada dibujado, todas las puertas desconocidas.

---

### Autoría del plano (flujo del diseñador)

El plano se autorea sobre la escena real y se hornea automáticamente a los datos estáticos:

1. **Calcar siluetas**: en la vista de escena, el diseñador dibuja el polígono de cada habitación directamente sobre el piso real, punto por punto, con un trazado inicial automático desde los límites de la habitación.
2. **Marcar puertas**: cada puerta interactuable recibe una marca de mapa (posición y tamaño de su rectángulo en el plano). Su identificador se toma solo del interactuable — nunca se escribe a mano.
3. **Acomodar el layout**: una ventana de edición 2D con grilla muestra el plano completo del deck y permite mover, rotar y escalar cada habitación en el espacio del plano, partiendo de sus posiciones reales.
4. **Horneado automático**: al guardar la escena, todo se vuelca al documento de datos del deck — siluetas, layout, identificadores de habitaciones, puertas e ítems contenidos. No hay paso manual de exportación; el plano no puede desincronizarse de la escena.

El horneado valida y reporta: habitaciones sin silueta o sin identificador, puertas sin marca, polígonos degenerados.

---

## Intención

> El mapa es la memoria del jugador hecha interfaz. Solo sabe lo que el jugador sabe.

En un survival horror la desorientación es parte del miedo, pero la frustración no. El mapa existe para eliminar la frustración sin matar la desorientación: las primeras veces en un deck se navega a ciegas, y el plano encontrado como ítem es una recompensa real que cambia cómo se juega ese deck.

El estado de las puertas convierte al mapa en una herramienta de planificación: "esa puerta roja del fondo necesita una llave que aún no tengo" es una nota mental que el juego toma por el jugador, exactamente como en los Resident Evil clásicos. El estado `Completada` cierra el circuito con el loot: un vistazo al mapa responde "¿dejé algo atrás?" sin re-explorar.

Que el layout del plano sea independiente de la geometría real protege la legibilidad: los planos de un barco real son densos e ilegibles; el nuestro debe leerse en un segundo bajo presión.

---

## Pendiente

- [ ] Definir la dirección de arte de la pantalla de mapa (paleta, materiales de cada estado, tipografía)
- [x] Definir el input exacto para abrir el mapa: resuelto, es una pestaña del menú de inventario (junto a Ítems y Notas), no un botón dedicado
- [ ] Ubicar los ítems de plano de cada deck en el diseño de niveles
- [ ] Decidir si los POIs narrativos (notas, puzzles) se marcan en el mapa (fuera de alcance en esta iteración)
- [ ] Icono posicional exacto del jugador y minimapa en HUD: descartados por ahora, reevaluar tras playtest
- [ ] Integrar con el sistema de guardado cuando esté disponible

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Transicion entre Decks]] | Ver [[Sistema de Interactuables]] | Ver [[Sistema de Inventario]]
