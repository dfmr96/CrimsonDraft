# Crimson Draft — Game Design Bible

**Versión:** Pre-producción — v2
**Última actualización:** 2026-07-15
**Fuente canon narrativa:** [[Ideas High Concept _ GDD]] (Design/References)

---

## 1. Overview / Concepto del Juego

**Título:** Crimson Draft (Calado Rojo)
**Tagline:** Una mezcla del Survival Horror de los *Resident Evil*, el combate de los *JRPG* clásicos, y la narrativa militar de *Metal Gear*, con el engaño de *Bioshock*.
**Género:** Tactical Survival Horror
**Plataforma:** PC (Steam) · Unity
**Público objetivo:** Jugadores adultos de ~35 años, familiarizados con survival horror y JRPGs de los 90s, con apetito por narrativas de thriller psicológico y mecánicas que cuestionan las expectativas del jugador.
**Estilo visual y sonoro:** Top-down, pixel art. Estética retro de consolas de 4ta/5ta generación.

### Cuatro lecturas del título
1. Discrepancia técnica en el calado del buque — la anomalía que desencadena la misión
2. La línea roja en el agua — señal visible de un barco demasiado pesado
3. El borrador (draft) de una guerra — lo que ocurre en el barco es solo el ensayo
4. Sangre y destrucción — la consecuencia final

### Identidad
> "Survival táctico con tragedia geopolítica. No es 'zombis en barco'. Es horror político con consecuencias irreversibles."

**Mensaje central:** "No es izquierda ni derecha. Es poder contra población."

**Tema:** Dependencia como herramienta de poder. El verdadero monstruo no es el infectado — es el sistema que lo diseñó.

### Sinopsis

*Atención: los acontecimientos y cualquier similitud con hechos del mundo real son mera coincidencia.*

Ante la crisis de narcotráfico marítimo, Estados Unidos endurece sus sanciones e inicia un bloqueo naval en el **Mar Caribe**. El ***Marinera*** (ex *Bella I*), un tanquero VLCC de la flota fantasma rusa, lleva semanas burlando a la Guardia Costera. Cuando la presión aumenta, Rusia pinta la bandera rusa en el casco, lo registra en Moscú y envía un submarino a escoltarlo — abordarlo sería un incidente diplomático. Pero el calado del buque no coincide con su manifiesto de "lastre vacío": algo pesa dentro que nadie declara.

La operación recibe el nombre en clave **Crimson Draft**. Un escuadrón del Maritime Special Response Team (MSRT) inserta por aire. El destructor de apoyo se retira casi de inmediato — el submarino ruso está demasiado cerca y nadie quiere un incidente. El escuadrón queda solo en cubierta, sin extracción, en medio del Atlántico. Lo que encuentran dentro no es contrabando.

> **Nota de consistencia (v2):** la versión anterior de este documento situaba el incidente en el Mar Negro. Se corrige a Caribe/Atlántico para alinear con el HC y con [[El Marinera]] (cronología: persecución desde el 20 dic 2025, abordaje el 7 ene 2026).

Ver [[El Marinera]] para cronología completa del incidente y contenido real del buque.

### Experiencia emocional buscada
- **Ansiedad sostenida:** no hay alivio. Cada combate deja al jugador con menos. La tensión se acumula, no se resuelve.
- **Paranoia:** ¿en quién confiar? El mejor operador del equipo miente. El ítem que salva, destruye.
- **Impotencia progresiva:** el jugador no se vuelve más fuerte, se vuelve más frágil.
- **Duelo:** perder un operador es perder un personaje con diálogos, poder en combate e identidad.
- **Incomodidad moral:** al descubrir qué es el ítem que lo mantiene a salvo, todas las decisiones previas se recontextualizan.
- **Rabia política:** EE.UU. y Rusia, enemigos públicos, socios privados. El equipo nunca fue una misión — fue un experimento.

---

## 2. Pilares de Diseño y Alcance

### 2.1 Los tres ejes
- **Lo Táctico:** cada acción de combate requiere lectura del enemigo, selección de munición y ejecución del QTE. No hay ataques genéricos ni automáticos.
- **Lo Survival:** los recursos son finitos y justificados narrativamente. No hay reabastecimiento mágico.
- **El Horror:** físico y político, no sobrenatural ni biológico. Los enemigos son humanos destruidos; el sistema que los destruye es el verdadero antagonista.

### 2.2 Los 6 pilares de diseño
Todo sistema nuevo debe poder justificarse contra al menos uno. Si viola alguno, no pertenece al juego.

**Pilar 1 — El combate es costo, no recompensa.** Combatir gasta recursos irrecuperables (munición, salud, integridad del party). La progresión es de desgaste, no de empoderamiento. Ver [[Tactical Survival Horror]].

**Pilar 2 — Agencia bajo presión.** El jugador siempre tiene control, pero ese control se degrada con el estado físico del operador. Los ataques se vuelven imprecisos y el QTE más difícil de acertar — nunca se le quita el control, se le hace más difícil ejercerlo. Ver [[Distractores Visuales]].

**Pilar 3 — Consecuencias irreversibles.** No hay revive, no hay recarga mágica. Los personajes mueren permanentemente, la munición gastada no vuelve, la exposición a Krokonil no se revierte. Ver [[Mecanicas de Supervivencia]].

**Pilar 4 — Horror tangible.** Los enemigos son humanos, no monstruos. Las armas son reales, no dependen de stats. El horror viene de lo que podría pasar por una mala decisión. Ver [[Krokonil]] · [[El Marinera]].

**Pilar 5 — Información como recurso.** La lectura visual del enemigo (protección, fase de deterioro, zona expuesta) es una habilidad del jugador, no un stat. El jugador que lee bien gasta menos recursos. Ver [[Diseño de Combate y Armas]].

**Pilar 6 — El juego no acusa, siembra.** El horror político no se explica en diálogos, se siembra en el entorno. Cada documento encontrado, cada ausencia, cada coincidencia es una pregunta sin respuesta. El juego nunca señala al culpable directamente. Ver [[La Conspiracion]] · [[Documentos del Marinera]].

### 2.3 USPs — Experiencia y narrativa
1. El combate te debilita, no te fortalece — sin tiendas, sin refuerzos, sin segunda oportunidad.
2. Experiencia ludonarrativa: el ítem "anti-muerte" que el jugador usa libremente es, en la ficción, la misma droga que destruyó a la tripulación del Marinera.
3. Sin barra de vida, doble condición de muerte: ECG (ritmo/BPM) y presión arterial, leídos por el jugador.
4. El mejor operador del equipo trabaja para el enemigo — dependencia mecánica real hacia él antes de la revelación.
5. Horror sin elementos sobrenaturales: los enemigos son humanos en colapso neuroquímico.
6. El sistema de guardado es parte de la historia: transmisiones por telégrafo Morse que resultan ser reales.

### 2.4 USPs — Diseño y producción
1. Navegación top-down con paredes inclinadas (solución Metal Gear MSX2 / Resident Evil Gaiden) — puertas en todos los ejes, backtracking real y diseño de nivel no lineal.
2. Combate en vista lateral con resolución elevada — estética diferenciada exploración/combate sin sobrecargar al equipo de arte con animación.
3. Cinemáticas estilo cómic animado por capas (inspirado en MGS: Peace Walker) — producción económica, alto impacto visual.
4. Permadeath que no termina el juego (inspirado en Sweet Home, 1989) — presión acumulada, no pantalla de fin.
5. Encuentros visibles, no aleatorios — el jugador siempre tiene información antes de comprometerse.
6. Sistema de combate híbrido: libertad del jugador (cualquier comando, cualquier momento) + presión de enemigos (timers propios) vía cooldowns individuales por acción, sin turnos fijos.

### 2.5 Non-goals / Riesgos identificados
- El uso de nombres/hechos reales puede ser controvertido — mitigar con disclaimer explícito.
- La salud sin barra de vida puede confundir si no se comunica bien (riesgo de muerte "sin explicación aparente").
- El permadeath restringe el branching de diálogo — requiere diálogos genéricos o branching real por personaje.
- La percepción del espacio en top-down puede ser difícil de lograr con backtracking real.
- **Fuera de alcance explícito:** encuentros aleatorios, barra de vida numérica visible, revive/segunda oportunidad, monstruos sobrenaturales o biológicos.

---

## 3. Core Gameplay Loop

### 3.1 Loop macro
Explorar el Marinera → gestionar recursos (munición, salud, ítems) → decidir si combatir o evitar encuentros visibles → investigar documentos y resolver puzzles → guardar progreso vía telégrafo Morse → extraer al equipo con vida.

### 3.2 Objetivos del jugador
- **Corto plazo:** evitar encuentros con enemigos, gestionar recursos, mantener al equipo con vida, explorar el Marinera.
- **Mediano plazo:** investigar qué le sucedió a la tripulación, resolver puzzles, desbloquear zonas inaccesibles.
- **Largo plazo:** transmitir los hallazgos al exterior, extraer al equipo con vida.

---

## 4. Mecánicas y Sistemas

### 4.1 Loop de combate en tiempo real
El combate ocurre en tiempo real. No hay turnos ni ATB fijo por prioridad de stats. Cada acción *ocupa* al personaje por una duración fija (cooldown); los enemigos atacan en sus propios timers independientes. El jugador controla un personaje a la vez; los demás quedan en idle. Ver [[Sistema de Combate en Tiempo Real]] · [[Sistema ATB de Combate]] para el flujo completo, estados de personaje y comportamiento de enemigos.

### 4.2 QTE bidimensional
El disparo se resuelve con un minijuego de dos ejes: la barra vertical oscila y el jugador fija Y (Confirm), luego la barra horizontal oscila y fija X (Confirm). La intersección es el punto de intención, que pasa por las 3 capas de dispersión. La velocidad de la barra varía por arma; no se puede cancelar una vez iniciado.

### 4.3 Dispersión y apuntado
Tres capas independientes transforman el punto de intención en punto de impacto:
- **L1 (HP):** radio proporcional al daño recibido, solo en el primer disparo de cada ráfaga.
- **L2 (mecánica):** desviación aleatoria fija del arma, siempre presente.
- **L3 (recoil):** patrón predefinido por arma desde el segundo disparo — aprendible, no eliminable.

Ver [[Sistema de Dispersion y Apuntado]] · [[Referencia GD - Dispersion y Recoil]] para fórmulas exactas y tablas por arma.

### 4.4 Distractores visuales
Seis canales de distracción se activan progresivamente según el HP del operador activo (vibración de QTE, screen shake, viñeta de sangre, ruido estático, parpadeo de silueta enemiga). Ver [[Distractores Visuales]] para umbrales exactos.

### 4.5 Armadura por capas
La protección enemiga es geometría visible, no un stat numérico. Tipos: casco militar, chaleco torso, chaleco+esternón, hombro, placas balísticas — 8 configuraciones catalogadas. Ver [[Diseño de Combate y Armas]].

### 4.6 Sistema de munición
Solo la munición 9mm tiene dos variantes tácticas (RIP vs FMJ) con multiplicadores de daño distintos contra carne/chaleco/placas. La elección ocurre durante la recarga. Ver [[Diseño de Combate y Armas]] · [[Sistema de Conteo de Balas por Disparo]].

### 4.7 Armas y patrones de recoil
Cada arma tiene identidad mecánica única vía su patrón de recoil, aprendible pero nunca eliminable, con espejado según mano dominante del operador. Ver [[Sistema de Dispersion y Apuntado]] · [[Referencia GD - Capas de Recoil]].

### 4.8 Detección de impacto y daño por zona
El punto de impacto se resuelve contra una textura secundaria de color por píxel (cabeza, torso, extremidades, bordes de silueta), evitando la imprecisión de colliders sobre pixel art. Ver [[Sistema de Deteccion de Impacto]] · [[Sistema de Feedback de Daño de Disparo]].

### 4.9 Salud y presión arterial
Dos recursos independientes, dos vías de muerte: HP (impactos + hemorragia) y presión arterial (hemorragia, shock si sistólica ≤ 40). Sin barra de vida visible — el jugador lee el ECG (color + BPM + presión). Ver [[Sistema de Salud]] · [[Sistema ECG de Operadores]].

### 4.10 Krokonil
Anti-permadeath con precio permanente: congela HP/presión sin penalización por 4-5 turnos, pero suma exposición acumulativa irreversible que degrada puntería y signos vitales pasado un umbral. Revelación narrativa clave del Acto III. Ver [[Krokonil]].

### 4.11 Inventario
Grilla 4×4 por operador, ítems con dimensiones físicas rotables. El inventario se pierde con el operador si muere. Ver [[Sistema de Inventario]] · [[Sistema de Item Socket]] · [[Sistema de Combinacion de Items]].

### 4.12 Recursos y escasez
Recursos finitos a nivel global, con escasez progresiva por acto (completos en Acto I, casi nulos en Acto V). Ver [[Mecanicas de Supervivencia]].

### 4.13 Detección e IA enemiga
Encuentros visibles, activados por proximidad o decisión del jugador — nunca aleatorios (fórmula Resident Evil Gaiden). Detección en 3 modos priorizados: proximidad (con histéresis), sonido (según velocidad del jugador) y visión (FOV + raycast en 2 pasadas). Ver [[Sistema de IA de Navegacion]] · [[Sistema de Ataque de Enemigos]].

### 4.14 Interactuables y puzzles
Objetos interactuables detectados por physics casting, con controladores UI dedicados por tipo (contenedores, radios, etc.). Ver [[Sistema de Interactuables]].

---

## 5. Exploración

### 5.1 Movimiento
Movimiento 4-direccional cardinal, sin diagonales ni botón de correr — el input análogo se cuantiza al eje dominante. Una sola velocidad refuerza la tensión de survival. Ver [[Sistema de Movimiento]].

### 5.2 Navegación top-down y paredes inclinadas
Solución adoptada de Metal Gear (MSX2) y Resident Evil Gaiden: paredes con inclinación que permiten puertas en todos los ejes, habilitando backtracking real, atajos y diseño de nivel no lineal — crítico para un survival horror y descartando la limitación de pasillos verticales de JRPGs como Chrono Trigger.

### 5.3 Guardado — Telégrafo Morse
Las zonas de guardado son salas con telégrafo radio-telegráfico. Guardar = transmitir un mensaje en Morse, siempre el mismo patrón — que resulta ser un mensaje oculto. En el final, Mateo envía el mismo mensaje por el mismo telégrafo; el guardado deja de ser mecánico y se vuelve acto narrativo. Ver [[Mecanicas de Supervivencia#Sistema de Guardado]] · [[Intro Cinematica]].

---

## 6. Personajes y Party

El party cambia durante el juego, reflejando las pérdidas y la escalada narrativa.

| Personaje | Cuerpo | Arma pesada | Destino |
|-----------|--------|------------|---------|
| Mateo Ibarra | MSRT | Por definir | Muere en el impacto del misil |
| MSRT Op. A | MSRT | Por definir | Muere en Acto I (scripteado) |
| MSRT Op. B | MSRT | Por definir | Muere en Acto I (scripteado) |
| SEAL Francotirador | Navy SEALs | Rifle de precisión | Variable |
| SEAL Médico | Navy SEALs | Rifle | Variable |
| SEAL Op. Joven | Navy SEALs | Escopeta | Variable |
| Agente CIA | CIA | Pistola suprimida | Se revela como antagonista |

El agente CIA funciona como "miembro Magus" — entra y sale del party, útil en combate, sus ausencias coinciden con sabotajes. Se niega absolutamente a usar Krokonil en cualquier circunstancia — pista narrativa encubierta.

Ver [[Personajes]] para perfiles completos, diálogos y mecánica Magus.

---

## 7. El Mundo — El Marinera

Tanquero VLCC (ex *Bella I*) en el Atlántico, interceptado en el Mar Caribe. Ambiente reactivo: se inclina, se inunda, los sistemas eléctricos fallan por sabotajes del Controlador (agente CIA). El barco no es un escenario estático — es un sistema vivo que presiona al jugador.

Zonas principales: cubierta exterior, puente de mando, enfermería, sala de máquinas, bodegas de carga, compartimentos de lastre, laboratorio experimental, zona restringida/centro de monitoreo CIA, bahía de vehículo de escape, estaciones de telégrafo Morse.

Ver [[El Marinera]] · [[Mecanicas de Supervivencia#El Barco como Sistema]] · [[Documentos del Marinera]] · [[Sistema de Mapa]] · [[Sistema de Transicion entre Decks]].

---

## 8. Narrativa

El juego ocurre en el *Marinera*, interceptado tras la Operación Crimson Draft. Un equipo MSRT sube a bordo para investigar la anomalía de calado. Lo que encuentran es un cargamento de Krokonil — un neuroquímico que destruyó a la tripulación — y un laboratorio de experimentación humana operado encubiertamente por la CIA.

### Arco narrativo en 5 actos

| Acto | Evento central |
|------|---------------|
| I    | Abordaje. Los MSRT descubren la situación. Los dos operadores MSRT mueren por desgaste. |
| II   | Reagrupamiento con Navy SEALs y agente CIA. Descubren los "reguladores KRK-NL". |
| III  | Revelación: los reguladores son Krokonil. El CIA empieza a mostrar fisuras. |
| IV   | El CIA se revela como antagonista. Party debilitado, combates más duros. |
| V    | Carrera contra el reloj. Misil en camino. Protocolo SCUTTLE. |

Ver [[Acto I - Diseño Detallado]] · [[Estructura Narrativa]] · [[Camino del Heroe]] · [[La Conspiracion]] · [[Protocolo SCUTTLE]] · [[Contexto Geopolitico]] · [[Proyecto Meridian]] · [[Marco Narrativo]] · [[Premisa y Sinopsis]].

---

## 9. Arte y Video

> Sección nueva — sin doc de sistema dedicado aún salvo lo indicado. Contenido a expandir.

- **Dirección visual:** top-down, pixel art, estética retro de consolas de 4ta/5ta generación.
- **Cambio de perspectiva en combate:** de top-down a side-scroller de mayor resolución, inspirado en JRPGs clásicos (Final Fantasy IV), evitando el costo de animación de un combate en el mismo plano que la exploración.
- **Cinemáticas:** estilo cómic animado por capas (frames estáticos + zoom/paneo de cámara + sprites animados selectivamente), inspirado en Metal Gear Solid: Peace Walker.
- **Render:** ver [[Configuracion Tecnica - Render 16bit]] para el pipeline de renderizado 16-bit.
- **Distractores visuales de combate:** ver [[Distractores Visuales]] (cross-ref con Mecánicas §4.4).

`[TODO: moodboard de referencia, color script por acto, guía de estilo de sprites y paleta]`

---

## 10. Sonido y Música

> Sección nueva — el diseño formal de audio aún no tiene doc dedicado; esto documenta el estado de implementación actual en Wwise.

- Estados de música por `PlayerState`: `Navigation`, `Combat`, transición vía estado intermedio `None` para evitar condiciones de carrera en el batching de Wwise.
- `SafeRoom` implementado como valor del switch `MarineraSector` (no como `PlayerState` separado), consolidando la navegación de estado de audio.
- Transiciones de combate disparadas por `CombatStartedEvent` / `CombatEndedEvent` (MessagePipe) hacia `MusicManagerController`.
- Interactuable de radio con evento `Stop_Radio` y RTPC de proximidad.

`[TODO: dirección de audio, referencias tonales (ej. "el chime de X al abrir Y"), lista de música por acto/zona, diseño de SFX de combate y ambiente]`

---

## 11. Interfaz (UI/UX)

> Sección nueva — sin doc de sistema dedicado aún.

- **HUD de combate:** monitor ECG (color, BPM, presión) como fuente principal de lectura de estado — sin barra de vida numérica (Pilar "Información como recurso").
- **Menú de comandos:** máquina de estados jerárquica (`CombatMenuController`) — `OperatorSelectionState` → `CommandPanelState` → (`ShotCountSelectionState` | `SubPanelState` | `AimingState` → `TargetSelectionState`).

`[TODO: wireframes de HUD, flowchart de pantallas de menú, accesibilidad]`

---

## 12. Progresión por Acto

| Acto | Recursos          | Amenaza               | Party                          |
|------|-------------------|-----------------------|-------------------------------|
| I    | Completos, limitados | Enemigos lentos     | MSRT×3 → CIA entra en Encuentro 1 |
| II   | Empiezan a escasear | Más resistentes     | Mateo + CIA + SEALs×3 |
| III  | Escasez notable   | Exposición ambiental  | Party variable, CIA sospechoso |
| IV   | Críticos          | Escasez extrema       | Party sin CIA                 |
| V    | Casi nulos        | Presión temporal total | Supervivientes                |

Ver [[Acto I - Diseño Detallado]] para el único acto con diseño de nivel completo.

---

## 13. Especificaciones Técnicas

**Motor:** Unity. **DI:** VContainer. **Eventos:** MessagePipe (pub/sub). **Async:** UniTask. **Tweens:** DOTween. **Audio:** Wwise. **Pathfinding:** NavMesh built-in. **Level art:** ProBuilder. **Inspector UX:** NaughtyAttributes.

**Jerarquía de scopes:** `GameLifetimeScope` (servicios globales, bus raíz de MessagePipe) → `NavigationScope` (navegación, jugador, rooms, inventario, IA enemiga — reutiliza el bus del padre) → `CombatScope` (ATB, UI de combate, orchestrator — cargado aditivamente al iniciar combate).

**Testing:** tests EditMode vía Unity Test Runner, sin mocks (fakes en C# plano). No hay comandos de test por CLI.

---

## 14. Referentes e Influencias

| Referencia | Elemento tomado |
|-----------|----------------|
| Resident Evil Gaiden | Barra móvil para resolver ataques — base del QTE bidimensional; encuentros visibles no aleatorios |
| Shadow Hearts | Judgment Ring: zonas críticas de timing donde el jugador decide |
| Vagrant Story | Targeting por zona anatómica dentro de sistema táctico |
| Parasite Eve | RPG con gestión de recursos, munición como recurso físico |
| Lost Odyssey | Ring timing: el anillo que se cierra afecta daño y precisión |
| Sweet Home (NES) | Grupo atrapado en espacio cerrado, permadeath real, survival de desgaste |
| Chrono Trigger | Combates en el mismo mundo, sin pantalla de transición |
| Metal Gear / Metal Gear 2 (MSX2) | Paredes inclinadas para navegación top-down no lineal |
| Escape From Tarkov | Inyectores como curativos; hitboxes de armadura por capas; ítem Obdolbos como inspiración de Krokonil |
| Obscure | Party con compañeros IA, permadeath que no termina el juego |
| Resident Evil Outbreak / Outbreak 2 | Compañeros NPC, muerte del compañero dificulta sin ser Game Over |
| Metal Gear Solid: Peace Walker | Cinemáticas tipo cómic animado por capas |

Ver [[Referencias e Influencias]] para análisis extendido.

---

## 15. Brechas de Diseño Pendientes / Roadmap

| Sistema | Estado | Prioridad |
|---------|--------|----------|
| Panel de comandos (combat-ui) | En implementación | Alta |
| QTE integrado con flujo de combate | En implementación | Alta |
| Enemy AI behaviors por fase de deterioro | Sin diseño formal | Alta |
| Acts II–V diseño detallado de nivel | Sin diseño | Media |
| Encuentros de enemigos por zona del barco | Sin diseño | Media |
| Sistema de zonas seguras / almacenamiento | Pendiente en inventario | Media |
| Doc formal de Arte y Dirección Visual | Sin doc dedicado (ver §9) | Media |
| Doc formal de Sonido y Música | Sin doc dedicado (ver §10) | Media |
| Doc formal de UI/UX | Sin doc dedicado (ver §11) | Baja |
| Stack máximo de cajas de balas (inventario) | Pendiente | Baja |
| Tamaño del Krokonil como item | Pendiente | Baja |
| Sistema de interacción y highlight de objetos | Pendiente en movimiento | Media |

---

## 16. Changelog

- **v2 (2026-07-15):** Reestructuración completa usando como referencia de forma el ToC del design doc de *Silent Hill 2* (Design/Reference) y las guías modernas de GDD (Design/References/GD_Guidelines.md). Fusión de "Sistemas de Combate" + "Sistemas de Supervivencia" en una única sección de Mecánicas. Reconciliación de pilares (5→6: se agrega "El juego no acusa, siembra" del HC). **Corrección de canon:** el setting narrativo pasa de "Mar Negro" a Caribe/Atlántico, alineado con el HC y con [[El Marinera]]. Secciones nuevas: Overview ampliado, Core Gameplay Loop explícito, Arte y Video, Sonido y Música, Interfaz, Especificaciones Técnicas.
- **v1 (2026-03-03):** Versión original (Pre-producción).

---

Volver a [[Crimson Draft]]
