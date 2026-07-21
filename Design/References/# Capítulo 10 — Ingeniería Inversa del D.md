# Capítulo 10 — Ingeniería Inversa del Diseño

> *"La función real de un sistema se descubre cuando imaginamos el juego sin él."*

---

# Introducción

Hasta ahora hemos descrito la Mansión Spencer como una red de espacios, decisiones y relaciones.

Sin embargo, todavía no sabemos qué importancia tiene cada uno de esos elementos.

Para responder esa pregunta utilizaremos un método habitual en ingeniería de sistemas:

**el análisis por eliminación.**

La idea es sencilla.

En lugar de preguntar:

> ¿Para qué sirve este sistema?

Preguntaremos:

> ¿Qué ocurre si desaparece?

Cuanto mayor sea el impacto de su ausencia, mayor será su importancia estructural.

---

# El principio de eliminación

Supongamos que eliminamos un elemento del juego.

Después analizamos qué sistemas dejan de funcionar.

No buscamos determinar si el juego sigue siendo divertido.

Buscamos medir cuántas relaciones desaparecen.

Por ejemplo.

Eliminar la música.

¿Qué cambia?

- disminuye la tensión
- aumenta la incertidumbre acústica
- el combate sigue funcionando
- la economía permanece igual

La música tiene un efecto emocional importante.

Pero su impacto sobre la estructura del juego es limitado.

Ahora probemos otro caso.

Eliminar el inventario.

¿Qué ocurre?

- desaparece la planificación
- desaparecen las decisiones sobre objetos
- desaparecen muchos recorridos hacia los Item Boxes
- disminuye el valor de los atajos
- cambia la economía
- cambia el ritmo
- cambia la dificultad

Un único sistema afecta a muchos otros.

Eso indica una alta centralidad dentro del diseño.

---

# Dependencias entre sistemas

Podemos representar Resident Evil como una red.

```text
Inventario

├── Recursos

├── Exploración

├── Atajos

├── Item Box

├── Guardado

└── Combate
```

Si eliminamos el inventario, todos esos sistemas cambian.

En cambio.

Si eliminamos un único enemigo.

La estructura general permanece prácticamente intacta.

No todos los elementos tienen el mismo peso.

---

# Centralidad

Tomando prestado un concepto de la teoría de grafos, podemos hablar de **centralidad sistémica**.

No mide la importancia narrativa de un elemento.

Mide cuántas relaciones mantiene con otros sistemas.

Por ejemplo.

| Sistema | Relaciones estimadas |
|----------|----------------------:|
| Inventario | Muy alta |
| Llaves | Muy alta |
| Mapa | Muy alta |
| Recursos | Muy alta |
| Cámaras | Alta |
| Música | Media |
| Enemigos individuales | Baja |
| Decoración | Muy baja |

Esto no significa que un zombi sea irrelevante.

Significa que sustituir un zombi por otro apenas altera la estructura del juego.

Eliminar el sistema de inventario, en cambio, transforma la experiencia completa.

---

# Sistemas primarios y secundarios

Proponemos distinguir dos categorías.

## Sistemas estructurales

Son aquellos cuya modificación altera gran parte del juego.

Ejemplos.

- Inventario.
- Llaves.
- Economía de recursos.
- Diseño del mapa.
- Guardado.
- Progresión.

---

## Sistemas expresivos

Afectan principalmente a la presentación de la experiencia.

Ejemplos.

- Música.
- Iluminación.
- Sonido ambiental.
- Modelos de enemigos.
- Efectos visuales.

Estos sistemas son esenciales para la atmósfera, pero dependen de los sistemas estructurales para producir tensión sostenida.

---

# Un experimento mental

Imaginemos dos versiones de Resident Evil.

Versión A.

Con zombis, música, cámaras y gráficos originales.

Pero:

- inventario infinito
- munición abundante
- guardado automático
- sin llaves

La mayoría de jugadores diría que "ya no se siente como Resident Evil".

Ahora imaginemos la versión B.

Con todos los sistemas originales.

Pero utilizando modelos temporales sin texturas y sin música.

Probablemente seguiríamos reconociendo el mismo diseño.

La atmósfera sería peor.

Pero la estructura permanecería.

---

# Hipótesis

La identidad de Resident Evil depende mucho más de sus sistemas estructurales que de sus elementos audiovisuales.

La presentación amplifica la experiencia.

No la crea por sí sola.

Esta hipótesis explica por qué tantos juegos han logrado imitar la estética del survival horror sin conseguir reproducir su tensión.

---

# Aplicación metodológica

A partir de este punto, cada sistema importante será analizado mediante cuatro preguntas.

1. ¿Qué problema resuelve?

2. ¿Con qué otros sistemas interactúa?

3. ¿Qué ocurre si desaparece?

4. ¿Qué principios de diseño pueden extraerse?

Este procedimiento se repetirá para:

- inventario
- llaves
- mapas
- enemigos
- armas
- Item Boxes
- Save Rooms
- cámaras
- economía
- progresión

Al finalizar obtendremos un mapa completo de las dependencias internas de Resident Evil.