# Crimson Draft — Game Design Document
*Estructura base: [[silent-hill-design-document]] (Design/Reference) — Versión 0.1, borrador de esqueleto*

---

## 1. Game Concept (Concepto del Juego)

### 1.1 Introduction
*Crimson Draft* es una experiencia top-down de Tactical Survival Horror para PC. El jugador comanda un escuadrón del Maritime Special Response Team (MSRT) insertado por aire en el ***Marinera***, un tanquero de la flota fantasma rusa interceptado en el Mar Caribe bajo el nombre en clave de la operación: **Crimson Draft**. El destructor de apoyo se retira casi de inmediato — el submarino ruso escolta está demasiado cerca. El escuadrón queda solo en cubierta, sin extracción, en medio del Atlántico. Lo que encuentran dentro no es contrabando.

### 1.2 Background
*Atención: los acontecimientos y cualquier similitud con hechos del mundo real son mera coincidencia.*

Dada la crisis de drogas, Estados Unidos ha endurecido sus sanciones contra países que atentan contra la salud pública, iniciando un bloqueo naval en el Mar Caribe en busca de embarcaciones dedicadas al narcoterrorismo. El *Marinera* (ex *Bella I*) lleva semanas burlando a la Guardia Costera: es parte de la llamada flota fantasma rusa, buques que cambian de nombre, de bandera y apagan sus sistemas de rastreo para mover petróleo sancionado sin dejar rastro. Cuando la presión aumenta, Rusia juega su carta: pinta la bandera rusa en el casco, lo registra oficialmente en Moscú y envía un submarino a escoltarlo. El mensaje es claro: este barco es territorio ruso, abordarlo sería un incidente diplomático.

Un barco vacío no necesita escolta militar. Nadie esperaba que Estados Unidos lo abordara de todas formas. El calado (*draft*, en inglés náutico) no miente: el *Marinera* declara ir en lastre, pero su línea de flotación está varios pies más abajo de lo que debería. Algo pesa dentro que dos potencias mundiales están dispuestas a proteger, o a negar.

Ver [[El Marinera]] para cronología completa del incidente.

### 1.3 Description
El jugador no libera a la tripulación ni resuelve un misterio sobrenatural: administra la supervivencia de un equipo cada vez más pequeño y más frágil. Es trabajo del jugador desentrañar qué le pasó a la tripulación del Marinera — ya sea evitando el mayor número posible de encuentros, o enfrentándolos cuando no queda alternativa. Lo único que no puede evitar son los puzzles de acceso y la verdad incómoda detrás del ítem que lo mantiene con vida.

El combate no empodera: cada bala gastada es permanente, cada operador que muere se va para siempre. No hay tiendas, ni refuerzos, ni segunda oportunidad — el jugador sobrevive administrando lo que le queda, no acumulando poder.

### 1.4 Key Features
- **Permadeath sin Game Over:** perder un operador no termina la partida, es una herida permanente — el equipo continúa con menos recursos y menos opciones tácticas (inspirado en *Sweet Home*, 1989).
- **Doble condición de muerte sin barra de vida:** el estado del operador se lee vía ECG, BPM y presión arterial — no hay número de HP visible.
- **Engaño ludonarrativo:** el ítem que previene la muerte permanente (Krokonil) es, en la ficción, la misma droga que destruyó a la tripulación. El jugador repite, sin saberlo, la tragedia que investiga.
- **Encuentros visibles, no aleatorios:** los enemigos están en el mapa; el combate se activa por proximidad o decisión del jugador, nunca a ciegas.
- **ATB clásico estilo Chrono Trigger:** cada operador y enemigo llena un gauge propio en tiempo real; al llegar a 100% queda READY para actuar. En modo Wait, los gauges corren mientras el jugador delibera — el jugador no juega contra el reloj, juega contra su propia velocidad de decisión.
- **QTE de disparo bidimensional:** cada bala se gana, no se regala. El jugador selecciona su munición y tiene una fracción de segundo para clavar dos barras — vertical y horizontal — antes de que el arma dispare. Pulso firme y lectura rápida definen si el tiro entra en la cabeza o se pierde en el aire; el recoil del arma castiga cualquier titubeo en el siguiente disparo.
- **Horror sin elementos sobrenaturales:** los enemigos son humanos en colapso neuroquímico; el antagonista real es el sistema que diseñó y distribuyó la droga.
- **El guardado es parte de la historia:** el jugador transmite su progreso por telégrafo Morse — un sistema que resulta ser real dentro de la ficción.

### 1.5 Genre
Tactical Survival Horror. El "Tactical" no viene de la raíz JRPG del combate, sino de *Metal Gear*: navegación militar, lectura de amenazas y decisiones de infiltración/evasión antes que del sistema de combate en sí. El combate en tiempo real (ATB) y el survival horror de 4ta/5ta generación aportan el resto de la identidad de género.

### 1.6 Platform
PC (Steam). Desarrollado en Unity.

### 1.7 Concept Art

## 2. Game Mechanics (Mecánicas del Juego)

### 2.1 Core Game Play
El loop macro: explorar el Marinera en top-down → decidir si evitar o enfrentar encuentros visibles → si hay combate, resolverlo con ATB en tiempo real + QTE de disparo → gestionar salud (ECG/presión), munición e inventario tras el enfrentamiento → investigar documentos y resolver puzzles de acceso → guardar progreso vía telégrafo Morse → repetir con un equipo cada vez más pequeño y con menos recursos, hasta extraer (o no) al equipo con vida.

Objetivos del jugador:
- **Corto plazo:** evitar encuentros, gestionar recursos, mantener al equipo con vida, explorar.
- **Mediano plazo:** investigar qué le pasó a la tripulación, resolver puzzles, desbloquear zonas.
- **Largo plazo:** transmitir los hallazgos al exterior, extraer al equipo con vida.

### 2.2 Game Flow
El jugador navega el Marinera en tiempo real. Al entrar en rango de un enemigo visible, o al elegir iniciar combate, el juego transiciona a la resolución de combate (ATB + QTE); al terminar el enfrentamiento, el jugador vuelve a la navegación con el estado de sus operadores (salud, munición, exposición a Krokonil) persistido — no hay reinicio ni recompensa automática entre un estado y otro.

Las transiciones entre habitaciones del barco se resuelven con una breve cutscene de puerta animada, y el punto de aparición del jugador depende de la sala desde la que viene, reforzando la sensación de espacio continuo y no linealidad.

### 2.3 Characters
El party jugable es el escuadrón de la Operación Crimson, y cambia durante el juego reflejando las pérdidas y la escalada narrativa.

| Personaje | Cuerpo / Afiliación | Rol | Nota de diseño |
|-----------|---------------------|-----|----------------|
| **Ethan Miller** | MSRT | Boarding Specialist — interdicción marítima y aseguramiento de buques | Protagonista. Cree ciegamente en el sistema que investiga — su fe es su ancla y su grieta. *"I'm loyal to nothing, except the law."* |
| **Darius Mercer** | Navy SEALs / CIA | Team Leader — estrategia y comando táctico | Se revela como antagonista. Ve a sus subordinados como componentes funcionales, no como pares. *"Power isn't given. It's taken... and kept."* |
| **Lilou Vance** | Navy SEALs | Recon Sniper — infiltración y eliminación a larga distancia | Sin arraigo a ningún lugar; opera en soledad, a distancia, con precisión quirúrgica. *"I was born nowhere... and I'll die somewhere else."* |
| **Marcus Hale** | Navy SEALs | Combat Engineer — demoliciones, apertura de estructuras y soporte táctico pesado | Cree en el destino, no en el futuro. Rol de protector: abre lo cerrado, sostiene lo que colapsa. *"People think about their future. I think about destiny."* |

Darius Mercer cumple la función narrativa de "miembro Magus" — el mejor operador del equipo trabaja para el enemigo. El jugador construye una dependencia mecánica real hacia él (mando táctico, utilidad en combate) antes de que su rol como antagonista se revele.

**Personajes de trasfondo (no jugables, descubiertos vía documentos y entorno):**
- **Adrian Volkov** — infiltrado de la CIA a bordo bajo cobertura de chef; observador encubierto, no agente de campo convencional.
- **Vanessa Stoian** — sin afiliación oficial, ingresó al Marinera a través de Adrian Volkov; expuesta a una variante experimental de KRK-NL, motor de "El Incidente" que precede al abordaje.
- **El Ingeniero** — tripulante fallecido (rango "Caballo"), su diario y su búsqueda del gato Bola de Nieve documentan el deterioro cognitivo progresivo por KRK-NL antes de la llegada del jugador.

Ver [[Personajes]] (Narrativa/Personajes) para perfiles completos.

### 2.4 Monsters
Los enemigos se llaman **Wanderers**. El término "zombi" no aplica — no son muertos vivientes, son tripulantes y sujetos del laboratorio expuestos a Krokonil, primero sin saberlo a través de la comida (microdosis en los "alimentos fortificados" del cargamento), y después al compuesto puro. No hay diseño de "especies" — hay una progresión de deterioro por drogadicción, y el diseño busca leerse explícitamente como decadencia humana, no como monstruo de ficción.

**Dirección visual:** aspecto de vagabundo — ropa sucia, desaliñados, cuerpos descuidados. Krokonil está sintetizado a partir de Krokodil (droga rusa real que devora tejido) y fentanilo, por lo que los Wanderers presentan músculo expuesto y necrosis de piel visible en fases avanzadas — la misma consecuencia física documentada del krokodil real, no una mutación fantástica. El horror no viene de una silueta imposible sino de reconocer, en cada Wanderer, a alguien que se dejó de cuidar por completo. Es la misma decadencia que cualquier adicción severa produce en un cuerpo, llevada a un entorno cerrado sin escapatoria.

**Tipos de Wanderer:**

| Tipo | Origen | Trigger de combate | Perfil de amenaza |
|---|---|---|---|
| **Tripulantes del Marinera** | Tripulación civil expuesta por microdosis en la comida | Contacto/hit — lentos, sin reacción a distancia | El "zombi común" del juego: lento, predecible, bajo riesgo individual |
| **Equipo de asalto ruso** | Escuadrón ruso que intentó abordar el Marinera antes de la Operación Crimson Draft; repelido y luego dosificado con Krokonil para ser controlado | Línea de visión — detectan y reaccionan a distancia | Enemigos difíciles: llevan arma y armadura, visibles como cobertura geométrica en la silueta del QTE (ver §2.5 Weapon Properties / Armadura por capas) |
| **Experimentos** | Sujetos del laboratorio clandestino con exposición directa al compuesto puro | Variable | Presentan ampollas visibles como puntos débiles marcados en la silueta del QTE — el jugador apunta a la ampolla para maximizar daño |

Ver [[El Marinera]] para el catálogo completo.

### 2.5 Game Play Elements

**Weapon Properties**
Cada arma tiene una tabla de puntos de recoil predefinidos, uno por número de disparo dentro de la ráfaga. Al disparar, se genera una elipse centrada en el punto de tabla correspondiente a ese número de disparo, y la posición final de impacto es aleatoria dentro de esa elipse. El patrón es aprendible (el centro de cada elipse es predecible y define la trayectoria general del recoil) pero nunca perfectamente eliminable (el resultado dentro de la elipse es azar), y se espeja en el eje horizontal según la mano dominante del operador.

| Operador / Facción | Arma |
|---|---|
| Navy SEALs (general) | P226 |
| MSRT (general) | P229 |
| Darius Mercer | Five-Seven · MP7 |
| Ethan Miller | Mk18 |
| Lilou Vance | MCX Rattler |
| Marcus Hale | Benelli M4 |

Cada arma tiene su propia tabla de puntos de recoil.

Munición 9mm con dos variantes tácticas:

| Tipo | vs carne | vs chaleco | vs placas |
|------|----------|-----------|----------|
| RIP | ×1.0 | ×0.4 | ×0.2 |
| FMJ | ×0.8 | ×0.7 | ×0.5 |

Ver [[Diseño de Combate y Armas]] · [[Sistema de Conteo de Balas por Disparo]].

**Game Physics and Statistics**

**Artificial Intelligence**
Los enemigos patrullan un recorrido fijo o permanecen quietos (idle). Si detectan al jugador por cualquiera de sus métodos de detección, lo siguen — sin estado intermedio de duda. Una vez persiguiendo, no pierden al jugador de vista hasta atraparlo o hasta que este abandone la zona. La detección prioriza tres modos, en este orden: proximidad (con distancias distintas para detectar y para perder de vista al jugador, evitando parpadeos), sonido (según qué tan rápido se mueve el jugador) y visión (campo de visión más línea de visión directa). Un enemigo derrotado en combate victorioso queda desactivado permanentemente.

Ver [[Sistema de IA de Navegacion]] · [[Sistema de Ataque de Enemigos]].

**Player Controls**
Movimiento libre en 360°, con caminar y correr como velocidades distintas — correr cubre terreno más rápido pero anuncia la posición del jugador a mayor distancia (ver detección por sonido en §2.5 Artificial Intelligence). En combate, moverse entre operadores listos para actuar y abrir el panel de comandos ocurre en tiempo real; el QTE de disparo y los submenús de recarga/ítems pausan el ritmo del combate mientras el jugador decide. Ver [[Sistema de Movimiento]].

### 2.6 Interface

**Flowcharts**
El flujo del combate sigue el ciclo de ATB:

```
Gauge de un operador se llena → queda LISTO
  → el jugador lo selecciona y abre su Panel de Comando
       ├─ Disparar   → cantidad de balas → se compromete la acción (el gauge resetea de inmediato)
       ├─ Recargar / Ítems / Defender → se compromete la acción
       └─ mientras tanto, los gauges de los enemigos siguen corriendo en paralelo
  → cuando le toca el turno a una acción comprometida de Disparar, recién ahí se resuelve
     el QTE de apuntado (selección de objetivo + las dos barras de precisión)
  → un enemigo listo ataca directamente, sin pasar por ningún menú
  → el combate termina cuando un bando queda sin actores en pie
```

Comprometerse a una acción no es lo mismo que resolverla: el jugador puede encolar "Disparar" sabiendo que el QTE llegará más tarde, bajo la presión de lo que haya pasado en el combate mientras tanto. Fuera del QTE y de los submenús, todos los gauges —propios y enemigos— siguen corriendo. El jugador no juega contra un reloj externo: juega contra su propia velocidad de decisión.

**Functional Requirements**
El combate se resuelve como una capa independiente que se activa sobre la navegación y se retira al terminar, devolviendo al jugador al mismo punto del mapa con el estado de sus operadores (salud, munición, exposición a Krokonil) intacto.

**Mock Up Screens**
`[TODO: wireframes de HUD de combate, panel de comandos y menú de inventario — aún no existen mockups formales, solo la implementación en curso]`

## 3. Art and Video (Arte y Video)
### 3.1 Overall Goals
### 3.2 2D Art
### 3.3 3D Art & Animation
### 3.4 Cinematics

## 4. Sound and Music (Sonido y Música)
### 4.1 Overall Goals
### 4.2 Sound Effects & Music

## 5. Story (Historia)
### 5.1 Story Overview
### 5.2 Multiple Endings

## 6. Level Overview (Niveles)
### 6.1 Location Overviews
### 6.2 Puzzles

## 7. Market Analysis (Análisis de Mercado)
### 7.1 Target Market
### 7.2 Top Performers
### 7.3 Feature Comparison

## 8. Bibliography (Referencias)

---

Volver a [[Crimson Draft]]
