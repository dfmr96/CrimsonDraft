# Datos de combate y análisis de game design de la trilogía clásica de Resident Evil (RE1 1996 · RE2 1998 · RE3 1999)

## TL;DR
- Los tres juegos comparten la filosofía de "cada bala cuenta", pero operan en **escalas numéricas incompatibles**: los originales de PS1 usan cifras de HP pequeñas (zombis de RE2 con ~65–143 HP; personajes de RE3 con 200 HP fijos), mientras que el remake de 2002 de RE1 (base del HD Remaster) reescaló todo a cifras grandes (zombis 300–2600 HP, magnum con 2500 de daño). Nunca debe darse un único número como "canónico".
- El principio de diseño más constante es la **regla de supervivencia a 0 HP**: casi todos los ataques primero te dejan en 0/1 HP y solo el siguiente golpe mata; RE3 lo hereda literalmente (200 HP, muerte solo con HP negativo) y añade **esquiva, giro rápido (quick-turn), crafting de munición por pólvora** y un sistema de **rango** por tiempo, guardados y curación.
- La evolución del combate va de la escasez pura con enemigos estáticos (RE1) → mayor variedad de armas y jefes con fases y puntos débiles (RE2, Tyrant/Birkin) → movilidad del jugador, munición fabricable con afinidades elementales y un perseguidor con IA de rastreo entre salas (Nemesis) en RE3.

## Key Findings
- **Escalas de HP incompatibles entre versiones.** No existe un único valor "canónico" de HP de zombi para la saga. En RE2 original de PS1 (versión Dual Shock, SLUS-00748, Normal) los zombis tienen HP aleatorio en el rango 65–143; en el remake de RE1 de 2002 / HD Remaster los zombis tienen 300–2600 HP. Presentar un solo valor sería un error de método.
- **La decapitación con escopeta a quemarropa es un instakill compartido** por los tres juegos: apuntar hacia arriba a corta distancia decapita/mata al zombi ignorando su HP restante.
- **Los perseguidores evolucionan de "jefe con guion" a "IA que rastrea".** El Tyrant de RE2 (T-00/Mr. X) aparece en encuentros con HP fijo por sala (220–400); Nemesis en RE3 persigue entre salas, conserva el daño acumulado, esquiva granadas y cohetes al caminar, y mata a cualquier zombi que golpee de un solo golpe.
- **RE3 introduce el crafting de munición (pólvora A/B/C), la esquiva, el quick-turn y un sistema de rango** basado en tiempo, número de guardados y HP recuperado.
- **El daño crítico/decapitación por headshot** está presente en RE1 (el Director's Cut da la Beretta "Custom" con probabilidad de instakill) y se codifica más formalmente en los remakes.

## Details

### Nota metodológica sobre fuentes y versiones
Los datos numéricos más fiables provienen de testing de la comunidad (data-mining con Cheat Engine y herramientas de speedrun) más que de cifras oficiales publicadas por Capcom, salvo las guías japonesas *Inside of BIO-HAZARD* (Famitsu, 1997, ISBN 4-89366-659-2) y *biohazard Kaitai Shinsho* (Famitsu, 2002). El problema central es que **la mayoría de tablas numéricas que circulan en la web corresponden a los remakes (RE1 2002, RE2/RE3 2020), no a los originales de PS1**. Este análisis distingue explícitamente cada caso y señala las lagunas.

Fuentes primarias empleadas: el índice de testing de "cheezeit" (pastebin.com/UA6EuvBH); el hilo de GameFAQs "RE2 (PS1) HP/Enemy/Weapon Mechanics" de cheezeit (testing directo en PS1 Dual Shock, Normal); el "RE3_damage_sheet_EN.pdf" de klardendum.com (datos del original de PS1, modo Original vs Arrange); la guía de daño de RE3 de speedrun.com; el foro de speedrun.com de RE1; wikis Fandom y StrategyWiki; foros de modding (residentevilmodding.boards.net, tapatalk RE123); y el artículo de diseño de mapas de Chris Pruett (horror.dreamdawn.com).

---

### 1) RESIDENT EVIL (1996 / Director's Cut / HD Remaster 2002)

**Salud del jugador y regla de 0 HP.** En el original de PS1, 0 HP no es muerte: solo el HP negativo mata. Un mordisco de zombi hace 10 de daño en la versión japonesa y 12 en la versión US/PAL y en New Game+. El estado "Danger" empieza en 23 HP para Jill y 34 HP para Chris, lo que refleja que Jill es el personaje "fácil" (más margen) y Chris el "difícil". Los perros tienen un mordisco en salto (~12 de daño) que se convierte en animación de instakill por debajo de cierto umbral de HP y —según testing de la comunidad de speedrun.com— solo tiene animación de muerte programada de frente, no por la espalda.

**HP de enemigos (remake 2002 / HD Remaster).** Estos valores son del **remake, NO del original de 1996** (el original usa una escala numérica menor y no documentada públicamente en cifras exactas). Según la tabla de daño de RE1 (versión GameCube/HD):
- Zombi: 300–2600 HP (aleatorio) · Crimson Head: 300–2600 HP · Crimson Head Prototype 1 (jefe): 2300–5600 HP
- Perro (Cerberus): 400–1230 · Araña (Web Spinner): 990–1590 · Hunter (α): 990–1600 · Chimera: 800–1220
- Yawn (serpiente): 4000 · Black Tiger (araña gigante): 4040
- Tyrant (T-002, primera forma): 800 · Super Tyrant (helipuerto): 6000
- HP de personaje en el remake: Chris 1400, Jill 960.

**El mecanismo Crimson Head (exclusivo del remake 2002/HD).** Un zombi "muerto" pero no eliminado correctamente resucita como Crimson Head, más rápido y letal. Se previene: (a) destruyendo la cabeza o una extremidad mientras está activo, (b) quemando el cuerpo con queroseno/canteen, o (c) con las balas incendiarias del lanzagranadas de Jill. El tiempo de resurrección depende de la dificultad — según la guía de AVVesker (Neoseeker/GameFAQs): *"Easy – Usually an hour, sometimes less. Normal – 30–45 minutes, sometimes less. Hard – 20–30 minutes, sometimes less."* La probabilidad de "V-ACT" está ligada a un "rank" preasignado al zombi. Según la wiki Fandom (Crimson Head/gameplay, tabla adaptada de Famitsu / *Kaitai Shinsho*): *"Ranks 1 and 2 appear in EASY; Rank 3 on NORMAL, and Ranks 4, 5 and 6 on HARD. Each of the available minutes is picked at random. For example, a Rank 1 Zombie has a 25% chance of taking one hour."* Un impacto de magnum o de bala entre los ojos es crítico e instakill, evitando la mutación.

**Daño de armas (tabla del remake 2002/HD; formato cerca/media/lejos para escopetas):**
- Cuchillo: 140 (crítico 340) vs zombi
- Beretta (handgun): 200 de daño base, con crítico marcado como 9999 (instakill) — el crítico de la pistola es la decapitación/muerte instantánea
- Escopeta (Remington M870): 1000/666/500 (cerca/media/lejos) vs zombi
- Escopeta de asalto (Richard's): 1300/866/650 vs zombi
- Magnum (Colt Python): 2500 vs zombi (instakill efectivo); vs Hunter 1300
- Lanzagranadas — Explosivo 2000 (directo)/1000 (splash); Ácido 2000/1000; Incendiario 2000/1000 vs zombi
- Self Defense Gun y Magnum de Barry: 9999 (instakill) contra la mayoría; lanzacohetes: 9999 (instakill universal)
- Lanzallamas (Chris): daño bajo, ~4 s de llama para matar un zombi; su mejor uso es contra arañas.

**Diferencias por versión (originales vs Director's Cut).** El cambio de balance mejor documentado, confirmado por la wiki Fandom citando *Inside of BIO-HAZARD*: *"Magnums, namely the Colt Python, initially took two shots to take down the Hunter. This was changed in the Director's Cut version to only one. They have remained a one-hit kill in all subsequent appearances."* Es decir, en el **original de 1996** el magnum necesitaba **2 disparos** para matar a un Hunter; el **Director's Cut lo redujo a 1**. El Director's Cut también sustituye la Beretta estándar por una "Custom Edition" con probabilidad aleatoria de instakill (crítico) contra cualquier enemigo salvo jefes.

**Comportamiento/derribo.** Los zombis se tambalean o "caen" antes de morir; conviene dejar de disparar cuando empiezan a colapsar porque el daño no cuenta durante la animación de caída, y rematarlos ya en el suelo. El disparo a las rodillas o el cuchillo pueden derribar sin matar. El combate de RE1 está subordinado al diseño de mapa "Recursive Unlocking": la escasez de munición hace que el juego se vuelva un problema de rutas y evasión. Como escribe Chris Pruett en "Recursive Unlocking: Analyzing Resident Evil's Map Design with Data Visualization" (horror.dreamdawn.com): *"There's simply not enough ammo to dispatch all of the zombies in the game, so route planning and deftly maneuvering through the Victorian building is eventually the key source of challenge."*

---

### 2) RESIDENT EVIL 2 (1998, PS1 original)

Datos del testing de cheezeit en la versión Dual Shock de PS1 (SLUS-00748), dificultad Normal. La muerte requiere HP **negativo** (un jefe de 600 HP necesita 601 de daño). **Importante: RE2 original NO tiene dificultad adaptativa** — esa es una característica del remake de 2019.

**Salud del jugador y mecánica de "mercy".** Leon y Claire comparten el mismo HP máximo (valor numérico exacto no confirmado en el testing; se expresa en umbrales porcentuales: Fine >50%, Yellow Caution 50%, Orange Caution 20%, Danger 10%). Existe una mecánica de "mercy": por encima de cierto umbral de HP, un ataque que "debería" matar deja al jugador en 1 HP. Además los zombis tienen "auto-resist" en el segundo mordisco: si el primero te deja en 1 HP, el juego te libera automáticamente del segundo, de modo que un zombi normal solo puede matarte desde estado Danger. Las Ivys son la excepción peligrosa: su agarre no tiene auto-resist en el segundo golpe, así que pueden matar desde Yellow Caution.

**HP de enemigos (RNG salvo jefes):**
- Zombi: valores discretos entre 65 y 143 (65, 73, 75, 80, 81, 90, 94, 95, 97, 98, 109, 110, 143); los zombis de calle previos a comisaría llevan −15 HP
- Zombi (escena de Brad): 250
- Perro zombi: rangos 69–72, 80–83, 95–98, 129–132
- Licker: 85–125; Licker evolucionado (2ª mitad): 77–116 (el gas del laboratorio resta 30 HP)
- Araña: 99–132; Ivy: 79–132 (gas del lab: −50 HP)
- Polilla (Moth): 150; Caimán (Alligator): 240
- **Tyrant / Mr. X (T-00):** HP fijo variable por sala: 220 (pasillo del tigre disecado), 300 (varias salas), 350 (pasillo del ascensor), 400 (pasillo de interrogatorios/cuervos)
- **Super Tyrant:** 200 HP — solo el lanzacohetes lo mata (necesita 201+ de daño), o esquivarlo 60 segundos para que Ada lance el arma
- **William Birkin (formas G):** GAdult (primera forma parasitaria) 600; G1 (brazo mutado) 500; G2 700 (regenera 3,75 HP/seg por debajo de 500 HP salvo en estado crítico); G3 300 en escenario A / 900 en escenario B; G4 700; G5 (final) 600. A diferencia del remake, G5 en el original no tiene ataque de muerte instantánea; solo mata el temporizador. (Nota: la numeración de las formas de Birkin varía entre fuentes; aquí se listan los 6 encuentros de jefe tal como los etiqueta cheezeit.)

**Daño de armas (vs zombi, formato cerca/media/lejos donde aplica):**
- Cuchillo: 3 vs zombi (escala hasta ~20 vs formas G tardías)
- Pistola (Leon/Claire, misma base): 16/15/14 (cerca/media/lejos); la Colt SAA ≈ pistola, a veces +1; la Custom "Burst" y la Colt tienen "iframes" en algunos enemigos (solo los disparos impares 1º/3º/5º dañan)
- Cadencia: normal 0,8 s; con disfraz 0,55 s; Custom Burst 0,35 s
- Escopeta: Instakill/60/40 (el disparo a quemarropa mata hasta 3 zombis; apuntar arriba decapita); Escopeta Custom: Instakill/80/60 (tan potente que no deja "crawlers")
- Bowgun (ballesta, solo Claire): 3 flechas de 30 c/u (90 total) vs zombi; no decapita
- Lanzagranadas (vs zombi): Granada normal 50 o 200 (200 si conectan varios fragmentos); Ácido 200; Llama 200 — las especiales hacen una deducción fija de golpe (salvo daño por tiempo en Brad); ácido y llama son más fuertes contra enemigos post-comisaría
- Magnum (Desert Eagle): instakill vs zombi; contra jefes tiene ligera caída por distancia (vs Tyrant 81/78/75; vs G1 90/88/85); Magnum Custom: sin caída y perfora varios enemigos (vs Tyrant 100 fijo)
- Subfusil (MAC-11): 4/impacto (40/seg) vs zombi
- Sparkshot: 60 vs zombi; Gatling: 8/impacto (80/seg), codificada como SMG con windup y ~80% más daño por bala; Lanzallamas (Claire): 15/impacto (90/seg) + 4/seg de afterburn (el DoT solo afecta a zombis)
- Lanzacohetes: instakill vs enemigos normales; daño fijo vs jefes (p.ej. G2 300, Tyrant "Instakill (500)")

**Comportamiento/derribo.** Los zombis tienden a caer con 3 headshots, 6 bodyshots o 5 legshots. El Tyrant final se puede aturdir en su carga con 2–3 balas de pistola. El combate de RE2 mantiene la escasez pero introduce jefes con múltiples fases (Birkin G1–G5) y puntos débiles (el ojo/núcleo de Birkin), más un perseguidor recurrente con HP fijo por sala (Mr. X).

---

### 3) RESIDENT EVIL 3: NEMESIS (1999, PS1 original)

Datos del RE3_damage_sheet_EN.pdf (klardendum.com), que distingue **modo Original (regiones JPN/CHN)** y **modo Arrange (regiones USA/PAL)** — donde Arrange = versión occidental. Las cifras difieren entre modos.

**Salud del jugador y regla de 0 HP.** Todos los personajes jugables (Jill, Carlos) tienen **200 HP** y sobreviven a 0 HP: solo el HP negativo mata, en cualquier modo y dificultad. Umbral "Caution" por debajo de 100 HP; varios ataques de instakill de Hunters solo se activan cuando el personaje está por debajo de 100 HP.

**HP de enemigos (original de PS1).** Las cifras exactas de los enemigos comunes de RE3 original están peor documentadas que su daño; el testing hexadecimal de modders confirma que los valores base son pequeños (el zombi tiene un byte de HP base editado en "0A" en las tablas del EXE de PC, coherente con la escala baja de PS1). El dato más firme es la salud de **Nemesis**: en la versión occidental (Arrange/US-PAL) tiene **900 HP** y "cae" (se desploma) cuando su HP baja de **400**; luego hay que hacerle daño adicional para inutilizar la forma. Lo confirma el modder residentevilartist en el hilo "RE3: Change Enemy Health" (tapatalk RE123): *"his original health is 900 and he falls on 400."* Otro modder, midiendo por knife-only en la versión PC/PAL, reportó cifras distintas: *"So, Nemesis, in Easy Mode on my PC version, the PAL version... has 720 HP. On hard, my test revealed that it took 51 knife stabs... his health is 920 on Hard."* Hay, por tanto, discrepancia entre fuentes y versiones, y no debe darse un único número como definitivo. La versión estadounidense requiere más disparos que la japonesa (por eso los speedrunners prefieren la japonesa).

**Daño que recibe el jugador (Original / Arrange):**
- Zombi mordisco: 20 / 30; mordisco a la pierna 5/5 (no puede matar); vómito 10/20
- Perro zombi (mordisco en salto): 12 / 22
- Drain Deimos / Brain Sucker (agarre): 30 / 40; zarpazo 10/20
- Araña grande (mordisco): 20/30; escupitajo de veneno 10/20
- Hunter Beta: zarpazo 15/25; zarpazo en salto 25/35; **decapitación (instakill)** posible con <100 HP
- Hunter Gamma: zarpazo 15/25; **"Eat" (instakill)** por engullimiento, encadenado desde el claw thrust con <100 HP
- Nemesis: puñetazo al caminar 20/30; puñetazo con carrera 25/35; agarre-lanzamiento 25/35; **empalamiento con tentáculo (5 + instakill)** si no se escapa; su lanzacohetes 40–70 según el encuentro; su "Rib Scissor" por la espalda hace 101
- Barril rojo explosivo: instakill a corta (~300 de daño), ~100 a media distancia

**Mecánicas nuevas de combate en RE3** (confirmadas por la wiki Fandom, gameplay):
- **Giro rápido (quick-turn):** rotación de 180° con combinación de botones, implementada después en toda la saga
- **Esquiva (dodge):** evade ataques enemigos; muy útil porque los enemigos de RE3 son más rápidos y ahora se mueven por las escaleras
- **Subir escaleras sin pulsar botón** (primer juego de la saga en hacerlo)
- **Barriles/objetos explosivos** disparables para daño de área
- **Aleatorización:** posiciones de munición, medicina e incluso algunas armas, y colocación de enemigos, varían entre partidas
- **Live Selection:** decisiones rápidas con tiempo límite que afectan el arena de combate y la historia

**Crafting de munición (pólvora).** RE3 introduce la fabricación de munición combinando pólvora (Reloading Tool + pólvora A/B/C y sus mezclas) para producir balas de distintos tipos — evolución directa del principio de escasez: en vez de solo encontrar munición, el jugador la gestiona y "fabrica" el tipo que necesita.

**Tipos de munición del lanzagranadas y afinidades.** El lanzagranadas de RE3 usa granadas normales, de ácido, de llama y de congelación (freeze). Según las notas de cheezeit (referenciadas en el pastebin index): los zombis y perros de RE3 son **débiles al ácido**, y las balas de llama son mejores contra las arañas. El disposal Nemesis final cae con ~11 balas de congelación y el Grave Digger con ~12 de ácido, según testing de la comunidad de residentevil.org.

**Sistema de rango/puntuación.** RE3 puntúa sobre tres factores (guía oficial *BIOHAZARD 3 LAST ESCAPE*): tiempo de juego, número de guardados y HP recuperado con objetos de curación. Cada factor da puntos; la suma total determina el rango. Umbrales: 100 pts por terminar en ≤2:30:00, decreciendo por tramos; 100 pts por 0 guardados, decreciendo. Se necesitan ~270+ puntos para rango A/S. El modo Easy no otorga ranking ni epílogo. La dificultad afecta al equipo inicial: en Easy Jill empieza con rifle de asalto, botiquín y las armas en la caja; en Hard solo con pistola y cuchillo. Nightmare y Expert (solo en Dreamcast/PC) hacen a los enemigos mucho más letales.

**IA de Nemesis (perseguidor).** A diferencia del Tyrant de RE2, Nemesis persigue a Jill entre salas, conserva el daño acumulado entre habitaciones de un mismo encuentro (excepto cuando lo enfrenta Carlos, cuyo daño no se guarda salvo que lo derribe), esquiva todas las granadas y cohetes cuando "camina con decisión" (power walking), mata a cualquier zombi que golpee de un solo golpe, y tiene una "regla anti-instakill": con el lanzacohetes su daño siempre se limita a umbrales, sobreviviendo con 0 HP si se le golpea al levantarse (solo el siguiente ataque lo mata).

---

### 4) Evolución y comparación entre los tres juegos

**Lo que se mantuvo constante:**
- **Escasez de munición / "cada bala cuenta":** núcleo de los tres, subordinando el combate a la gestión de recursos y la evasión.
- **Regla de supervivencia a 0 HP:** presente en RE1, RE2 y RE3 (muerte solo con HP negativo).
- **Decapitación con escopeta a quemarropa** como instakill que ignora el HP.
- **Magnum y lanzacohetes** como armas "de jefe" de altísimo daño y munición escasísima.
- **HP de enemigos aleatorizado** dentro de rangos por tipo (RE1 remake, RE2, RE3).

**Lo que evolucionó:**
- **Escala numérica:** los originales de PS1 usan HP pequeño (RE2 zombis 65–143; RE3 personajes 200); el remake de RE1 reescaló a miles.
- **Enemigos perseguidores:** de ausencia (RE1, salvo el Tyrant final scripteado) → Mr. X con HP fijo por sala (RE2) → Nemesis con IA de rastreo entre salas, daño persistente y esquiva de proyectiles (RE3).
- **Movilidad del jugador:** RE3 añade esquiva, quick-turn, subir escaleras automáticamente y objetos explosivos del entorno.
- **Gestión de munición:** de solo recolección (RE1/RE2) → crafting por pólvora con tipos especializados y afinidades elementales (RE3: ácido vs zombis/perros, llama vs arañas).
- **Jefes con fases y puntos débiles:** RE1 tiene jefes de HP alto pero simples; RE2 formaliza formas múltiples (Birkin G1–G5) con puntos débiles (ojo/núcleo); RE3 da a Nemesis varias formas y un corazón como punto débil expuesto por ventanas de tiempo.
- **Sistemas de rango:** RE2 y RE3 formalizan puntuación por tiempo/guardados/curación con recompensas (armas infinitas), un incentivo de rejugabilidad ligado al desempeño.
- **Crimson Heads:** exclusivos del remake de RE1 (2002), no de los originales; añaden una capa de "gestión de cadáveres" ausente en RE2/RE3 originales.

## Recommendations
- **Para diseño/análisis comparativo:** trata cada versión como un dataset separado. Nunca mezcles cifras del remake (RE1 2002, RE2/RE3 2020) con las de los originales de PS1; la escala numérica es incompatible (zombi de ~100 HP en PS1 vs 2600 en el remake).
- **Para citar HP de zombi:** usa rangos, no valores únicos: RE2 PS1 65–143; RE1 remake 300–2600. Señala siempre la versión.
- **Para el HP de Nemesis:** cita el rango con la discrepancia explícita — **900 HP con caída a 400** (Arrange/occidental, según klardendum y el modder residentevilartist); **~720 (Easy) / ~920 (Hard)** según medición knife-only en PC/PAL. La versión japonesa requiere menos daño que la estadounidense.
- **Umbrales que cambiarían las conclusiones:** si aparece un data-mine oficial del EXE de RE1 1996 (*Inside of BIO-HAZARD* digitalizado) o del binario de RE2 PS1 con el HP máximo del personaje, actualiza esas dos lagunas (HP exacto del personaje en RE1 y RE2 originales).
- **Para verificación adicional:** el hilo de GameFAQs de cheezeit para RE2 PS1 y el damage sheet de klardendum para RE3 son las fuentes más granulares; para RE1 original conviene buscar el escaneo de *Inside of BIO-HAZARD*.

## Caveats
- **Datos de comunidad, no oficiales.** La mayoría de cifras exactas provienen de testing con Cheat Engine y herramientas de speedrun, no de Capcom. Son internamente consistentes y version-specific, pero no verificados oficialmente.
- **Laguna: HP máximo numérico del personaje** en RE1 original y RE2 original no está confirmado (solo umbrales porcentuales y de estado). En RE1 remake se cita Chris 1400 / Jill 960; en RE3 el personaje tiene 200 HP fijos.
- **HP de enemigos comunes de RE3 original** está peor documentado en cifras exactas que su daño; los valores hexadecimales de modders (byte "0A") sugieren escala baja pero no son un mapeo completo al HP jugable.
- **Discrepancias por región/versión reconocidas:** RE3 Original (JPN/CHN) vs Arrange (USA/PAL) difieren en daño y posiblemente en HP de jefes. Según el damage sheet de klardendum, el modo Arrange de la edición Mediakite JP para PC es una anomalía: *"Except the Arrange mode of Mediakite, where it brings him to 425 HP from 900, so you need to deal 25 more damage to him for him to go down the first time"* — es decir, da más HP a los jefes o no adapta el daño del lanzacohetes.
- **RE1 original vs remake:** las tablas numéricas de HP/daño ampliamente difundidas (zombi 300–2600, etc.) son del remake de 2002; los números internos exactos del original de 1996 no están documentados públicamente en inglés más allá de comportamientos (2 disparos de magnum al Hunter en el original vs 1 en Director's Cut).
- **Dificultad adaptativa:** es una característica de los remakes de 2019/2020, NO de los originales de PS1 de RE2 y RE3. Los originales usan HP aleatorio por RNG dentro de rangos fijos, no ajuste dinámico por desempeño.