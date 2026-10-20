# Resident Evil 2: referencia técnica (ingeniería inversa)

> **Fuente**: análisis propio de Resident Evil 2 (1998) en tres versiones:
> - **PC (GOG)**, `LeonU.exe` y los `.RDT` de `Pl0/Rdu` (escenario de Leon).
> - **PlayStation, Biohazard 2 Beta 2** (disco con símbolos de depuración `MAIN.SYM`), ejecutado en DuckStation.
> - **PlayStation, versión comercial Dual Shock (USA)** y **prototipo de noviembre de 1996**, solo para comparar.
>
> Es material de referencia, no de diseño: describe cómo funciona el juego original. Las decisiones de Crimson Draft van en el GDD.

## Convenciones

- **Verificado**: comprobado en el código, en los datos o en el juego en ejecución.
- **Deducido**: encaja con todo lo observado, pero no se vio la instrucción o el dato que lo confirma.
- Las direcciones de PC son **VA** de `LeonU.exe` (base `0x400000`). Las de PlayStation son direcciones de RAM (`0x80xxxxxx`).
- "u" = unidades del motor. 1000 u ≈ 1 m (deducido: la separación entre plantas es 1800 u).

## Índice

### Formatos de archivo
- [Formato RDT (salas)](formatos/rdt.md)
- [Scripts SCD y opcodes](formatos/scd-opcodes.md)
- [Codificación de mensajes](formatos/mensajes.md)
- [Texturas TIM y paletas](formatos/tim.md)
- [Ejecutables PC y PlayStation](formatos/ejecutables.md)

### Cámaras
- [FOV y proyección](camaras/fov.md)
- [Cambio de cámara por zonas](camaras/cambio-de-camara.md)
- [Cámara al entrar en una sala](camaras/entrada-a-sala.md)

### Navegación
- [Movimiento del jugador (estados, giro, velocidad, dirección)](navegacion/movimiento-jugador.md)
- [Espacio navegable (sin navmesh; superficie encima de objetos)](navegacion/espacio-navegable.md)
- [Colisión del escenario (SCA)](navegacion/colision-sca.md)
- [Objetos empujables](navegacion/objetos-empujables.md)
- [Subir y bajar (escaleras de mano, bordes)](navegacion/subir-y-bajar.md)

### Interacción
- [AOT: áreas de disparo ("hitboxes")](interaccion/aot.md)
- [Reglas de uso de ítems (resumen de diseño)](interaccion/reglas-de-uso.md)
- [Receptáculos y transformadores (Fuse Case, cuarto oscuro)](interaccion/receptaculos.md)

### Ítems e inventario
- [Tabla de atributos de ítems](items/atributos.md)
- [Combinaciones](items/combinaciones.md)
- [Curación](items/curacion.md)
- [Munición infinita y truco de botones](items/municion-infinita.md)
- [Casillas del inventario](items/casillas.md)
- [Números del inventario (contadores y colores)](items/numeros-inventario.md)

### Jugador y progresión
- [Vida de los personajes](jugador/vida.md)
- [Rango y desbloqueos](jugador/rango-y-desbloqueos.md)
- [Guardado e Ink Ribbon](jugador/guardado.md)

### Enemigos
- [Estructura de entidad (jugador y enemigos)](enemigos/estructura-entidad.md)
- [Vida de los enemigos](enemigos/vida.md)
- [Daño de las armas](enemigos/danio.md)
- [IA: máquina de estados y percepción](enemigos/ia.md)

### Texto e interfaz
- [Cómo se dibuja el texto](texto/render-de-texto.md)
- [Colores del texto (código F9)](texto/colores.md)
- [Mapa de paletas en VRAM](texto/vram-paletas.md)
- [Menú de comandos del inventario](texto/menu-comandos.md)

### Direcciones de memoria
- [Mapa de memoria: PC (LeonU.exe)](direcciones/pc-leonu.md)
- [Mapa de memoria: Beta 2 (PlayStation)](direcciones/beta2-psx.md)

### Beta 2 y otras versiones
- [Contenido del disco y símbolos de depuración](beta2/disco-y-simbolos.md)
- [Menú de depuración](beta2/menu-debug.md)
- [Comparación de versiones PS1](beta2/versiones.md)
- [Traducción al inglés (parches)](beta2/traduccion.md)
- [Restos de depuración en PC](beta2/restos-debug-pc.md)

### Herramientas y método
- [Herramientas construidas](herramientas/herramientas.md)
- [Conexión a DuckStation (GDB)](herramientas/duckstation-gdb.md)
- [Método de trabajo](herramientas/metodo.md)

### Documento general previo
- [Reglas de interacción con ítems (documento completo)](../re2-interaction-rules.md)
