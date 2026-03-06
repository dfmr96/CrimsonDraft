# Sistema de Ataque de Enemigos Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implementar el scheduler de ataques enemigos con lock por duración, daño instantáneo a operadores y feedback visual (vibración, texto flotante y flash rojo en ECG), según `[[Sistema de Ataque de Enemigos]]`.

**Architecture:** Lógica de scheduling en clase pura testable (`EnemyAttackScheduler`) + controlador MonoBehaviour que integra Encounter, estado de enemigos vivos/muertos, salud de operadores y feedback en escena. El daño de operadores debe aplicarse sobre el runtime de salud existente (Sistema de Salud), no sobre un HP paralelo.

**Tech Stack:** Unity, VContainer, MessagePipe, DOTween, NUnit (EditMode tests).

**Spec:** Implements `[[Sistema de Ataque de Enemigos]]` (GDD).

**Branch workflow:** ejecutar en una rama Git del workspace actual (sin worktrees).

---

### Task 1: EnemyAttackScheduler Tests (Base + Reglas del GDD)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/EnemyAttackSchedulerTests.cs`

**Step 1: Write the failing tests**

Cubrir como mínimo:
- Bloqueo por `attack_lock_until` (nadie ataca durante lock).
- Selección del enemigo con menor `next_attack_time`.
- Selección aleatoria de operador vivo.
- Recomputo de `next_attack_time` con jitter.
- Desempate aleatorio cuando dos enemigos tienen mismo `next_attack_time`.
- Sin operadores vivos: no hay ataque.
- Enemigo marcado muerto (`MarkDead`) no vuelve a participar.

**Step 2: Run test to verify it fails**

Run: `"C:\Program Files\Unity\Hub\Editor\<VERSION>\Editor\Unity.exe" -runTests -projectPath "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\Game\CrimsonDraft" -testPlatform editmode -testResults "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\TestResults.xml"`
Expected: FAIL because scheduler API/behavior is not fully implemented.

**Step 3: Commit failing test**

```bash
git add Game/CrimsonDraft/Assets/Tests/EditMode/EnemyAttackSchedulerTests.cs
git commit -m "test: add enemy attack scheduler specs"
```

---

### Task 2: Implement EnemyAttackScheduler

**Files:**
- Create/Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/EnemyAttackScheduler.cs`

**Step 1: Implement minimal scheduler**

Requisitos de implementación:
- Estado por enemigo: `next_attack_time`, `is_dead`.
- Estado global: `attack_lock_until`.
- `Initialize`: programa `next_attack_time = now + base ± jitter`.
- `TryScheduleAttack`:
  - Si `now < attack_lock_until`, retorna false.
  - Si no hay operadores vivos, retorna false.
  - Toma enemigo vivo listo con menor `next_attack_time`.
  - Si hay empate de `next_attack_time`, desempata al azar.
  - Al atacar: devuelve daño/attacker/target y actualiza lock + next attack.
- `MarkDead(slot)`: excluye ese enemigo de futuras selecciones.

**Step 2: Run tests**

Run: `"C:\Program Files\Unity\Hub\Editor\<VERSION>\Editor\Unity.exe" -runTests -projectPath "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\Game\CrimsonDraft" -testPlatform editmode -testResults "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\TestResults.xml"`
Expected: PASS for `EnemyAttackSchedulerTests`.

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/EnemyAttackScheduler.cs
git commit -m "feat: implement enemy attack scheduler"
```

---

### Task 3: Health Integration Tests (Sistema de Salud)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatOperatorHealthBridgeTests.cs`

**Step 1: Write the failing tests**

Crear tests para un bridge/adaptador de salud de operadores que use el sistema runtime existente:
- Aplica daño directo y clamp a cero.
- Permite consultar slots vivos actuales.
- Refleja muerte de operador tras daño letal.
- No crea ni mantiene HP paralelo desacoplado.

**Step 2: Run test to verify it fails**

Run: `"C:\Program Files\Unity\Hub\Editor\<VERSION>\Editor\Unity.exe" -runTests -projectPath "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\Game\CrimsonDraft" -testPlatform editmode -testResults "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\TestResults.xml"`
Expected: FAIL because bridge/adapter does not exist yet.

**Step 3: Commit failing test**

```bash
git add Game/CrimsonDraft/Assets/Tests/EditMode/CombatOperatorHealthBridgeTests.cs
git commit -m "test: add operator health bridge tests"
```

---

### Task 4: Implement Health Bridge (No Parallel HP)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatOperatorHealthBridge.cs`

**Step 1: Implement bridge**

Implementar una clase que adapte el runtime de `[[Sistema de Salud]]` para este sistema:
- `GetAliveSlots()`
- `ApplyDamage(slotIndex, damage)`
- `IsAlive(slotIndex)` (si hace falta para control)

La clase debe depender de interfaces/servicios existentes del sistema de salud del proyecto y no duplicar estado de HP.

**Step 2: Run tests**

Run: `"C:\Program Files\Unity\Hub\Editor\<VERSION>\Editor\Unity.exe" -runTests -projectPath "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\Game\CrimsonDraft" -testPlatform editmode -testResults "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\TestResults.xml"`
Expected: PASS for `CombatOperatorHealthBridgeTests`.

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/CombatOperatorHealthBridge.cs
git commit -m "feat: integrate enemy attacks with health runtime"
```

---

### Task 5: Extend EnemyData with Attack Fields

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs`

**Step 1: Add serialized fields**

Agregar:
- `attackBaseSec`
- `attackJitterSec`
- `attackDurationSec`
- `attackDamage`

con validaciones mínimas (`Min`) y propiedades públicas de lectura.

**Step 2: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs
git commit -m "feat: add enemy attack tuning fields"
```

---

### Task 6: ECG Flash Support

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWaveGraphic.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWidget.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgPanel.cs`

**Step 1: Implement flash**

Agregar API para disparar flash rojo breve por daño (`0.15-0.20s`) y mapear widgets por slot de operador.

**Step 2: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWaveGraphic.cs Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWidget.cs Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgPanel.cs
git commit -m "feat: add ECG damage flash support"
```

---

### Task 7: Battlefield Feedback for Enemy Attacks

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Step 1: Extend feedback hooks**

Agregar:
- `PlayEnemyAttackFeedback(int enemySlotIndex)` (vibración de enemigo atacante).
- `ShowOperatorDamage(int operatorSlotIndex, int damage)` (texto flotante `-X`).

Actualizar fakes/tests afectados por la interfaz.

**Step 2: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat: add enemy attack feedback hooks"
```

---

### Task 8: EnemyAttackController + DI Wiring

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/EnemyAttackController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs`

**Step 1: Implement controller**

Requisitos:
- Inicializar `EnemyAttackScheduler` desde enemigos del encounter.
- Obtener operadores vivos desde `CombatOperatorHealthBridge`.
- Aplicar daño usando bridge de salud.
- Disparar feedback de battlefield + ECG.
- Escuchar/sincronizar muerte de enemigos (eventos/señales existentes en combate) para llamar `scheduler.MarkDead(slot)`.
- Si no hay operadores vivos, scheduler se detiene en práctica (no emite ataques).

**Step 2: Register in CombatScope**

Registrar `EnemyAttackController`, `CombatOperatorHealthBridge` y `OperatorEcgPanel` en DI según convenciones del proyecto.

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/EnemyAttackController.cs Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs Game/CrimsonDraft/Assets/Scripts/Combat/CombatOperatorHealthBridge.cs
git commit -m "feat: wire enemy attack controller"
```

---

### Task 9: Final Verification

**Step 1: Run EditMode tests**

Run: `"C:\Program Files\Unity\Hub\Editor\<VERSION>\Editor\Unity.exe" -runTests -projectPath "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\Game\CrimsonDraft" -testPlatform editmode -testResults "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\TestResults.xml"`
Expected: PASS (all EditMode tests).

**Step 2: Smoke check in play mode**

Validar en escena de combate:
- Se respeta lock (no ataques simultáneos).
- En empate, atacantes varían (desempate aleatorio).
- Enemigos muertos no vuelven a atacar.
- Operadores muertos dejan de ser objetivos.
- Se muestran vibración, daño flotante y flash ECG.

**Step 3: Commit final fixes (if any)**

```bash
git add -A
git commit -m "test: verify enemy attack system"
```
