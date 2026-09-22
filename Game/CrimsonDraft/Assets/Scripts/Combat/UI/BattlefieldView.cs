#nullable enable

using System;
using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using VContainer;
using CrimsonDraft.Operators;
using CrimsonDraft.Audio;

namespace CrimsonDraft.Combat
{
    public sealed class BattlefieldView : MonoBehaviour, IBattlefieldView
    {
        private sealed class EnemyRuntimeState
        {
            public int CurrentHp;
            public int MaxHp;
            public bool IsDead;
            public int CurrentPoise;
            public int InitialPoise; // the roll this enemy resets to on a silent Poise reset
            public bool IsStaggered;
            public int StaggerActionsRemaining;
            public bool RecoveryQueued; // true once its EnemyRecover action has been enqueued
            // Set when a hit lands while the enemy is down (TriggerEnemyFlinch skips the
            // reaction animation while IsStaggered); consumed by RecoverEnemyStagger's
            // wait-for-StaggerUp coroutine to play the deferred Flinch once it's back up.
            public bool PendingFlinchAfterRecovery;
            public float PendingFlinchStaggerPct;
        }

        [SerializeField] private Transform[] enemySlotTransforms  = Array.Empty<Transform>();
        [SerializeField] private Transform[] playerSlotTransforms = Array.Empty<Transform>();
        [SerializeField] private GameObject  operatorIndicator    = null!;
        [SerializeField] private GameObject  enemyTargetIndicator = null!;
        [SerializeField, Min(0.1f)] private float enemyDeathAnimTimeoutSec = 3f;
        [SerializeField, Min(0f)] private float bloodPoolRevealDurationSec = 0.5f;
        [SerializeField] private Canvas? operatorDamageCanvas;
        [SerializeField] private GameObject? operatorDamageTextPrefab;
        [SerializeField] private Vector3 enemyTargetIndicatorOffset = new(0f, 0f, 0f);
        [SerializeField] private Vector3 operatorDamageOffset = new(0f, 0.9f, 0f);
        [SerializeField, Min(0.01f)] private float operatorDamageDuration = 0.6f;
        [SerializeField] private GameObject? bloodHitFxPrefab;

        private readonly List<GameObject> spawnedSprites = new();
        private readonly Dictionary<int, EnemyRuntimeState> enemyStateBySlot = new();
        private readonly Dictionary<int, GameObject> enemyGoBySlot = new();
        private readonly Dictionary<int, MeshRenderer> enemyRendererBySlot = new();
        private int[] occupiedEnemySlots = Array.Empty<int>();
        private EnemyData?[] currentEnemySlots = Array.Empty<EnemyData?>();
        private readonly Dictionary<int, Animator> operatorAnimatorBySlot = new();
        private readonly Dictionary<int, OperatorCombatWeaponPose> operatorWeaponPoseBySlot = new();

        private static readonly int ShootHash = Animator.StringToHash("Shoot");
        private static readonly int AimHash = Animator.StringToHash("Aim");
        private static readonly int FlinchHash = Animator.StringToHash("Flinch");
        private static readonly int OperatorDeathHash = Animator.StringToHash("Death");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");

        private static readonly int KnifeAttackHash = Animator.StringToHash("KnifeAttack");

        private readonly Dictionary<int, Animator> enemyAnimatorBySlot = new();
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int IsStaggeredHash = Animator.StringToHash("IsStaggered");
        // Enemy_Combat_Controller v2: a single Flinch trigger replaces v1's alternating
        // Hit1/Hit2, routed to Flinch01/02/03 by the Stagger float (100=full poise, 0=broken).
        private static readonly int EnemyFlinchHash = Animator.StringToHash("Flinch");
        private static readonly int EnemyStaggerFloatHash = Animator.StringToHash("Stagger");
        private static readonly int EnemyStaggerRecoverHash = Animator.StringToHash("StaggerRecover");
        private static readonly int EnemyDeathHash = Animator.StringToHash("Death");
        private readonly Dictionary<int, EnemyDeathMarker> enemyDeathMarkerBySlot = new();
        private readonly Dictionary<int, float> enemyAttackResolvedDurationBySlot = new();
        private readonly Dictionary<int, EnemyAttackEventRelay> enemyAttackEventRelayBySlot = new();
        private readonly Dictionary<int, OperatorHitFxMarker> operatorHitFxMarkerBySlot = new();
        private readonly HashSet<int> settledDeadOperatorSlots = new();
        private readonly IRandomSource poiseRandom = new UnityRandomSource();
        private readonly IRandomSource enemyStatRandom = new UnityRandomSource();
        private const int DefaultMaxHp = 100;

        private IOperatorRoster? roster;

        [Inject]
        public void Construct(IOperatorRoster roster)
        {
            this.roster = roster;
        }

        private void Awake()
        {
            this.operatorIndicator.SetActive(false);
            this.enemyTargetIndicator.SetActive(false);
        }

public void Populate(EncounterData encounter)
        {
            foreach (var go in this.spawnedSprites)
                Destroy(go);
            this.spawnedSprites.Clear();
            this.enemyStateBySlot.Clear();
            this.enemyGoBySlot.Clear();
            this.enemyRendererBySlot.Clear();
            this.operatorAnimatorBySlot.Clear();
            this.operatorWeaponPoseBySlot.Clear();
            this.enemyAnimatorBySlot.Clear();
            this.enemyDeathMarkerBySlot.Clear();
            this.operatorHitFxMarkerBySlot.Clear();
            this.settledDeadOperatorSlots.Clear();
            this.enemyAttackResolvedDurationBySlot.Clear();
            this.enemyAttackEventRelayBySlot.Clear();
            this.currentEnemySlots = encounter.EnemySlots;

            var occupied = new List<int>();
            for (int i = 0; i < encounter.EnemySlots.Length && i < this.enemySlotTransforms.Length; i++)
            {
                var enemy = encounter.EnemySlots[i];
                if (enemy == null) continue;

                occupied.Add(i);
                GameObject go;
                if (enemy.BattlefieldPrefab != null)
                {
                    go = Instantiate(enemy.BattlefieldPrefab, this.enemySlotTransforms[i], false);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.transform.SetParent(this.enemySlotTransforms[i], false);
                    go.GetComponent<MeshRenderer>().material.color = Color.red;
                }
                go.name = $"Enemy_{i}";
                var mr = go.GetComponentInChildren<MeshRenderer>();
                this.spawnedSprites.Add(go);
                this.enemyGoBySlot[i] = go;
                if (mr != null) this.enemyRendererBySlot[i] = mr;
                var enemyAnimator = go.GetComponentInChildren<Animator>();
                if (enemyAnimator != null) this.enemyAnimatorBySlot[i] = enemyAnimator;
                var deathMarker = go.GetComponentInChildren<EnemyDeathMarker>();
                if (deathMarker != null) this.enemyDeathMarkerBySlot[i] = deathMarker;
                var attackEventRelay = go.GetComponentInChildren<EnemyAttackEventRelay>();
                if (attackEventRelay != null) this.enemyAttackEventRelayBySlot[i] = attackEventRelay;
                int rolledPoise = this.poiseRandom.NextInt(enemy.MinPoise, enemy.MaxPoise + 1);
                int rolledMaxHp = RollMaxHp(enemy);
                this.enemyStateBySlot[i] = new EnemyRuntimeState
                {
                    CurrentHp               = Mathf.Max(1, rolledMaxHp),
                    MaxHp                   = Mathf.Max(1, rolledMaxHp),
                    IsDead                  = false,
                    CurrentPoise            = rolledPoise,
                    InitialPoise            = rolledPoise,
                    IsStaggered             = false,
                    StaggerActionsRemaining = 0,
                    RecoveryQueued          = false,
                    PendingFlinchAfterRecovery = false,
                    PendingFlinchStaggerPct = 0f
                };
            }
            this.occupiedEnemySlots = occupied.ToArray();

            for (int i = 0; i < encounter.Operators.Length && i < this.playerSlotTransforms.Length; i++)
            {
                var op = encounter.Operators[i];
                if (op == null) continue;
                // A dead operator has no body on the battlefield — MarkDead already keeps them
                // out of turns/targeting, this keeps their empty slot visually empty too. They
                // also never get a PlayOperatorDeath call during this encounter (they were
                // already dead before it started, e.g. loaded from a save), so mark the slot
                // settled immediately or SyncOperatorWipe would wait forever for an animation
                // that will never play.
                if (this.roster != null && i < this.roster.Count && !this.roster[i].IsAlive)
                {
                    this.settledDeadOperatorSlots.Add(i);
                    continue;
                }

                GameObject go;
                if (op.BattlefieldPrefab != null)
                {
                    go = Instantiate(op.BattlefieldPrefab, this.playerSlotTransforms[i], false);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.transform.SetParent(this.playerSlotTransforms[i], false);
                    go.GetComponent<MeshRenderer>().material.color = Color.blue;
                }
                go.name = $"Operator_{i}";
                this.spawnedSprites.Add(go);

                var operatorAnimator = go.GetComponentInChildren<Animator>();
                if (operatorAnimator != null)
                    this.operatorAnimatorBySlot[i] = operatorAnimator;

                // Drives the shotgun idle/aim pose blend and silences the Health Overlay layer
                // while aiming (same trick as Navigation's PlayerAimController), and sets the
                // Animator's GunType from whatever weapon this operator currently has equipped.
                var weaponPose = go.GetComponentInChildren<OperatorCombatWeaponPose>();
                if (weaponPose != null)
                {
                    this.operatorWeaponPoseBySlot[i] = weaponPose;
                    var activeWeapon = this.roster != null && i < this.roster.Count ? this.roster[i].ActiveWeapon : null;
                    weaponPose.SetGunType(activeWeapon?.GunType ?? GunType.Pistols);
                }

                var hitFxMarker = go.GetComponentInChildren<OperatorHitFxMarker>();
                if (hitFxMarker != null) this.operatorHitFxMarkerBySlot[i] = hitFxMarker;

                var operatorAudio = go.GetComponentInChildren<OperatorCombatAudio>();
                if (operatorAudio != null && this.roster != null)
                    operatorAudio.Bind(this.roster, i);
            }
        }

        private int RollMaxHp(EnemyData enemy)
        {
            int[] pool = enemy.MaxHpPool;
            if (pool == null || pool.Length == 0)
            {
                Debug.LogWarning($"[BattlefieldView] {enemy.name} has no MaxHpPool configured; using default {DefaultMaxHp}.", enemy);
                return DefaultMaxHp;
            }

            return pool[this.enemyStatRandom.NextInt(0, pool.Length)];
        }

        public int[] GetOccupiedEnemySlots() => this.occupiedEnemySlots;

        public AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.currentEnemySlots.Length)
                return null;

            EnemyData? enemy = this.currentEnemySlots[slotIndex];
            if (enemy == null) return null;

            if (IsEnemyStaggered(slotIndex) && enemy.StaggeredHitMaskProfile != null)
                return enemy.StaggeredHitMaskProfile;

            return enemy.HitMaskProfile;
        }

        public EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage, int decapitationPellets)
        {
            if (!this.enemyStateBySlot.TryGetValue(slotIndex, out var state))
                return new EnemyDamageResult(slotIndex, 0, 0, false, false);

            if (state.IsDead)
                return new EnemyDamageResult(slotIndex, 0, 0, true, false);

            EnemyData? enemyData = slotIndex >= 0 && slotIndex < this.currentEnemySlots.Length
                ? this.currentEnemySlots[slotIndex]
                : null;

            int appliedDamage = Mathf.Max(0, hpDamage);
            state.CurrentHp = Mathf.Max(0, state.CurrentHp - appliedDamage);

            // Decapitation is a guaranteed kill independent of remaining HP -- enough
            // pellets/points to the head in one action ends the fight regardless of how
            // tanky the enemy still is.
            bool isDecapitated = enemyData != null
                && CombatMenuController.ShouldDecapitate(decapitationPellets, enemyData.DecapitationPelletThreshold);
            bool isDead = state.CurrentHp <= 0 || isDecapitated;
            if (isDead)
            {
                // Only the HP/flag state is set here — the fade-out and removal from
                // occupiedEnemySlots (which is what SyncDeadEnemies/CombatEndedEvent key
                // off) are deferred to FinalizeEnemyDeath(), called once the operator's
                // shoot animation finishes playing, so combat can never end mid-burst.
                state.IsDead = true;
                return new EnemyDamageResult(slotIndex, appliedDamage, 0, true, false, isDecapitated);
            }

            bool willStagger = false;
            // Poise doesn't drain further while the enemy is already down — it only
            // matters again once it recovers (see RecoverEnemyStagger).
            if (!state.IsStaggered)
            {
                state.CurrentPoise -= Mathf.Max(0, poiseDamage);

                if (enemyData != null && state.CurrentPoise <= 0)
                {
                    // Only the decision is made here — the actual knockdown (state flag,
                    // timer, animation, ATB reset) is deferred to TriggerEnemyStagger(),
                    // called once the operator's shoot animation finishes playing, so the
                    // enemy never visually collapses mid-burst.
                    willStagger = CombatMenuController.ShouldStagger(
                        state.CurrentPoise, state.CurrentHp, state.MaxHp, enemyData.StaggerHpThresholdPct);
                    if (!willStagger)
                        state.CurrentPoise = state.InitialPoise; // silent reset — enemy too healthy to stagger yet
                }
            }

            return new EnemyDamageResult(slotIndex, appliedDamage, state.CurrentHp, false, willStagger);
        }

        public void TriggerEnemyStagger(int slotIndex)
        {
            if (!this.enemyStateBySlot.TryGetValue(slotIndex, out var state) || state.IsDead) return;

            EnemyData? enemyData = slotIndex >= 0 && slotIndex < this.currentEnemySlots.Length
                ? this.currentEnemySlots[slotIndex]
                : null;
            if (enemyData == null) return;

            state.IsStaggered             = true;
            state.StaggerActionsRemaining = Mathf.Max(0, enemyData.StaggerRecoveryActionCount);
            state.RecoveryQueued          = false;
            if (this.enemyAnimatorBySlot.TryGetValue(slotIndex, out var anim) && anim != null)
            {
                anim.SetBool(IsStaggeredHash, true);
                // Fires the AnyState -> StaggerFlinchFall transition now that IsStaggered is
                // true, regardless of whatever Flinch01/02/03/Idle state the last hit left it in.
                anim.SetTrigger(EnemyFlinchHash);
            }
        }

        public void RecoverEnemyStagger(int slotIndex)
        {
            if (!this.enemyStateBySlot.TryGetValue(slotIndex, out var state)) return;

            state.IsStaggered    = false;
            state.CurrentPoise   = state.InitialPoise; // fresh Poise for the next round of combat
            state.RecoveryQueued = false;
            if (this.enemyAnimatorBySlot.TryGetValue(slotIndex, out var anim) && anim != null)
            {
                anim.SetBool(IsStaggeredHash, false);
                anim.SetTrigger(EnemyStaggerRecoverHash); // StaggerFlinchFall -> StaggerUp -> Idle

                if (state.PendingFlinchAfterRecovery)
                    StartCoroutine(this.PlayPendingFlinchAfterStaggerUp(slotIndex, anim, state.PendingFlinchStaggerPct));
                state.PendingFlinchAfterRecovery = false;
            }
        }

        // A hit that landed while the enemy was down (see TriggerEnemyFlinch) doesn't get
        // lost -- it waits here for StaggerUp to actually finish playing and land back on
        // Idle before playing the deferred Flinch reaction, so the enemy never reacts to
        // damage mid-getup, only once it's genuinely back on its feet.
        private IEnumerator PlayPendingFlinchAfterStaggerUp(int slotIndex, Animator anim, float staggerPct)
        {
            float giveUpAt = Time.time + this.enemyDeathAnimTimeoutSec;
            while (!anim.GetCurrentAnimatorStateInfo(0).IsName("Idle") && Time.time < giveUpAt)
                yield return null;

            // Don't stack a stray Flinch on top of whatever happened while we were waiting
            // (e.g. it died, or got staggered again from another hit in the meantime).
            if (!this.enemyStateBySlot.TryGetValue(slotIndex, out var state) || state.IsDead || state.IsStaggered)
                yield break;

            anim.SetFloat(EnemyStaggerFloatHash, staggerPct);
            anim.SetTrigger(EnemyFlinchHash);
        }

        private readonly List<int> readyToRecoverSlotsBuf = new();

        public int[] NotifyActionDequeued()
        {
            this.readyToRecoverSlotsBuf.Clear();
            foreach (var kvp in this.enemyStateBySlot)
            {
                var state = kvp.Value;
                if (!state.IsStaggered || state.RecoveryQueued) continue;

                state.StaggerActionsRemaining--;
                if (state.StaggerActionsRemaining > 0) continue;

                state.RecoveryQueued = true;
                this.readyToRecoverSlotsBuf.Add(kvp.Key);
            }
            return this.readyToRecoverSlotsBuf.ToArray();
        }

        public bool IsEnemyStaggered(int slotIndex) =>
            this.enemyStateBySlot.TryGetValue(slotIndex, out var state) && state.IsStaggered;

        // Reflects EnemyRuntimeState.IsDead the instant HP crosses to 0 — unlike
        // occupiedEnemySlots, which only drops the slot once FinalizeEnemyDeath's fall +
        // blood-pool sequence finishes. CombatOrchestrator needs this immediate signal so a
        // dying-but-still-mid-animation enemy can never queue or execute an attack.
        public bool IsEnemyDead(int slotIndex) =>
            this.enemyStateBySlot.TryGetValue(slotIndex, out var state) && state.IsDead;

        public bool HasAliveEnemies() => this.occupiedEnemySlots.Length > 0;

        public void FinalizeEnemyDeath(int slotIndex)
        {
            if (!this.enemyStateBySlot.TryGetValue(slotIndex, out var state)) return;
            StartCoroutine(this.PlayDeathSequenceThenFinalize(slotIndex));
        }

        // RE-style "definitely dead" marker: a blood pool (a child object referenced via
        // EnemyDeathMarker) reveals under the corpse so the player can tell it won't get
        // back up. Enemy_Combat_Controller v2 has a dedicated Death state/clip (distinct
        // from the Stagger knockdown), so it always plays here regardless of whether the
        // enemy was already down -- the AnyState -> Death transition cuts in immediately
        // either way. The corpse is never hidden or destroyed -- once removed from
        // occupiedEnemySlots it's untargetable and dead to the ATB system (SyncDeadEnemies
        // picks it up from there), but stays visible in the scene as an inert prop.
        private IEnumerator PlayDeathSequenceThenFinalize(int slotIndex)
        {
            if (this.enemyAnimatorBySlot.TryGetValue(slotIndex, out var anim) && anim != null)
            {
                anim.SetTrigger(EnemyDeathHash);
                yield return this.WaitForAnimatorStateChange(anim);
            }

            if (this.enemyDeathMarkerBySlot.TryGetValue(slotIndex, out var marker) && marker != null && marker.BloodPool != null)
            {
                marker.BloodPool.SetActive(true);
                yield return new WaitForSeconds(this.bloodPoolRevealDurationSec);
            }

            RemoveFromOccupiedSlots(slotIndex);
        }

        // Doesn't assume a specific destination state name, just waits for whatever new
        // state IsStaggered leads to and reads its real clip length. Safety-timeouts out
        // rather than hanging combat forever if it isn't wired to a reachable transition.
        private IEnumerator WaitForAnimatorStateChange(Animator anim)
        {
            int startStateHash = anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
            float giveUpAt = Time.time + this.enemyDeathAnimTimeoutSec;
            while (anim.GetCurrentAnimatorStateInfo(0).fullPathHash == startStateHash && Time.time < giveUpAt)
                yield return null;

            if (anim.GetCurrentAnimatorStateInfo(0).fullPathHash == startStateHash)
                yield break;

            var clipInfo = anim.GetCurrentAnimatorClipInfo(0);
            float duration = clipInfo.Length > 0 ? clipInfo[0].clip.length : 0f;
            if (duration > 0f)
                yield return new WaitForSeconds(duration);
        }

        private void RemoveFromOccupiedSlots(int slotIndex)
        {
            var nextOccupied = new List<int>(this.occupiedEnemySlots.Length);
            foreach (int slot in this.occupiedEnemySlots)
            {
                if (slot != slotIndex)
                    nextOccupied.Add(slot);
            }
            this.occupiedEnemySlots = nextOccupied.ToArray();
        }

public async UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots, bool isMelee = false)
        {
            if (!this.operatorAnimatorBySlot.TryGetValue(operatorSlotIndex, out var animator) || animator == null)
                return;

            this.operatorWeaponPoseBySlot.TryGetValue(operatorSlotIndex, out var weaponPose);

            if (isMelee)
            {
                // A melee swing is always a single strike (SlashStrategy resolves every slash
                // point under BulletIndex 0), so it plays once - no Aim gating needed either,
                // since "KnifeAttack" is an AnyState transition on the v2 controller. The operator
                // is fighting with the knife, not whatever gun is equipped, so hide that gun's
                // model for the duration of the swing (EnterMelee/ExitMelee), same idea as
                // EnterAim/ExitAim below for shooting.
                if (weaponPose != null)
                {
                    weaponPose.EnterMelee();
                    weaponPose.TriggerKnifeAttack();
                }
                else
                    animator.SetTrigger(KnifeAttackHash);

                bool anyHit = false;
                foreach (var shot in shots)
                {
                    if (shot.Zone != ShotZone.Miss)
                    {
                        anyHit = true;
                        break;
                    }
                }
                if (anyHit)
                    this.TriggerEnemyFlinch(enemySlotIndex);

                while (!animator.GetCurrentAnimatorStateInfo(0).IsName("KnifeAttack"))
                    await UniTask.NextFrame();

                var knifeClipInfo = animator.GetCurrentAnimatorClipInfo(0);
                float knifeDuration = knifeClipInfo.Length > 0 ? knifeClipInfo[0].clip.length : 0f;
                if (knifeDuration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(knifeDuration));

                weaponPose?.ExitMelee();

                return;
            }

            bool isShotgun = this.roster != null && operatorSlotIndex < this.roster.Count &&
                this.roster[operatorSlotIndex].ActiveWeapon?.GunType is GunType.Shotgun or GunType.REShotgun;
            string aimIdleState = isShotgun ? "ShotgunAimIdle" : "PistolAimIdle";
            string shootState = isShotgun ? "ShotgunShoot" : "PistolShoot";

            // The "Shoot" trigger only has an outgoing transition defined from the weapon-appropriate
            // AimIdle state (Operator_Combat_Controller v2), so Aim must be entered - via
            // OperatorCombatWeaponPose.EnterAim when available, so the shotgun pose blend and Health
            // Overlay silencing kick in exactly like Navigation's PlayerAimController - and the
            // transition into that AimIdle state must actually complete before triggering Shoot has
            // any effect.
            if (weaponPose != null)
                weaponPose.EnterAim();
            else
                animator.SetBool(AimHash, true);
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName(aimIdleState))
                await UniTask.NextFrame();

            // One animation trigger per bullet fired, not per pellet - a shotgun shell that
            // resolves into several ResolvedShot entries (same BulletIndex) still plays the
            // shoot animation once.
            int bulletCount = AimViewController.CountBullets(shots);
            for (int b = 0; b < bulletCount; b++)
            {
                if (weaponPose != null)
                    weaponPose.TriggerShoot();
                else
                    animator.SetTrigger(ShootHash);

                bool anyHit = false;
                foreach (var shot in shots)
                {
                    if (shot.BulletIndex == b && shot.Zone != ShotZone.Miss)
                    {
                        anyHit = true;
                        break;
                    }
                }
                if (anyHit)
                    this.TriggerEnemyFlinch(enemySlotIndex);

                while (!animator.GetCurrentAnimatorStateInfo(0).IsName(shootState))
                    await UniTask.NextFrame();

                var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
                float duration = clipInfo.Length > 0 ? clipInfo[0].clip.length : 0f;
                if (duration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(duration));
            }

            if (weaponPose != null)
                weaponPose.ExitAim();
            else
                animator.SetBool(AimHash, false);
        }

        private void TriggerEnemyFlinch(int enemySlotIndex)
        {
            if (enemySlotIndex < 0) return;

            this.SpawnBloodHitFx(enemySlotIndex);

            if (!this.enemyAnimatorBySlot.TryGetValue(enemySlotIndex, out var animator) || animator == null) return;
            if (!this.enemyStateBySlot.TryGetValue(enemySlotIndex, out var state)) return;

            float? staggerPct = state.InitialPoise > 0
                ? Mathf.Clamp(100f * state.CurrentPoise / state.InitialPoise, 0f, 100f)
                : (float?)null;

            // While the enemy is down from a stagger it just stays on the ground -- no flinch
            // reaction plays until StaggerUp actually finishes and it's back on its feet. The
            // hit isn't lost: it's remembered (with the Poise/Stagger it had when it landed,
            // frozen while staggered) and played the instant recovery completes -- see
            // RecoverEnemyStagger / PlayPendingFlinchAfterStaggerUp.
            if (state.IsStaggered)
            {
                state.PendingFlinchAfterRecovery = true;
                if (staggerPct.HasValue)
                    state.PendingFlinchStaggerPct = staggerPct.Value;
                return;
            }

            // Reports how much Poise is left (100 = full, 0 = broken) so Enemy_Combat_Controller
            // v2 can route the Flinch trigger to Flinch01/02/03 by itself -- Flinch01 while
            // still mostly full, down to Flinch03 right before it collapses into
            // StaggerFlinchFall (TriggerEnemyStagger fires that transition separately once the
            // burst finishes and the stagger is confirmed).
            if (staggerPct.HasValue)
                animator.SetFloat(EnemyStaggerFloatHash, staggerPct.Value);

            animator.SetTrigger(EnemyFlinchHash);
        }

        private void SpawnBloodHitFx(int enemySlotIndex)
        {
            if (this.bloodHitFxPrefab == null) return;
            if (!this.enemyGoBySlot.TryGetValue(enemySlotIndex, out var enemyGo) || enemyGo == null) return;

            Transform? hitFxPoint = this.enemyDeathMarkerBySlot.TryGetValue(enemySlotIndex, out var marker) && marker != null
                ? marker.HitFxPoint
                : null;
            Vector3 spawnPos = hitFxPoint != null ? hitFxPoint.position : enemyGo.transform.position;

            Instantiate(this.bloodHitFxPrefab, spawnPos, this.bloodHitFxPrefab.transform.rotation);
        }

#if UNITY_EDITOR || DEBUG_COMBAT
        public (int Current, int Max, bool IsDead, int Poise, bool IsStaggered) GetEnemyHpDebug(int slotIndex)
        {
            if (this.enemyStateBySlot.TryGetValue(slotIndex, out var state))
                return (state.CurrentHp, state.MaxHp, state.IsDead, state.CurrentPoise, state.IsStaggered);
            return (0, 0, true, 0, false);
        }
#endif

        public void SetOperatorIndicator(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.playerSlotTransforms.Length) return;
            this.operatorIndicator.SetActive(true);
            this.operatorIndicator.transform.position = this.playerSlotTransforms[slotIndex].position;
            var mr = this.operatorIndicator.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = Color.white;
        }

        public void DimOperatorIndicator()
        {
            var mr = this.operatorIndicator.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(0.4f, 0.4f, 0.4f, 1f);
        }

        public void PlayEnemyAttackFeedback(int enemySlotIndex, Action onAttackImpact)
        {
            this.enemyAttackResolvedDurationBySlot.Remove(enemySlotIndex);

            // The impact callback is what plays the operator's hit reaction -- and, crucially,
            // PlayOperatorDeath, which is the only thing that marks a dead operator's slot as
            // settled for CombatOrchestrator.SyncOperatorWipe. If it never runs, a wiped party
            // never triggers defeat and combat hard-locks. So it must fire exactly once per
            // attack, whether it comes from the Attack clip's Animation Event (normal path) or
            // from the fallback below (clip missing the event, no relay, no Animator, ...).
            bool impactDelivered = false;
            void DeliverImpactOnce()
            {
                if (impactDelivered) return;
                impactDelivered = true;
                onAttackImpact();
            }

            if (this.enemyAttackEventRelayBySlot.TryGetValue(enemySlotIndex, out var relay) && relay != null)
                relay.Bind(DeliverImpactOnce);

            if (!this.enemyAnimatorBySlot.TryGetValue(enemySlotIndex, out var animator) || animator == null)
            {
                DeliverImpactOnce();
                return;
            }

            animator.SetTrigger(AttackHash);
            StartCoroutine(this.ResolveEnemyAttackDuration(enemySlotIndex, animator));
            StartCoroutine(this.EnsureEnemyAttackImpact(enemySlotIndex, DeliverImpactOnce));
        }

        // Safety net for PlayEnemyAttackFeedback: waits for the full Attack clip (the duration
        // ResolveEnemyAttackDuration reports, same one CombatOrchestrator uses for its animation
        // lock) and delivers the impact if the Animation Event hasn't by then. A no-op in the
        // normal case, since DeliverImpactOnce ignores a second call.
        private IEnumerator EnsureEnemyAttackImpact(int enemySlotIndex, Action deliverImpactOnce)
        {
            float startedAt = Time.time;
            float giveUpAt  = startedAt + this.enemyDeathAnimTimeoutSec;

            float duration = 0f;
            while (Time.time < giveUpAt && !this.enemyAttackResolvedDurationBySlot.TryGetValue(enemySlotIndex, out duration))
                yield return null;

            float fireAt = startedAt + duration;
            while (Time.time < fireAt)
                yield return null;

            deliverImpactOnce();
        }

        public bool TryGetResolvedEnemyAttackDuration(int enemySlotIndex, out float durationSec) =>
            this.enemyAttackResolvedDurationBySlot.TryGetValue(enemySlotIndex, out durationSec);

        // Mirrors WaitForAnimatorStateChange's state-change + real-clip-length detection,
        // but reports the resolved duration back into a dictionary instead of yielding a
        // delay itself — CombatOrchestrator polls it to correct its own animation-lock
        // timestamp once the real Attack clip length is known. Times out silently (leaves
        // nothing in the dictionary) if the transition never fires, so the orchestrator's
        // own provisional lock duration is left standing instead.
        private IEnumerator ResolveEnemyAttackDuration(int enemySlotIndex, Animator anim)
        {
            int startStateHash = anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
            float giveUpAt = Time.time + this.enemyDeathAnimTimeoutSec;
            while (anim.GetCurrentAnimatorStateInfo(0).fullPathHash == startStateHash && Time.time < giveUpAt)
                yield return null;

            if (anim.GetCurrentAnimatorStateInfo(0).fullPathHash == startStateHash)
                yield break;

            var clipInfo = anim.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length > 0)
                this.enemyAttackResolvedDurationBySlot[enemySlotIndex] = clipInfo[0].clip.length;
        }

        public void PlayOperatorHitFx(int operatorSlotIndex)
        {
            if (this.bloodHitFxPrefab == null) return;
            if (operatorSlotIndex < 0 || operatorSlotIndex >= this.playerSlotTransforms.Length) return;

            Transform? hitFxPoint = this.operatorHitFxMarkerBySlot.TryGetValue(operatorSlotIndex, out var marker) && marker != null
                ? marker.HitFxPoint
                : null;
            Vector3 spawnPos = hitFxPoint != null ? hitFxPoint.position : this.playerSlotTransforms[operatorSlotIndex].position;

            Instantiate(this.bloodHitFxPrefab, spawnPos, this.bloodHitFxPrefab.transform.rotation);
        }

        public void PlayOperatorFlinch(int operatorSlotIndex)
        {
            if (!this.operatorAnimatorBySlot.TryGetValue(operatorSlotIndex, out var animator) || animator == null)
                return;

            animator.SetTrigger(FlinchHash);
        }

public void PlayOperatorReload(int operatorSlotIndex)
        {
            if (!this.operatorAnimatorBySlot.TryGetValue(operatorSlotIndex, out var animator) || animator == null)
                return;

            animator.SetTrigger(ReloadHash);
        }


        public void PlayOperatorDeath(int operatorSlotIndex)
        {
            if (!this.operatorAnimatorBySlot.TryGetValue(operatorSlotIndex, out var animator) || animator == null)
                return;

            animator.SetTrigger(OperatorDeathHash);
            StartCoroutine(this.PlayOperatorDeathSequence(operatorSlotIndex, animator));
        }

        // Reuses the same generic state-change wait as the enemy death sequence, then
        // reveals the pre-placed blood-pool decal exactly like EnemyDeathMarker.BloodPool.
        // Only once this settles does the slot count toward CombatOrchestrator's operator-wipe
        // check, so a defeat can never be declared mid-death-animation.
        private IEnumerator PlayOperatorDeathSequence(int operatorSlotIndex, Animator anim)
        {
            yield return this.WaitForAnimatorStateChange(anim);

            if (this.operatorHitFxMarkerBySlot.TryGetValue(operatorSlotIndex, out var marker)
                && marker != null && marker.BloodPool != null)
            {
                marker.BloodPool.SetActive(true);
            }

            this.settledDeadOperatorSlots.Add(operatorSlotIndex);
        }

        public bool HasOperatorDeathSettled(int operatorSlotIndex) =>
            this.settledDeadOperatorSlots.Contains(operatorSlotIndex);

        public void ShowOperatorDamage(int operatorSlotIndex, int damage)
        {
            if (operatorSlotIndex < 0 || operatorSlotIndex >= this.playerSlotTransforms.Length)
                return;

            if (this.operatorDamageTextPrefab == null)
            {
                Debug.LogWarning("[BattlefieldView] operatorDamageTextPrefab is not assigned.");
                return;
            }

            Canvas? targetCanvas = this.operatorDamageCanvas != null
                ? this.operatorDamageCanvas
                : GetComponentInParent<Canvas>();
            if (targetCanvas == null)
            {
                Debug.LogWarning("[BattlefieldView] Missing Canvas for operator damage text.");
                return;
            }

            Transform anchor = this.playerSlotTransforms[operatorSlotIndex];
            var textGo = Instantiate(
                this.operatorDamageTextPrefab,
                targetCanvas.transform);
            var tmp = textGo.GetComponentInChildren<TMP_Text>();
            if (tmp == null)
            {
                Destroy(textGo);
                return;
            }

            PositionDamageTextOnCanvas(textGo.transform, targetCanvas, anchor.position + this.operatorDamageOffset);

            tmp.text = $"-{Mathf.Max(0, damage)}";
            tmp.alpha = 1f;

            Vector3 moveTarget = textGo.transform.position + (Vector3.up * 0.4f);
            textGo.transform.DOMove(moveTarget, this.operatorDamageDuration);
            tmp.DOFade(0f, this.operatorDamageDuration).OnComplete(() =>
            {
                if (textGo != null)
                    Destroy(textGo);
            });
        }

        private static void PositionDamageTextOnCanvas(Transform textTransform, Canvas canvas, Vector3 worldPosition)
        {
            if (textTransform is not RectTransform textRt)
            {
                textTransform.position = worldPosition;
                return;
            }

            if (canvas.transform is not RectTransform canvasRt)
            {
                textTransform.position = worldPosition;
                return;
            }

            Camera? eventCamera = null;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldPosition);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPoint, eventCamera, out Vector2 localPoint))
                textRt.anchoredPosition = localPoint;
        }

        public void SetEnemyTargetIndicator(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.enemySlotTransforms.Length) return;
            this.enemyTargetIndicator.SetActive(true);
            this.enemyTargetIndicator.transform.position = this.enemySlotTransforms[slotIndex].position + this.enemyTargetIndicatorOffset;
        }

        public void HideEnemyTargetIndicator()
        {
            this.enemyTargetIndicator.SetActive(false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (this.enemySlotTransforms == null) return;

            foreach (var kvp in this.enemyStateBySlot)
            {
                int slot = kvp.Key;
                var state = kvp.Value;
                if (slot < 0 || slot >= this.enemySlotTransforms.Length) continue;
                if (this.enemySlotTransforms[slot] == null) continue;

                float hpRatio = state.MaxHp > 0 ? (float)state.CurrentHp / state.MaxHp : 0f;
                var labelPos = this.enemySlotTransforms[slot].position + new Vector3(0f, 0.9f, 0f);

                UnityEditor.Handles.color = state.IsDead
                    ? Color.gray
                    : Color.Lerp(Color.red, Color.green, hpRatio);

                string text = state.IsDead
                    ? $"Enemy {slot} - DEAD"
                    : $"Enemy {slot} - HP {state.CurrentHp}/{state.MaxHp}";
                UnityEditor.Handles.Label(labelPos, text);
            }
        }
#endif
    }
}
