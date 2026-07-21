# Resident Evil 2 (1998, PS1 original): números de combate y decisiones de diseño

**Fuente principal de datos:** testing exhaustivo del usuario "cheezeit" en GameFAQs, hecho por data-mining directo de la memoria del juego emulado (mednafen + herramientas de terceros y scripts propios) sobre la **versión PS1 Dual Shock (SLUS-00748), dificultad Normal**, salvo donde se indique lo contrario. Hilo: *"RE2 (PS1) HP/Enemy/Weapon Mechanics"* (gamefaqs.gamespot.com/boards/562855-resident-evil-2-dual-shock-edition/80273925), con correcciones y ampliaciones del propio autor a lo largo del hilo. Es, hasta donde se pudo verificar, el testing más granular disponible públicamente para el original de 1998 (no el remake de 2019).

---

## PARTE 1 — Los números

### 1.1 Salud del jugador

- **Todos los personajes controlables tienen 200 "puntos" de HP**: Leon, Claire, Ada (IA) y Hunk. Sherry también tiene 200. **Tofu tiene 400** (el doble), y como se cura por porcentaje pero recibe daño plano de los enemigos, "en la práctica recibe la mitad de daño".
- **Umbrales de estado** (en % de HP máximo): Fine >50%, Yellow Caution 50%, Orange Caution 20%, Danger 10%.
- **Regla de HP negativo:** un enemigo con, por ejemplo, 600 HP necesita recibir un mínimo de 601 de daño para morir — el HP debe cruzar a negativo, nunca basta con llegar a 0.
- **Mecánica de "Mercy":** si el personaje tiene más de cierto HP, un golpe que en teoría lo mataría lo deja en 1 HP en su lugar. Regla general observada por el propio tester: si el "overkill" del ataque no empujaría el HP a −7,5% o menos, el personaje sobrevive con 1 HP (hay excepciones puntuales por ataque).
- **Auto-resistencia (autoresist):** el mordisco normal de zombi puede golpear dos veces si no se resiste, pero el juego libera automáticamente al personaje del segundo mordisco si el primero ya lo dejó en 1 HP — por eso un solo zombi normal **solo puede matar desde el estado Danger**. Las Ivy (plantas) son la excepción: su segundo golpe de agarre **no** tiene autoresist, así que pueden matar desde Yellow Caution.
- **Ataques de muerte instantánea documentados:** las cucarachas (Roaches) si ~6 o más están sobre el personaje a la vez; el "Roar Slam" de G1; y tocar el cuerpo del Alligator (o quedarse sin espacio en el pasillo del script).
- **Enemigos que pueden matar desde Yellow Caution:** zombis desnudos/Brad (si no se resiste), Lickers, Ivys (si no se resiste), Tyrant normal, el Alligator, G4, y el combo de G3 en el Escenario B.
- **Enemigos que pueden matar desde Orange Caution:** zombis desnudos/Brad, GAdult, la polilla (Moth) y el Super Tyrant.
- **Veneno:** drena 0,5% de HP por segundo; no puede matar por sí solo, pero drena hasta dejar al personaje en 1 HP.
- **Curación:** hierba verde = 25% de HP; doble hierba verde = 50%; triple hierba/hierba roja+verde/spray de primeros auxilios (FAS) = curación completa. El propio tester señala que, por eficiencia, conviene usar 2 hierbas verdes al llegar a Yellow Caution y curación completa al llegar a Orange Caution.
- **Sherry** tiene una particularidad: siempre muestra la animación de "Fine" (no se ralentiza visualmente al perder HP), aunque sí gira más lento en movimiento cuando está por debajo del 50% (giros ~50% más anchos en Caution, ~200% más anchos en Danger, según estimación del tester). Los zombis solo usan su ataque de vómito (más débil) contra ella, y los perros son su única amenaza real (mueren en 8 mordiscos desde Fine).
- **Ada (IA)** también tiene 200 HP pero recibe daño ligeramente distinto de ciertos enemigos (p. ej. el embiste de araña le hace 10% en vez de 15%, pero su escupitajo le hace 20% en vez de 15%); no es agarrada por zombis (solo vomitada), y aparentemente es inmune al veneno.

### 1.2 HP de enemigos comunes (aleatorio dentro de un rango, "re-rolleado" al reentrar a una sala)

| Enemigo | Rango de HP (Normal) | Notas |
|---|---|---|
| Zombi | 65, 73, 75, 80, 81, 90, 94, 95, 97, 98, 109, 110, 143 (valores discretos) | Los zombis de la calle previa a la comisaría tienen −15 HP fijo |
| Zombi (Brad Vickers) | 250 | HP fijo, mucho más alto que un zombi normal |
| Perro zombi | 69–72 / 80–83 / 95–98 / 129–132 | Cuatro "bandas" de HP |
| Licker (normal) | 85, 91, 97, 109, 111, 120, 125 | |
| Licker evolucionado (2ª mitad) | 77, 80, 84, 85, 90, 95, 97, 104, 105, 112, 115, 116 | HP promedio similar al Licker normal; las armas hacen 20–40% menos daño |
| Araña | 99–102 / 109–112 / 129–132 | |
| Ivy | 79–82 / 99–103 / 107–110 / 129–132 | Con gas de laboratorio: −50 HP fijo (aprox. mitad del HP promedio) |
| Cuervo (Crow) | (no se documenta HP; siempre requiere exactamente 3 golpes de picoteo antes de autoresist) | |
| Zombis del laboratorio (buff especial) | Igual HP que fuera del laboratorio | Pero reciben **menos daño** de casi todas las armas (ver 1.4) |

**Nota sobre RNG:** el HP no es continuo sino que se elige de una lista de valores discretos predefinidos, distinta para cada tipo de enemigo — es decir, no es "cualquier número entre X e Y" sino un pequeño conjunto de valores posibles codificados.

### 1.3 HP de jefes (fijo, sin RNG)

| Jefe | HP | Notas |
|---|---|---|
| Tyrant (T-00 / Mr. X) | 220 (Pasillo del Tigre Disecado) · 300 (Sala de Prensa, Vestíbulo 3F, Sala de Monitores Hidráulicos) · 350 (Pasillo del Ascensor) · 400 (Pasillo de Cuervos, Pasillo de Interrogatorios) | **HP fijo distinto por sala/encuentro**, no un HP único persistente. Deja la sala si el jugador escapa (con excepción del encuentro de la Sala de Prensa). Deja objetos al ser derrotado. |
| GAdult (primera forma de Birkin) | 600 | |
| G1 | 500 | |
| G2 (pelea real, no la del tranvía) | 700 | **Regenera 3,75 HP/seg** si está entre 101 y 499 HP; deja de regenerar por debajo de 100 HP (entra en "estado crítico") |
| G2 (mano en el tranvía) | Tiene HP pero es **imposible de vaciar en juego normal**; la pelea termina tras 5 embestidas sin importar el daño | El arma de Ada "no hace nada" realmente, es solo cosmético |
| G3 (Escenario A) | 300 | Se transforma en G4 al ser derrotado |
| G3 (Escenario B) | 900 | HP muy superior al G3 de Escenario A — mismo "jefe" narrativo, HP completamente distinto según escenario |
| G4 | 700 | |
| Super Tyrant (helipuerto) | 200 | Solo el lanzacohetes puede matarlo; alternativamente, esquivar 60 segundos hace que Ada lo lance |
| G5 (forma final) | 600 | A diferencia del remake, no tiene ningún ataque de muerte instantánea; solo el temporizador puede matar en ese tramo |
| Alligator | 240 | Solo el hocico tiene hitbox; el cuerpo no tiene colisión real |
| Moth (polilla) | 150 | |
| Tyrant en Extreme Battle | ~450 | Modo extra, HP distinto al de la campaña principal |

### 1.4 Daño de armas contra zombi (formato Cerca/Media/Lejos donde aplica)

| Arma | Daño vs zombi | Notas |
|---|---|---|
| Cuchillo | 3 | El daño de cuchillo más bajo de toda la serie clásica, según comparación del propio tester con RE1 y RE3 |
| Pistola (Leon/Claire, base) | 16/15/14 | |
| Escopeta | Instakill / 60 / 40 | A quemarropa mata hasta 3 zombis de un disparo; apuntar hacia arriba decapita (instakill); los disparos al cuerpo pueden dejar "crawlers" |
| Escopeta Custom | Instakill / 80 / 60 | Nunca deja crawlers, por lo que apuntar hacia arriba es innecesario con ella |
| Magnum (normal y Custom) | Instakill | La Custom además atraviesa varios enemigos y no tiene caída de daño por distancia |
| Subfusil (SMG) | 4 por impacto (40/seg) | Consumo de 10 disparos (2,5% de munición) por segundo, con un contador de "roll-down" que da munición extra gratuita tras cada recarga |
| Lanzallamas (Claire) | 15 por impacto (90/seg) + 4/seg de quemadura (afterburn) | El daño por quemadura solo afecta a zombis |
| Gatling | 8 por impacto (80/seg) | Codificada internamente como un SMG con tiempo de carga y +80% de daño por bala |
| Lanzacohetes | Instakill | |
| Ballesta (Bowgun, solo Claire) | 30 × 3 flechas (90 total) | No decapita, según testing y confirmación de otro usuario del hilo |
| Granada normal | 50 o 200 | 200 solo si conectan varios proyectiles a la vez; si conecta uno solo, el daño es reducido |
| Granada ácida | 200 | Daño plano de golpe |
| Granada de fuego | 200 | Daño plano de golpe |
| Sparkshot | 60 | |

**Zombis del laboratorio (buff especial de resistencia):** cuchillo 3 (igual), pistola 11/10/9 (−33% aprox.), escopeta Instakill/40/30, Custom Instakill/60/50, SMG 3/hit (30/seg), lanzallamas 12/hit (72/seg, −20%), Gatling 6/hit (60/seg, −25%), ballesta 25×3=75 (−17%), granada normal 50 u 80 (ya no instakill garantizado), ácida Instakill (sigue siendo instakill), fuego 80 + quemadura, Sparkshot 61. Es decir: **los zombis del laboratorio no tienen más HP, pero sí más resistencia efectiva a casi todas las armas**, con la notable excepción del ácido, que sigue siendo instakill.

### 1.5 Diferencias de daño contra otros enemigos comunes (selección)

- **Ivy:** muy frágil — pistola hace 5 (igual a cualquier distancia), cuchillo 4, escopeta 25/22/20, magnum 30 (60 con la Custom). El gas de laboratorio reduce su HP a la mitad mientras que sus armas no cambian, haciéndolas efectivamente aún más fáciles.
- **Licker:** la pistola hace 14/13/12 pero está sujeta a "iframes" con la Colt de Claire (ver 1.6); la escopeta base hace 50/45/40, la Custom 110/95/80; el ácido es instakill.
- **Perro:** más resistente que el zombi promedio a ciertas armas de área (la escopeta Custom es instakill a quemarropa), pero el ácido y el fuego también son instakill.
- **Araña:** notablemente más resistente al magnum (130 de daño, sin ser instakill) que la mayoría de los enemigos comunes.

### 1.6 El sistema de "iframes" de la pistola en modo automático

Esta es una de las mecánicas menos intuitivas documentadas: al disparar en modo automático con la **Pistola Custom (Burst) de Leon** o el **Colt SAA de Claire**, ciertos enemigos son inmunes a los disparos pares (2º, 4º, 6º...) y solo reciben daño de los impares (1º, 3º, 5º...). Esto reduce el daño real de la Burst a **dos tercios** y el del Colt a **la mitad** contra los enemigos afectados.

- **Afectados por iframes:** todos los "enemigos normales" no-zombi (excepto Ivy), la polilla y el Tyrant.
- **No afectados (reciben daño completo):** zombis, perros, Ivys, y **todos los demás jefes** (GAdult, G1, G2, G3, G4, G5, Super Tyrant reciben daño completo del Colt; solo GAdult, G1 y G5 tienen iframes contra la Burst).
- En la práctica, el tester señala que esto rara vez importa por la abundancia de munición de pistola en el juego, pero es una desventaja real y no documentada oficialmente de las armas "mejoradas".

### 1.7 Mecánicas de derribo y "poise" de zombi (sistema no documentado oficialmente)

- Cada zombi tiene un contador oculto de **"poise"** (término elegido por el tester, inspirado en Dark Souls) con un valor inicial aleatorio entre 15 y 31 al entrar a la sala.
- El cuchillo resta 9 de poise por golpe; la pistola normal resta 15; el Colt 20; la pistola Burst 35.
- Cuando el contador de poise llega a 0, si el zombi tiene **83 HP o más**, no pasa nada y el contador se reinicia — es decir, **los zombis con mucho HP restante son más resistentes a ser derribados**, no solo más resistentes a morir.
- Si tiene menos de 83 HP, el siguiente golpe de cuchillo lo derriba instantáneamente (knockdown); el siguiente disparo de pistola lo hace tambalearse (stagger), y hace falta un segundo disparo de pistola durante el stagger para completar el derribo (si no llega ese segundo disparo, el zombi "recupera el equilibrio" y el contador se reinicia).
- **Excepción:** los zombis previos a llegar a la comisaría no recuperan su "poise" tras un stagger — una vez tambaleados, el siguiente disparo de pistola siempre los derriba, sin importar cuánto tiempo pase.
- A poise máximo (31), hacen falta exactamente 5 disparos de pistola sin modificar o 5 cuchillazos para forzar un derribo.
- Al llegar a 0 HP, hay una probabilidad aleatoria de que el zombi "se haga el muerto" en el piso y solo pueda morder la pierna si alguien se acerca — pero el juego ya lo marca como "muerto" y desaparecerá si se sale de la sala.

### 1.8 Sistema de rango (ranking / puntuación)

- Se calculan **3 grupos de puntos**, cada uno con 100 puntos iniciales y penalizaciones:
  - **Sprays de primeros auxilios (FAS) usados:** −15 puntos por cada uno, hasta llegar a 0 con 6 o más usados. Las hierbas nunca penalizan.
  - **Número de guardados:** −5 puntos por cada guardado, hasta 0 con 20 o más.
  - **Tiempo final:** −10 puntos al llegar a 2:30:00; −20 al llegar a 3:30:00 (nota del propio tester: corrigió después este dato a −30 puntos en 4:30 y −40 en 5:30, tras verificación adicional).
- Suma máxima: 300 puntos. Umbrales de rango: **A ≥ 270, B ≥ 265, C ≥ 195, D ≥ 140, E ≥ 95.**
- **Penalización por "armas trampa":** usar cualquiera de las 3 armas de munición infinita desbloqueables resta un total fijo de **50 puntos** al marcador final (no puntos por categoría) — la penalización se activa al disparar el primer tiro, no simplemente por llevarlas en el inventario. El modo "Rookie" (Novato) ya empieza el juego con esta penalización activa por dar las 3 armas infinitas desde el inicio.
- El modo 4th Survivor (Hunk) usa un sistema de rango basado solo en el tiempo: A en ≤4 min, B en ≤5, C en ≤6, D en ≤7, E en más de 7.

### 1.9 Diferencias documentadas por dificultad (dentro del juego original, no dificultad adaptativa)

- **Fácil:** el pool de HP de zombi cambia a 28, 40, 54, 70, 100 — es decir, **30% a 60% menos HP** que en Normal (el zombi más débil muere con solo 2 disparos de pistola). También se otorgan 120 balas de pistola extra al empezar. El tester no observó diferencias en el daño recibido o infligido por el jugador entre Fácil y Normal — el cambio de dificultad ajusta el HP enemigo y el inventario inicial, **no el daño de las armas del jugador ni el daño que reciben los personajes**.
- **Rookie (modo fácil de versiones posteriores como GameCube):** parece idéntico a Normal salvo por empezar con las 3 armas de munición infinita (y, por tanto, la penalización de rango de 50 puntos ya activa desde el inicio).
- **Extreme Battle (modo extra, no la campaña principal):** los Tyrants tienen 450 HP (distinto al de la campaña). El personaje jugable Chris tiene una pistola con 6% de probabilidad de crítico (medido sobre una muestra de 3000 disparos), y esos críticos están codificados literalmen­te como disparos de magnum base (el mismo truco que el "Eagle" crítico de RE3, según nota del propio tester). En el nivel de dificultad 3 (LV3) de este modo, el jugador recibe multiplicadores de daño no uniformes por ataque (1x, 2x o 5x según el ataque específico, no un multiplicador global como se asumió inicialmente) — el testing confirma que esto es exclusivo de Extreme Battle, no de la campaña principal.
- **No hay evidencia de dificultad adaptativa** (ajuste dinámico según el desempeño del jugador) en el original de 1998; esa es una característica introducida en el remake de 2019, que queda fuera del alcance de esta investigación.

---

## PARTE 2 — Abstracciones y decisiones de diseño

### 2.1 La regla de "HP negativo para matar" + "Mercy" es un sistema de gestión de la ansiedad del jugador, no solo de dificultad

Que un enemigo con 600 HP necesite 601 de daño para morir, combinado con el mecanismo de "Mercy" (un golpe que en teoría mataría deja al personaje en 1 HP en vez de matarlo), revela una decisión de diseño muy deliberada: **el juego está diseñado para que la muerte nunca sea sorpresiva ni "injusta"**. El jugador casi siempre tiene una advertencia — un golpe que lo deja en 1 HP — antes del golpe que realmente mata. Esto es coherente con el objetivo de terror por *tensión sostenida* más que por *muerte súbita*: el miedo de RE2 no viene de perder instantáneamente sin aviso, sino de administrar el pánico sabiendo que "un golpe más" puede ser el último. La regla de autoresistencia en el segundo mordisco de zombi refuerza esto: **un solo zombi jamás debería poder matar a un jugador con salud razonable**, lo que empuja el peligro real hacia los enjambres, las emboscadas y los enemigos "especiales" (Ivy, Licker, Tyrant) en vez del enemigo más común del juego.

### 2.2 El HP fijo del Tyrant por sala, en vez de un HP único persistente, es una decisión de ritmo narrativo, no solo técnica

Que el Tyrant tenga HP distinto según la sala (220 en un encuentro, hasta 400 en otro) — y que "huir" de un encuentro simplemente lo despawnee sin conservar el daño acumulado — muestra que Mr. X **no fue diseñado como un combate que se "gana" de forma persistente**, sino como una serie de encuentros discretos, cada uno calibrado a lo que el jugador probablemente tiene disponible en ese punto del juego. Esto es coherente con su rol de diseño real: un generador de tensión ambiental y presión de tiempo ("¿me quedo a pelear o corro?"), no un jefe final progresivo. El HP relativamente bajo (220–400, comparable a un GAdult o menos) sumado a que casi siempre conviene huir en vez de pelear, es la prueba numérica de que el diseño **empuja activamente al jugador a evitar el combate directo** con este enemigo, reforzando su función narrativa de "amenaza que hay que esquivar" antes que de "obstáculo que hay que vencer".

### 2.3 El HP de G3 casi se triplica entre el Escenario A (300) y el Escenario B (900): el Zapping también es un sistema de recalibración de dificultad, no solo de historia

Este es uno de los datos más reveladores del testing: **el mismo jefe narrativo (G3) tiene un HP completamente distinto según qué escenario se esté jugando**. Esto conecta directamente con lo que Hideki Kamiya explicó en la entrevista de 1998 (citada en la investigación anterior sobre game design): el Escenario B fue diseñado para ser más difícil porque el jugador ya conoce el mapa y los puzzles en su segunda vuelta. El salto de 300 a 900 HP confirma numéricamente esa intención: **no es solo que aparezca el Tyrant en el Escenario B — el propio balance de combate del juego se recalibra jefe por jefe** para compensar la pérdida de "sorpresa" estructural. Es una forma de dificultad progresiva *entre* partidas, no *dentro* de una sola partida (ya que, como confirma el testing, no hay ajuste dinámico según desempeño dentro del original de 1998).

### 2.4 La curva de daño del arsenal está diseñada para hacer visible, arma por arma, el costo de oportunidad de cada decisión táctica

Los números de daño del cuchillo (3, el más bajo de la serie según comparación directa con RE1 y RE3), la pistola (14-21 según enemigo, con ligera penalización de iframes en las versiones "mejoradas" contra ciertos enemigos) y el arsenal pesado (instakills de escopeta/magnum/lanzacohetes contra zombis) trazan una curva muy clara: **cada arma superior no solo hace más daño, sino que "resuelve" un problema táctico específico** (la escopeta despeja grupos a quemarropa; el magnum ignora la caída de daño por distancia en su versión Custom; el lanzacohetes es la única forma de derrotar al Super Tyrant). El dato del "iframe" de la pistola Custom/Colt en modo automático es particularmente revelador: **el arma "mejorada" no es estrictamente mejor en todos los escenarios** — reduce cadencia efectiva contra ciertos enemigos a cambio de mayor cadencia bruta. Es una decisión de diseño que castiga sutilmente el "spam" de disparo automático sin previo conocimiento del sistema, empujando indirectamente al jugador experimentado hacia el disparo semi-automático calculado, more coherente con la filosofía de "cada bala cuenta".

### 2.5 El buff de resistencia de los "zombis del laboratorio" es una forma de escalar la dificultad sin tocar el HP visible

Que los zombis del tramo final (laboratorio) mantengan el mismo HP pero reciban menos daño de casi toda arma (hasta −33% con la pistola) es una decisión de diseño elegante: **desde la perspectiva del jugador, el enemigo "se siente" más resistente sin que el juego tenga que mostrarle una barra de vida distinta o un modelo distinto**. Es un ajuste de dificultad invisible, coherente con la filosofía general de RE de nunca mostrar números de HP en pantalla. La única excepción — el ácido sigue siendo instakill — es una decisión de diseño que preserva una "solución garantizada" para el jugador que gestionó bien su inventario de granadas especiales hasta el tramo final, recompensando la planificación de recursos a largo plazo.

### 2.6 El "poise" oculto de los zombis convierte el combate cuerpo a cuerpo en un mini-sistema de gestión de riesgo aparte del HP

El sistema de poise (15 a 31 puntos ocultos, resta fija por arma, umbral de 83 HP restante para poder ser derribado) es una capa de diseño completamente invisible para el jugador promedio, pero que tiene consecuencias tácticas reales: **un zombi con mucho HP restante no puede ser derribado aunque se rompa su poise**, lo que significa que gastar cuchillazos en un zombi "fresco" con la intención de derribarlo y pasar de largo es una estrategia que falla silenciosamente hasta que su HP baja lo suficiente. Esto crea una capa de "conocimiento tácito" que separa al jugador novato del experimentado sin necesidad de un tutorial explícito — un patrón de diseño típico de la era (comparable al "input reading" oculto de otros juegos de la generación PS1), y coherente con la filosofía general de la saga de premiar el aprendizaje por repetición y observación por sobre la explicación directa.

### 2.7 El sistema de rango convierte tres recursos escasos (curación, guardados, tiempo) en una sola métrica de "habilidad", y penaliza explícitamente las herramientas anti-frustración

Que las hierbas nunca penalicen el rango pero los sprays de primeros auxilios sí (−15 puntos cada uno) es una jerarquía de valor deliberada: **el juego premia la curación "gestionada" (hierbas, que exigen backtracking o planificación de inventario) frente a la curación "instantánea" (FAS, que cura completo sin esfuerzo)**. De igual modo, penalizar cada guardado (−5 puntos) convierte al recurso más "seguro" del juego (el ink ribbon) en un recurso con costo de oportunidad medible, reforzando la tensión constante entre seguridad y rendimiento. El dato más contundente en esta línea es la penalización fija de 50 puntos por usar cualquier arma de munición infinita: es una declaración de diseño explícita de que **la dificultad "real" del juego, a ojos de sus propios creadores, está ligada a la escasez de recursos**, no al daño de combate en sí — eliminar la escasez (con munición infinita) es tratado como "hacer trampa" incluso cuando el jugador dispara la misma cantidad de balas que gastaría con munición normal.

### 2.8 Conclusión: la escasez no está solo en la munición encontrada, sino codificada en decenas de reglas invisibles de daño, resistencia y recuperación

Tomados en conjunto, estos números muestran que "cada bala cuenta" en RE2 no es solo una consecuencia de la cantidad de munición que el diseño de niveles coloca en el mapa (tema ya cubierto en la investigación anterior sobre el diseño de niveles y el Zapping), sino una propiedad que **el sistema de combate refuerza en capas adicionales e invisibles**: HP discreto y aleatorio por enemigo, poise oculto que penaliza el desperdicio de golpes en enemigos "frescos", iframes que castigan el disparo automático sin criterio, resistencias variables por tramo del juego, y un sistema de puntuación que trata cualquier forma de abundancia (FAS, guardados extra, munición infinita) como una forma de trampa. Es un diseño de combate que, número a número, empuja consistentemente hacia la misma conclusión de diseño de nivel: la verdadera dificultad de Resident Evil 2 no está en la letalidad bruta de sus enemigos, sino en la gestión fina de un sistema de recursos que el juego mide y valora en todos sus rincones, visibles e invisibles.

---

## Fuente y método

Todos los datos numéricos de este documento provienen del testing de "cheezeit" en el hilo de GameFAQs *"RE2 (PS1) HP/Enemy/Weapon Mechanics"* para *Resident Evil 2: Dual Shock Edition* (PlayStation, SLUS-00748, dificultad Normal salvo donde se indique), obtenido por emulación (mednafen) y lectura directa de memoria del juego con herramientas de terceros. El propio autor documenta correcciones a sus datos originales a lo largo del hilo (por ejemplo, la corrección del umbral de tiempo de rango de 3:30/4:30 a 4:30/5:30), las cuales se incorporaron en este documento. No se han mezclado datos con el remake de 2019 en ningún punto de este análisis.

**Lagunas reconocidas:** el testing no incluye un desglose exhaustivo de HP para todos los enemigos menores (cuervos, ratas), ni una verificación independiente en la versión JPN/PAL del juego, por lo que no se puede descartar alguna variación regional menor no documentada aquí.
