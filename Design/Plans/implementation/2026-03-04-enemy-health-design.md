# Brainstorming — Enemy Health Feature

> [!WARNING]
> **DEPRECADO** — Este documento pertenece al sistema de salud anterior. No usar como referencia de implementación.

**Date:** 2026-03-04  
**Scope:** combat systems (`CombatMenuController`, `BattlefieldView`, enemy data/model)

## Goal

Agregar salud a enemigos para que los disparos del jugador tengan consecuencia mecánica real:

- aplicar daño al enemigo objetivo seleccionado,
- matar enemigo cuando su HP llegue a 0,
- reflejar muerte en battlefield (sprite/slot),
- terminar combate con victoria cuando no queden enemigos vivos.

---

## Current State (repo-grounded)

1. `AimViewController` ya emite `OnShotFired(Vector2 normalizedPos, ShotZone zone)`.
2. `CombatMenuController` recibe el evento pero hoy no aplica daño.
3. `BattlefieldView` hoy solo instancia sprites y mantiene slots ocupados.
4. `EnemyData` existe (sprite + id + perfil de hit mask), pero no tiene HP.
5. `CombatSessionController.EndCombat(bool victory)` existe y publica `CombatEndedEvent`.

Conclusión: la infraestructura base está, falta modelar HP enemigo y conectar el flujo de disparo a daño/muerte/victoria.

---

## Proposed MVP (recommended)

### A. Data model

- Extender `EnemyData` con:
  - `maxHp` (int, default 100)
  - multiplicadores por zona (`headMultiplier`, `torsoMultiplier`, `armsMultiplier`, `legsMultiplier`)
- Mantener daño base fijo inicial en código (ej. 20) para no bloquear por sistema de armas completo.

### B. Runtime enemy state

- Crear estado runtime por slot enemigo (`currentHp`, `isDead`, referencia a `EnemyData`).
- Fuente inicial: `EncounterData.EnemySlots`.
- El estado runtime no modifica ScriptableObjects.

### C. Damage pipeline

Cuando `CombatMenuController` recibe `OnShotFired(..., zone)`:

1. Identificar `targetSlot` actual.
2. Calcular daño:
   - `damage = baseDamage * zoneMultiplier`
   - `Miss` => 0
3. Restar HP al enemigo target.
4. Si HP <= 0:
   - marcar muerto,
   - ocultar/quitar sprite del slot,
   - recalcular lista de ocupados/seleccionables.
5. Si no quedan enemigos vivos:
   - `combatSessionController.EndCombat(victory: true)`.

### D. UI/feedback (MVP)

- Sin barra de HP visible (alineado con tono del proyecto).
- Debug log temporal:
  - slot objetivo,
  - zona (`ShotZone`),
  - daño aplicado,
  - HP restante.

### E. Out of scope (MVP)

- IA de enemigos atacando al party.
- Sistema completo de armas/munición/penetración.
- Hemorragia de enemigos.
- Animaciones complejas de hit reaction.

---

## Recommended Damage Defaults

- `baseDamage = 20`
- Multiplicadores:
  - `Head = 2.0`
  - `Torso = 1.0`
  - `Arms = 0.7`
  - `Legs = 0.8`
  - `Hit = 1.0` (compatibilidad)
  - `Miss = 0.0`

---

## Edge Cases to lock in GDD

1. Si el jugador dispara y justo ese enemigo murió en el mismo frame por otra causa (futuro): ignorar daño.
2. Si muere el target actual, el cursor debe saltar al siguiente enemigo vivo (si existe).
3. Si no quedan enemigos al abrir `AimView`, no debería abrirse (o se cierra inmediatamente con victoria).
4. `ShotZone.Hit` debe mapearse a torso/default para compatibilidad de assets viejos.

---

## Suggested Phase 2 Artifact

Crear/actualizar GDD en:

- `Sistema de Combate en Tiempo Real.md` (sección "Salud de enemigos"), o
- nuevo doc `Sistema de Salud de Enemigos.md` si prefieres separación estricta.

Recomendación: **nueva sección en `Sistema de Combate en Tiempo Real`** para evitar fragmentación.

---

## Open Decisions (need your approval)

1. ¿Usamos `baseDamage` fijo (20) en MVP o ya conectamos daño desde arma equipada?
2. ¿Muerte de enemigo = ocultar sprite directo o dejar "cadáver" visual en slot?
3. ¿Quieres logs debug siempre en esta iteración o sólo bajo `#if UNITY_EDITOR`?

