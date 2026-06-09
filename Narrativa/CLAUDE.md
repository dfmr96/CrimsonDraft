# CrimsonDraft — Instrucciones de Contexto

Este archivo es leído automáticamente por Claude Code al inicio de cada sesión. Contiene las instrucciones de escritura, estructura del proyecto y convenciones establecidas para la narrativa de CrimsonDraft.

---

## Qué es este proyecto

**CrimsonDraft** es la narrativa de un videojuego de terror y acción táctica ambientado en enero de 2026. El jugador aborda el buque **El Marinera** como parte de la **Operación Crimson** — una misión que cree rutinaria y que en realidad es el último paso de un experimento de control social de décadas.

La bóveda de Obsidian en esta carpeta documenta toda la narrativa, personajes, sistemas y lore del juego.

---

## Estructura de archivos

```
Narrativa/
├── CrimsonDraft — MOC.md          ← índice central, empezar aquí
├── Mundo/
│   ├── El Mundo — Contexto.md
│   └── Hipótesis del Mundo.md
├── Personajes/
│   ├── Ethan Miller.md            ← protagonista, MSRT, leal a la ley
│   ├── Darius Mercer.md           ← team leader SEALs/CIA, busca dominio
│   ├── Lilou Vance.md             ← sniper SEALs, sin raíces, adaptable
│   ├── Marcus Hale.md             ← combat engineer SEALs, cree en el destino
│   ├── Adrian Volkov.md           ← agente CIA infiltrado como chef
│   ├── Vanessa Stoian.md          ← amante de Adrian, variante reptiliana KRK-NL
│   └── El Ingeniero.md            ← caballo, busca a su gato, descubre todo
├── El Incidente/
│   ├── El Marinera — Timeline.md          ← cronología completa con fechas
│   ├── El Marinera — Secciones.md         ← Deck B y Deck C, cada habitación
│   ├── El Marinera — Carteles.md          ← 6 carteles de propaganda institucional
│   ├── La Estructura Interna — Rangos del Marinera.md
│   ├── Adrian y Vanessa — El Incidente.md
│   ├── Operación Crimson.md
│   └── El Diario del Ingeniero.md         ← 9 entradas con deterioro progresivo
└── Sistemas/
    ├── KRK-NL — Krokonil.md
    ├── M.E.R.I.md
    ├── M.E.R.I. — Registros de Prueba.md  ← 4 registros de pruebas fallidas
    └── Protocolo SCUTTLE.md
```

---

## Estilo de escritura — IMPORTANTE

### Formato: Opción B — Híbrido literario

- **Tablas de datos rápidos** al inicio de cada ficha (edad, unidad, especialidad) — solo para referencia inmediata
- **Todo lo demás es prosa narrativa** — no listas técnicas, no bullet points de información
- Los bullet points existen solo como enumeraciones poéticas dentro de la prosa, nunca como estructura informativa principal

### Tono

El tono es de **novelista consciente del peso filosófico de lo que narra**. No es un documento técnico de diseño. Es un texto que muestra la realidad de un mundo donde las naciones trabajan en secreto entre ellas mientras presentan al público narrativas de conflicto.

Los personajes son **actores menores** dentro de sistemas que los trascienden. Eso debe sentirse en cada descripción.

Usar:
- Metáforas concretas, no decorativas
- Conceptos filosóficos tratados con respeto y profundidad cuando aparecen
- Frases que cierren con peso, no que simplemente terminen

Evitar:
- Lenguaje técnico de diseño de juegos ("el jugador experimenta X")
- Descripciones neutras de hechos sin carga narrativa
- Listas de características psicológicas sin elaboración

### Referencias y callouts de Obsidian

Cuando se menciona una referencia cultural, filosófica o histórica real, agregar inmediatamente un callout:

```
> [!info] Referencia — *Título*, Autor (año)
> Descripción breve de la obra.
> 
> **Por qué aplica aquí**: explicación de la conexión con la narrativa.
```

**Referencias ya establecidas y usadas:**
- *They Live*, John Carpenter (1988) — arquitectura de control invisible
- *Rebelión en la Granja*, George Orwell (1945) — jerarquía diseñada vs. emergida; Snowball como motor narrativo
- *The Wanderer and His Shadow*, Nietzsche (1880) — sombra que cree que camina; conocimiento como carga
- *Edipo Rey*, Sófocles — el conocimiento del futuro como condena autoimpuesta
- CLAP venezolano, Libreta cubana, SNAP estadounidense — modelos reales de dependencia alimentaria estatal

---

## Convenciones técnicas de Obsidian

- **Wiki-links**: usar siempre `[[Nombre del archivo]]` para conectar notas
- **M.E.R.I.**: el link correcto es `[[M.E.R.I]]` (sin punto final) — el archivo tiene este alias en su frontmatter
- **Frontmatter**: todos los archivos tienen `tags` y `aliases` donde corresponde
- **Callouts**: usar `> [!info]` para referencias, `> [!note]` para notas de diseño/narrativa
- **Documentos in-game** (diarios, carteles, registros): se escriben como artefactos reales del mundo, no como análisis. Tono diferente al de las fichas narrativas.

## Sistema de rangos del Marinera

| Animal | Color cartel | Acceso | Rol |
|--------|-------------|--------|-----|
| Oveja 🐑 | Amarillo | Exterior, balcones | Tripulantes comunes |
| Caballo 🐴 | Rojo | Zonas técnicas (Energy Room, Water Room, Cat Room) | Ingenieros |
| Perro 🐕 | Celeste | Camarotes, comedor, seguridad | Capitán y seguridad |
| Cerdo 🐖 | Blanco | Laboratorio, consultorios, cubículos | Científicos y líderes |
| Cerdo tachado | Negro | Protocolo SCUTTLE — único dispositivo | Acceso especial |

**Referencia**: *Rebelión en la Granja*, Orwell — jerarquía diseñada desde arriba, no emergida.

## Estructura física del Marinera

**Deck B** — habitabilidad y mando: Sub Captain Room, Radio, Storage, Save Room, Cheff Room, Lavatory, Kitchen, DinnerRoom, BathDroom B, Engineer Room, Seaman Room 1/2, Broken Seaman Room, Hallway A/B1/B2, Exterior HallWay, Balcony A/B, Port Stairs.

**Deck C** — laboratorio y técnico: Lab A/B, Lab Hall A/B, Lab HallWay, Diorama Room, Hallway A/B, Cells A/B, Subject Room, Office, Save Room, Balcony A/B, Energy Room, Water Room, Cat Room.

**Cat Room** = sector C-4, donde el ingeniero escondió a Bola de Nieve. Aquí está la tarjeta negra robada bajo la manta del gato. Aquí ocurrió algo que el ingeniero no recuerda.

---

## Temas centrales del proyecto

1. **El poder como única ideología real** — los Estados no compiten, administran conflictos
2. **La profecía de Edipo** — saber el futuro puede condenarte a él si lo tratas como inevitable
3. **El colapso como metamorfosis** — la transformación requiere primero la disolución completa
4. **La cooperación secreta entre enemigos declarados** — EE.UU. y Rusia como socios silenciosos
5. **El conocimiento fragmentado como sistema de control** — cada rango sabe solo lo que necesita para funcionar

---

## Timeline del Marinera

| Fecha | Evento |
|-------|--------|
| Agosto 2025 | El Marinera zarpa desde la costa iraní |
| 5 Oct 2025 | Adrian Volkov se infiltra como chef |
| Oct–Nov 2025 | Vanessa Stoian sube al barco |
| Nov 2025 | Deterioro de la relación; Vanessa investiga |
| 3 Dic 2025 | Tormenta + sabotaje de suministros por Vanessa |
| 4 Dic 2025 | Adrian comienza distribución de raciones MERI con KRK-NL |
| 4–20 Dic 2025 | Efectos progresivos del KRK-NL en la tripulación |
| 20 Dic 2025 | Confrontación; Vanessa se autoadministra variante reptiliana |
| 21–23 Dic 2025 | Fase híbrida de Vanessa; colapso inicial |
| 23 Dic 2025 | Capitán contacta Rusia; CIA corta comunicaciones |
| 26 Dic 2025 | Fuerzas rusas abordan encubiertamente |
| 27 Dic 2025 | Adrian intenta SCUTTLE; Vanessa lo intercepta; falla |
| 28 Dic – 6 Ene | Silencio total — el ingeniero muere en este período |
| **7 Ene 2026** | **Operación Crimson** |

## Arco narrativo central (spoilers)

`KRK-NL` desarrollada por CIA → filtrada a Sudamérica → distribuida vía `M.E.R.I.` → `Vanessa Stoian` sabotea suministros del Marinera (motivo personal) → `Adrian Volkov` distribuye raciones MERI con KRK-NL → tripulación colapsa → Vanessa se autoadministra variante reptiliana → Adrian no puede activar `SCUTTLE` → `Operación Crimson` enviada a cerrar lo que Adrian no pudo → **el jugador entra sin saber que es el cierre**.

**Clímax**: los personajes descubren que todos sus inyectables contenían KRK-NL. Ya están contaminados. La pregunta del juego: ¿el saber del futuro te condena a él?

---

## Procedimiento de trabajo establecido

1. El usuario pasa bloques de contenido o ideas
2. Claude estructura y propone organización antes de escribir
3. Se espera confirmación antes de crear archivos nuevos
4. Al agregar contenido nuevo: revisar si afecta archivos existentes y actualizarlos
5. Siempre actualizar `CrimsonDraft — MOC.md` cuando se crean archivos nuevos
6. Las referencias reales (filosóficas, culturales, históricas) siempre llevan callout `[!info]` en el punto exacto donde aparecen
