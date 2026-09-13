# Crimson Draft — Game Design Document
*Versión 0.16 — 1 de septiembre de 2026 — Estado: borrador inicial*

## 1. Overview

**Título:** Crimson Draft
**Tagline:** *"No es un juego de zombis. Es un juego sobre cómo sistemas que existen en la actualidad pueden convertir a la gente en monstruos."*

**Género:** Survival Horror con combate por turnos tipo ATB (híbrido de survival horror clásico + JRPG táctico).
**Plataforma:** PC (Steam).
**Motor:** Unity.
**Equipo:** 5 personas (trabajo final universitario, con intención de continuar más allá de la entrega).

**Logline:** El jugador integra un equipo especial de respuesta marítima de Estados Unidos que aborda el tanker *Marinera* para incautarlo, sin saber con qué se va a encontrar a bordo. A medida que explora el barco, descubre que la tripulación fue víctima de un experimento gubernamental que usa un opioide llamado *krokonil*, distribuido a través de un programa de alimentación estatal, como herramienta de control social. Los infectados —los *Wanderers*— no son zombis sobrenaturales, sino personas en decadencia por hambre y adicción. Lo que el jugador no sabe al principio es que sus propios operadores están expuestos al mismo sistema, y que sostenerlos con vida tiene un costo.

**Audiencia objetivo:** Jugadores de 30-40 años que vivieron la época dorada del survival horror (PS1/PS2) en su juventud. Hoy tienen menos tiempo disponible, buscan experiencias con identidad fuerte o vuelven a rejugar clásicos del género. *(Perfil basado en experiencia/intuición del equipo, pendiente de validación externa — ver sección 11.)*

---

## 2. Pilares de diseño

1. **Horror plausible y político, no sobrenatural.** El miedo viene de un sistema real (control social a través de la alimentación) llevado al extremo, no de monstruos fantásticos.
2. **Vulnerabilidad real del jugador.** Sin niveles, sin experiencia, sin poder que se gane peleando. El único poder viene de gestionar bien los recursos. La dificultad es un rasgo de identidad, no un obstáculo a eliminar.
3. **Peso real de las decisiones.** Permadeath duro: perder un operador es definitivo. Curar de más tiene un costo (exposición a krokonil). Las decisiones importan porque las consecuencias son irreversibles.
4. **Misterio narrativo descubierto jugando.** El jugador no sabe al principio que sus operadores están afectados por el mismo sistema que destruyó al Marinera. La revelación llega a través de un documento in-game llamado "Crimson Draft".
5. **Fidelidad al survival horror clásico (era PS1).** Guardado limitado en habitaciones específicas, sin checkpoints modernos, estética low poly con postprocesado PS1. El juego está diseñado para un público que quiere esa experiencia específica, no una versión suavizada para audiencias casuales.

---

## 3. Fuera de alcance (non-goals)

- **Finales múltiples.** Se reconoce como una expectativa común del género, pero queda fuera del scope del MVP. Puede evaluarse en una versión futura una vez que el core esté sólido.
- Nada más está explícitamente descartado en esta versión del documento — el resto del scope se define por los pilares de diseño de la sección 2 (ej. no hay progresión de personaje por diseño, no por recorte de alcance).

---

## 4. Core Gameplay Loop

**Loop principal:**
Explorar → gestionar recursos limitados → toparse con un soft gate → resolver un puzzle ambiental o encontrar la llave/ítem correspondiente → avanzar a una nueva área del hub → repetir.

Este loop está atravesado por dos sistemas transversales:
- **Decaimiento de operadores:** cada curación acerca a un personaje a un límite (ver sección 5.d). El jugador no lo sabe hasta cierto punto de la historia.
- **Misterio narrativo:** el impulso real para seguir explorando no es solo el loop mecánico, sino la curiosidad por entender qué pasó con la tripulación del Marinera — y, más adelante, qué les está pasando a los propios operadores.

**Verbos del jugador:**
- Explorar el hub (el tanker Marinera) y sus áreas desbloqueables.
- Gestionar inventario y recursos (munición, curativos, ítems clave).
- Resolver puzzles ambientales (estilo Resident Evil clásico).
- Combatir en turnos (ATB) cuando un Wanderer lo golpea.
- Curar operadores desde el inventario, combinando ítems para potenciar el efecto.
- Decidir, implícitamente, cuánto sostener a un operador expuesto al krokonil antes de que se vuelva una carga.

---

## 5. Mecánicas y sistemas

### 5.a Sistema de combate y stats

- **Combate por turnos tipo ATB** (referencia: Chrono Trigger), activado por contacto: los Wanderers patrullan/están en idle en el mapa; si el jugador entra en su radio de detección (que varía según camine o corra), el Wanderer lo persigue; el combate arranca recién cuando el enemigo conecta un golpe.
- **Stats de personaje:** únicamente **HP máximo** y **velocidad** (esta última determina qué tan rápido se llena la barra ATB de cada operador). No hay atributos de ataque/defensa por personaje.
- **Daño:** determinado por el arma usada y la **zona del cuerpo impactada** en el enemigo (sistema de zonas de daño, similar a Resident Evil 4 / Dead Space). `[TODO: definir multiplicadores exactos por zona tras playtesting]`.
- **Sin escalado de poder:** deliberado, ligado al pilar de vulnerabilidad. No hay niveles, XP, ni recompensas de combate que hagan al jugador más fuerte con el tiempo.

### 5.b Sistema de salud (ECG)

- Cada operador muestra su estado de salud como un **ECG estilo Resident Evil clásico**: el jugador no ve un número de vida, sino un estado — **Fine, Caution o Danger**.
- **Umbrales (placeholder, tomados de RE2 remake mientras se define un balance propio):**

  | Estado | % de vida |
  |---|---|
  | Fine | 60%–100% |
  | Caution (amarillo) | 30%–59% |
  | Caution (naranja) | 15%–29% |
  | Danger | 1%–14% |

- La HP máxima de cada operador todavía no está definida (ver sección 11).

### 5.c Curación e inyectores

- Los ítems curativos son **inyectores** (visualmente similares a inyectores de insulina), pensados como estimulantes de combate. Existen dos tipos base: **Verde (G)** y **Rojo (R)**.
- **Combinaciones válidas:** G+G, G+G+G, R+G. No hay otras combinaciones posibles en esta versión del diseño.
- Puede existir un inyector especial equivalente al *First Aid Spray* de RE, de color aún sin definir.
- La curación se aplica directamente desde el inventario, sin curación pasiva fuera de combate más allá de estos ítems (a confirmar si el save room ofrece algo adicional — ver sección 11).

### 5.d Sistema de decaimiento (krokonil)

- Los ítems curativos (inyectores) aumentan la exposición de un personaje al krokonil.
- Un operador curado repetidamente eventualmente se vuelve una carga (mecánicamente, no solo narrativamente) — `[TODO: definir la fórmula/índice de exposición, el umbral, y la penalización concreta: pérdida de velocidad, daño reducido, riesgo de perderlo permanentemente, etc.]`.
- Este sistema es el punto de entrada mecánico del dilema moral central del juego: sostener a un operador vs. dejarlo ir.
- El jugador no conoce este sistema explícitamente hasta encontrar el documento "Crimson Draft", que revela el experimento y sus consecuencias.

### 5.e Party y roles de operador

- Party activo de **3 operadores**, con un cuarto operador que se une/abandona el grupo en momentos específicos de la historia.
- No hay clases ni builds: cada operador tiene HP y velocidad distintos, y porta un **ítem especial único y no intercambiable** (ej. un encendedor, una ganzúa — en la tradición de Resident Evil clásico).
- Si el operador dueño de un ítem especial muere (permadeath), existen ítems secundarios de reemplazo (ej. llaves pequeñas) para que el jugador no quede bloqueado en su progreso.

**Armamento por operador:**

| Operador | Arma primaria | Arma secundaria |
|---|---|---|
| Ethan Miller | Mk18 (5.56) | P229 (9mm) |
| Lilou Vance | MCX Rattler (5.56) | P226 (9mm) |
| Marcus Hale | Benelli M4 (12ga) | P226 (9mm) |
| Darius Mercer | MP7 (4.6) | Five-Seven (5.7) |

Darius tiene deliberadamente el armamento más fuerte del roster, para que el jugador sienta una pérdida de poder real cuando Darius sale del party (ver 5.i para el detalle narrativo/mecánico de por qué Darius entra y sale).

**Valores de daño tentativos (por disparo, contra zona sin blindaje):**

| Arma | Calibre | Operador | Daño (Rip) | Daño (Armor Piercing) |
|---|---|---|---|---|
| Mk18 | 5.56 | Ethan (primaria) | 32 | 24 |
| MCX Rattler | 5.56 (cañón corto) | Lilou (primaria) | 28 | 20 |
| Benelli M4 | 12ga (postas) | Marcus (primaria) | 45 (multi-perdigón, alto a corta distancia) | — (sin munición AP para escopeta en esta versión) |
| MP7 | 4.6×30 | Darius (primaria) | 16 por impacto, alta cadencia | 12 |
| P229 | 9mm | Ethan (secundaria) | 18 | 13 |
| P226 | 9mm | Lilou / Marcus (secundaria) | 18 | 13 |
| Five-Seven | 5.7×28 | Darius (secundaria) | 15 | 11 |

**Daño contra zonas blindadas** (el Rip pierde efectividad drásticamente contra blindaje; el Armor Piercing está pensado justo para esto — con la excepción del Five-Seven, cuyo calibre 5.7×28 es conocido en la realidad por su capacidad de penetración, por lo que conserva una ventaja natural contra blindaje ligero incluso con munición Rip):

| Arma | Daño Rip vs. blindaje | Daño AP vs. blindaje |
|---|---|---|
| Mk18 | 10 | 28 |
| MCX Rattler | 8 | 24 |
| Benelli M4 | 12 (pierde la mayoría de perdigones) | — |
| MP7 | 5 | 14 |
| P229 / P226 | 6 | 15 |
| Five-Seven | 9 | 19 |

**Puntos débiles (ampollas):** un impacto en un punto débil aplica un multiplicador de daño (tentativo: ×1.5 a ×2) independientemente de si esa zona específica tiene blindaje o no.

`[TODO: todos estos valores son tentativos y deben ajustarse con playtesting. Falta definir capacidad de munición por arma, cadencia de disparo/número de balas por volley de cada arma, y el multiplicador exacto de puntos débiles.]`

### 5.f Vida y velocidad de los enemigos (pool aleatorio)

Los Wanderers no tienen HP ni velocidad fijos: en cada encuentro, su **HP máximo** y su **stat de velocidad** se determinan mediante un roll sobre un pool de valores discretos predefinido para su tipo, en vez de un rango continuo. Esto significa que el mismo tipo de Wanderer puede sentirse ligeramente distinto —más o menos resistente, más o menos rápido para actuar en el ATB— cada vez que se lo enfrenta, incluso reentrando a la misma sala.

`[TODO: definir los valores discretos concretos del pool de HP y velocidad por tipo de Wanderer — placeholder tentativo: HP {70, 85, 95, 110, 125, 140}, velocidad {6, 8, 10} en unidades arbitrarias — a validar con el resto del balance de combate]`

### 5.g Poise (estabilidad del enemigo)

Cada Wanderer tiene un contador oculto de **Poise** (estabilidad), con un valor inicial aleatorio al entrar en combate (ej. entre 15 y 30, tentativo). Cada impacto recibido resta una cantidad de Poise dependiente del arma usada (las armas de mayor calibre/impacto restan más).

- Si el Poise llega a 0 **y** el Wanderer está por debajo de un umbral de HP restante (ej. ~40%, tentativo), se produce un **stagger o knockdown**: el Wanderer pierde su próxima acción, o el jugador gana una ventana de disparo más favorable (QTE más fácil o daño garantizado).
- Si el Wanderer todavía conserva mucho HP, el Poise se reinicia sin efecto visible aparente — gastar munición tratando de desestabilizar a un Wanderer "fresco" no funciona todavía. Esto crea una capa de conocimiento táctico que el jugador aprende por repetición, no por tutorial explícito: hay un momento óptimo (cuando el HP ya bajó lo suficiente) para intentar el derribo.
- **Silueta derribada:** al caer (knockdown), la silueta del Wanderer cambia visualmente a una pose derribada. Esto tiene una consecuencia directa en el QTE (5.p/5.q): el patrón de recoil de una ráfaga, calculado para una silueta de pie (donde varios disparos consecutivos pueden ser certeros a la cabeza), ahora "sube" contra una silueta tumbada — los disparos siguientes de una ráfaga alta terminan impactando espalda y/o piernas en vez de la cabeza. Esto le agrega dinamismo táctico: derribar a un Wanderer cambia qué zonas son alcanzables por el resto de la ráfaga, no solo si puede o no devolver el golpe.
- **Multiplicadores ocultos de daño a Poise:**
  - Los disparos a las **piernas** aplican un multiplicador extra de daño a Poise (además de su daño normal a HP) — apuntar bajo es una estrategia válida para forzar un derribo, no solo para reducir movilidad narrativamente.
  - Las balas **Rip** también aplican un multiplicador oculto de daño a Poise, por la liberación de energía propia de ese tipo de munición al impactar — una razón mecánica adicional (más allá del daño a blindaje, ver 5.r) para elegir Rip sobre Armor Piercing en ciertas situaciones tácticas.

`[TODO: definir los valores exactos de resta de Poise por arma, el umbral de HP que habilita el stagger/knockdown, y los multiplicadores exactos de piernas/Rip sobre Poise]`

### 5.h Mercy (tolerancia a la muerte súbita)

Para reforzar el pilar de tensión sostenida por sobre la muerte sorpresiva, el juego aplica dos reglas relacionadas:

- **HP negativo para morir:** un enemigo (o un operador) no muere al llegar exactamente a 0 HP — el daño debe cruzar a negativo. Es una diferencia sutil pero constante en el cálculo de combate.
- **Mercy:** si un golpe que en teoría sería letal no supera un umbral de "overkill" (tentativo: no empujar el HP más allá de -10% del máximo), el personaje u operador queda en 1 HP en lugar de morir. Esto asegura que, salvo golpes especialmente devastadores, el jugador reciba una advertencia (quedar en HP crítico) antes del golpe que realmente puede matar, en vez de perder a un operador de sorpresa sin haber tenido oportunidad de reaccionar.

Esta regla es coherente con el pilar de vulnerabilidad del juego (sección 2): el miedo debe venir de administrar el riesgo sostenido, no de una muerte injusta y repentina — el permadeath (5.i) sigue siendo real y duro, pero Mercy asegura que casi siempre haya una señal de alerta antes de que ocurra.

`[TODO: definir el porcentaje exacto de overkill que activa Mercy, y si existen enemigos/ataques especiales que ignoran esta regla deliberadamente (para mantener cierta amenaza real de muerte súbita en momentos puntuales)]`

### 5.i Permadeath y asistencia oculta al jugador

- **Permadeath duro:** perder un operador en combate o por falta de recursos es definitivo. No hay revivir ni recargar automático.
- **Excepción — Darius Mercer (⚠️ spoiler de diseño / información confidencial de trama):** Darius está scriptado para **no morir nunca** bajo condiciones críticas, y tampoco puede ser curado para evitar su muerte. Esto es intencional: Darius es el **antagonista principal oculto** del juego, y su inmunidad al permadeath debe sentirse como una decisión de diseño consciente (reforzando por qué es tan fuerte en combate) sin que el equipo de arte/narrativa revele la razón real fuera del equipo de diseño. `[Nota: mantener esta información restringida en cualquier material compartido externamente, ej. prensa o playtesters, hasta el momento narrativo del reveal]`
- **Asistencia oculta (inspirada en RE1/RE2 clásicos):** al cargar una partida guardada, la próxima curación del jugador está potenciada y la probabilidad de golpe crítico aumenta. El objetivo es suavizar el fallo sin que el jugador lo perciba conscientemente, para que la dificultad se sienta justa sin dejar de ser incómoda.

### 5.j Guardado

- Sin checkpoints. El jugador solo puede guardar en habitaciones específicas designadas.
- Los guardados son limitados en cantidad y deben gestionarse como un recurso más — decisión de diseño explícitamente defendida como no negociable (ligada al pilar de fidelidad al género clásico).

### 5.k Puzzles ambientales

- Puzzles estilo Resident Evil clásico, integrados en la exploración del hub, como parte del ritmo entre exploración y combate.

### 5.l Inventario

El inventario se organiza en **tres pestañas**:

- **Items:** vista principal — retrato del operador, su ECG (ver 5.b), arma equipada, ítem especial único (ver 5.e), y una **grilla 4x4 individual** (cada operador tiene la suya propia, no se comparte entre el party).
  - **Navegación:** un cursor se mueve celda a celda por la grilla. Los ítems tienen tamaños variables (1x1, 2x2, 3x1, etc.); el cursor se ajusta automáticamente al tamaño del ítem sobre el que está posicionado.
  - **Menú contextual (al hacer Submit):** hasta tres acciones, según el tipo de ítem:
    - **Use / Equip:** equipar si es un arma, o usar si es un ítem consumible o un Key Item que se aplica en un lugar específico del mundo.
    - **Combine:** disponible en todos los ítems, pero solo se ejecuta si existe una receta válida (ver 5.c para las combinaciones de inyectores).
    - **Examine:** revela el nombre real y una descripción detallada del ítem, con una vista del modelo 3D que puede rotarse. Algunos ítems examinables (ej. un maletín) pueden "abrirse" con Submit, descartando el contenedor y revelando el ítem real que tenían dentro.
- **Files:** almacena de forma persistente todos los archivos/documentos encontrados durante la exploración (incluyendo, eventualmente, el documento "Crimson Draft" — ver sección 8).
- **Map:** silueta del mapa del barco, con codificación de color:
  - **Puertas:** gris (sin interactuar), rojo (bloqueada), azul (desbloqueada/libre).
  - **Habitaciones:** amarillo (jugador presente), rojo (quedan ítems por recoger), verde (todos los ítems de la habitación ya recogidos).

`[TODO: definir el límite de espacio para armas/ítems especiales que no entran en la grilla, y si es posible transferir ítems entre las grillas de distintos operadores]`

### 5.m Controles y cámara

- **Personaje visible:** en modo navegación, solo se ve un personaje en pantalla, que actúa como avatar de todo el party (no los tres/cuatro operadores a la vez). `[Explorando: posiblemente seleccionable — el jugador podría elegir qué operador mostrar como "líder" visible, a confirmar]`.
- **Movimiento:** 360 grados, sin restricción de dirección.
- **Cámara:** en picado (top-down/high-angle), fija en rotación (no gira con el jugador). Dependiendo del tamaño de la habitación, puede seguir al jugador o permanecer estática — recurso clásico del survival horror de cámara fija para generar tensión y ángulos ciegos.

### 5.n Navegación y puertas

- El Marinera está dividido en **habitaciones y pasillos**, con **solo una escena activa en render a la vez** (optimización clásica de survival horror por cargas).
- **Transición entre habitaciones:** al interactuar con una puerta se reproduce la animación clásica de la puerta abriéndose en primer plano, y luego el personaje es transportado a la habitación siguiente.
- **Tipos de puerta:**
  - Bloqueada por un puzzle (requiere resolver una interacción específica).
  - Bloqueada con llave (requiere un ítem llave).
  - Bloqueada e inaccesible (permanentemente cerrada, al menos en el estado actual del diseño).
  - Bloqueada de un solo lado (se abre desde un lado del recorrido, no desde el otro — mecanismo clásico de shortcuts en survival horror).
  - Desbloqueada / libre.

Esta codificación de puertas se refleja directamente en el color de las puertas en la tab Map del inventario (ver 5.l): gris (sin interactuar), rojo (bloqueada), azul (desbloqueada/libre). `[TODO: confirmar cómo se distinguen visualmente en el mapa los distintos subtipos de puerta bloqueada — puzzle, llave, inaccesible, un solo lado — o si todos comparten el mismo color rojo]`

### 5.o Estados de juego

El juego alterna entre **dos estados**:

- **Modo Navegación:** exploración libre del hub (ver 5.n), como se describió en las secciones anteriores.
- **Modo Combate:** se dispara cuando un Wanderer golpea al jugador, o cuando el jugador decide dispararle a uno primero durante la navegación.

### 5.p Flujo de combate (detallado)

- **Escenografía:** el combate ocurre en una **escena aditiva y estática**, con el party ubicado en el lateral izquierdo y los enemigos en el lateral derecho — layout clásico de los Final Fantasy de la era 2D/PS1.
- **Sistema ATB:** cada entidad en el campo de batalla (operadores y enemigos) tiene un contador ATB interno que se llena a una velocidad determinada por su stat de velocidad (ver 5.a).
- **Acciones del operador (al llenarse su ATB):**
  - **Disparar:** envía la acción a una cola de ejecución.
  - **Usar ítems:** despliega el inventario en combate, permitiendo recargar, equipar otra arma, o usar un ítem curativo.
- **Resolución de un disparo (cuando la acción llega al frente de la cola):**
  1. El jugador selecciona la cantidad de balas a disparar, limitada por el máximo del arma equipada.
  2. Se selecciona el enemigo objetivo.
  3. **QTE de puntería:** se muestra la silueta del enemigo con un eje horizontal que rebota entre los bordes de la pantalla; al presionar Submit, se fija la posición X. Luego se repite el proceso con un eje vertical, fijando la posición Y.
  4. El primer disparo del volley impacta en esa posición (X, Y) sobre la silueta del enemigo (ver sistema de zonas de daño, 5.a).
  5. Los disparos siguientes del mismo volley se posicionan según el **patrón de recoil** propio del arma usada.

`[TODO: definir los patrones de recoil por arma; definir qué pasa si el jugador falla el QTE por completo (¿el disparo no impacta ninguna zona, o cae en una posición aleatoria?); definir el comportamiento del ATB/acciones de los Wanderers en combate]`

### 5.q Dificultad del QTE según estado del operador

El QTE de puntería (5.p) no es estático: su dificultad escala según la salud del operador que dispara, reforzando el pilar de vulnerabilidad (sección 2) de forma directamente jugable:

- **Ejes más erráticos:** a menor salud, el movimiento de los ejes horizontal/vertical se vuelve menos predecible.
- **Visión de túnel:** la silueta del enemigo deja de verse completa.
- **Parpadeos / viñeta negra:** interfieren visualmente con la coordinación del jugador.

`[Posible extensión: parte de estos modificadores podría depender no solo del HP, sino del índice de exposición al krokonil del operador — a confirmar cuando se defina la fórmula de exposición, sección 5.d]`

Este diseño convierte el estado Fine/Caution/Danger (5.b) en algo con consecuencia mecánica directa en combate, no solo un indicador visual.

### 5.r Tipos de bala y zonas de armadura

Para profundizar el QTE como diferenciador del juego (más allá de "apuntar a la cabeza"):

- **Tipos de munición:** al menos dos definidos — **Armor Piercing** y **Rip** (expansivas). El daño de cada disparo depende de la combinación bala elegida + zona impactada.
- **Enemigos con armadura:** algunos Wanderers tienen zonas del cuerpo protegidas en su silueta; una bala Rip contra una zona blindada rinde menos que una Armor Piercing, y viceversa contra tejido expuesto.
- **Puntos débiles (ampollas):** ciertos enemigos presentan ampollas visibles como puntos débiles en su silueta, en zonas distintas a la cabeza — esto evita que la estrategia óptima sea siempre "apuntar a la cabeza" y obliga a leer la silueta de cada tipo de Wanderer antes de disparar.

`[TODO: definir la tabla completa de multiplicadores (tipo de bala × zona × ¿armadura o ampolla?); definir cuántos tipos de bala existen en total y su disponibilidad/rareza como recurso]`

### 5.s Animation Lock y Synced Shoot

- **Animation Lock:** mientras se reproduce la animación de disparo de un arma, el jugador queda bloqueado (no puede realizar otras acciones). La duración de ese bloqueo está ligada a la cadencia de fuego del arma: las armas de **alta cadencia** (ej. el MP7 de Darius) encadenan sus animaciones de disparo sin demora entre tiro y tiro, lo que le permite al jugador **liberarse antes** del Animation Lock. Las armas de **baja cadencia** (ej. la Benelli M4 de Marcus) mantienen al jugador bloqueado por más tiempo por cada disparo.
- **Problema que resuelve Synced Shoot:** si el jugador juega exclusivamente con operadores de armas de baja cadencia, pasa proporcionalmente más tiempo bloqueado sin poder actuar, lo que puede sentirse punitivo frente a enemigos que actúan mientras tanto.
- **Synced Shoot (nueva acción):** funciona de forma similar a las *techs* combinadas de Chrono Trigger. El jugador puede marcar a varios operadores con Synced Shoot mientras esperan su turno; cuando cualquiera de los operadores marcados ejecuta Shoot, **todos los operadores marcados disparan juntos**, resolviendo un único QTE compartido en vez de uno por operador.
- **Efecto táctico:** permite consumir el ATB acumulado de varios operadores a la vez, a cambio de una descarga de daño concentrado o fuego de supresión — útil contra enemigos fuertes donde vale la pena sacrificar la cadencia individual de disparos por un golpe grande y coordinado.
- **Qué pasa si muere el operador que falta:** los operadores marcados quedan bloqueados esperando a que **otro**, sin marcar, ejecute Shoot y dispare al grupo entero. Eso deja un punto de falla: si ese operador libre muere antes de llegar a hacerlo (o mueren suficientes operadores marcados como para que no quede ninguno sin marcar), nadie puede completar la orden que el grupo está esperando. Sin ningún tipo de resolución, el combate quedaría trabado indefinidamente — los operadores marcados nunca vuelven a estar disponibles, pero tampoco están muertos, así que el combate no termina ni en victoria ni en derrota.
- **Liberación automática:** en cuanto deja de haber al menos un operador vivo y sin marcar, el juego libera automáticamente a los operadores marcados que queden — vuelven a estar disponibles para actuar con normalidad, como si nunca los hubieran marcado. El Synced Shoot que se estaba armando se cancela sin infligir daño, pero el combate nunca se traba esperando una orden que no puede llegar.
- **Los enemigos evitan (parcialmente) al operador que falta:** perder un Synced Shoot ya armado sigue siendo un costo para el jugador — el ATB que invirtió en coordinarlo se pierde. Para que la liberación automática sea una salvaguarda poco frecuente y no la forma habitual en la que termina un Synced Shoot, mientras haya operadores marcados esperando, los enemigos tienen **menos probabilidad de elegir como blanco al operador todavía libre** para disparar — no cero: sigue pudiendo ser atacado, y sigue pudiendo morir, sólo que ya no le toca la misma proporción de ataques que a cualquier otro operador presente.

`[TODO: definir si Synced Shoot consume munición de todos los operadores marcados o solo de uno; definir el límite de operadores que pueden marcarse a la vez (¿hasta 3? ¿hasta 4 con Darius en el party?); definir cómo se resuelve el daño del QTE compartido — ¿todos apuntan al mismo punto, o cada arma aporta su propio patrón de recoil sobre la misma posición base?]`

`[TODO: la reducción de probabilidad de ser blanco para el operador libre es un valor placeholder sin testear. Falta definir con playtesting qué frecuencia de "Synced Shoot cancelado por liberación automática" se considera aceptable, y ajustar el valor en función de eso — hoy prioriza no romper el combate por sobre preservar la sensación de riesgo real de la mecánica.]`

---

## 6. Mundo / Niveles

**Estructura:** hub central con áreas que se van desbloqueando progresivamente, al estilo Resident Evil clásico — el tanker *Marinera* como escenario único, dividido en zonas conectadas entre sí que se abren a medida que el jugador resuelve soft gates y puzzles.

`[TODO: mapear el layout específico del Marinera — cantidad de zonas, orden de desbloqueo, ubicación de save rooms]`

---

## 7. Progresión y economía

- **No hay progresión de personaje** (por diseño — ver pilar de vulnerabilidad). El "crecimiento" del jugador es de habilidad y gestión, no de poder del personaje.
- **Economía de recursos:** munición, ítems curativos, ítems de combinación y ítems clave/especiales por operador son los recursos centrales a administrar.
- `[TODO: definir tasas de drop/hallazgo de recursos y su balance general — pendiente de playtesting]`

---

## 8. Narrativa (breve)

El jugador integra un equipo de respuesta marítima que aborda el *Marinera* para incautarlo. A través de archivos y pistas encontradas durante la exploración, reconstruye qué le pasó a la tripulación: fueron víctimas de un programa de alimentación estatal que distribuye *krokonil*, un opioide más potente que la morfina que causa necrosis, gangrena, y decadencia física — llevando a algunos a la desesperación y el canibalismo (los *Wanderers*). El giro central llega con el hallazgo del documento "Crimson Draft" (que da nombre al juego), que revela que el mismo sistema está afectando a los propios operadores del jugador. A partir de ahí, el misterio deja de ser sobre el Marinera y se vuelve personal: el jugador debe decidir cuánto sostener a los suyos antes de que sea demasiado tarde.

**Referentes narrativos/mecánicos:** Resident Evil (horror y estructura), Metal Gear (contexto bélico/institucional), Bioshock (engaño hacia el jugador), Sweet Home (combate por turnos + permadeath, origen del género), Signalis (caso de éxito con origen similar: trabajo final universitario devenido en juego reconocido), Vultures Scavengers of Death (hibridación survival horror + TRPG bien recibida).

### 8.a Roster de operadores

**Ethan Miller** — 30 años (22/02/1996) — *Boarding Specialist (MSRT)*
Ideal: *"I'm loyal to nothing, except the law."*
Disciplinado, íntegro, el primero en entrar y el último en retirarse. Hijo de un suboficial de la Marina; estuvo cerca de presentarse a las pruebas de los SEALs pero no dio el paso, por una duda profunda sobre operar en un entorno donde las decisiones tácticas pueden chocar con principios morales personales. Encontró en el MSRT un marco más claro entre lo correcto y lo necesario. Confía plenamente en el sistema que lo entrena y en el propósito de su labor — esa confianza es su mayor fortaleza operativa, pero también reduce su capacidad de ver fallas estructurales en ese mismo sistema.

**Darius Mercer** — 40 años (25/04/1986) — *Team Leader (SEALs) — estrategia, comando táctico, operaciones encubiertas*
Ideal: *"Power isn't given. It's taken… and kept."*
Líder nato, estratega excepcional, presencia dominante. Creció en un entorno de desigualdad social que canalizó en una obsesión por el poder y el control más que en rencor hacia el sistema. Ascendió meteóricamente en la policía y luego en el ejército hasta liderar un equipo SEAL de élite. Único sobreviviente de una operación donde su equipo fue aniquilado — lo interpretó no como culpa, sino como pérdida de control operativo. Reclutado después por la CIA para liderar una nueva unidad reducida bajo sus propios estándares; no ve a sus subordinados como pares, sino como piezas funcionales de un sistema que debe operar con precisión. No busca estabilidad. Busca dominio.

**Lilou Vance** — 26 años (10/02/2000) — *Recon Sniper (SEALs) — exploración avanzada, infiltración, eliminación a larga distancia*
Ideal: *"I was born nowhere… and I'll die somewhere else."*
Hija de diplomáticos, creció mudándose constantemente entre países europeos (España, Francia, Reino Unido, Alemania, Italia), lo que la volvió altamente adaptable pero emocionalmente distante — nunca desarrolló un sentido de pertenencia ni vínculos duraderos. Políglota por necesidad y luego por interés propio. Ingresó al ejército casi por imposición familiar, pero encontró ahí, por primera vez, una estructura estable; se destacó y pasó a los SEALs como francotiradora de élite. Confianza que roza la arrogancia en el campo; aprendió a adaptarse, pero no a quedarse.

**Marcus Hale** — 34 años (06/06/1992) — *Combat Engineer (SEALs) — demoliciones, apertura de estructuras, soporte táctico pesado*
Ideal: *"People think about their future. I think about destiny."*
Creció rodeado de maquinaria pesada en el suroeste de EE.UU. (su padre era mecánico), lo que le dio una comprensión casi intuitiva de sistemas mecánicos y estructurales. Estudió ingeniería y se especializó en demolición controlada, entendiendo la destrucción como conocimiento estructural, no como caos. Antes de enlistarse tuvo una experiencia que interpreta como un "llamado" no religioso — la certeza de que su rol es proteger a otros, y de que en algún momento alguien va a necesitar que él esté ahí. Apariencia intimidante; rol real dentro del equipo es el de protector.

`[TODO: definir el quinto operador — el que se une/abandona el party en momentos específicos según la sección 5.e — y confirmar cuál de los cuatro anteriores conforma el party activo de 3 vs. el rotativo]`

### 8.b NPCs clave y el incidente del Marinera

**Adrian Volkov** — 36 años (02/09/1990) — *Infiltración / Inteligencia encubierta / Observación operativa (cobertura: chef del Marinera)*
Ideal: *"Remember, no politics. Issues confuse people."*
Agente encubierto de la CIA, de padre ruso y madre estadounidense. Infiltrado en el barco bajo identidad de chef, con la misión de monitorear el laboratorio y reportar información crítica sin exponerse. Pragmático, escurridizo, sin lealtades absolutas — sobrevive en las sombras, priorizando su propia continuidad operativa por sobre la acción directa.

**Vanessa Stoian** — 32 años (03/02/1994) — *Manipulación social / Control interpersonal*
Ideal: *"Remember, no politics. Issues confuse people."*
Mujer ambiciosa y manipuladora que se infiltra en el barco a través de una relación con Adrian, buscando constantemente el control de la dinámica. No tolera la incertidumbre ni la pérdida de control — esa necesidad la lleva a escalar un conflicto que termina desatando el incidente central del juego.

**El incidente (resumen):**
Vanessa ingresó al Marinera a través de su relación con Adrian. Cuando la distancia emocional y las ausencias de él crecieron, saboteó deliberadamente los suministros de comida del barco (~90% de las reservas perdidas) para recuperar su atención. La relación siguió deteriorándose; Vanessa, buscando información y control, sedujo al capitán del barco, quien —ya no del todo en sus cabales— reveló detalles sensibles sobre el laboratorio. Con esa información, Vanessa encontró un ascensor de acceso restringido a las instalaciones del laboratorio y confrontó a Adrian con lo descubierto. Adrian intentó contener la situación y estableció un límite, negándose a continuar la relación bajo manipulación.

Ante la pérdida de control, Vanessa se autoadministró una variante experimental del krokonil desarrollada en el laboratorio, diseñada con propiedades de regeneración biológica basadas en material genético reptiliano. Inicialmente ganó regeneración acelerada y una influencia magnética sobre la tripulación ya expuesta a dosis menores de la sustancia (que respondía con atracción, protección y subordinación), consolidándose en una posición dominante. Sin embargo, la administración reiterada degradó progresivamente su razonamiento, hasta quedar dominada por impulsos primarios — el núcleo de una estructura basada en instinto, no en estrategia.

Adrian, al identificar la pérdida total de contención, intentó activar un protocolo de auto-hundimiento (*scuttle*) del barco para contener el incidente, pero fue interceptado por Vanessa antes de completarlo. Eso lo obligó a abortar la misión y ocultarse dentro del barco. A partir de ahí, el incidente dejó de ser recuperable bajo parámetros operativos estándar, derivando en el colapso biológico y conductual de toda la tripulación que el jugador encuentra al abordar el Marinera.

`[TODO: definir el rol jugable/narrativo de Vanessa y Adrian dentro del MVP — ¿son encontrables como parte de la exploración, boss fights, o solo se conocen a través de archivos/lore, como el resto del trasfondo del Marinera?]`

---

## 9. Arte y audio (referencias)

- **Estética visual:** low poly con postprocesado estilo PS1 (referencia local: *Iris Dissolution*, estética similar aunque tono distinto).
- **Tono:** tenso, opresivo, anclado en lo plausible más que en lo sobrenatural.
- `[TODO: referencias de audio/sonido ambiente — no se discutieron todavía]`

---

## 10. Especificaciones técnicas

- **Motor:** Unity.
- **Plataforma:** PC, distribución vía Steam.
- **Control de versiones:** `[TODO: no especificado — recomendable Git dado el tamaño de equipo]`.
- **Escala de equipo:** 5 personas (Franco Bulgarella — PM, Enrique Aebi — Game Designer, Lorenzo Lodigiani — Arte 2D/Guion/Level Design, Tomás Bermúdez — Arte 3D, David — Director de Proyecto). El principal cuello de botella actual es la producción de assets 3D en el estilo PS1 elegido; se está buscando ayuda externa para esto.
- **Contexto de producción:** proyecto de trabajo final de carrera (Universidad Argentina de la Empresa), con milestone de fin de año para entregar un MVP.

---

## 11. Supuestos / a confirmar

- El umbral y la penalización exacta del sistema de exposición al krokonil (cuánto es "demasiada curación", qué le pasa mecánicamente a un operador afectado) no está definido — es el sistema más importante del juego y probablemente merece su propio documento de diseño detallado.
- No está confirmado si el save room ofrece algo de curación pasiva, o si toda la curación pasa exclusivamente por ítems de inventario.
- El layout específico del hub (Marinera) — cantidad de zonas, orden de desbloqueo — no está mapeado todavía.
- Multiplicadores de daño por zona de impacto: no definidos.
- Economía de recursos (tasas de drop, balance de escasez): no definida, pendiente de playtesting.
- El perfil de público objetivo (30-40 años) está basado en intuición del equipo, no en investigación formal — el plan de validación es compartir avances en comunidades/foros de survival horror.
- Modelo económico: premium con DLC cosmético/de conveniencia (desbloqueo de recompensas, vestimenta única, armas especiales o con munición infinita) — sostenible solo si el proyecto continúa más allá de la entrega final de la materia.

---

## 12. Changelog

- **v0.16 — 01/09/2026:** Documentado el edge case de Synced Shoot donde el operador libre para gatillarlo podía morir antes de hacerlo, trabando el combate: se agregó liberación automática de los operadores marcados cuando ya no queda nadie sin marcar que pueda disparar, y una reducción (no eliminación) de la probabilidad de que los enemigos ataquen al operador todavía libre mientras el grupo está pendiente, para que esa liberación sea una salvaguarda poco frecuente en vez de la forma habitual en la que termina la mecánica.
- **v0.15 — 16/07/2026:** Corregida la lógica del Animation Lock — las armas de alta cadencia permiten liberarse antes del bloqueo (no al revés como se había registrado inicialmente).
- **v0.14 — 16/07/2026:** Agregado el sistema de Animation Lock (duración de animación ligada a cadencia de fuego) y la acción Synced Shoot (disparo coordinado entre operadores marcados, inspirado en las techs de Chrono Trigger).
- **v0.13 — 16/07/2026:** Agregado el cambio de silueta al derribar a un Wanderer (afecta dónde impactan los disparos siguientes de una ráfaga) y los multiplicadores ocultos de daño a Poise por disparos a piernas y por munición Rip.
- **v0.12 — 16/07/2026:** Agregados valores de daño tentativos por arma (Rip/Armor Piercing, contra blindaje y sin blindaje), el sistema de pool aleatorio de HP/velocidad de los Wanderers, y dos mecánicas nuevas: Poise (estabilidad oculta que habilita stagger/knockdown) y Mercy (evita la muerte súbita sin advertencia).
- **v0.11 — 16/07/2026:** Agregado el armamento por operador (tabla de armas primaria/secundaria) y marcada la excepción de Darius Mercer al permadeath, ligada a su rol de antagonista oculto (contenido marcado como spoiler/confidencial).
- **v0.10 — 16/07/2026:** Agregada la dificultad escalable del QTE según la salud del operador (ejes erráticos, visión de túnel, viñeta), y el sistema de tipos de bala (Armor Piercing, Rip) contra zonas de armadura y puntos débiles tipo ampolla.
- **v0.9 — 16/07/2026:** Agregados los dos estados de juego (Navegación/Combate) y el flujo de combate detallado (escena aditiva estilo FF clásico, ATB, cola de disparo, QTE de puntería en dos ejes).
- **v0.8 — 16/07/2026:** Agregado el sistema de navegación entre habitaciones y los 5 tipos de puerta (puzzle, llave, inaccesible, un solo lado, desbloqueada).
- **v0.7 — 16/07/2026:** Agregado el sistema de controles y cámara (avatar único de party en navegación, movimiento 360°, cámara fija en picado que puede o no seguir al jugador).
- **v0.6 — 16/07/2026:** Detallada la navegación de la grilla de ítems (tamaños variables, cursor adaptativo) y el menú contextual Use/Equip — Combine — Examine, incluyendo el mecanismo de contenedores tipo maletín.
- **v0.5 — 16/07/2026:** Agregado el sistema de inventario (tabs Items/Files/Map, codificación de color del mapa).
- **v0.4 — 16/07/2026:** Agregados los NPCs clave (Adrian Volkov, Vanessa Stoian) y el resumen del incidente que originó el colapso de la tripulación del Marinera.
- **v0.3 — 16/07/2026:** Agregado el roster de operadores (Ethan Miller, Darius Mercer, Lilou Vance, Marcus Hale) con biografías, ideales y rol dentro del equipo.
- **v0.2 — 16/07/2026:** Detallado el sistema de salud (ECG con estados Fine/Caution/Danger, umbrales placeholder tomados de RE2 remake) y el sistema de curación (inyectores Verde/Rojo, combinaciones válidas G+G, G+G+G, R+G, inyector especial sin color definido).
- **v0.1 — 16/07/2026:** Primera versión del documento, construida a partir de una sesión de entrevista de marketing y una entrevista de diseño complementaria. Cubre overview, pilares, core loop, sistemas de combate/decaimiento/permadeath, estructura de mundo, narrativa y especificaciones técnicas iniciales.
