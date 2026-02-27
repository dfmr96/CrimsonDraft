# Diseño de Combate y Armas

## Influencias de Gameplay

| Referencia | Elemento Tomado |
|---|---|
| Sweet Home (SNES) | Grupo atrapado en espacio cerrado, survival |
| Resident Evil Gaiden | Combate hibrido con tension, barra movil para resolver ataques |
| Parasite Eve | RPG con gestion de recursos, municion como recurso fisico, posicionamiento + timing |
| Chrono Trigger | Combates en el mismo mundo, sin pantalla de transicion, ATB |
| Shadow Hearts | Judgment Ring: barra con zonas criticas donde el jugador debe presionar en timing |
| Lost Odyssey | Ring timing: anillo que se cierra, afecta daño y precision |
| Vagrant Story | Targeting por zona anatomica dentro de sistema tactico, chain attacks |

---

## Filosofia de Combate

Este NO es un RPG de empoderamiento. Es un RPG de desgaste.

- En Chrono Trigger, combatir es progreso y poder creciente
- En Crimson Draft, combatir es **costo, desgaste, riesgo y decision moral**
- La pregunta constante del jugador: "Gasto balas ahora?"

### El combate no siempre es buena idea

- A veces obligatorio
- A veces evitable
- A veces un error

El sistema de objetivos reactivos permite:
- Enemigos que desaparecen si avanzas historia
- Otros que escalan si los ignoras
- Otros que consumen recursos del mapa si los dejas vivos

---

## Sistema de Armas

### Estructura por operador

Cada operador lleva **dos armas**:

1. **Sidearm (pistola)** - Ataque basico, siempre disponible (evita softlocks)
2. **Arma primaria** - Subfusil, escopeta o rifle. Habilidades especiales, municion escasa

### Composicion del party (ver [[Personajes]])

El party cambia a lo largo del juego:
- **Acto I:** Mateo + 2 MSRT (mueren por daño acumulado en 4 encuentros) + CIA (desde Encuentro 1)
- **Acto II-IV:** Mateo + Navy SEALs + Agente CIA (intermitente)
- **Acto IV-V:** Mateo + Navy SEALs (sin CIA)

### Loadout MSRT (Acto I)

Equipo estandar de Coast Guard para abordaje maritimo. Toda la municion es **anti-ricochet** — diseñada para no rebotar en paredes metalicas de barcos.

| Operador | Sidearm | Primaria | Notas |
|---|---|---|---|
| Mateo (lider) | SIG P229 (9mm RIP) | H&K MP5 (9mm RIP) | Versatil, subfusil confiable |
| Operador Joven | SIG P229 (9mm RIP) | Benelli M4 (00 buckshot) | Escopeta para pasillos |
| Francotirador | SIG P229 (9mm RIP) | Mk18 (5.56 RRLP) | Rifle CQB, municion anti-ricochet |

### Loadout Navy SEALs (Acto II+)

Equipo militar completo. Municion estandar NATO — **penetra proteccion** pero puede rebotar.

| Operador | Sidearm | Primaria | Notas |
|---|---|---|---|
| SEAL 1 | SIG P226 MK25 (9mm M882 FMJ) | Benelli M4 (00 buckshot) | Escopeta + FMJ |
| SEAL 2 | SIG P226 MK25 (9mm M882 FMJ) | H&K MP7 (4.6x30mm) | PDW anti-armadura |
| SEAL 3 | SIG P226 MK25 (9mm M882 FMJ) | Mk18 Mod 1 (5.56 NATO) | Rifle estandar |

### Loadout Agente CIA

Armamento premium, suprimido. Pocos tiros pero devastadores. Municion muy escasa.

| Operador | Sidearm | Primaria | Notas |
|---|---|---|---|
| Agente CIA | H&K USP Tactical (.45 ACP suprimida) | SIG MCX Rattler (.300 Blackout) | Silencioso, letal, raro |

### Categorias de arma

| Categoria | Armas | Rol en combate | QTE |
|---|---|---|---|
| **Pistola** | P229, P226 MK25, USP Tactical | Ataque basico, siempre disponible | Barra lenta, dispersion baja |
| **Subfusil (SMG)** | MP5, MP7 | Rafagas rapidas, control | Barra media, dispersion media |
| **Escopeta** | Benelli M4 | Desmembramiento, pasillos, multiples perdigones | Barra lenta, dispersion alta, 6 perdigones |
| **Rifle** | Mk18, Mk18 Mod 1, MCX Rattler | Daño alto, penetracion | Barra rapida, dispersion minima |

### Progresion de armamento

La llegada de los SEALs en el Acto II es un **salto de poder**:
- El jugador pasa de municion anti-ricochet (buena vs carne, mala vs proteccion) a municion militar real
- La 9mm M882 FMJ del P226 es la primera municion que penetra proteccion de forma viable
- El 4.6x30mm del MP7 es literalmente diseñado para perforar armadura corporal
- El .300 Blackout del MCX Rattler es devastador pero escasisimo

### Ventajas del sistema:
- El ataque basico (sidearm) nunca desaparece (evita softlocks)
- Las primarias se vuelven decisiones estrategicas
- El recurso no es "mana", es algo fisico: municion
- Cada faccion tiene una identidad de armamento distinta

---

## Tipos de Municion

### Catalogo completo

| Calibre | Armas que lo usan | Disponibilidad | Faccion de origen |
|---|---|---|---|
| **9mm RIP** | P229, MP5 | Comun (desde inicio) | MSRT |
| **9mm M882 FMJ** | P226 MK25 | Acto II+ (llegan SEALs) | Navy SEALs |
| **00 Buckshot** | Benelli M4 | Raro | MSRT / SEALs |
| **5.56 RRLP** | Mk18 | Raro | MSRT |
| **5.56 NATO** | Mk18 Mod 1 | Raro (Acto II+) | Navy SEALs |
| **4.6x30mm** | MP7 | Muy raro (Acto II+) | Navy SEALs |
| **.45 ACP suprimida** | USP Tactical | Muy raro | CIA |
| **.300 Blackout** | MCX Rattler | Escasisimo | CIA |

### Variantes de 9mm — RIP vs M882 FMJ

Las dos facciones usan pistolas de 9mm pero con municion radicalmente diferente. El jugador puede cargar cualquiera de las dos en cualquier pistola de 9mm (P229 o P226):

| Municion | vs Carne expuesta | vs Proteccion | Caracter |
|---|---|---|---|
| **9mm RIP (frangible)** | 1.0x daño (fragmentacion en tejido) | ~0.3x daño (se rompe contra placa) | Anti-ricochet, letal en carne |
| **9mm M882 FMJ** | 0.8x daño (atraviesa limpio) | ~0.8x daño (penetra parcialmente) | Versatil, penetra proteccion |

**Justificacion narrativa:** El MSRT carga 9mm RIP (Radically Invasive Projectile) como estandar para operaciones en espacios cerrados — en un barco con paredes metalicas, las balas convencionales rebotan. Las RIP se fragmentan al impactar superficies duras, eliminando el riesgo de ricochet. Esto es doctrina tactica real para CQB maritimo.

### Variantes de 5.56 — RRLP vs NATO

| Municion | vs Carne expuesta | vs Proteccion | Caracter |
|---|---|---|---|
| **5.56 RRLP** | 1.0x daño | ~0.5x (baja penetracion, diseñada asi) | Anti-ricochet para barcos |
| **5.56 NATO** | 0.9x daño | ~0.8x (penetracion estandar) | Militar, versatil |

El RRLP (Reduced Ricochet Limited Penetration) es la version anti-ricochet del 5.56 — misma filosofia que las 9mm RIP pero para rifle.

### Municion premium (CIA)

| Municion | Daño base | vs Proteccion | Notas |
|---|---|---|---|
| **.45 ACP suprimida** | Alto (1.3x vs pistola 9mm) | ~0.6x | Silenciosa, criticos altos |
| **.300 Blackout** | Muy alto (1.5x vs rifle 5.56) | ~0.7x | Suprimida, devastadora, escasisima |

Las armas del CIA son **objetivamente superiores** en daño bruto, pero su municion es tan rara que cada disparo es una decision de peso. Esto refuerza la narrativa: el CIA tiene equipo de elite, lo cual hace que su traicion sea mas impactante — el jugador pierde acceso a las mejores armas del party.

### 4.6x30mm (MP7) — Anti-armadura

| Municion | vs Carne expuesta | vs Proteccion | Notas |
|---|---|---|---|
| **4.6x30mm** | 0.7x (pequeño calibre) | ~0.9x (diseñado para penetrar) | El mejor calibre contra proteccion |

El 4.6x30mm del MP7 fue diseñado especificamente por H&K para penetrar armadura corporal NATO. Hace menos daño a carne que un 9mm, pero es el calibre mas efectivo del juego contra chalecos y placas. Esto le da al MP7 un rol tactico unico: el arma anti-armadura del party.

### Progresion de gameplay por municion

1. **Acto I (MSRT):** Solo 9mm RIP + 5.56 RRLP + buckshot. Enemigos sin proteccion — funciona bien
2. **Acto II (llegan SEALs):** 9mm M882 FMJ + 5.56 NATO + 4.6x30mm disponibles. Enemigos con proteccion empiezan a aparecer. El jugador tiene opciones
3. **CIA presente:** .45 ACP + .300 BLK disponibles pero escasas. El jugador las reserva para enemigos dificiles
4. **Post-traicion CIA:** Se pierde acceso al .45 y .300 BLK permanentemente. El jugador siente la perdida mecanica

**Decision de recarga:** Al recargar una pistola de 9mm, el jugador elige entre RIP y M882 FMJ (si tiene ambas). Al recargar un Mk18, elige entre RRLP y NATO (si tiene ambas). Esto agrega una capa tactica a cada recarga.

### Consumo por accion

- Disparo individual: 1 bala
- Rafaga de N disparos: N balas (el jugador elige cuantos)
- Disparo perforante (habilidad): 3 balas
- Recarga: 0 balas (rellena cargador, consume turno, elige tipo de municion)

Cada disparo y cada rafaga es una micro-decision economica. El jugador decide cuantas balas gastar por turno.

---

## Efectividad contra Infectados

Los infectados por [[Krokonil]] no sienten dolor. Esto cambia toda la estrategia:

| Arma | Efecto |
|---|---|
| Pistola (9mm) | Solo ralentiza. Headshot necesario. Control temporal |
| Escopeta | Desmembramiento util. Ideal en pasillos |
| Subfusil | Control de masas. Alto consumo de balas |
| Rifle | Penetracion craneal limpia. Recurso critico |

### Regla clave
- Disparo al torso: siguen avanzando
- Disparo a la cabeza: efectivo pero dificil
- Las armas pesadas son necesarias para eliminacion definitiva
- Pistola = control / Arma pesada = decision final

---

## Mecanicas de Supervivencia

### Escasez Justificada Narrativamente

La mision no era para combate prolongado:
- Equipo enviado para toma rapida
- Municion limitada por diseño
- Sin armas pesadas masivas
- Sin apoyo inmediato

La municion viene de:
- Lockers sellados en el barco
- Equipamiento ruso abandonado
- Paquetes tacticos que trajeron (limitados)

### Sin reabastecimiento magico
No hay cajas de municion convenientemente colocadas. Todo tiene justificacion.

---

## Mecanica "Magus" — Party Member Intermitente

El agente CIA es un miembro del party que entra y sale a lo largo de Actos II-IV (ver [[Personajes]]).

### Impacto en balance de combate:

| Estado del CIA | Efecto en el party |
|---|---|
| Presente | Party mas fuerte: +1 miembro, criticos altos, util en combate |
| Ausente | Party debilitado: -1 miembro, el jugador siente la perdida de DPS |
| Revelado (Acto IV+) | Permanentemente fuera: el party pierde poder para siempre |

### Implicaciones de diseño:

- Los combates cuando el CIA esta ausente deben ser balanceados para party -1
- Los sabotajes del CIA (que generan combates) ocurren cuando el esta ausente — el jugador enfrenta las consecuencias sin su ayuda
- Esto es deliberado: el CIA debilita al party ANTES de cada escalada para maximizar los datos de combate
- Cuando el jugador descubre la traicion, el impacto es tambien mecanico: el juego se sentia mas dificil sin el, y ahora sabe por que

### Muertes por desgaste (Acto I):

Los 2 MSRT del party inicial mueren por **daño acumulado** a lo largo de 4 encuentros de combate durante el descenso del puente a cubierta. No es una cinematica: el jugador los ve debilitarse piso a piso y caer durante el combate final. Esto establece:
- Muerte permanente como regla (no hay revive)
- El desgaste es real y visible — los personajes se deterioran encuentro a encuentro
- Las muertes se sienten inevitables pero organicas, no arbitrarias
- Ansiedad permanente sobre el resto del party

---

## Sistema ATB / Tiempo

El combate es tipo Chrono Trigger (en mundo, sin transicion), pero con armas de fuego hay riesgo de perder tension si es demasiado rapido.

Opciones para mantener tension horror:
- Mantener sistema tipo ATB
- Zonas de combate delimitadas dentro del mapa
- Tiempo desacoplado como en Parasite Eve

---

## Sistema de Punteria — QTE Bidimensional

### Concepto

El ataque basico ("Attack") no es automatico. El jugador ejecuta un **QTE de doble eje (X + Y)** que determina el punto exacto de impacto en el cuerpo del enemigo. Es como un blanco de practica de tiro (silueta de tiro).

**Referencia base:** RE Gaiden (barra horizontal) + Shadow Hearts (zonas criticas) + targeting anatomico (Vagrant Story).

### Flujo del ataque

1. El jugador elige "Attack" (disparo con pistola)
2. Aparece **silueta del enemigo** (blanco de tiro)
3. Barra vertical se mueve — el jugador presiona accion → se fija **eje Y** (altura)
4. Barra horizontal se mueve — el jugador presiona accion → se fija **eje X** (lateral)
5. Se aplica **dispersion** segun arma + vida del jugador (ver [[#Sistema de Dispersion]])
6. El punto final de impacto determina la **zona de impacto**
7. Se calcula daño segun zona + armadura

**Nota de diseño:** El eje Y (altura) se resuelve primero porque es la decision mas importante (cabeza vs torso vs piernas). Si fuera al reves, un timeout en X forzaria al jugador a perder su eleccion de altura.

**Tiempo limite:** Cada eje tiene un tiempo limite (default: 2 segundos). Si el jugador no presiona a tiempo, el eje se fija automaticamente en la posicion actual de la barra. Esto evita bloqueos y agrega presion.

### Zonas de impacto

El cuerpo del enemigo se divide en regiones:

| Zona | Efecto al impactar |
|---|---|
| Cabeza | Daño critico / stun / riesgo alto (zona pequeña) |
| Torso | Daño estable (zona grande, pero con armadura frecuente) |
| Brazos | Reduce precision del enemigo |
| Piernas | Reduce velocidad / turnos del enemigo |

### Precision y multiplicadores

La distancia al centro de la zona impactada define la precision real:

| Precision | Multiplicador | Condicion |
|---|---|---|
| Centro perfecto | 1.5x daño | Distancia normalizada < 20% |
| Impacto solido | 1.0x daño | Distancia normalizada < 60% |
| Borde de zona | 0.75x daño | Distancia normalizada >= 60% |
| MISS | 0 daño | Fuera de toda hitbox |

### Formula de daño

```
daño = daño_base_arma × multiplicador_zona × multiplicador_precision
```

La dispersion NO modifica multiplicadores — solo desplaza el punto de impacto. Si la dispersion mueve el impacto del torso a un brazo, el jugador recibe los multiplicadores del brazo, no una penalizacion artificial.

### Cuando se activa el QTE

| Accion | Sistema |
|---|---|
| **Attack (pistola)** | QTE completo X+Y |
| **Habilidades (arma pesada)** | Resolucion automatica o variacion simplificada |

Las habilidades NO usan el QTE completo. Son resolucion automatica basada en stats. Esto diferencia el ataque basico (skill expression) de las habilidades (decision estrategica).

### Variaciones para habilidades (opcional)

Cada habilidad podria tener una variacion simplificada del QTE:

| Habilidad | QTE | Descripcion |
|---|---|---|
| Disparo / Rafaga (cualquier arma) | X + Y completo (1er disparo) | QTE define punto de intencion, disparos subsiguientes se calculan desde impacto anterior con recoil |
| Disparo cargado / precision | Una barra lenta con zona critica | Timing puro |
| Ultimate | Sin QTE | Resolucion automatica cinematografica |

### Variables que modifican el QTE

| Variable | Efecto en el QTE |
|---|---|
| Arma: Pistola | Barra lenta (facil), dispersion baja (15px) |
| Arma: Rifle | Barra rapida (dificil, mas daño), dispersion minima (8px) |
| Arma: Escopeta | Barra lenta, dispersion alta (40px), 6 perdigones |
| Estado: Herido (leve) | Vibracion por latido, flash rojo, viñeta sutil, screen shake |
| Estado: Herido (medio) | + Ghost lines, ruido estatico, viñeta agresiva |
| Estado: Herido (critico) | + Silueta parpadea, todos los efectos maximos |
| Estado: Exposicion Krokonil | Barra intermitente |
| Stat: Precision del personaje | Velocidad de barra menor |
| Entorno: Oscuridad | Zona "hit" se vuelve intermitente |

### Regla critica: el disparo siempre ocurre

El jugador **nunca falla por timing**. Si el timing es malo, el disparo impacta una zona no ideal, pero siempre dispara. Sin embargo, si la dispersion o el recoil llevan el impacto fuera de toda hitbox, el resultado es **MISS** (0 daño). No hay "grazing shots" — o le das a algo, o fallas completamente.

### Experiencia que genera

- **Tension breve** (2-3 segundos por QTE, no rompe ritmo JRPG)
- **Agencia real** (el jugador apunta, no un dado)
- **Lectura tactica** (donde apuntar depende de la armadura del enemigo)
- **Skill expression** (el ataque basico no es relleno, es mecanica activa)
- Elimina frustracion tipo "95% chance miss" de XCOM

---

## Sistema de Rafaga (Burst Fire)

### Concepto

El jugador no dispara un solo tiro por turno. Al iniciar un ataque, **elige cuantos disparos** realizar de su cargador actual (1 hasta el maximo disponible). Solo el **primer disparo** usa el QTE completo — los disparos subsiguientes se calculan automaticamente encadenados desde el punto de impacto anterior.

### Flujo de rafaga

1. El jugador presiona atacar
2. Selecciona numero de disparos (1-N, limitado por municion disponible)
3. QTE bidimensional (X+Y) para el **primer disparo** unicamente
4. Se aplica dispersion de tres capas al primer disparo
5. Disparos 2, 3, ... N se calculan **desde el punto de impacto del disparo anterior**
6. Cada disparo subsiguiente aplica el **patron de recoil predefinido** del arma
7. Se muestra resultado completo: todos los impactos, daño por disparo, total

### Encadenamiento de disparos

Cada disparo en la rafaga **parte del punto de impacto anterior**, no del punto original del QTE. Esto significa:

- El recoil se acumula naturalmente — el arma "sube" con cada disparo
- El jugador que conoce el patron puede compensar apuntando bajo
- Rafagas largas pierden control progresivamente
- El primer disparo es el mas preciso; los ultimos pueden salirse del blanco

### Escopeta en rafaga

Cada disparo de la escopeta genera sus 6 perdigones. Los perdigones comparten el offset de recoil (L3) del disparo, pero cada perdigon tiene su propia dispersion L1 y L2 independiente. Esto significa que disparar 3 veces con escopeta = 18 perdigones totales.

### Decision tactica

La rafaga introduce una decision de riesgo-recompensa:
- **1 disparo:** Maximo control, conserva municion
- **3-5 disparos:** Balance agresivo, recoil manejable
- **Cargador completo:** Maximo daño potencial, pero los ultimos disparos probablemente sean MISS por recoil acumulado

---

## Sistema de Cargador y Recarga

### Concepto

Cada arma tiene un **cargador con capacidad limitada**. El jugador debe gestionar su municion y decidir cuando recargar.

### Capacidades

| Arma | Capacidad | Identidad |
|---|---|---|
| Pistola (9mm) | 15 balas | Muchos tiros, rafagas largas viables |
| Rifle | 10 balas | Balance, rafagas medias |
| Escopeta | 6 cartuchos | Pocos tiros, cada uno cuenta |

### Mecanica de recarga

- **Tecla R:** Recarga el arma actual al maximo
- La recarga **resetea el recoil acumulado** a 0 (el arma se estabiliza)
- Si el cargador esta vacio, no se puede disparar — el jugador debe recargar
- Cambiar de arma tambien resetea el recoil

### Decision tactica

- Recargar despues de pocos disparos para mantener el recoil bajo
- Aguantar con cargador parcial para no perder el turno recargando
- Disparar hasta vaciar para maximizar daño a costa de precision

---

## Sistema de Dispersion — Tres Capas de Incertidumbre

### Concepto

El QTE determina el **punto de intencion** del disparo, pero el impacto final se calcula a traves de **tres capas independientes de desviacion**. Cada capa representa una fuente distinta de imprecision, y su interaccion crea profundidad tactica.

El jugador siempre tiene agencia (elige donde apuntar), pero la dispersion agrega incertidumbre controlada: "apunte a la cabeza pero le di en el hombro porque estoy herido y el arma sube."

### Las tres capas

#### Layer 1 — Dispersion por vida (estado fisico)

Solo se aplica al **primer disparo** de una rafaga. Representa la incertidumbre del pulso del personaje.

```
radio_L1 = dispersion_base_arma × (1 + (1 - %vida) × (factor - 1))
```

- A **100% vida**: radio = dispersion base del arma (minimo)
- A **50% vida**: radio = base × 1.5
- A **0% vida**: radio = base × 2.0 (factor configurable)
- Punto aleatorio uniformemente distribuido dentro del circulo

**Excepcion: Escopeta.** Para la escopeta, L1 se aplica a **cada perdigon en cada disparo**, porque representa la dispersion natural del cartucho (spread), no la imprecision del pulso.

#### Layer 2 — Desviacion mecanica del arma

Se aplica **siempre** a cada disparo. Representa las imperfecciones mecanicas del arma.

- Offset uniforme en X e Y de `±weapon_deviation` pixeles
- Valores por arma: Pistola ±2px, Rifle ±1px, Escopeta ±3px

#### Layer 3 — Recoil acumulativo (patron predefinido)

Se aplica a partir del **segundo disparo** en una rafaga. Cada arma tiene un **patron de recoil predefinido** (ver [[#Patrones de Recoil por Arma]]) que dicta la direccion y magnitud del desplazamiento.

- El indice del patron avanza con cada disparo consecutivo
- Se añade una variacion aleatoria controlada por `pattern_spread`
- El primer disparo (indice 0) no tiene recoil — solo L1 + L2
- Los disparos se encadenan desde el impacto anterior, asi que el recoil se acumula

### Resultado final

```
impacto = punto_QTE + L1 (solo 1er disparo) + L2 (siempre) + L3 (desde 2do disparo)
```

El punto final se clampea a los limites de la grilla. Si cae fuera de toda hitbox, el resultado es **MISS** (0 daño).

### Dispersion por arma

| Arma | Daño base | Dispersion base (L1) | Desviacion mecanica (L2) | Velocidad QTE |
|---|---|---|---|---|
| Pistola (9mm) | 25 | 15px | ±2px | Lenta |
| Rifle | 45 | 8px | ±1px | Rapida |
| Escopeta | 12 × 6 perdigones | 40px | ±3px | Lenta |

### Escopeta — Disparo multiple

La escopeta dispara **6 perdigones** simultaneos. Cada perdigon tiene:
- **L1 independiente** — cada perdigon se dispersa dentro del radio (esto ES el spread del cartucho)
- **L2 independiente** — desviacion mecanica individual
- **L3 compartido** — todos los perdigones comparten el mismo offset de recoil del disparo

Comportamiento:
- Cada perdigon resuelve zona de impacto y daño por separado
- Daño total = suma de todos los perdigones que impactan
- Perdigones fuera de hitbox = MISS individual (0 daño para ese perdigon)
- A buena salud, los perdigones se concentran → alto daño en pocas zonas
- A baja vida, la dispersion los separa → daño repartido, mas MISS

### Reglas de dispersion

1. La dispersion puede llevar el impacto fuera de toda hitbox → **MISS** (0 daño)
2. La dispersion **no modifica multiplicadores** de daño — solo mueve el punto de impacto
3. El jugador siente la imprecision como "le di en el brazo en vez de la cabeza", no como un numero reducido
4. El punto de impacto se clampea dentro de la grilla de combate
5. L1 solo aplica al primer disparo (excepto perdigones de escopeta)

### Variables que afectan la dispersion

| Variable | Capa | Efecto |
|---|---|---|
| Arma equipada | L1, L2, L3 | Define radio base, desviacion mecanica y patron de recoil |
| % vida actual | L1 | A menor vida, mayor radio de dispersion base |
| Disparos consecutivos | L3 | Mas disparos = mas desplazamiento por patron de recoil |
| Recarga / cambio de arma | L3 | Resetea recoil acumulado a 0 |

### Feedback visual

Al resolver un disparo o rafaga, el jugador ve:
- **Circulo de dispersion** que crece con cada disparo consecutivo (muestra recoil acumulado)
- **Cruz de intencion** (donde quiso apuntar)
- **Linea de desvio** (de la intencion al impacto real)
- **Marca de impacto** por cada disparo de la rafaga
- Para escopeta: multiples puntos de impacto con colores por zona
- **Desglose de capas** en el panel de resultado: L1 (Vida), L2 (Arma), L3 (Retroceso)

### Interaccion con tiempo limite del QTE

Si el jugador no presiona a tiempo, el disparo se ejecuta automaticamente en la posicion actual de la barra. La dispersion se aplica igual. Esto significa que un timeout + baja vida puede resultar en un impacto muy suboptimo, reforzando la presion del sistema.

---

## Patrones de Recoil por Arma

### Concepto

Cada arma tiene un **patron de recoil predefinido** estilo CS:GO: una secuencia de offsets (dx, dy) que se aplican disparo a disparo. El patron es **determinista** con una pequeña variacion aleatoria (`pattern_spread`), lo que permite al jugador **aprender y compensar** el recoil de cada arma.

### Personalidad de cada arma

| Arma | Forma del patron | Compensacion del jugador |
|---|---|---|
| Pistola (9mm) | Forma de "7" — sube y luego deriva a la **derecha** | Tirar abajo-izquierda |
| Rifle | "J invertida" — sube **fuerte** y luego curva a la **izquierda** | Tirar abajo-derecha |
| Escopeta | Kick vertical **masivo**, luego baja a la derecha | Tirar muy abajo al inicio |

### Patrones detallados

**Pistola (9mm)** — `pattern_spread: 2`
- Disparo 2: arriba suave (0, -6)
- Disparos 3-5: empieza a derivar derecha
- Disparos 6-10: deriva derecha constante, menos subida
- Disparos 11-15: casi puro horizontal derecha, se estabiliza

**Rifle** — `pattern_spread: 2`
- Disparos 2-3: subida vertical agresiva (-14 a -16 px)
- Disparos 4-6: empieza a curvar izquierda
- Disparos 7-10: diagonal izquierda pronunciada, se aplana

**Escopeta** — `pattern_spread: 4`
- Disparo 2: kick vertical enorme (-25 px)
- Disparos 3-4: baja y tira derecha
- Disparos 5-6: se estabiliza horizontal derecha

### Variacion aleatoria

Cada offset del patron recibe una variacion de `±pattern_spread` pixeles en ambos ejes. Esto evita que el patron sea perfectamente memorizable pero mantiene su forma general reconocible. Valores mas altos (escopeta: 4) hacen el arma menos predecible.

### Mano dominante del operador

Cada operador tiene una **mano dominante** que afecta la direccion horizontal del recoil:

| Valor | Mano | Efecto |
|---|---|---|
| 1 | Diestro | Patron de recoil normal |
| -1 | Zurdo | Componente X del recoil **invertido** (espejado) |

Esto significa que el mismo arma se comporta diferente segun quien la use:
- Un diestro con la pistola ve el recoil derivar a la **derecha** (forma de "7")
- Un zurdo con la pistola ve el recoil derivar a la **izquierda** (forma de "7" espejado)

Solo se invierte el componente horizontal (X) del patron. El componente vertical (Y) y todas las demas capas (L1, L2) permanecen iguales. Esto obliga al jugador a **aprender la compensacion de recoil de cada operador**, no solo de cada arma.

### Reset de recoil

El indice del patron se resetea a 0 cuando:
- El jugador **recarga** (tecla R)
- El jugador **cambia de arma**
- Se inicia un nuevo turno de combate

---

## Tipos de Enemigo

Los enemigos a bordo del [[El Marinera|Marinera]] no son "monstruos". Son humanos en diferentes estados de deterioro y con diferentes niveles de equipamiento.

### Clasificacion

| Tipo | Origen | Vida | Armadura | Precision | Comportamiento |
|---|---|---|---|---|---|
| Tripulacion civil | Operadores del barco | Baja | Ninguna | Baja | Erraticos, pueden huir |
| Seguridad del barco | Guardias privados | Media | Chaleco ligero | Media | Usan cobertura |
| Militares infectados | Soldados del cargamento | Alta | Chaleco + casco | Variable | Agresivos, no sienten dolor |
| Rusos reactivados | Abordaje fallido previo | Alta | Equipamiento militar | Media-alta | Disparan, conservan entrenamiento residual |

### Fases de deterioro por [[Krokonil]]

Los infectados no son todos iguales. Su nivel de exposicion define su comportamiento:

| Fase | Estado | Comportamiento en combate |
|---|---|---|
| 1 | Confusos, erraticos | Se mueven sin patron claro. Atacan cuerpo a cuerpo sin coordinacion |
| 2 | Agresivos, resistentes | Ignoran dolor. Mas rapidos. Embisten |
| 3 | Casi tacticos | Conservan entrenamiento residual. Usan armas. Los mas peligrosos |

---

## Sistema de Eliminacion Permanente

### Concepto

Derrotar a un enemigo en combate (reducir su HP a 0) no lo elimina permanentemente — queda **inhabilitado**. El level design controla cuando y si ese enemigo vuelve a levantarse, lo que convierte la reactivacion en una herramienta de pacing narrativo.

Eliminar a un enemigo permanentemente requiere uno de tres metodos especificos.

### Estado "Derribado"

Cuando un enemigo pierde todo su HP en combate, queda caido e inhabilitado. No ataca, no se mueve. El level design determina cuando se reactiva — puede ser en un momento especifico de la narrativa, al entrar a cierta zona, o al avanzar la historia. El jugador no sabe exactamente cuando volveran, lo que genera ansiedad ante cuerpos que no quemo.

### Metodos de eliminacion permanente

#### Metodo 1 — Headshot critico (durante el combate)

Todo disparo que impacte la zona **CABEZA** tiene un **porcentaje fijo** de probabilidad de eliminar al enemigo instantaneamente en lugar de solo dañarlo. El porcentaje es plano — no cambia segun si el impacto fue centro perfecto, solido o borde de zona.

- **Justificacion narrativa:** Los infectados no sienten dolor y siguen funcionando con trauma corporal severo. Un disparo bien ubicado en la cabeza puede destruir el sistema nervioso central de forma irreversible. Pero es impredecible — el angulo, el movimiento, el calibre, todos los factores hacen que no sea garantizado.
- **Implicacion tactica:** El jugador que prioriza headshots tiene una chance de eliminar permanentemente en combate, ahorrando queroseno para despues. Pero no puede contar con ello.

> *Nota de iteracion futura: si las dimensiones del sprite del blanco en el QTE quedan definidas con suficiente precision, este porcentaje puede graduarse por precision de impacto (centro perfecto > solido > borde). Por ahora es plano para simplicidad y comunicacion clara.*

#### Metodo 2 — Destruccion craneal por escopeta (durante el combate)

Si el **daño total de un disparo de escopeta a la zona CABEZA en una sola accion** supera un umbral fijo X, el enemigo es eliminado instantaneamente. Funciona por acumulacion de perdigones:

- A distancia corta: los perdigones se concentran en la zona de cabeza, el daño total supera el umbral facilmente
- A distancia media: la dispersion separa los perdigones, menos impactan la cabeza, el total puede o no alcanzar el umbral
- A distancia larga: improbable por dispersion

**El umbral es determinista** — no hay azar mas alla del QTE y la dispersion. El jugador que cierra distancia con la Benelli M4 y apunta a la cabeza puede garantizar la eliminacion.

#### Metodo 3 — Queroseno (post-combate)

El jugador puede encontrar en el barco un **encendedor** y **queroseno**. Aplicado sobre un enemigo caido, garantiza la eliminacion permanente. No requiere habilidad de combate.

- El queroseno es un recurso escaso con justificacion narrativa (barco de carga, sala de maquinas)
- La decision no es "si quemarlo" sino **"a quien quemarlo"** — el jugador no sabe exactamente cuando volvera cada uno
- Usar queroseno en el enemigo equivocado puede significar que el peligroso se reactive en el peor momento

### La dualidad del sistema

Los metodos 1 y 2 son **recompensas de habilidad**: el jugador que ejecuta bien puede salir del encuentro con menos amenazas pendientes, sin gastar queroseno. El metodo 3 es el **seguro**: cuesta un recurso pero es seguro, y el level design determina la urgencia de usarlo. El metodo 4 es la **defensa reactiva**: convierte un ataque entrante en una oportunidad de eliminacion, a costa de un item que puede perderse permanentemente.

| Momento | Metodo | Costo | Certeza |
|---|---|---|---|
| Durante combate | Headshot critico (% plano) | Municion | Incierta — depende del dado |
| Durante combate | Escopeta threshold | Municion + proximidad | Determinista si se cumple el umbral |
| Post-combate | Queroseno | Queroseno (recurso escaso) | Garantizada |
| Durante combate (reactivo) | Defenderse con puñal | Puñal (riesgo de perderlo) | Incierta — depende del dado |
| Durante combate (reactivo) | Defenderse con granada | Granada (recurso unico) | Garantizada |

---

## Accion Defenderse

### Concepto

Accion **reactiva** disponible cuando un enemigo telegrafía un ataque contra el personaje activo. Solo esta disponible si ese personaje tiene un **item defensivo** en su inventario (puñal o granada). Sin item, la accion no existe — el ataque conecta con daño completo.

El jugador debe estar **controlando al personaje atacado** cuando activa la defensa. Si en ese momento controla otro personaje, debe cambiar primero — lo que puede costarle el QTE activo o la accion en curso.

### Items defensivos

#### Puñal de combate

Cada operador comienza con un cuchillo de combate (equipo militar estandar). Al activar la defensa con puñal:

- El daño del ataque entrante se **reduce**
- El personaje contraataca automaticamente con un **% de critico fijo**:
  - **Critico:** eliminacion permanente del atacante + **puñal recuperado**
  - **Sin critico:** el atacante recibe daño pero no muere permanentemente + **puñal perdido** permanentemente

La perdida del puñal es irreversible salvo encontrar otro en el barco. Un operador sin puñal no puede defenderse de ataques futuros.

#### Granada

Al activar la defensa con granada:

- El daño del ataque entrante se **reduce**
- **Eliminacion permanente garantizada** del atacante
- Granada **consumida**

La granada es el item defensivo mas poderoso — garantiza tanto la reduccion de daño como la eliminacion permanente — pero es de uso unico y muy escasa.

### La presion del sistema

El jugador enfrenta tres tensiones simultaneas al ver un telegrafio:

1. **Atencion:** ¿estoy controlando al personaje correcto o debo cambiar?
2. **Recursos:** ¿tiene puñal o granada? ¿Vale la pena gastar la granada aqui?
3. **Riesgo con puñal:** ¿activo la defensa sabiendo que puedo perder el puñal si no cae el critico?

Un operador que pierde el puñal sin conseguir el critico es un operador permanentemente vulnerable — no puede defenderse de ningun ataque futuro. Eso es el tipo de consecuencia que define el genero.

### Interaccion con el Sistema de Combate en Tiempo Real

La defensa no interrumpe el flujo de tiempo real. Mientras el jugador cambia de personaje para defender, los timers de los demas enemigos siguen corriendo y los QTEs activos continuan sin input. Defender a un personaje siempre tiene un costo de atencion sobre el resto del party. Ver [[Sistema de Combate en Tiempo Real]].

---

## Sistema de Proteccion por Capas

### Concepto

Los enemigos pueden llevar **proteccion fisica** (cascos, chalecos) que se implementa como **hitboxes superpuestas** sobre las hitboxes del cuerpo. La proteccion no es un stat global — es geometria visible que el jugador lee y explota.

Un impacto puede atravesar una hitbox de proteccion antes de llegar al cuerpo, o puede impactar una zona expuesta directamente. La decision de **donde apuntar** depende de lo que el enemigo lleva puesto.

### Proteccion como hitbox superpuesta

La proteccion se implementa como una **capa adicional de hitboxes** dibujada encima de las hitboxes del cuerpo. Cuando un disparo impacta:

1. Se evalua primero si toca una hitbox de proteccion
2. Si toca proteccion: el daño se reduce segun el tipo de proteccion y la municion usada
3. Si no toca proteccion: daño completo a la zona del cuerpo

Esto significa que la proteccion tiene **forma y cobertura**, no es un porcentaje global. Un chaleco que cubre solo el torax deja el estomago expuesto. Un casco militar deja la mandibula y el cuello al descubierto.

### Tipos de proteccion

#### Casco militar

Cubre la **parte superior de la cabeza** (~60-70% de la hitbox de cabeza). La mandibula y el cuello quedan expuestos.

- Reduccion de daño: ~50%
- El headshot critico sigue siendo posible apuntando a la parte baja de la cabeza (mandibula/cuello)
- El jugador aprende a apuntar "bajo dentro de la cabeza" para evitar el casco

#### Chaleco blando

Proteccion textil (Kevlar). Varias configuraciones de cobertura:

| Variante | Cobertura | Zonas expuestas |
|---|---|---|
| **Torax completo** | Pecho + espalda superior | Estomago, brazos, cuello |
| **Torax + estomago** | Pecho + abdomen | Brazos, cuello, flancos |
| **Un solo hombro** | Solo un pectoral | Pectoral opuesto, estomago, brazos |

- Reduccion de daño: ~60%
- La variante de un solo hombro es tematica de infectados: un zombie le arranco parte del chaleco, dejando un lado expuesto
- El jugador debe leer **cual lado** esta expuesto y apuntar ahi

#### Chaleco de placas (plate carrier)

Proteccion rigida con placas ceramicas/metalicas insertadas.

| Variante | Cobertura | Zonas expuestas |
|---|---|---|
| **Frontal + dorsal** | Pecho completo | Estomago bajo, flancos, brazos |
| **Solo frontal** | Pecho frontal | Estomago, flancos (si hay angulo) |

- Reduccion de daño: ~80%
- Las frangibles son casi inutiles contra placas (~0.3x)
- FMJ penetra parcialmente (~0.8x de la reduccion)
- Presente en militares y rusos reactivados

### Variantes narrativas de proteccion

| Tipo de enemigo | Proteccion tipica | Justificacion |
|---|---|---|
| Tripulacion civil | Ninguna | Sin equipo militar |
| Seguridad del barco | Chaleco blando (torax) | Equipo de seguridad privada |
| Infectado (Fase 1-2) | Ninguna o restos de chaleco (un hombro) | Deterioro por infeccion, arrancado por otros infectados |
| Infectado (Fase 3, ex-militar) | Chaleco + casco (parcial, dañado) | Conserva equipo pero deteriorado |
| Rusos reactivados | Placas + casco militar | Equipamiento militar completo |
| Soldados del cargamento | Placas + casco | Los mas protegidos |

### Feedback al jugador

| Resultado | Mensaje en pantalla |
|---|---|
| Daño normal (sin proteccion) | Numero de daño |
| Critico (zona vulnerable, centro perfecto) | Numero grande + efecto visual |
| Proteccion absorbe la mayor parte | **"ABSORBED"** + numero muy bajo |
| Impacto en zona expuesta de enemigo protegido | Daño completo (recompensa por lectura tactica) |

**MISS vs ABSORBED:** MISS es consecuencia de estado fisico o recoil descontrolado (fuera de hitbox = 0 daño). ABSORBED es mala decision tactica (le diste a la placa con frangibles). La diferencia es clave para el feedback.

### Interaccion municion + proteccion

| Municion | vs Sin proteccion | vs Chaleco blando | vs Placas |
|---|---|---|---|
| **9mm Frangible** | 1.0x | ~0.4x (se fragmenta) | ~0.2x (inutil) |
| **9mm FMJ** | 0.8x | ~0.7x (penetra parcial) | ~0.5x (penetra algo) |
| **Rifle** | 1.0x | ~0.8x (penetra) | ~0.7x (penetra bien) |
| **Escopeta** | 1.0x por perdigon | ~0.5x | ~0.3x |

### Interaccion armas + proteccion

| Arma | Rol contra proteccion |
|---|---|
| Pistola (frangible) | Casi inutil contra proteccion — apuntar a zonas expuestas |
| Pistola (FMJ) | Penetra parcialmente — versatil pero no ideal |
| Escopeta | Multiples perdigones pueden rodear la proteccion — algunos impactan zonas expuestas |
| Rifle | Mejor penetracion — puede atravesar chalecos blandos efectivamente |

### Lectura visual

La proteccion debe ser **visible en el sprite del enemigo**:
- Chaleco visible → el jugador sabe que el torso esta protegido
- Casco visible → el jugador sabe que parte de la cabeza esta protegida
- Chaleco de un solo hombro → el jugador identifica el lado expuesto
- Proteccion destruida → cambio visual (chaleco roto, casco partido)

El jugador nunca deberia sentir que la proteccion es informacion oculta. La lectura visual es parte de la tactica.

---

## Sistema de Vibracion por Latido (Heartbeat)

### Concepto

Las barras del QTE no parten de posiciones aleatorias. En su lugar, arrancan siempre desde la misma esquina (Y desde abajo, X desde la izquierda) pero sufren una **vibracion involuntaria** que escala con la vida perdida. Esta vibracion sigue un patron de **latido cardiaco** (lub-dub), no una oscilacion continua.

### Comportamiento del latido

El patron replica un latido real:
- **Lub** (0-12% del ciclo): pico fuerte, amplitud completa
- **Silencio** (12-18%): pausa breve
- **Dub** (18-28% del ciclo): pico menor, 60% de amplitud
- **Silencio largo** (28-100%): reposo hasta el siguiente latido

### Escalado con vida

| Variable | 100% HP | 50% HP | 10% HP |
|---|---|---|---|
| BPM | 60 (calmo) | 110 (agitado) | 160 (panico) |
| Amplitud maxima | 0px | 7px | 14px |
| Efecto en gameplay | Sin vibracion | Vibracion notable | Casi imposible de controlar |

A vida completa el operador esta calmo: sin vibracion, sin latido visible. A medida que pierde vida, el corazon se acelera y las manos tiemblan mas, afectando tanto la posicion visual de las barras como el punto real donde se fija el disparo al presionar accion.

### Impacto en el QTE

La vibracion se aplica al **momento de fijar** cada eje (SPACE o timeout). Esto significa que incluso si el jugador ve la barra en buena posicion, el offset del latido puede desviar el punto final unos pixeles. El jugador debe anticipar el ritmo del latido y presionar durante los silencios para minimizar la desviacion.

---

## Feedback Visual del Latido

### Concepto

El latido cardiaco no solo afecta la mecanica — es **visible** en la interfaz del QTE. Las barras, la grilla y los marcadores reaccionan al ritmo del corazon del operador. A mas vida perdida, mas agresivo el feedback visual.

### Efectos en las barras QTE

- **Color del marcador** pulsa de verde a rojo con cada latido
- **Tamaño del marcador** crece +10px en el pico del latido
- **Glow rojo** circular (radio hasta 50px, alpha 160) aparece alrededor del marcador
- **Track de la barra** pulsa rojo en fondo y borde
- **Borde del track** se engrosa de 1px a 3px con el latido

### Efecto en la grilla

- **Flash rojo** en el borde de la grilla durante cada pico (alpha hasta 200, grosor hasta 5px)
- El flash es visible desde vida con solo ~5% de daño, pero sutil
- Se vuelve agresivo y dominante a vida baja

### Escalado visual

El feedback visual usa un multiplicador de `damage_ratio * 1.8` para que sea perceptible incluso con poco daño perdido. Esto hace que el jugador note que algo cambia antes de que la vibracion mecanica sea un problema real.

---

## Sistema de Distracciones Visuales

### Concepto

Ademas del latido y la vibracion, el operador herido sufre **distracciones visuales progresivas** durante el QTE. Cada efecto tiene un umbral de activacion diferente, creando una escalada gradual: a medida que pierde vida, la experiencia de apuntar se degrada capa por capa.

Los efectos estan diseñados para dificultar **activamente** el QTE, no solo dar feedback estetico. Son penalizaciones reales al gameplay.

### Efectos y umbrales

| Efecto | Umbral de activacion | Intensidad maxima | Descripcion |
|---|---|---|---|
| Heartbeat (vibracion + flash) | 5% daño | 14px, alpha 200 | Latido cardiaco visible y mecanico |
| **Screen Shake** | 10% daño | ±8px | La grilla entera tiembla con cada latido |
| **Blood Vignette** | 15% daño | 40px profundidad, alpha 180 | Oscurecimiento rojo desde los bordes |
| **Ghost Lines** | 15% daño | 5px offset | Vision doble en las lineas de referencia |
| **Static Noise** | 25% daño | 150 pixeles | Interferencia visual parpadeante |
| **Silhouette Flicker** | 35% daño | 15% tiempo invisible | La silueta desaparece brevemente |

### Screen Shake (Sacudida de grilla)

La grilla, la silueta y las hitboxes se desplazan con un offset aleatorio sincronizado con el heartbeat. Las barras QTE **no** se sacuden — permanecen estables. Esto crea una disociacion entre la referencia del jugador (barras) y el objetivo (silueta), dificultando la coordinacion.

- Solo se activa durante picos del latido (lub y dub), no es constante
- Amplitud maxima: ±8px a vida minima
- El contenido de la grilla "brinca" brevemente y vuelve a su sitio entre latidos

### Blood Vignette (Viñeta de sangre)

Overlay rojo semitransparente que oscurece los **bordes** de la grilla hacia el centro. Simula vision periferica deteriorada / sangre en los ojos.

- Se dibuja despues de la grilla y silueta pero antes de las barras
- 4 franjas desde cada borde con alpha decreciente hacia el centro
- Profundidad: hasta 40px desde cada borde a vida minima
- Pulsa con el heartbeat: mas opaca durante lub/dub, mas transparente en silencio
- Reduce el area visible efectiva de la grilla, ocultando parcialmente las hitboxes perifericas

### Ghost Lines (Lineas fantasma)

Las lineas de la grilla se **duplican** con un offset que oscila suavemente, creando un efecto de vision doble.

- Las lineas fantasma son tenues (alpha ~50) y se mueven con `sin(time)`
- Offset maximo: 5px a vida minima
- Dificulta leer las coordenadas exactas de la grilla
- Solo visible durante QTE; en IDLE la grilla se ve normal

### Static Noise (Ruido estatico)

Pixeles aleatorios parpadeantes superpuestos sobre la grilla. Simulan interferencia visual o vision borrosa por dolor.

- Solo aparece a partir de 25% de daño (vida media-baja)
- Densidad: hasta 150 pixeles de 3x3 cada uno
- Color blanco/gris con alpha variable
- Se regeneran **cada frame** para efecto de parpadeo constante
- Entorpecen visualmente la lectura de la silueta y las zonas

### Silhouette Flicker (Parpadeo de silueta)

La silueta del enemigo **desaparece brevemente** durante el QTE, forzando al jugador a recordar de memoria donde estan las zonas.

- Solo se activa a partir de 35% de daño (vida baja)
- Las hitboxes **siguen visibles** si estan activadas — solo la silueta parpadea
- Periodo de parpadeo: 800ms a vida media → 400ms a vida minima (mas frecuente)
- Ventana de invisibilidad: hasta 15% del periodo a daño maximo
- El jugador debe memorizar la posicion del cuerpo o activar las hitboxes como apoyo

### Diseño de escalado

Los umbrales estan escalonados deliberadamente:
1. **5-15% daño:** El jugador nota que algo cambia (vibracion sutil, flash rojo, viñeta leve)
2. **15-25% daño:** Los efectos empiezan a estorbar activamente (shake, ghost lines, viñeta notable)
3. **25-35% daño:** El QTE se vuelve significativamente mas dificil (ruido visual denso)
4. **35%+ daño:** Condicion critica — la silueta parpadea, todos los efectos en maxima intensidad

Esto refuerza la filosofia de combate: **estar herido tiene consecuencias mecanicas reales**. No es solo un numero de HP bajando — el jugador **siente** fisicamente el deterioro de su operador a traves de la degradacion progresiva del QTE.

---

## Sistema de SFX Sinteticos

### Concepto

Todos los sonidos del prototipo se generan **programaticamente** con `numpy` + `pygame.mixer`. No hay archivos de audio externos. Cada sonido es una combinacion de ondas sinusoidales, cuadradas y/o ruido blanco, con envelopes de fade in/out para evitar clicks.

### Catalogo de sonidos

| Sonido | Trigger | Descripcion auditiva |
|---|---|---|
| **lock_y** | Fijar eje Y (SPACE) | Click agudo (800Hz, 60ms) |
| **lock_x** | Fijar eje X (SPACE) | Click mas agudo (1000Hz, 60ms) |
| **timeout** | Timeout de eje | Tono descendente (600Hz → 400Hz) — señal de penalizacion |
| **fire_pistol** | Disparo con pistola | Cuadrada 150Hz + ruido blanco, decay rapido |
| **fire_rifle** | Disparo con rifle | Cuadrada 100Hz + ruido corto, mas seco |
| **fire_shotgun** | Disparo con escopeta | Cuadrada 80Hz + ruido largo, grave y contundente |
| **hit_critical** | Centro perfecto | Tono alto brillante (1200Hz) — recompensa |
| **hit_solid** | Impacto solido | Tono medio (800Hz) — confirmacion |
| **hit_graze** | Borde de zona | Tono bajo suave (500Hz) — suboptimo |
| **miss** | Fuera de hitbox | Tono grave con fade largo (200Hz) — castigo |
| **reload** | Recargar (R) | Doble click metalico (2000Hz + 1500Hz) |
| **empty** | Disparar sin municion | Click seco cortisimo (3000Hz, 20ms) |
| **select_shot** | Cambiar cantidad de disparos | Pip agudo (1500Hz, 30ms) |
| **heartbeat_lub** | Pico del latido | Tono sub-grave (50Hz, 100ms), volumen escala con daño |
| **weapon_switch** | Cambiar de arma | Tono ascendente (600Hz → 900Hz) |

### Heartbeat sonoro

El latido cardiaco tiene componente auditivo ademas de visual. El sonido `heartbeat_lub` se reproduce sincronizado con los picos del latido visual (`beat_t > 0.5`). Su volumen escala con `damage_ratio`:

- **100% HP:** Sin sonido (umbral > 10% daño)
- **50% HP:** Latido suave, audible pero no molesto
- **10% HP:** Latido fuerte y rapido, contribuye a la sensacion de panico

### Sonidos de disparo por arma

Cada arma tiene una "personalidad" sonora distinta generada con la misma base (onda cuadrada + ruido blanco) pero con parametros diferentes:

| Arma | Frecuencia base | Duracion | Ruido | Caracter |
|---|---|---|---|---|
| Pistola | 150Hz | 100ms | 60ms | Medio, equilibrado |
| Rifle | 100Hz | 80ms | 40ms | Seco, corto, preciso |
| Escopeta | 80Hz | 150ms | 100ms | Grave, largo, contundente |

### Feedback de impacto por precision

El sonido de impacto comunica la calidad del disparo **antes** de que el jugador lea los numeros:

- **CENTRO PERFECTO** → tono alto y satisfactorio (1200Hz)
- **IMPACTO SOLIDO** → tono medio, confirmacion (800Hz)
- **BORDE** → tono bajo, suboptimo (500Hz)
- **MISS** → tono grave con fade largo, claramente negativo (200Hz)

---

## Prototipo — Herramientas de Visualizacion

El prototipo en `Prototype/qte_prototype.py` incluye herramientas para iterar sobre el balance:

### Panel de patron de recoil ([P])
- Muestra un **mapa de calor** por arma simulando 100 cargadores completos
- Visualiza la densidad de impactos como gradiente de color (frio → caliente)
- Superpone la linea del patron ideal para comparar con la dispersion real
- Permite verificar que cada arma tiene una "personalidad" reconocible
- [F5] regenera la simulacion con nueva semilla aleatoria

### Zonas de hitbox ([Z])
- Muestra las hitboxes de cabeza, torso y piernas sobre la silueta
- Las hitboxes de cabeza y torso estan conectadas (sin gap entre ellas)
- Cualquier impacto fuera de toda hitbox = MISS

### Controles del prototipo

```
[SPACE] Disparar    [R] Recargar
[1] Pistola  [2] Rifle  [3] Escopeta
[UP/DOWN] Vida +/- 10
[H] Mano  [P] Patron Recoil  [Z] Zonas
[F5] Regenerar heatmap  [ESC] Salir
```

---

## Sistemas Existentes (de la Jam previa)

Ya desarrollados:
- Sistema de combate (estilo Chrono Trigger)
- Sistema de party
- Sistema de objetivos, progresion de historia y objetos reactivos al progreso
- Inventario
- Dialogos con Yarn Spinner

Solo hay que cambiar:
- Fantasia > Horror
- Economia de mana > Economia de municion
- Ritmo heroico > Ritmo de desgaste

---

Volver a [[Crimson Draft]]
