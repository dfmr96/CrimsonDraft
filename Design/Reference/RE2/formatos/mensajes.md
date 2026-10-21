# Codificación de mensajes

> Verificado en los RDT de PC y de la Beta 2, y en pantalla en DuckStation (parche de traducción).

## Caracteres

| Bytes | Contenido |
|---|---|
| `00`–`56` | Espacio, puntuación, dígitos, A–Z, a–z (tabla inglesa, abajo) |
| `57`–`9F` (aprox.) | **Hiragana**, en orden あいうえお… desde `0x57` (`に` = `6C`, `の` = `6F`, `は` = `70`, `る` = `7F`) |
| `A0`–`E9` (aprox.) | **Katakana** (nombres de ítems) |
| `EA`–`EF` + 1 byte | **Kanji** (2 bytes; `外` = `EF 3F`, `出` = `EE 24`, `使` = `EE 22`) |

Las letras latinas ocupan **los mismos códigos** en la fuente japonesa y en la inglesa (verificado: el texto inglés se ve bien en la Beta japonesa).

### Tabla inglesa (índice = código)

```
" .___()__“”_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz"
```

(`_` = posición no usada o símbolo especial.)

## Códigos de control

| Código | Parámetro | Efecto |
|---|---|---|
| `F7` | — | Separador de la lista de nombres de ítems |
| `F8` | 1 byte | Visto junto a `F9` en mensajes del sistema; probablemente inserta un valor variable (deducido) |
| `F9 p` | color | Cambia la paleta del texto → [colores](../texto/colores.md) |
| `FA xx` | — | Inicio de mensaje |
| `FB xx` | — | Pregunta **Sí/No** |
| `FC` | — | Salto de línea |
| `FD xx` | — | Salto de página (espera a que el jugador pulse) |
| `FE` | — | Fin de mensaje |

En la Beta, el `switch` de estos códigos está en `mess_trans`, con su tabla de saltos en `0x8001087C` (códigos `EE`–`FE`).

## Segunda codificación (menús de PC)

Los textos de menú de `LeonU.exe` usan **ASCII − `0x24`**: `)AIKNU` = "Memory", `+RANSNEPA` = "Overwrite".

## Detalles de maquetación

- La fuente japonesa avanza el ancho de un carácter japonés por cada letra latina: en la Beta, una línea admite unas **18–20 letras** (verificado en pantalla).
- El inglés de PC está maquetado para su fuente estrecha. En la Beta hubo que repartir de nuevo las líneas → [traducción](../beta2/traduccion.md).
