# Cómo hacer un buen Game Design Document (GDD) en 2026: guía completa y práctica

## TL;DR
- Un GDD moderno es un **documento vivo, liviano y buscable** —no un tomo de 100+ páginas— cuyo trabajo real es alinear al equipo, controlar el scope y servir como fuente de verdad; empieza como one-pager en preproducción y crece por secciones a medida que el prototipo demuestra qué funciona.
- No existe una plantilla única: la estructura (overview, pilares, core loop, mecánicas, narrativa, arte, UI/UX, progresión/balance, especificaciones técnicas) debe escalarse según **tamaño de equipo** (indie/solo → 1-30 págs; AAA → wikis en Confluence/Notion) y **género** (RPG = tablas de stats y fórmulas; roguelike = estructura de run y meta-progresión; TPS = gunplay, cover y TTK).
- Para portafolio, un GDD bien hecho demuestra pensamiento de diseño: usa case studies escaneables, artefactos reales (flowcharts, spreadsheets de balance, wireframes), deja claro qué hiciste tú, y toma como referencia documentos públicos como Grim Fandango, Dirty Bomb, Deus Ex, Diablo y GTA/Race'n'Chase.

## Key Findings

**1. El GDD dejó de ser un contrato y pasó a ser una brújula.** El consenso de la industria (gamedeveloper.com, GitBook, Nuclino, Codecks) es que los GDD monolíticos de estilo waterfall están obsoletos. Como resume Jim Highsmith (firmante del Manifiesto Ágil), "abrazamos la documentación, pero no cientos de páginas de tomos nunca mantenidos y rara vez usados". El GDD moderno se define por dos cualidades: **encontrable y actualizado**.

**2. El GDD no es lo mismo que un pitch deck, un creative brief ni una game/narrative bible.** Cada documento tiene un trabajo distinto y forman una cadena de concreción creciente.

**3. La mejor práctica es "empezar amplio, luego específico" y mantenerlo vivo.** One-pager en preproducción → documento de ~10 páginas cuando el juego toma forma → GDD completo con todo el contenido. Datar ediciones, mantener un changelog, asignar dueños por sección.

**4. La estructura escala con el proyecto y el género.** Un puzzle simple no necesita 20 páginas de backstory; un RPG sí necesita tablas de stats y fórmulas de daño.

**5. Los errores más comunes son evitables:** sin dueño del documento, teoría sin sistemas prototipables, scope creep, y documentos desactualizados (peor que no tener ninguno).

**6. La herramienta importa menos que el hábito, pero hay opciones claras según contexto.**

**7. Existen GDD públicos excelentes para aprender.**

**8. Un GDD es una pieza de portafolio válida y valorada**, especialmente para roles de systems/combat/narrative design.

---

## Details

### 1. Qué es un GDD y para qué sirve realmente

Un **Game Design Document (GDD)** es un documento interno que describe qué juego estás construyendo y por qué: captura la visión, el core loop, los sistemas, el contenido y las restricciones (scope, pilares, no-goals). Según Danielle Riendeau (editora en jefe de Game Developer), un buen GDD debe "comunicar muy claramente la visión del diseñador... de forma que sea útil y legible para cada miembro del equipo o stakeholder, sin importar su disciplina": artistas, animadores, ingenieros, diseñadores de sistemas y niveles, sonido, compositores, marketing y producción.

Para qué sirve realmente (según Game Developer y Game Industry Career Guide):
- Da al equipo una comprensión común de **qué es el juego y qué NO es**.
- Establece un plan de producción (aunque cambie).
- Permite feedback temprano antes de invertir demasiados recursos.
- Rastrea cómo evoluciona el juego.
- Identifica problemas de diseño y de recursos temprano.
- Incluso para un solo desarrollador, sirve para responder "¿qué estaba pensando?" cuando vuelves al proyecto meses después.

**Diferencias clave con documentos vecinos:**

| Documento | Trabajo principal | Formato típico |
|---|---|---|
| **Pitch deck / pitch doc** | *Vender* el concepto a publishers/inversores | Corto, muy visual, 10-20 slides |
| **Creative brief** | Versión ampliada del one-pager que describe los aspectos principales antes de entrar en producción | Slideshow de 20-30 slides |
| **GDD** | *Construir* el juego: define gameplay, sistemas, contenido, reglas | Wiki/doc vivo |
| **TDD (Technical Design Document)** | *Cómo* se construye: arquitectura, código, APIs, sistemas | Doc técnico |
| **Game bible / narrative bible** | Lore, personajes, tono, worldbuilding (a veces paraguas que incluye GDD + guías de arte/marca) | Referencia narrativa |

La cadena habitual: **one-sheet → pitch deck → GDD → production plan → vertical slice/prototype**. Cada paso agrega profundidad. Se empieza con un pitch doc y se expande en GDD; los ingenieros luego derivan los TDD de ambos. Ojo con "game bible": los equipos lo usan de forma distinta —normalmente es una biblia narrativa (lore, personajes, tono) o un paraguas más amplio.

Un matiz importante desde publishers: hoy **un juego jugable (vertical slice) siempre gana sobre cualquier documento**. Los publishers ya no exigen un GDD completo como parte del contrato; suelen pedir documentación reflejada en el acuerdo de milestones y entregables. Distribuidores como Microsoft o Sony sí piden documentos con visión general detallada.

### 2. Mejores prácticas actuales: liviano vs. extenso, documentos vivos, versionado

**Liviano vs. extenso — el veredicto:** los GDD rígidos y extensos "no tienen lugar en el desarrollo moderno" (Nuclino). Pero eso no significa abandonar la documentación: con equipos distribuidos y multi-estudio, la necesidad de documentación centralizada no desaparece. La respuesta es un **enfoque pragmático y sin desperdicio**: usar el GDD donde tiene sentido, descartar lo obsoleto.

Tres tips para un GDD moderno (Codecks):
1. **Mantenlo mínimo.** Enfócate en los principios guía, las mecánicas básicas y la visión general. En vez de explicar mecánicas en gran detalle, enfócate en los objetivos: ¿cómo debe sentirse el jugador? ¿cómo se diferencia de la competencia? ¿cuál es la intención artística?
2. **Escríbelo colaborativamente** (salvo solo dev): involucra al equipo desde el inicio. Que el GDD sea el hub central donde el equipo descubre, discute y resuelve.
3. **Que evolucione con el proyecto.** Un GDD solo es útil si está actualizado. Usa una herramienta con historial de versiones y actualiza a diario.

**Documentos "vivos" y versionado (prácticas concretas):**
- **Data tus ediciones** y marca qué has testeado. Stone Librande recomienda datar los one-pagers impresos para saber cuál es la versión vigente.
- **Mantén un changelog / decision log.** Para un solo dev, "la parte más útil de un GDD solo fue el 'decision log' donde escribí por qué elegí una mecánica y qué esperaba aprender" (foro Conflingo, 2026). Trata el GDD "como código —estructurado, iterativo y colaborativo": usa números de versión, changelogs y convenciones de nombres consistentes.
- **Control de versiones:** en equipos, usa herramientas con historial (GitBook con change requests estilo pull-request; Confluence separa borradores de contenido publicado). Para solo dev basta un changelog datado.
- **Cuándo actualizarlo:** en cada milestone, y con sesiones de revisión de GDD regulares con el equipo (no solo para actualizar sino para discutir decisiones y alinear). **Cuándo "congelarlo":** al inicio de producción; después, los cambios grandes deben seguir un proceso formal para evitar scope creep.
- **Timebox del diseño escrito:** 1-3 horas para definir una feature nueva, luego prototipar 2-8 horas, jugar, y recién ahí actualizar el doc. Si te encuentras puliendo texto y diagramas en vez de construir, para.

**Post-lanzamiento (live service):** crea una sección o documento separado para updates post-launch (bug fixes, contenido, balance, feedback) para no saturar el GDD original; usa spreadsheet/wiki/Trello y categoriza por tipo de update.

### 3. Estructura y secciones recomendadas + cómo escalar

No hay una estructura "oficial", pero los buenos GDD siguen el flujo **de lo amplio a lo específico**. Secciones habituales (combinando gamedeveloper.com, GitBook, gamedesigning.org, Nuclino):

**A. Overview / Introducción**
- Título y tagline.
- **Vision statement** (qué será exactamente el juego, por qué será divertido/único), **logline** (una frase) y **gameplay synopsis** (un par de párrafos del core loop y estructura).
- Género, plataformas, audiencia objetivo.
- **Design pillars** (3-5): las experiencias/emociones núcleo que filtran toda decisión. Ejemplos reales: Hades — "combate rápido y fluido" + "profundidad narrativa a través de runs repetidas"; God of War usó pilares que eran los nombres de sistemas independientes (combate intenso, historia padre/hijo, exploración). Max Pears (CD Projekt Red) los define como "los elementos y emociones más básicos que un juego intenta explorar y hacer sentir al jugador". Regla: 3-8 pilares; evita "palabras vacías" (ej. "hacer un juego divertido"), tareas o conceptos técnicos como pilares.
- **Non-goals / scope boundary:** lista explícita de lo que NO harás ("Sin multiplayer. Sin generación procedural. Cinco niveles máximo"). Cada idea nueva se mide contra esta lista.

**B. Core gameplay loop.** Explícalo como una lista corta: ej. explorar → pelear → lootear → mejorar → repetir. Empieza con un diagrama de core loop con descripciones breves. Es la sección más expuesta a stakeholders externos, así que incluye ayudas visuales.

**C. Mecánicas y sistemas.** Los "verbos" del jugador y cómo el juego responde. Captura mecánicas, progresión, economía, combate, IA. Incluye casos límite (diseñadores e ingenieros los necesitan). Usa diagramas, flowcharts, storyboards. Precaución de veterano: no escribas "el pescado es como en Stardew Valley, ve a jugarlo y cópialo" —los puntos de referencia están bien, pero "clona esto" es inútil. Sé específico: no basta "el personaje puede volar" —¿cómo vuela?, ¿qué acciones toma el jugador?

**D. Game world / niveles.** Cómo se divide el mundo (niveles discretos, overworld, medios de traversal), mapas rough, arte de referencia (más mood board que producto final).

**E. Personajes.** Si aplica: web de personajes con relaciones, arte de referencia.

**F. Narrativa / historia.** Varía enormemente por género: de un logline a un script tipo Baldur's Gate 3. Si es liviano en historia, basta logline + sinopsis; si es rico, agrega outlines detallados, scripts, y enlaza a documentación narrativa separada. Define **mecánicas narrativas** (cómo interactúan historia y gameplay, ej. los audio logs de BioShock).

**G. Arte / dirección visual.** Boards de referencia, color scripts, ejemplos de tono. Pega imágenes de referencia en vez de escribir párrafos describiendo el estilo.

**H. UI/UX.** Esquema de controles, diagramas de interfaz (HUD, menús), flowchart de pantallas, wireframes, accesibilidad. Ejemplo: Dead Space integró las barras de salud en el traje del personaje (UI diegética documentada en su GDD).

**I. Audio / sonido.** Notas de alto nivel + media list (ej. 12 piezas de música por nivel, sets de SFX). Ejemplos de referencia ("el chime de Ocarina of Time al abrir un portal mágico").

**J. Progresión y balance.** Cómo crece el jugador (XP, perks, upgrades, gear), curvas de dificultad. Para juegos con stats, incluye tablas y fórmulas.

**K. Especificaciones técnicas.** Motor (Unity, Unreal, Godot), plataformas objetivo, requisitos, frame rate objetivo, control de versiones (Git/Perforce), herramientas de colaboración.

**L. Producción.** Milestones, riesgos, modelo de negocio, timeline.

**Cómo escalar (de one-pager a GDD completo):**
- **One-pager:** un movie poster + resumen. Título/tagline, quick pitch, género, core loop, key features. Ideal para game jams, brainstorming rápido, pitches de 60 segundos y como semilla del GDD completo. Stone Librande (entonces Creative Director en EA/Maxis; luego lead en Riot Games) argumentó en su charla "One-Page Designs" (GDC 2010): "After all, why create a document with more than one page if most people only read the first page anyway?" —diagramas anotados que caben en una página, con mucho espacio en blanco e imágenes centrales fuertes. Se inspiró en dibujos arquitectónicos, manuales de Lego y placemats infantiles. En el mismo talk mostró one-pagers de Diablo III, The Simpsons Game, Spore y SimCity; en su charla de seguimiento (GDC 2013, "Simulating a City, One Page at a Time") relató haber usado extensivamente estos documentos de una página a lo largo de todo el proyecto de SimCity.
- **~10 páginas:** cuando el juego toma forma; agrega mecánicas núcleo y beats de historia.
- **GDD completo:** todo el contenido y detalle. En estudios grandes se estructura como **Master Document + Feature Documents** (uno por feature: p.ej. trampas de rocas, el hookshot, el sistema de música adaptativa), cada uno con elevator pitch, user stories, requisitos y referencias.

Plantilla mínima recomendada para primer proyecto (Ziva, 5 secciones): Pitch (2 frases) → Core Loop → Out of Scope → Art Direction (3-5 imágenes de referencia) → Release (plataforma, precio, versión mínima viable). Debería tomarte menos de una hora; si toma más, tu scope es demasiado grande.

### 4. Diferencias por tipo/escala de proyecto y por género

**Por escala de equipo:**

- **Indie / solo dev:** GDD conciso, típicamente **10-30 páginas** (o incluso menos). Prioriza claridad y agilidad: concept art/mood boards, flowcharts, player journey maps. Sirve como guía, dejando espacio para descubrimientos durante el playtesting. Advertencia: "a menos que tengas un presupuesto de millones, no intentes escribir un GDD estilo AAA; te quemarás antes de escribir tu primera línea de código". Para game jams, un one-pager es suficiente. Caso ilustrativo: un desarrollador reportó que su primer juego indie se estancó dos veces por no tener GDD; la tercera, con un documento vivo simple, lo terminó en 9 meses evitando feature creep.
- **Equipo pequeño:** GDD colaborativo desde día uno, con dueños por sección. Wiki liviano (Nuclino, Notion, Slite) con secciones cruzadas.
- **Estudio grande / AAA:** rara vez usan un solo documento; construyen **wikis internos vivos** (Confluence, Notion). La comunicación es el mayor cuello de botella con cientos de personas en distintas zonas horarias. Troy Dunniway (Head of Game Design, CG Spectrum, 25+ años) señala que "los equipos grandes realmente necesitan mucho más proceso y documentación para mantener a sus equipos de 500+ personas funcionando fluidamente", y que la mayoría de equipos AAA reales aún documentan extensamente. Splash Damage explica que los GDD "fueron un estándar durante décadas y reflejaban las necesidades de la entrega de producto en caja"; su wiki de los últimos años fue "algo vivo y respirante, muy distinto de los GDD de antaño". Sistema de "Levels of Quality" (L0-L4) y estatus Gold/Silver/Bronze por feature para acotar el esfuerzo.

**Por género:**

- **RPG / JRPG (sistemas de combate y stats):** el GDD debe cubrir con detalle sistemas, árboles de progresión, reglas de combate y **balance**, priorizando consistencia sobre brevedad. Elementos clave a documentar:
  - **Atributos primarios** (los "Big Six" clásicos: STR, DEX, CON, INT, WIS, CHA) y **stats derivados** (calculados de los primarios). Muestra la fórmula: "Daño melé = STR × 2 + Arma" en tooltip genera confianza.
  - **Función de escalado:** lineal (+5 daño por punto, se siente trivial al final), porcentual (rompe balance al final), o **rendimientos decrecientes** (recomendado —cada punto vale un poco menos que el anterior, crea soft caps naturales).
  - **Guardrails matemáticos:** soft caps (umbral donde el beneficio cae drásticamente), hard caps (límite absoluto), diminishing returns (evita 100% de invulnerabilidad).
  - **Spreadsheet de balance:** God of War Ragnarök (2022) usó una hoja de progresión de armas; el lead combat designer Rob Meyer explicó que "intentamos modelar nuestras expectativas de adquisición y progresión de gear de forma que se sintiera sana en el papel, y luego constantemente playtesteamos e iteramos para que la realidad del playtest coincidiera con nuestros modelos". Documenta la economía (corto plazo: salud/consumibles; largo plazo: XP/perks/gear).
  - **Enemigos y encuentros:** roster variado con comportamientos, fortalezas y debilidades distintas.

- **Roguelike de acción (isométrico/2D):** documenta la **estructura de run** y la **meta-progresión**, no cantidad de contenido. El contenido viene de *combinaciones*: "20 armas × 30 upgrades × 15 layouts de sala = miles de runs únicas". Secciones críticas:
  - Las tres features núcleo (Berlin Interpretation): generación procedural, permadeath, aleatorización de ítems.
  - **Generación procedural:** enfoque común = salas pre-diseñadas conectadas proceduralmente; define tipos de sala (Combat, Reward, Event, Rest, Boss), tiers de dificultad, restricciones de piso. Distingue elementos "hard" (afectan progresión: upgrades, biomas, tipos de enemigo) de "soft" (spawns, estructura básica).
  - **Balance de combinaciones:** cap stacking, diminishing returns, anti-synergy items, escalado de dificultad de run. "La única forma confiable de encontrar combinaciones rotas es jugar miles de runs."
  - **Meta-progresión:** qué persiste entre runs (skills incrementales tipo Rogue Legacy, o solo skins cosméticos tipo Spelunky). Sweet spot de duración de run: 20-40 minutos.
  - Los **design pillars** son especialmente importantes aquí: un devlog de un roguelike incremental (Strata) muestra cómo una decisión menor (ítems con efectos aleatorios que escalan fácil) "socava todo el pilar de diseño del que se originó".

- **Shooter en tercera persona (TPS):** documenta gunplay, sistema de cover, movilidad y feel. Elementos:
  - **Cámara:** movimiento 360° con cámara detrás del personaje; modo apuntado over-the-shoulder para precisión; lock-on para consola.
  - **Sistema de cover:** el jugador se pega a objetos, asoma a izquierda/derecha, dispara expuesto, blind-fire para supresión, vault sobre el cover. Distingue "cover como táctica" (duck-and-cover) de "cover como sistema" (estado distinto, el modo de combate por defecto). **Kill.Switch de Namco (2003)** fue "el primer third-person shooter en presentar el sistema de cover como mecánica de juego núcleo" e introdujo el blind fire; su lead designer Chris Esaki fue después contratado por Epic Games para **Gears of War (2006)**, que popularizó el sistema.
  - **Gunplay y feel:** TTK (time-to-kill) es el influenciador primario del daño —para mantenerlo consistente, un arma más lenta hace más daño. Shooters competitivos tienen TTK rápido; experiencias casuales (Call of Duty) más largo. Documenta VFX (muzzle flash, decals, blood spray), animaciones de arma visibles de cerca, recoil, spread, tamaño de cargador, velocidad de recarga.
  - **Loadout:** típicamente dos armas primarias + una secundaria; armas para distintos rangos (escopeta corto alcance, rifle medio, sniper largo).
  - **IA de enemigos:** coordinación (algunos flanquean, otros suprimen, otros lanzan granadas); la dificultad escala ajustando qué tan agresivamente coordinan.
  - El GDD público de **Dirty Bomb** (Splash Damage, 300+ páginas) es la referencia ideal para este género —documenta gunplay, mecánicas de arma (bolt-action, burst-fire, escopetas), long jump/wall jump, sistema de XP, y goals de core gameplay como "las mecánicas de disparo deben tener el mismo nivel de pulido que los shooters modernos, pero con una mentalidad más old-school".

### 5. Errores comunes y cómo evitarlos

| Error | Consecuencia | Cómo evitarlo |
|---|---|---|
| **Sin dueño del documento** | El GDD queda desactualizado rápido | Un dueño claro (lead designer o producer) responsable de estructura, precisión y actualización; contribuciones de todos pero una sola persona responsable |
| **Sobredocumentación** | Nadie lo lee; se pule texto en vez de construir | Timebox el diseño escrito; solo tan largo como sea necesario; "explícalo como a un niño de 10 años" |
| **Teoría sin sistemas prototipables** | Features irreales o inviables | Si una mecánica no se puede construir/testear, simplifícala; "el diseño no es real hasta que sobrevive al contacto con el playtesting" |
| **Scope creep** | Deadlines perdidos, presupuesto reventado, crunch, burnout | Define MVP y pilares; lista explícita de "out of scope"; mide cada idea nueva contra los pilares; usa MoSCoW/RICE para priorizar. Según PMI (Pulse of the Profession 2018), el 52% de los proyectos experimentó scope creep en los 12 meses previos —"up from 43 percent five years ago" |
| **Documento desactualizado** | "Falsa alineación es más dañina que ninguna alineación" | Revisión en cada milestone; trata el contenido obsoleto como riesgo de producción. Caso real: un ingeniero construyó un sistema de customización completo basado en una sección desactualizada del GDD —el trabajo se descartó |
| **Solo texto, sin visuales** | Comunica peor | Flowcharts, wireframes, sketches; "doodles de MS-Paint" comunican la cantidad justa de información |
| **Escribir en exceso antes de producción** | Semanas perdidas en sistemas que cambian al prototipar | Empieza liviano, agrega profundidad al desarrollar |
| **GDD solo desde la óptica del desarrollador** | Se olvida la experiencia del jugador | Incluye cómo se ve y siente para el jugador; secciones para ingeniería, marketing, management |

Un caso emblemático de manejo de scope: para el lanzamiento inicial de Curious Expedition 1, el equipo tomó "la medida drástica de eliminar toda la mecánica de combate" que habían prototipado durante meses, al darse cuenta de que no era parte de la experiencia núcleo. La agregaron después, más alineada con la visión.

### 6. Herramientas y formatos recomendados (pros/contras, precios 2026)

Regla general (gamedeveloper.com, dev @Geometric): "no me importa si está en Google Docs, Notion, Confluence o una carpeta de archivos HTML en Dropbox, pero quiero secciones cruzadas organizadas con un directorio para referencia fácil". Word es desaconsejado: su naturaleza rígida y cerrada garantiza que el contenido "termine encerrado en el disco duro de alguien, sin actualizarse ni abrirse".

| Herramienta | Free tier | Plan de entrada | Fortaleza para GDD | Debilidad para GDD |
|---|---|---|---|---|
| **Notion** | Bloques ilimitados, archivos 5MB, historial 7 días | Plus $10/usuario/mes (anual) | Bases de datos + colaboración en tiempo real; recomendado para equipos pequeños | Soporte offline débil; historial corto en free |
| **Confluence** | ≤10 usuarios, 2GB | Standard ~$5.42/usuario/mes (anual) | Historial de versiones robusto + jerarquía de páginas + integración Jira + whiteboards; estándar en organizaciones grandes | Costo escala rápido; UI pesada/lenta; add-ons cuestan extra |
| **Nuclino** | 50 items, 2GB | Starter $6/usuario/mes (anual) | Muy rápido, curva de aprendizaje mínima, vistas graph/board/table, canvas visual integrado; popular en estudios de juegos | Cap de 50 items se supera rápido; sin historial en free |
| **GitBook** | 1 usuario, subdominio | Premium ~$65/mes/sitio + $12/usuario | Change requests estilo pull-request + historial + sync bidireccional GitHub/GitLab; ideal para GDD ligados al código; docs públicos | Precios duales (sitio+usuario) confusos y caros para equipos; más orientado a docs de producto |
| **Obsidian** | Gratis (uso personal y comercial) | Sync $4/mes (anual) | Local-first Markdown (propiedad total, offline), links bidireccionales + graph view; sin lock-in | Sin co-edición en tiempo real (malo para equipos); posibles conflictos de sync |
| **Google Docs / Workspace** | Docs gratis (personal) | Business Starter $7/usuario/mes | Colaboración en tiempo real + historial de revisiones robusto + offline + familiaridad universal | Sin jerarquía wiki/linking ni bases de datos; un GDD grande se vuelve inmanejable |

Recomendaciones prácticas por contexto:
- **Solo dev:** Google Docs, un archivo Markdown, u Obsidian (si valoras links y offline). Notion también.
- **Equipo pequeño (2-10):** Notion o Nuclino (rápidos, baratos, colaborativos).
- **Equipo grande / con Jira:** Confluence.
- **GDD ligado a repo de código / docs públicos:** GitBook.

Otras herramientas mencionadas por la industria: **Miro/FigJam** (diagramas, flowcharts, sistemas), **Figma** (mockups de UI), **Codecks/Trello/Jira** (gestión de tareas), **Slite** (wiki con capa de IA que detecta docs obsoletos), **Drafft** (editor específico para GDD, diálogos, quests).

### 7. Ejemplos reales y templates públicos de referencia

GDD y documentos de diseño públicos que sirven como material de aprendizaje:

- **Grim Fandango — Puzzle Document** (LucasArts, abril 1996; autores: Tim Schafer, Peter Tsacle, Eric Ingerson, Bret Mogilefsky, Peter Chan, según la Video Game History Foundation): el más "querido" y conocido. 72 páginas que recorren todo el flujo del juego mapeando historia, puzzles (80+) y soluciones, con ilustraciones, storyboards y notas manuscritas (evidencia del documento vivo). Contiene ingenio marca Schafer y contenido cortado. Anécdota reveladora del propio Schafer: "We didn't have the last puzzle designed when I wrote that document, so I wrote two nonsense paragraphs and then overlapped them in the file so it would look like the final puzzle description was in there, but obscured by a print formatting error. That way I could turn the document in by the deadline." Disponible en el archivo de la Video Game History Foundation.
- **Dirty Bomb** (Splash Damage, 300+ páginas): GDD completo de un shooter free-to-play, liberado públicamente. Cubre core gameplay, mecánicas de arma, modos, diseño de mapas y habilidades de personajes. Ideal para TPS/FPS.
- **Deus Ex** (documento anotado de los inicios): muestra el scope original incluyendo multiplayer competitivo y un tercer acto en una estación espacial que no llegaron a producción.
- **Diablo** (pitch document): ideas iniciales, gameplay, timelines, marketing.
- **GTA / Race'n'Chase** (Mike Dailly, DMA Design, marzo 1995): antes de ser GTA, explica el concepto del título top-down.
- **BioShock** (pitch document): detalles iniciales del concepto y arte; muestra cuánto cambió respecto al draft original.
- **Monaco: What's Yours is Mine:** el diseño original con arte de referencia y flowcharts; notable lo cerca que quedó el juego final de la visión original.
- **Otros archivados** (gamedocs.org, Video Game History Foundation): Metal Gear Solid 2 "Grand Game Plan", Planescape Torment Vision Statement, Fallout, The Flame in the Flood pitch, Guacamelee!, Prince of Persia 2 design bible, Leisure Suit Larry.

Templates gratuitos: Indie Game Academy (GDD + one-pager, PDF descargable/copiable), Nuclino, gamedesigning.org, GitBook (copy/paste friendly), gamedesignskills.com (3 plantillas: AAA, indie, solo dev).

### 8. Consejos específicos para portafolio

Un GDD bien hecho es una pieza de portafolio válida y valorada, especialmente para roles de systems, combat, balance y narrative design. La revisión inicial de un reclutador es brutalmente breve: el estudio de eye-tracking de TheLadders (2018) midió que "the average recruiter spends 7.4 seconds reviewing each resume" (su predecesor de 2012 medía 6 segundos), así que la presentación importa tanto como el contenido.

**Qué buscan los reclutadores (patrones de portafolios fuertes, según Twine y Game Design Skills):**
- **Una especialidad clara al frente** (level design, systems, narrative, UX, tech design).
- **Proyectos enmarcados como problemas resueltos**, no "cosas que hice".
- **Tu contribución explícita:** qué hiciste tú vs. qué hizo el equipo.
- **Artefactos de proceso visibles:** blockouts, diagramas, docs, spreadsheets, before/after. Un buen ejemplo (Lucas Zakaria): rol, timeline, tarea clara ("nuevo sistema económico"), y lo más importante —entregables inspeccionables (documentación + spreadsheet de economía).
- **Prueba de shipping:** títulos lanzados, updates, mods, game jams, prototipos, playtests.

**Reglas prácticas:**
- **Match con el puesto:** un systems designer adjunta documentación de sistemas; un balance designer, tablas con cálculos; un combat designer, prototipos de combate. Para juniors/interns, una decomposición de mecánicas de otros juegos es suficiente.
- **Menos es más:** 2-5 proyectos fuertes; empieza cada uno con la información más relevante. "Nadie en un estudio quiere leer un documento de 200 páginas."
- **Una frase por bullet:** qué hiciste con qué herramienta, bajo un video del juego. Ajusta el wording a las keywords de las ofertas (ej. "Concepté, whiteboxeé e iteré puzzles ambientales para un juego 3D de terror sci-fi en tercera persona").
- **Documenta durante cada proyecto:** cada juego nuevo es otra oportunidad para preparar un mejor ejemplo. Itera el portafolio como un equipo de live ops.
- **Evita errores comunes** (MY.GAMES): ejemplos que no demuestran suficientemente tus skills; énfasis excesivo en arte/screenshots si aplicas a game design; sobreestimar tus propias habilidades.
- El GDD como pieza demuestra pensamiento crítico y creativo: "con diagramas y flowcharts que documentan tu viaje creativo de inicio a fin, puedes mostrar tu creatividad, pensamiento técnico y otras skills relevantes" (Champlain College).

Para el perfil del usuario (estudiante avanzado con Unity y experiencia profesional): idealmente combina **un juego jugable/prototipo en Unity** con **un GDD que documente sus sistemas** —"tener un gran GDD y algo jugable para presentar son igualmente importantes para entrar en la industria" (Game Design Skills).

---

## Recommendations

**Fase 1 — Empieza ya (esta semana):**
1. Escribe un **one-pager** de tu proyecto actual o próximo (pitch de 2 frases, core loop, género, 3-5 pilares, lista "out of scope", 3-5 imágenes de referencia, plataforma/precio/MVP). Debería tomarte menos de una hora; si toma más, tu scope es demasiado grande.
2. Elige tu herramienta según contexto: **Notion o Nuclino** si trabajas en equipo pequeño; **Google Docs o un archivo Markdown/Obsidian** si eres solo dev; **Confluence** si ya usas Jira; **GitBook** si quieres ligarlo al repo o publicarlo.
3. Comparte el one-pager con un compañero para feedback.

**Fase 2 — Al prototipar (semanas siguientes):**
4. Expande sección por sección solo cuando el prototipo demuestre que la mecánica funciona. Timebox: 1-3 h de escritura por feature, luego 2-8 h de prototipo.
5. Asigna **un dueño** del GDD (tú, si es solo dev) y **dueños por sección** si hay equipo.
6. Añade un **decision log / changelog datado** y marca qué has testeado. Convierte los pilares en un filtro para cada idea nueva.
7. Sustituye párrafos por **visuales**: diagrama de core loop, flowcharts de mecánicas, wireframes de UI, mood boards de arte.

**Fase 3 — En producción:**
8. **Congela** el GDD al inicio de producción; cambios grandes vía proceso formal.
9. Revisión de GDD en **cada milestone**; trata lo desactualizado como riesgo. Programa revisiones semanales durante desarrollo activo.
10. Para género específico: RPG → construye el spreadsheet de balance en paralelo (fórmulas visibles, soft/hard caps); roguelike → documenta estructura de run + tabla de combinaciones/synergias + meta-progresión; TPS → define TTK, loadout, sistema de cover y coordinación de IA.

**Fase 4 — Para portafolio:**
11. Convierte tu GDD (o un extracto: la sección de sistemas/combate) en una **case study escaneable**: problema → tu contribución → artefactos (spreadsheet, flowchart, wireframe) → resultado/aprendizaje.
12. Empareja el GDD con un **prototipo jugable en Unity**. Ajusta el wording a las keywords de las ofertas objetivo.

**Benchmarks que cambian tus decisiones:**
- Si tu GDD supera ~30 páginas siendo indie/solo dev → estás sobredocumentando; recorta.
- Si nadie del equipo lo abre en 2 semanas → problema de accesibilidad/herramienta; muévete a algo más rápido y buscable.
- Si el GDD ya no refleja el juego actual → detente y actualiza antes de seguir construyendo (falsa alineación > sin alineación).
- Si una feature no se puede prototipar en 1-2 días → simplifícala o córtala.
- Si una idea nueva no encaja en ningún pilar → va a la lista "out of scope" (post-launch/DLC/secuela).

## Caveats

- **No hay consenso universal.** Algunos diseñadores competentes no usan GDD en absoluto. Matthew "Queso" Niederberger prefiere slideshows como outline y sostiene que "un documento estático comunica la información equivocada la mayor parte del tiempo" y que gran parte del proceso de escritura puede ser una pérdida de tiempo, porque "el diseño no es real hasta que sobrevive al contacto con el playtesting". El punto es: el GDD es una herramienta para resolver dos problemas (ayudarte a pensar el diseño y comunicarlo al equipo), no un fin en sí mismo.
- **Muchas fuentes de "cómo hacer un GDD" son marketing de contenidos** de empresas de documentación o estudios (Document360, Nuclino, GitBook, Kevuru, Gamix, TekRevol, Wayline). Su consejo estructural es sólido y consistente entre sí, pero tienen interés en venderte su herramienta/servicio. Los conté como corroboración cuando coinciden con fuentes primarias (gamedeveloper.com, GDC talks de Stone Librande, GDD públicos reales).
- **Algunas cifras están atribuidas de forma vaga o son difíciles de verificar** en fuentes secundarias (p.ej. afirmaciones tipo "una encuesta de GDC encontró que 68% de indies usan un flowchart" o "The Witcher 3 tuvo 200+ páginas de GDD", citadas por gamedesigning.org sin enlace primario; también la cifra de "100+ one-pagers para SimCity" no está confirmada en fuente primaria). Las traté como ilustrativas, no como hechos firmes.
- **Los precios de las herramientas cambian.** Las cifras 2026 provienen de páginas oficiales y comparativas recientes, pero GitBook (precio dual sitio+usuario) y Confluence (anual vs. mensual, mínimos de facturación) muestran variación entre fuentes; verifica al momento de suscribirte.
- **El perfil del usuario importa:** con experiencia profesional previa y Unity, probablemente puedas saltar plantillas académicas de 20 secciones e ir directo a un GDD lean orientado a sistemas prototipables, que es exactamente lo que valoran los estudios AAA según Troy Dunniway y los reclutadores citados.