# Sistema de Mapa — Guía de Uso

> **Versión:** 2026-07-06
> **Implementa:** [[Sistema de Mapa]] (GDD) · plan técnico `docs/plans/2026-07-06-map-system-impl.md`
> **Audiencia:** level design / dev — cómo autorear, hornear y probar el mapa de un deck

Esta guía es práctica: cómo agregar el mapa de un deck nuevo, cómo dibujar habitaciones y
puertas, y cómo probarlo. Para el diseño (qué significa cada estado, por qué existe fog of
war, etc.) ver el GDD [[Sistema de Mapa]].

---

## 1. Resumen del pipeline

```
Escena (RoomController + puerta)
  → MapRoomShape / MapDoorMarker (autoría)
      → MapBaker (automático al guardar escena / entrar a Play Mode)
          → MapData (asset, geometría + IDs)
              → MapRenderer + MapScreenController (runtime)
```

No hay paso manual de "exportar": todo lo que dibujás en la escena se hornea solo al
guardar. Si algo no aparece en el mapa, el problema está en la autoría (ver [Troubleshooting](#6-troubleshooting)), no en el horneado.

---

## 2. Dar de alta un deck nuevo

1. Crear el asset de datos: **Assets → Create → CrimsonDraft → Map → Map Data**.
   - `Scene Name`: nombre exacto de la escena Unity del deck (ej. `Deck_C_Development`).
   - `Display Name` / `Abbreviation`: texto que ve el jugador (ej. "Deck C" / "C").
   - `Map Item Id`: el `itemId` del ítem de inventario que revela este deck (si existe).
   - `Grid Size` / `Cell Size`: tamaño del plano en unidades del Map Editor Window (no
     necesita coincidir con el tamaño real de la escena).
2. Agregarlo al `MapDataSet` existente (`Assets/Data/Map/MapDataSet.asset`) — arrastralo al
   array `Maps`. Si no aparece en este set, el deck nunca aparecerá en el selector de decks
   conocidos.
3. En la escena del deck, crear un GameObject (o reusar uno de raíz, ej. bajo
   `//GAMEPLAYCORE`) con el componente **`MapSceneConfig`** y asignarle el `MapData` recién
   creado. Sin este componente, `MapBaker` no sabe a qué asset hornear esta escena.

---

## 3. Autorear una habitación

1. Seleccionar el GameObject raíz de la `RoomController` de la habitación.
2. Agregar el componente **`MapRoomShape`**.
3. En el Inspector, botón **"Trace From Bounds"**: genera un rectángulo inicial a partir de
   los bounds combinados de todos los `Renderer` bajo la habitación.
4. En la **Scene View**, con el objeto seleccionado, aparecen puntos cian conectados por
   líneas — es el polígono en espacio local (plano XZ). Arrastrá cada punto para calcar la
   silueta real del piso. Botón **"Add Point"** en el Inspector agrega vértices.
5. Los campos `Map Offset` / `Map Rotation` / `Map Scale` / `Z Order` controlan dónde se
   dibuja la habitación en el plano — son independientes del transform 3D real (ver
   [Map Editor Window](#5-acomodar-el-layout-map-editor-window) para editarlos visualmente).

**Ítems recogibles**: cualquier `PickupInteractable` o `MapPickupInteractable` que sea hijo
de la habitación se linkea automáticamente al hornear (usa `GetComponentInChildren`) — no
hay que declararlo a mano. Esto es lo que habilita el estado `Completada` del GDD.

---

## 4. Autorear una puerta

1. Seleccionar el GameObject que ya tiene `RoomDoorInteractable` o `SceneDoorInteractable`
   (la puerta jugable real).
2. Agregar el componente **`MapDoorMarker`**.
3. El `DoorId` **no se configura a mano** — se lee automáticamente del interactable
   (`IDoorInteractable.DoorId`) al hornear. Si el marcador queda sin puerta interactable en
   el mismo GameObject, el bake lo omite con un warning.
4. Ajustar `Map Offset` (posición en el plano), `Map Rotation` y `Size` (tamaño del
   rectángulo dibujado).

**Puertas entre decks**: para que una escalera/puerta entre decks muestre el mismo estado
en el mapa de ambos lados, las dos direcciones deben compartir el mismo `doorId` en sus
respectivos `SceneDoorInteractable` — es una convención de nivel, no algo que el código
fuerce.

**Regla de visibilidad**: una puerta solo se dibuja si al menos una habitación *dibujada*
la tiene en su lista de puertas linkeadas. El bake linkea una puerta a la habitación si el
`MapDoorMarker` es hijo (directo o indirecto) de esa `RoomController`, o si la puerta es un
`RoomDoorInteractable` cuyo campo `Destination` apunta a esa habitación. Si tu puerta no
aparece, revisar que esté anidada bajo la habitación correcta en la jerarquía.

---

## 5. Acomodar el layout — Map Editor Window

**Tools → CrimsonDraft → Map Editor**.

Con la escena del deck abierta y su `MapSceneConfig` configurado:

- Grilla de fondo según `Grid Size` / `Cell Size` del `MapData`.
- Cada `MapRoomShape` se dibuja como contorno cian (amarillo si está seleccionado) con su
  `roomId` como etiqueta; cada `MapDoorMarker` como rectángulo rojo/amarillo.
- **Click** selecciona (sincroniza con la selección de Unity) y **arrastra** mueve
  (`MapOffset`).
- **R** rota la selección 90°.
- **Scroll** hace zoom, **click del medio + arrastrar** panea la vista.
- Botón **"Bake Now"** fuerza un horneado manual sin necesidad de guardar la escena.

Esta ventana solo edita la *posición en el plano* — la silueta (los puntos del polígono) se
edita en la Scene View con `MapRoomShapeEditor` (paso 3).

---

## 6. Troubleshooting

| Síntoma | Causa probable |
|---|---|
| La habitación no aparece en `MapData` tras guardar | `RoomId` vacío en la `RoomController`, o el `MapRoomShape` tiene menos de 3 puntos — revisar la consola, `MapBaker` deja un warning con el objeto señalado. |
| La puerta no aparece en `MapData` | `MapDoorMarker` sin `IDoorInteractable` en el mismo GameObject, o `DoorId` vacío en el interactable. |
| La puerta se hornea pero no se dibuja en runtime | Ninguna habitación *dibujada* la tiene linkeada — revisar el anidamiento en jerarquía (§4) o que `RoomDoorInteractable.Destination` apunte a la habitación correcta. |
| Cambié algo en el Map Editor Window pero el asset no se actualiza | El bake corre al *guardar la escena* o al *entrar a Play Mode* — usar "Bake Now" para forzarlo sin guardar. |
| El deck no aparece en el selector | Falta agregarlo al `MapDataSet`, o ninguna habitación está `Visitada` y el jugador no tiene el `MapItemId` en inventario (regla de conocimiento del deck, ver GDD). |
| Al buscar objetos por nombre con `GameObject.Find` en scripts de debug, aparece el objeto equivocado | Nombres de puertas se repiten entre habitaciones (ej. varias `DR_03`). Usar instancia/ruta completa o `GetComponentInParent<RoomController>()` desde el objeto correcto, nunca `Find` por nombre corto. |

---

## 7. Probar en Play Mode

- **Abrir el mapa**: acción de input `OpenMap` (ya definida en `IInputService`). Pausa la
  navegación (mismo tratamiento que el inventario: `Time.timeScale = 0`, `SwitchToUI()`).
- **Panear**: `UINavigate` (stick/WASD según binding).
- **Cambiar de deck**: `UIConfirm`, cicla entre los decks conocidos (`MapDataSet.Maps`
  filtrado por `MapStateResolver.IsDeckKnown`).
- **Cerrar**: `UIBack`, restaura `timeScale` y vuelve a `SwitchToGameplay()`.

La habitación donde está el jugador se resalta con un pulso de alpha (no hay ícono
posicional — ver GDD, fuera de alcance en esta iteración).

---

## 8. Referencia rápida de componentes

| Componente | Dónde va | Rol |
|---|---|---|
| `MapSceneConfig` | Un GameObject raíz por escena de deck | Bindea la escena a su `MapData` |
| `MapRoomShape` | En la `RoomController` de cada habitación | Silueta poligonal + posición en el plano |
| `MapDoorMarker` | En el GameObject de cada puerta interactable | Posición/tamaño de la marca de puerta en el plano |
| `MapData` (asset) | `Assets/Data/Map/` | Geometría y IDs horneados de un deck |
| `MapDataSet` (asset) | `Assets/Data/Map/` | Lista ordenada de todos los decks (selector) |
| `MapRenderer` | GameObject en escena (uno, con cámara ortográfica hija) | Genera mallas + RenderTexture en runtime |
| `MapScreenView` / `MapScreenController` | UI Canvas / DI | Pantalla de mapa a pantalla completa |

---

Volver a [[Sistema de Mapa]] · Ver plan técnico `docs/plans/2026-07-06-map-system-impl.md`
