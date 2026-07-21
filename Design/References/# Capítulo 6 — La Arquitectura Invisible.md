# Capítulo 6 — La Arquitectura Invisible

> *"El jugador explora habitaciones. El diseñador construye relaciones."*

---

# Introducción

Cuando un jugador recuerda Resident Evil suele pensar en lugares concretos.

El Hall Principal.

La Dining Room.

La residencia.

El laboratorio.

Sin embargo, desde el punto de vista del diseño, las habitaciones individuales tienen menos importancia que las relaciones entre ellas.

Podemos imaginar una analogía.

Una ciudad no funciona gracias a sus edificios.

Funciona gracias a las calles que los conectan.

Resident Evil sigue exactamente el mismo principio.

La Mansión Spencer no es una colección de habitaciones.

Es una red de conexiones.

Y esa red cambia constantemente durante la partida.

---

# Del plano arquitectónico al grafo

Un arquitecto dibuja planos.

Un diseñador de videojuegos puede abstraer ese plano como un grafo.

Formalmente definiremos:

## Nodo

Un espacio navegable.

Ejemplos:

- Hall Principal
- Dining Room
- Guardhouse
- Save Room

---

## Arista

Una conexión entre dos nodos.

Puede ser:

- puerta
- escalera
- ascensor
- túnel
- pasillo

---

## Estado

Una arista puede encontrarse en distintos estados.

Por ejemplo.

```text
Hall
 │
 ├────────────── Dining
 │
 ├────(Locked)── East Wing
 │
 ├────(Sword Key)
 │
 ├────(Armor Key)
 │
 └────(Broken Lock)
```

La geometría no cambia.

Lo que cambia es el estado de las conexiones.

---

# Un mapa que evoluciona

En muchos videojuegos abrir una puerta únicamente aumenta el área disponible.

En Resident Evil ocurre algo distinto.

Abrir una puerta modifica el valor de otras rutas.

Veamos un ejemplo.

Supongamos que existe un único camino.

```text
A → B → C
```

Si posteriormente desbloqueamos un atajo.

```text
A → C
```

No solamente aparece una nueva ruta.

También disminuye el coste de todas las rutas futuras que atraviesen C.

Una única puerta afecta a decenas de recorridos posteriores.

Esto es exactamente lo que ocurre con numerosos atajos de la Mansión Spencer.

---

# Coste de una ruta

En arquitectura, la distancia suele medirse en metros.

En Resident Evil proponemos otro modelo.

El coste de una ruta no depende únicamente de la longitud.

Depende de múltiples variables.

Podemos expresar la idea de forma conceptual.

```text
Coste de una ruta =

Distancia

+

Peligro

+

Consumo esperado de recursos

+

Tiempo

+

Carga cognitiva
```

No pretendemos utilizar esta fórmula para obtener un número exacto.

Su utilidad consiste en recordar que una ruta nunca es únicamente una línea sobre un mapa.

---

# Un mismo pasillo puede cambiar completamente

Imaginemos un corredor.

Durante la primera hora contiene dos zombis.

El jugador dispone de poca munición.

Atravesarlo resulta costoso.

Más adelante.

Los zombis han muerto.

Existe un atajo.

Disponemos de una escopeta.

La misma geometría produce una experiencia completamente distinta.

Esto nos lleva a una conclusión importante.

> El diseño espacial de Resident Evil es dinámico aunque el escenario sea estático.

---

# Información como recurso espacial

Existe otra variable que rara vez aparece en los análisis.

El conocimiento.

La primera vez que atravesamos una habitación.

No conocemos:

- cámaras
- enemigos
- objetos
- rutas de escape

La segunda vez.

Conocemos todo eso.

La habitación física es idéntica.

Pero el coste psicológico disminuye.

Por ello proponemos considerar la información como un recurso del mapa.

El jugador no solo descubre habitaciones.

También reduce la incertidumbre asociada a ellas.

---

# La arquitectura de la confianza

A medida que progresa la partida.

El jugador comienza a desarrollar confianza.

No porque tenga más balas.

Sino porque comprende mejor el espacio.

Empieza a recordar.

"En esta esquina siempre hay un zombi."

"Este pasillo conecta con la Save Room."

"Aquí puedo esquivar al Hunter."

El dominio del mapa reduce la ansiedad.

En consecuencia.

La Mansión Spencer produce una curva emocional muy particular.

```text
Desconocimiento

↓

Ansiedad

↓

Aprendizaje

↓

Confianza

↓

Nueva zona

↓

Desconocimiento

↓

Ansiedad
```

La experiencia consiste en repetir continuamente este ciclo.

---

# La ilusión del mundo vivo

Resident Evil casi nunca modifica la arquitectura.

Sin embargo.

El jugador tiene la sensación de que la mansión evoluciona.

¿Por qué?

Porque cambian continuamente tres elementos.

- nuevas llaves
- nuevos enemigos
- nuevas rutas

El resultado es un mapa que parece transformarse aunque sus paredes permanezcan inmóviles.

---

# Hipótesis

La Mansión Spencer puede entenderse como un sistema donde la geometría permanece estable mientras cambia constantemente el valor de cada conexión.

El jugador no explora un edificio.

Explora una red cuyo significado evoluciona durante toda la partida.

Esta idea servirá como fundamento para analizar posteriormente la RPD y Raccoon City.

En ambos casos veremos que Capcom reutiliza exactamente el mismo principio, aunque con diferentes objetivos de diseño.