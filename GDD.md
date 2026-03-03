# Crimson Draft — Game Design Bible

**Versión:** Pre-producción
**Última actualización:** 2026-03-03

---

## 1. Visión del Juego

**Título:** Crimson Draft (Calado Rojo)
**Género:** Tactical Survival Horror RPG
**Plataforma:** Unity (PC, posiblemente consola)

### Cuatro lecturas del título
1. Discrepancia técnica en el calado del buque — la anomalía que desencadena la misión
2. La línea roja en el agua — señal visible de un barco demasiado pesado
3. El borrador (draft) de una guerra — lo que ocurre en el barco es solo el ensayo
4. Sangre y destrucción — la consecuencia final

### Identidad
> "Survival táctico con tragedia geopolítica. No es 'zombis en barco'. Es horror político con consecuencias irreversibles."

**Mensaje central:** "No es izquierda ni derecha. Es poder contra población."

**Tema:** Dependencia como herramienta de poder. El verdadero monstruo no es el infectado — es el sistema que lo diseñó.

---

## 2. Pilares de Diseño

El juego se define por 5 pilares. Todo sistema nuevo debe poder justificarse contra al menos uno. Si viola alguno, no pertenece al juego.

### Pilar 1: El combate es costo, no recompensa
Combatir gasta recursos irrecuperables. La progresión es de desgaste, no de empoderamiento. El jugador termina el juego más débil que como empezó. Ver [[Tactical Survival Horror]].

### Pilar 2: Agencia bajo presión
El jugador siempre tiene control, pero ese control se degrada con el estado físico del operador. El QTE bidimensional da agencia real (no un dado), pero la vibración, los distractores y la dispersión erosionan esa agencia progresivamente. Ver [[Distractores Visuales]].

### Pilar 3: Consecuencias irreversibles
No hay revive, no hay recarga mágica. Los personajes mueren permanentemente y sus armas se pierden. La exposición al Krokonil no se revierte. Ver [[Mecanicas de Supervivencia]].

### Pilar 4: Horror tangible
Los enemigos son humanos en colapso neuroquímico. Las armas son reales. El horror viene de la plausibilidad — todo lo que ocurre *podría* pasar. Ver [[Krokonil]].

### Pilar 5: Información como recurso
La lectura visual del enemigo (protección, fase de deterioro, zona expuesta) es una habilidad del jugador, no un stat. El jugador que lee bien gasta menos recursos. Ver [[Diseño de Combate y Armas]].

---

## 3. Narrativa

El juego ocurre en un tanquero de carga ruso (El Marinera) en el Mar Negro. Un equipo MSRT sube a bordo para investigar una anomalía en el calado. Lo que encuentran es un cargamento ilegal de Krokonil — un neuroquímico que destruyó a la tripulación.

### Arco narrativo en 5 actos

| Acto | Evento central |
|------|---------------|
| I    | Abordaje. Los MSRT descubren la situación. Los dos operadores MSRT mueren por desgaste. |
| II   | Reagrupamiento con Navy SEALs y agente CIA. Descubren los "reguladores KRK-NL". |
| III  | Revelación: los reguladores son Krokonil. El CIA empieza a mostrar fisuras. |
| IV   | El CIA se revela como antagonista. Party debilitado, combates más duros. |
| V    | Carrera contra el reloj. Misil en camino. Protocolo SCUTTLE. |

Ver [[Acto I - Diseño Detallado]] · [[Estructura Narrativa]] · [[La Conspiracion]] · [[Protocolo SCUTTLE]]

---

## 4. Personajes y Party

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

## 5. El Barco (El Marinera)

Tanquero de carga multinacional en el Mar Negro. Ambiente reactivo: se inclina, se inunda, los sistemas eléctricos fallan. El barco no es un escenario estático — es un sistema vivo que cambia durante el juego y presiona al jugador.

Ver [[El Marinera]] · [[Mecanicas de Supervivencia#El Barco como Sistema]]

---

## 6. Sistemas de Combate

### 6.1 Loop de combate en tiempo real

El combate ocurre en tiempo real. No hay turnos ni ATB. Cada acción *ocupa* al personaje por una duración fija; los enemigos atacan en sus propios timers independientemente.

El jugador controla un personaje a la vez. Los demás quedan en idle. El loop de tensión central: gestionar múltiples personajes bajo presión simultánea de los timers enemigos.

Ver [[Sistema de Combate en Tiempo Real]] para el flujo completo, estados de personaje y comportamiento de enemigos.

### 6.2 QTE Bidimensional

El disparo se resuelve con un minijuego de dos ejes:
1. Barra vertical oscila — el jugador fija eje Y (Confirm)
2. Barra horizontal oscila — el jugador fija eje X (Confirm)
3. La intersección es el punto de intención → pasa por las 3 capas de dispersión

La velocidad de la barra varía por arma. No se puede cancelar una vez iniciado.

### 6.3 Dispersión y apuntado

Tres capas independientes que transforman el punto de intención en punto de impacto:

- **L1 (HP):** Radio proporcional al daño recibido. Solo en el primer disparo de cada ráfaga. A HP 0% el radio es el doble que a HP 100%.
- **L2 (mecánica):** Desviación aleatoria fija del arma. Siempre presente.
- **L3 (recoil):** Patrón predefinido por arma desde el segundo disparo. Aprendible pero no eliminable.

Ver [[Sistema de Dispersion y Apuntado]] para fórmulas exactas, tablas por arma y resolución de impacto.

### 6.4 Distractores visuales

Seis canales de distracción que se activan progresivamente según el HP del operador activo:

| HP (%) | Distractores activos |
|--------|---------------------|
| < 95%  | Vibración de barras QTE |
| < 90%  | + Screen shake (sincronizado con heartbeat) |
| < 85%  | + Viñeta de sangre + Ghost lines |
| < 75%  | + Ruido estático |
| < 65%  | + Parpadeo de silueta enemiga |

Ver [[Distractores Visuales]] para umbrales exactos, fórmulas e interacción con Krokonil.

### 6.5 Armadura por capas

La protección enemiga es geometría visible, no un stat numérico. El jugador puede leer qué zonas están protegidas y elegir dónde apuntar.

Tipos disponibles: casco militar, chaleco torso, chaleco+esternón, hombro, placas balísticas. Cada pieza tiene una cobertura geométrica relativa a la hitbox de su zona base y un factor de reducción de daño propio.

Ver [[Diseño de Combate y Armas]] para el catálogo completo y las 8 configuraciones de armadura.

### 6.6 Sistema de munición

Solo la munición 9mm tiene dos variantes tácticas con efectos distintos:

| Tipo | vs carne | vs chaleco | vs placas |
|------|----------|-----------|----------|
| RIP  | ×1.0     | ×0.4      | ×0.2     |
| FMJ  | ×0.8     | ×0.7      | ×0.5     |

RIP destruye carne pero rebota en protección. FMJ penetra mejor pero hace menos daño a carne expuesta. La elección ocurre durante la recarga — decisión táctica bajo presión temporal.

### 6.7 Armas y patrones de recoil

Cada arma tiene identidad mecánica única a través de su patrón de recoil. Los patrones son aprendibles pero nunca eliminables.

| Arma       | Forma del patrón  | Compensación ideal |
|------------|------------------|--------------------|
| P229       | "7"              | Abajo-izquierda    |
| MP5        | "I" (leve derecha)| Casi solo abajo   |
| Benelli M4 | "V invertida"    | Fuerte abajo       |
| Mk18       | "J invertida extendida" | Abajo-derecha |

Ver [[Sistema de Dispersion y Apuntado]] para los patrones completos disparo por disparo.

---

## 7. Sistemas de Supervivencia

### 7.1 Salud y presión arterial

Dos recursos independientes, dos vías de muerte:
- **HP:** Baja con impactos directos y hemorragia. Muerte si HP ≤ 0.
- **Presión arterial:** Baja con hemorragia. Muerte por shock si sistólica ≤ 40.

No hay barra de vida visible. El jugador lee el estado a través del ECG (color + BPM + lectura de presión). La trampa del IFAK: curar HP con IFAK mientras se ignora la hemorragia activa deja al operador con ECG verde mientras la presión cae silenciosamente.

Ver [[Sistema de Salud]] para el modelo completo, tablas de hemorragia por nivel e integración con QTE.

### 7.2 Krokonil

Anti-permadeath con precio permanente. Durante 4-5 turnos: HP y presión congelados, sin penalizaciones, muerte imposible. Al expirar: el estado real se revela de golpe.

Costo permanente: +15 krk_exposure por dosis. Nunca baja. A exposure > 50: síntomas de abstinencia cuando no hay dosis activa. A exposure > 70: degradación permanente de puntería y signos vitales.

La revelación dramática (Acto III): el "regulador KRK-NL" que el jugador usó libremente ES Krokonil — la misma droga que destruyó a los infectados. El juego retroactivamente convierte todas las decisiones previas del jugador en un espejo de lo que hicieron los perpetradores.

Ver [[Krokonil]] · [[Sistema de Salud#Krokonil como Item]]

### 7.3 Inventario

Grilla 4×4 por operador. Los items tienen dimensiones físicas (1×1 a 4×1) y se rotan 90°. El inventario del operador se pierde con él si muere — no hay almacén central durante el combate.

Items médicos vs munición compiten directamente por espacio. El jugador que no planifica llega a un combate sin torniquetes o sin balas.

Ver [[Sistema de Inventario]] para tamaños de items, controles de navegación y menú contextual.

### 7.4 Recursos y escasez

Los recursos son finitos a nivel global — lo que existe en el barco es todo lo que hay. Progresión de escasez progresiva por acto, de completos (Acto I) a casi nulos (Acto V).

Ver [[Mecanicas de Supervivencia]] para tablas de escasez por acto y mecánicas ambientales del barco.

---

## 8. Exploración

### 8.1 Movimiento

Movimiento 4-direccional cardinal, sin diagonales, sin botón de correr. El input análogo se cuantiza al eje dominante. Una sola velocidad que refuerza la tensión de survival: no se puede "huir rápido", solo retroceder al mismo ritmo.

Ver [[Sistema de Movimiento]] para controles, física y animación (8 estados, transiciones sin blend).

### 8.2 Guardado — Telégrafo Morse

Las zonas de guardado son salas con un telégrafo radio-telegráfico — el mismo sistema que usó el Titanic para pedir auxilio. Guardar = transmitir un mensaje en Morse. El patrón Morse es siempre el mismo. Más adelante el jugador descubre que ese patrón es un mensaje oculto.

Simetría narrativa: en el final, Mateo envía un último mensaje usando el mismo telégrafo que usó para guardar toda la partida. El guardado deja de ser mecánico — se convierte en acto narrativo.

Ver [[Mecanicas de Supervivencia#Sistema de Guardado]] · [[Intro Cinematica]]

---

## 9. Progresión por Acto

| Acto | Recursos          | Amenaza               | Party                          |
|------|-------------------|-----------------------|-------------------------------|
| I    | Completos, limitados | Enemigos lentos     | MSRT×3 → CIA entra en Encuentro 1 |
| II   | Empiezan a escasear | Más resistentes     | Mateo + CIA + SEALs×3 |
| III  | Escasez notable   | Exposición ambiental  | Party variable, CIA sospechoso |
| IV   | Críticos          | Escasez extrema       | Party sin CIA                 |
| V    | Casi nulos        | Presión temporal total | Supervivientes                |

Ver [[Acto I - Diseño Detallado]] para el único acto con diseño de nivel completo.

---

## 10. Referentes e Influencias

| Referencia | Elemento tomado |
|-----------|----------------|
| Resident Evil Gaiden | Barra móvil para resolver ataques — base del QTE bidimensional |
| Shadow Hearts | Judgment Ring: zonas críticas de timing donde el jugador decide |
| Vagrant Story | Targeting por zona anatómica dentro de sistema táctico |
| Parasite Eve | RPG con gestión de recursos, munición como recurso físico |
| Lost Odyssey | Ring timing: el anillo que se cierra afecta daño y precisión |
| Sweet Home (SNES) | Grupo atrapado en espacio cerrado, survival de desgaste |
| Chrono Trigger | Combates en el mismo mundo, sin pantalla de transición |

Ver [[Referencias e Influencias]] para análisis extendido.

---

## 11. Brechas de Diseño Pendientes

| Sistema | Estado | Prioridad |
|---------|--------|----------|
| Panel de comandos (combat-ui) | En implementación | Alta |
| QTE integrado con flujo de combate | En implementación | Alta |
| Enemy AI behaviors por fase de deterioro | Sin diseño formal | Alta |
| Acts II–V diseño detallado de nivel | Sin diseño | Media |
| Encuentros de enemigos por zona del barco | Sin diseño | Media |
| Sistema de zonas seguras / almacenamiento | Pendiente en inventario | Media |
| UI/UX final del juego | Sin diseño formal | Baja |
| Stack máximo de cajas de balas (inventario) | Pendiente | Baja |
| Tamaño del Krokonil como item | Pendiente | Baja |
| Sistema de interacción y highlight de objetos | Pendiente en movimiento | Media |

---

Volver a [[Crimson Draft]]
