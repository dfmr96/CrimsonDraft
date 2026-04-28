# Design: Migración 2D → 3D — CrimsonDraft Navigation & Combat

**Fecha:** 2026-04-01
**Branch:** `feature/migration-2d-to-3d`
**Scope:** Escenas Navigation y Combat, Player prefab, scripts afectados

---

## Contexto

CrimsonDraft era un RPG táctico top-down 2D con física 2D (Rigidbody2D, Collider2D), cámara ortográfica con PixelPerfect (16 PPU, 320x180), SpriteRenderers y Tilemaps. La decisión es migrar a 3D completo manteniendo la perspectiva top-down. Por ahora se usan primitivas (cápsulas, planos) como placeholders hasta que lleguen los assets definitivos.

---

## Decisiones de diseño

- **Cámara:** Perspective top-down (no ortográfica). FOV: 60°. Cinemachine sigue al Player desde arriba con offset `(0, 15, 0)` y rotación `(90, 0, 0)`.
- **Movimiento:** Plano XZ (estándar 3D top-down). Input `(x, y)` → `Vector3(x, 0, y)`.
- **Física:** Rigidbody 3D con `useGravity: false`, rotación congelada en X/Z.
- **Placeholders:** Cápsula primitiva para el Player. Plano primitivo para el suelo.

---

## Sección 1 — Physics & Scripts

| Elemento | Antes (2D) | Después (3D) |
|---|---|---|
| `PlayerController.cs` | `Rigidbody2D`, `linearVelocity`, plano XY | `Rigidbody`, `velocity`, plano XZ |
| `CombatTrigger.cs` | `OnTriggerEnter2D(Collider2D)` | `OnTriggerEnter(Collider)` |
| `Player.prefab` | `Rigidbody2D` + `CapsuleCollider2D` + `SpriteRenderer` + `Animator` | `Rigidbody` + `CapsuleCollider` + `MeshRenderer` |
| `BattlefieldView.cs` | `SpriteRenderer` instanciado para enemies/ops | `GameObject.CreatePrimitive(Capsule)` con color |

---

## Sección 2 — Navigation Scene

| Elemento | Antes | Después |
|---|---|---|
| Main Camera | Orthographic, `PixelPerfectCamera`, Far Clip: 11 | Perspective, FOV: 60°, Far Clip: 100 |
| CinemachineCamera | `CinemachinePixelPerfect`, Z=-10 | Offset `(0, 15, 0)`, rotación `(90, 0, 0)`, sin PixelPerfect |
| Tilemap (Background) | `Tilemap` + `TilemapRenderer` | `Plane` primitivo + `MeshCollider` |
| EnemyEncounterTrigger | `Collider2D` trigger | `BoxCollider` trigger 3D |

---

## Sección 3 — Combat Scene

| Elemento | Antes | Después |
|---|---|---|
| Camera | Orthographic | Perspective, FOV: 60°, top-down |
| `BattlefieldView.cs` | `SpriteRenderer` por slot | `CreatePrimitive(Capsule)` — enemigos: rojo, operadores: azul |
| Sorting layers | `SortingLayer` "Combat" | Removidos, se usa posición Z o Layer 3D |
| Colliders | `Collider2D` | `Collider` 3D equivalente |

---

## Criterios de aceptación

1. El Player (cápsula) se mueve en los 4 sentidos sobre el plano XZ en Navigation.
2. La cámara perspective top-down sigue al Player via Cinemachine.
3. Caminar sobre el `EnemyEncounterTrigger` dispara la transición a Combat sin errores.
4. En Combat, enemigos y operadores se muestran como cápsulas con colores diferenciados.
5. Sin referencias 2D (`Rigidbody2D`, `Collider2D`, `OnTriggerEnter2D`) en scripts migrados.
6. Console limpia (sin NullReferenceException) en Play Mode.
