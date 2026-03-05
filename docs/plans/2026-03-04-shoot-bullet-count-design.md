# Brainstorming — Shoot Bullet Count + Multi-Bullet Resolve

**Date:** 2026-03-04  
**Scope:** Combat UI flow (`Shoot`), bullet count selector, multi-bullet QTE resolve, per-bullet hit/damage/feedback.

## Goal

Agregar un feature donde el jugador seleccione cuántas balas disparar al elegir `Shoot`, y que cada bala tenga resolución propia de impacto/daño/feedback tras el QTE.

---

## Decisions Confirmed

1. **Flow principal**
   - `Shoot` abre primero un **panel contador**.
   - Tras confirmar cantidad, continúa a **Target Selection**.
   - Luego entra a **QTE**.
   - Al resolver QTE, se ejecuta la resolución multi-bala.

2. **Rango del contador**
   - Mínimo: `1`
   - Máximo: `6`
   - Límite real: `min(6, balas_en_cargador)`

3. **Input del contador**
   - `Left/Right` para decrementar/incrementar.
   - `Confirm` para aceptar cantidad.
   - `Cancel` para volver atrás sin disparar.

4. **Valor inicial del contador**
   - Siempre inicia en `1`.

5. **Resolución multi-bala**
   - Bala 1: posición random dentro de `dispersionRadius` (como hoy).
   - Balas siguientes: offset vertical acumulado hacia arriba (`+5` por bala en Y).
   - Cada bala calcula:
     - su posición final,
     - su `ShotZone`,
     - su daño.
   - Cada bala muestra:
     - `ShotMarker`,
     - `feedback text`.

6. **Daño total**
   - El daño aplicado al enemigo es la **suma de daños de todas las balas**.

7. **Presentación visual**
   - Balas y textos aparecen en **secuencia corta**, incluyendo la primera.

---

## Recommended MVP Implementation Direction

- Mantener el sistema actual de estado de menú y sumar un nuevo estado intermedio para el contador de balas.
- Extender el contrato de `IAimView` para permitir resolución de múltiples disparos en secuencia (posición + zona + daño por bala).
- Reusar el sistema existente de `ShotMarker` y feedback text para cada bala, disparado por una rutina secuencial corta.

---

## Edge Cases to Lock in GDD

1. Si `balas_en_cargador == 0`, `Shoot` no debe avanzar a contador/QTE.
2. Si la cantidad seleccionada supera munición disponible por cambios de estado, clamplear al confirmar.
3. Si una bala cae fuera de silueta, su daño es `0` y feedback correspondiente.
4. Si el enemigo muere antes de procesar todas las balas de la secuencia, definir si se corta la secuencia o se completa visualmente sin daño adicional.

---

## Output of Phase 1

Este documento captura el resultado del brainstorming y define el comportamiento esperado para pasar a GDD formal.

