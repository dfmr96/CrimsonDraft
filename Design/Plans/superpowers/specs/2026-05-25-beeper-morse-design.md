# Beeper — Morse Decoder Logic Design Spec

**Date:** 2026-05-25
**Status:** Approved
**Scope:** Lógica pura del decodificador Morse — C# sin dependencias de Unity

---

## Overview

El Beeper es un aparato del mundo de Crimson Draft que acepta entrada en código Morse (pulsaciones cortas y largas) y construye una palabra letra por letra. El aparato no conoce la palabra correcta — simplemente expone el estado actual de lo que el jugador ha ingresado. El sistema que lo contiene (una puerta, un panel, etc.) decide si la palabra ingresada es válida.

Esta spec cubre únicamente la lógica del decodificador: navegación del árbol binario de Morse, confirmación de letras, backspace y reset.

---

## Árbol binario de Morse

El árbol sigue el estándar internacional. Presión corta = punto (`.`) = rama derecha. Presión larga = guión (`-`) = rama izquierda.

```
           START
          /     \
         T       E
        / \     / \
       M   N   A   I
      /\ /\ /\ /\
     O G K D W R U S
    (nivel 4: Q Z Y C X B J P L F V H)
```

El diccionario completo (26 letras):

| Secuencia | Letra | Secuencia | Letra |
|-----------|-------|-----------|-------|
| `-`       | T     | `.`       | E     |
| `--`      | M     | `-.`      | N     |
| `.-`      | A     | `..`      | I     |
| `---`     | O     | `--.`     | G     |
| `-.-`     | K     | `-..`     | D     |
| `.--`     | W     | `.-.`     | R     |
| `..-`     | U     | `...`     | S     |
| `--.-`    | Q     | `--..`    | Z     |
| `-.--`    | Y     | `-.-.`    | C     |
| `-..-`    | X     | `-...`    | B     |
| `.---`    | J     | `.--.`    | P     |
| `.-..`    | L     | `..-.`    | F     |
| `...-`    | V     | `....`    | H     |

---

## Clase `MorseDecoder`

Clase C# pura. Sin MonoBehaviour. Sin dependencias de UnityEngine.

### Estado interno

| Campo              | Tipo            | Descripción                                          |
|--------------------|-----------------|------------------------------------------------------|
| `currentSequence`  | `StringBuilder` | Acumula `.` y `-` para la letra en progreso          |
| `word`             | `List<char>`    | Letras ya confirmadas                                |

### Propiedades públicas

| Propiedad           | Tipo                   | Descripción                                     |
|---------------------|------------------------|-------------------------------------------------|
| `CurrentSequence`   | `string`               | Secuencia en progreso (ej. `".-"`)              |
| `Word`              | `IReadOnlyList<char>`  | Letras confirmadas hasta ahora                  |

### Métodos públicos

| Método         | Efecto                                                                                       |
|----------------|----------------------------------------------------------------------------------------------|
| `InputDot()`   | Agrega `.` a `currentSequence`                                                               |
| `InputDash()`  | Agrega `-` a `currentSequence`                                                               |
| `Confirm()`    | Busca `currentSequence` en el diccionario. Si existe: agrega letra a `word`, limpia `currentSequence`. Si no existe o está vacío: no hace nada. |
| `Backspace()`  | Si `currentSequence` no está vacío: lo limpia. Si está vacío: elimina la última letra de `word`. Si todo está vacío: no hace nada. |
| `Reset()`      | Limpia `currentSequence` y `word`.                                                           |
| `GetWord()`    | Retorna `string` con las letras de `word` concatenadas (conveniencia).                       |

---

## Separación de responsabilidades

La detección del timing de pulsación (corta vs larga) **no es responsabilidad de `MorseDecoder`**. El llamador mide la duración del press y decide llamar `InputDot()` o `InputDash()`. Esto mantiene la lógica del árbol completamente testeable sin tiempo real.

```
[Input Handler]               [MorseDecoder]
  botón presionado
  botón soltado
  duracion < umbral  →  InputDot()
  duracion >= umbral →  InputDash()
  botón confirmar    →  Confirm()
  botón backspace    →  Backspace()
  escape / cerrar    →  Reset()
```

---

## Edge cases

| Situación                                          | Comportamiento                                  |
|----------------------------------------------------|-------------------------------------------------|
| `Confirm()` con `currentSequence` vacío            | No hace nada                                    |
| `Confirm()` con secuencia inválida (ej. `"....."`) | No hace nada — no hay match en el diccionario   |
| `Backspace()` con todo vacío                       | No hace nada                                    |
| `Backspace()` con `currentSequence` no vacío       | Limpia `currentSequence`, no toca `word`        |
| `InputDot/Dash()` más allá del nivel 4 del árbol   | Se agrega igual — `Confirm()` no tendrá match   |

---

## Fuera de scope (este prototipo)

- Detección de duración de pulsación
- MonoBehaviour / Unity Input System
- UI / display visual
- Validación de la palabra contra un código esperado
- Integración con el sistema de interactuables (`IInteractable`)
