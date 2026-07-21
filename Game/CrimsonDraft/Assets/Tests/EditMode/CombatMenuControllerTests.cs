#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using CrimsonDraft.Combat;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class CombatMenuControllerTests
    {
        private FakeCombatActionMenuView menuView            = null!;
        private FakeCommandPanelView     commandPanel        = null!;
        private FakeCombatInventoryView  combatInventoryView = null!;
        private FakeShotCountView        shotCountView       = null!;
        private FakePublisher            publisher           = null!;
        private FakeAimView              aimView             = null!;
        private FakeBattlefieldView      battlefieldView     = null!;
        private FakeOperatorRoster       roster              = null!;
        private FakeInventoryService     inventory           = null!;
        private FakeOrchestrator         orchestrator        = null!;

        [SetUp]
        public void SetUp()
        {
            this.menuView            = new FakeCombatActionMenuView();
            this.commandPanel        = new FakeCommandPanelView();
            this.combatInventoryView = new FakeCombatInventoryView();
            this.shotCountView       = new FakeShotCountView();
            this.publisher           = new FakePublisher();
            this.aimView             = new FakeAimView();
            this.battlefieldView     = new FakeBattlefieldView();
            this.roster              = new FakeOperatorRoster();
            this.inventory           = new FakeInventoryService();
            this.orchestrator        = new FakeOrchestrator();
        }

        private CombatMenuController BuildAndInit(FakeInventoryService? inv = null)
        {
            var controller = new CombatMenuController(
                this.menuView, this.commandPanel, this.combatInventoryView, this.shotCountView, this.publisher, this.aimView, this.battlefieldView, this.roster,
                inv ?? this.inventory, this.orchestrator);
            ((IInitializable)controller).Initialize();
            return controller;
        }

        private static void InvokeConfirm(CombatMenuController controller)
        {
            var onConfirm = typeof(CombatMenuController).GetMethod("OnConfirmPerformed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(onConfirm);
            onConfirm!.Invoke(controller, new object?[] { null });
        }

        // ── Existing subscription tests ────────────────────────────────

        [Test]
        public void Initialize_subscribesToMenuView()
        {
            BuildAndInit();
            Assert.IsTrue(this.menuView.HasSubscribers);
        }

        [Test]
        public void Initialize_updatesAmmoLabelsForAllOperators()
        {
            BuildAndInit();

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(this.menuView.TryGetAmmo(i, out var ammo));
                Assert.AreEqual(6, ammo.current);
                Assert.AreEqual(6, ammo.max);
            }
        }

        [Test]
        public void AfterDispose_unsubscribesFromMenuView()
        {
            var c = BuildAndInit();
            ((IDisposable)c).Dispose();
            Assert.IsFalse(this.menuView.HasSubscribers);
        }

        [Test]
        public void AfterDispose_operatorSelectedEvent_doesNotThrow()
        {
            var c = BuildAndInit();
            ((IDisposable)c).Dispose();
            Assert.DoesNotThrow(() => this.menuView.RaiseOnOperatorSelected(0));
        }

        // ── State machine ──────────────────────────────────────────────

        [Test]
        public void OperatorSelected_showsCommandPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(1);
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void CommandSelected_Items_showsCombatInventory()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Items);

            Assert.IsTrue(this.combatInventoryView.IsVisible);
            Assert.AreEqual(0, this.combatInventoryView.LastShownOperatorSlot);
            Assert.IsFalse(this.commandPanel.IsVisible);
        }

        [Test]
        public void Cancel_inCombatInventory_hidesInventory_commandPanelRemains()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Items);

            this.combatInventoryView.RaiseOnCancelled();

            Assert.IsFalse(this.combatInventoryView.IsVisible);
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void CombatInventory_itemUsed_enqueuesUseItemAction_andHidesInventory()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(1);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Items);

            this.combatInventoryView.RaiseOnItemUsed(5);

            Assert.AreEqual(1, this.orchestrator.EnqueueCallCount);
            Assert.AreEqual(PendingActionType.UseItem, this.orchestrator.LastEnqueuedAction?.Type);
            Assert.AreEqual(1, this.orchestrator.LastEnqueuedAction?.SlotIndex);
            Assert.AreEqual(5, this.orchestrator.LastEnqueuedAction?.ItemIndex);
            Assert.IsFalse(this.combatInventoryView.IsVisible);
        }

        [Test]
        public void Cancel_inCommandPanel_hidesCommandPanel()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.HandleCancelPressed();
            Assert.IsFalse(this.commandPanel.IsVisible);
        }

        [Test]
        public void Cancel_inOperatorSelection_publishesCombatEndedEvent()
        {
            var c = BuildAndInit();
            c.HandleCancelPressed();
            Assert.IsTrue(this.publisher.Published);
        }

        // ── Aim minigame (no enemies → bypasses TargetSelection) ───────

        [Test]
        public void ShootCommand_noEnemies_showsShotCountView()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            Assert.IsTrue(this.shotCountView.IsVisible);
            Assert.AreEqual(1, this.shotCountView.Value);
        }

        [Test]
        public void ShootCommand_doesNotShowCombatInventory()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            Assert.IsFalse(this.combatInventoryView.IsVisible);
        }

        [Test]
        public void ShotCount_cancel_returnsToCommandPanel()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            c.HandleCancelPressed();

            Assert.IsFalse(this.shotCountView.IsVisible);
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void ShotCount_confirm_usesSelectedValueAsAimShotCount()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            this.shotCountView.Increment();
            this.shotCountView.Increment();

            InvokeConfirm(c);

            Assert.AreEqual(3, this.aimView.LastShotCount);
            Assert.IsTrue(this.aimView.IsVisible);
        }

        [Test]
        public void ShotFired_keepsAimViewVisibleUntilExtraConfirm()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Miss, ShotPrecision.Normal, 0) });
            Assert.IsTrue(this.aimView.IsVisible);
        }

        [Test]
        public void ShotFired_keepsCommandPanelVisibleUntilExtraConfirm()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Miss, ShotPrecision.Normal, 0) });
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void ShotFired_extraConfirm_closesAimAndCommandPanel()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Miss, ShotPrecision.Normal, 0) });
            InvokeConfirm(c);

            Assert.IsFalse(this.aimView.IsVisible);
            Assert.IsFalse(this.commandPanel.IsVisible);
        }

        [Test]
        public void ShotFired_extraConfirm_playsOperatorShootBurst_withSelectedOperatorEnemyAndShots()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(2);
            c.BeginShootConfiguration(2);

            this.shotCountView.Increment();
            this.shotCountView.Increment(); // Value = 3

            InvokeConfirm(c); // ShotCountSelectionState -> TargetSelState (enemies present)
            InvokeConfirm(c); // TargetSelectionState -> AimingState

            var shots = new[]
            {
                new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20),
                new ResolvedShot(1, Vector2.zero, ShotZone.Miss, ShotPrecision.Normal, 0),
                new ResolvedShot(2, Vector2.zero, ShotZone.Head, ShotPrecision.Normal, 40),
            };
            this.aimView.FireResolvedShots(shots);

            InvokeConfirm(c); // dismiss aim window -> should trigger the burst

            Assert.AreEqual(1, this.battlefieldView.BurstCallCount);
            Assert.AreEqual(2, this.battlefieldView.LastBurstOperatorSlotIndex);
            Assert.AreEqual(1, this.battlefieldView.LastBurstEnemySlotIndex);
            Assert.AreEqual(3, this.battlefieldView.LastBurstShots.Length);
            Assert.AreEqual(ShotZone.Head, this.battlefieldView.LastBurstShots[2].Zone);
        }

        [Test]
        public void ShotFired_extraConfirm_noEnemyTarget_passesNegativeOneEnemySlotToBurst()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0); // no occupied enemy slots -> ShotCountSelectionState goes straight to AimingState

            InvokeConfirm(c); // ShotCountSelectionState -> AimingState directly (no enemies)

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            InvokeConfirm(c); // dismiss -> triggers burst

            Assert.AreEqual(-1, this.battlefieldView.LastBurstEnemySlotIndex);
            Assert.AreEqual(1, this.battlefieldView.LastBurstShots.Length);
        }

        [Test]
        public void OnConfirm_whileBurstPlaying_ignoresExtraConfirmAndDoesNotTransitionYet()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            this.battlefieldView.HoldNextBurst();
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c); // -> TargetSelState
            InvokeConfirm(c); // -> AimingState

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            InvokeConfirm(c); // dismiss -> starts burst, held pending

            Assert.AreEqual(1, this.battlefieldView.BurstCallCount);
            Assert.IsFalse(this.aimView.IsVisible);
            Assert.IsFalse(this.commandPanel.IsVisible);
            Assert.AreEqual(0, this.orchestrator.NotifyShootCompletedCallCount);

            InvokeConfirm(c); // should be ignored while the burst is still playing

            Assert.AreEqual(1, this.battlefieldView.BurstCallCount);
            Assert.AreEqual(0, this.orchestrator.NotifyShootCompletedCallCount);

            this.battlefieldView.CompletePendingBurst();

            Assert.AreEqual(1, this.orchestrator.NotifyShootCompletedCallCount);
        }

        [Test]
        public void ShotsResolved_hit_appliesDamageUsingShotPayload()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            Assert.AreEqual(20, this.battlefieldView.LastDamageResult.DamageApplied);
            Assert.AreEqual(80, this.battlefieldView.LastDamageResult.RemainingHp);
        }

        [Test]
        public void ShotsResolved_legsHit_sendsDoublePoiseDamage()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Legs, ShotPrecision.Normal, 16) });

            // FakeWeaponSlot's default PoiseDamage is 10 (Task 1) -> legs doubles it to 20.
            Assert.AreEqual(20, this.battlefieldView.LastPoiseDamageApplied);
        }

        [Test]
        public void ShotsResolved_missShot_contributesNoPoiseDamage()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Miss, ShotPrecision.Normal, 0) });

            Assert.AreEqual(0, this.battlefieldView.LastPoiseDamageApplied);
        }

        [Test]
        public void ShotsResolved_resultStaggered_doesNotNotifyOrchestratorBeforeBurstPlays()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            this.battlefieldView.ForceNextDamageResultStaggered();
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            // The knockdown must not fire mid-QTE — only after the shoot burst animation plays.
            Assert.AreEqual(0, this.orchestrator.NotifyEnemyStaggeredCallCount);
            Assert.AreEqual(0, this.battlefieldView.TriggerEnemyStaggerCallCount);
        }

        [Test]
        public void ShotsResolved_resultStaggered_triggersStaggerAfterBurstPlays()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            this.battlefieldView.ForceNextDamageResultStaggered();
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            InvokeConfirm(c); // dismiss aim window -> plays the burst, then triggers the stagger

            Assert.AreEqual(1, this.battlefieldView.TriggerEnemyStaggerCallCount);
            Assert.AreEqual(1, this.battlefieldView.LastTriggerStaggerSlot);
            Assert.AreEqual(1, this.orchestrator.NotifyEnemyStaggeredCallCount);
            Assert.AreEqual(1, this.orchestrator.LastStaggeredSlot);
        }

        [Test]
        public void ShotsResolved_resultNotStaggered_doesNotNotifyOrchestrator()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            InvokeConfirm(c); // dismiss aim window -> plays the burst

            Assert.AreEqual(0, this.battlefieldView.TriggerEnemyStaggerCallCount);
            Assert.AreEqual(0, this.orchestrator.NotifyEnemyStaggeredCallCount);
        }

        [Test]
        public void ShotsResolved_miss_appliesZeroDamage()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, new Vector2(0.25f, 0.75f), ShotZone.Miss, ShotPrecision.Normal, 0) });

            Assert.AreEqual(0, this.battlefieldView.LastDamageResult.DamageApplied);
            Assert.AreEqual(100, this.battlefieldView.LastDamageResult.RemainingHp);
            Assert.IsFalse(this.battlefieldView.LastDamageResult.IsDead);
        }

        // ── Target selection ───────────────────────────────────────────

        [Test]
        public void ShootCommand_withEnemies_showsEnemyTargetIndicator()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 0, 2 });
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);

            Assert.IsTrue(this.battlefieldView.EnemyTargetVisible);
            Assert.IsFalse(this.aimView.IsVisible);
        }

        [Test]
        public void Cancel_inTargetSelection_hidesIndicator_returnsToCommandPanel()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 0 });
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c);
            c.HandleCancelPressed();
            Assert.IsFalse(this.battlefieldView.EnemyTargetVisible);
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void ConfirmTarget_configuresAimWithSelectedEnemyMaskProfile()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 2 });
            var expected = ScriptableObject.CreateInstance<AimHitMaskProfile>();
            this.battlefieldView.SetMaskProfile(2, expected);

            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c);
            InvokeConfirm(c);

            Assert.AreSame(expected, this.aimView.LastConfiguredProfile);
        }

        [Test]
        public void ComputeShotDamage_head_normalPrecision_returns40()
        {
            Assert.AreEqual(40, CombatMenuController.ComputeShotDamage(ShotZone.Head, 1f));
        }

        [Test]
        public void ComputeShotDamage_head_graze_returns20()
        {
            Assert.AreEqual(20, CombatMenuController.ComputeShotDamage(ShotZone.Head, 0.5f));
        }

        [Test]
        public void ComputeShotDamage_head_weakPoint_returns80()
        {
            Assert.AreEqual(80, CombatMenuController.ComputeShotDamage(ShotZone.Head, 2f));
        }

        [Test]
        public void ComputeShotDamage_torso_normalPrecision_returns20()
        {
            Assert.AreEqual(20, CombatMenuController.ComputeShotDamage(ShotZone.Torso, 1f));
        }

        [Test]
        public void ComputeShotDamage_torso_graze_returns10()
        {
            Assert.AreEqual(10, CombatMenuController.ComputeShotDamage(ShotZone.Torso, 0.5f));
        }

        [Test]
        public void ComputeShotDamage_arms_normalPrecision_returns14()
        {
            Assert.AreEqual(14, CombatMenuController.ComputeShotDamage(ShotZone.Arms, 1f));
        }

        [Test]
        public void ComputeShotDamage_legs_normalPrecision_returns16()
        {
            Assert.AreEqual(16, CombatMenuController.ComputeShotDamage(ShotZone.Legs, 1f));
        }

        [Test]
        public void ComputeShotDamage_miss_returns0()
        {
            Assert.AreEqual(0, CombatMenuController.ComputeShotDamage(ShotZone.Miss, 1f));
        }

        [Test]
        public void ComputePoiseDamage_torso_returnsWeaponValueUnchanged()
        {
            Assert.AreEqual(10, CombatMenuController.ComputePoiseDamage(ShotZone.Torso, 10));
        }

        [Test]
        public void ComputePoiseDamage_head_returnsWeaponValueUnchanged()
        {
            Assert.AreEqual(10, CombatMenuController.ComputePoiseDamage(ShotZone.Head, 10));
        }

        [Test]
        public void ComputePoiseDamage_legs_doublesWeaponValue()
        {
            Assert.AreEqual(20, CombatMenuController.ComputePoiseDamage(ShotZone.Legs, 10));
        }

        [Test]
        public void ComputePoiseDamage_zeroWeaponPoise_returnsZeroEvenOnLegs()
        {
            Assert.AreEqual(0, CombatMenuController.ComputePoiseDamage(ShotZone.Legs, 0));
        }

        [Test]
        public void ShouldStagger_positivePoise_returnsFalseRegardlessOfHp()
        {
            Assert.IsFalse(CombatMenuController.ShouldStagger(poiseAfterDamage: 5, currentHp: 1, maxHp: 100, staggerHpThresholdPct: 40f));
        }

        [Test]
        public void ShouldStagger_zeroPoise_hpAboveThreshold_returnsFalse()
        {
            Assert.IsFalse(CombatMenuController.ShouldStagger(poiseAfterDamage: 0, currentHp: 50, maxHp: 100, staggerHpThresholdPct: 40f));
        }

        [Test]
        public void ShouldStagger_zeroPoise_hpBelowThreshold_returnsTrue()
        {
            Assert.IsTrue(CombatMenuController.ShouldStagger(poiseAfterDamage: 0, currentHp: 30, maxHp: 100, staggerHpThresholdPct: 40f));
        }

        [Test]
        public void ShouldStagger_negativePoise_hpBelowThreshold_returnsTrue()
        {
            Assert.IsTrue(CombatMenuController.ShouldStagger(poiseAfterDamage: -8, currentHp: 30, maxHp: 100, staggerHpThresholdPct: 40f));
        }

        [Test]
        public void ShouldStagger_hpExactlyAtThreshold_returnsFalse()
        {
            Assert.IsFalse(CombatMenuController.ShouldStagger(poiseAfterDamage: 0, currentHp: 40, maxHp: 100, staggerHpThresholdPct: 40f));
        }

        [Test]
        public void ShotFired_appliesDamageToSelectedEnemy()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            Assert.AreEqual(1, this.battlefieldView.LastDamageResult.SlotIndex);
            Assert.AreEqual(20, this.battlefieldView.LastDamageResult.DamageApplied);
            Assert.AreEqual(80, this.battlefieldView.LastDamageResult.RemainingHp);
            Assert.IsFalse(this.battlefieldView.LastDamageResult.IsDead);
        }

        [Test]
        public void ShotFired_whenEnemyHpReachesZero_marksEnemyDead()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 10);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            Assert.IsTrue(this.battlefieldView.LastDamageResult.IsDead);
            Assert.AreEqual(0, this.battlefieldView.LastDamageResult.RemainingHp);
        }

        [Test]
        public void ShotFired_killingShot_doesNotFinalizeDeathBeforeBurstPlays()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 10);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            // Combat-end (SyncDeadEnemies -> CombatEndedEvent) keys off HasAliveEnemies /
            // GetOccupiedEnemySlots. Neither must flip before the shoot burst has played,
            // or combat ends mid-animation.
            Assert.IsTrue(this.battlefieldView.HasAliveEnemies());
            Assert.AreEqual(0, this.battlefieldView.FinalizeEnemyDeathCallCount);
        }

        [Test]
        public void ShotFired_killingShot_finalizesDeathAfterBurstPlays()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 10);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            InvokeConfirm(c); // dismiss aim window -> plays the burst, then finalizes the death

            Assert.AreEqual(1, this.battlefieldView.FinalizeEnemyDeathCallCount);
            Assert.AreEqual(1, this.battlefieldView.LastFinalizedDeathSlot);
            Assert.IsFalse(this.battlefieldView.HasAliveEnemies());
        }

        [Test]
        public void ShotFired_killingShot_doesNotAlsoTriggerStagger()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 10);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Head, ShotPrecision.Normal, 40) });

            InvokeConfirm(c); // dismiss aim window -> plays the burst

            Assert.AreEqual(0, this.battlefieldView.TriggerEnemyStaggerCallCount);
            Assert.AreEqual(0, this.orchestrator.NotifyEnemyStaggeredCallCount);
        }

        [Test]
        public void CommandPanel_focusFire_marksOperatorAndFreezesAtb()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire);

            CollectionAssert.Contains(c.FocusFireMarked, 0);
            Assert.AreEqual(1, this.orchestrator.MarkOperatorForFocusFireCallCount);
            Assert.AreEqual(0, this.orchestrator.LastMarkedFocusFireSlot);
            Assert.AreEqual(1, this.menuView.FocusFireMarkedCallCount);
            Assert.IsTrue(this.menuView.LastFocusFireMarkedValue);
            Assert.IsFalse(this.commandPanel.IsVisible);
            Assert.IsTrue(this.menuView.OperatorDimmedByIndex[0]); // marked operator visually dimmed + non-selectable
        }

        [Test]
        public void OperatorSelected_withNoneMarked_enablesFocusFire()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);

            Assert.IsTrue(this.commandPanel.IsCommandEnabled(CombatCommand.FocusFire));
        }

        [Test]
        public void OperatorSelected_withOneOfThreeMarked_stillEnablesFocusFireForAnother()
        {
            var c = BuildAndInit(); // default FakeOperatorRoster has 3 slots, all alive
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire); // marks 0

            this.menuView.RaiseOnOperatorSelected(1);

            Assert.IsTrue(this.commandPanel.IsCommandEnabled(CombatCommand.FocusFire));
        }

        [Test]
        public void OperatorSelected_withAllOthersMarked_disablesFocusFireForTheLastOne()
        {
            var c = BuildAndInit(); // 3 slots
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire); // marks 0

            this.menuView.RaiseOnOperatorSelected(1);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire); // marks 1

            this.menuView.RaiseOnOperatorSelected(2); // only unmarked operator left

            Assert.IsFalse(this.commandPanel.IsCommandEnabled(CombatCommand.FocusFire));
        }

        [Test]
        public void CommandPanel_shoot_withMarkedOperators_enqueuesFocusFireAction()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire); // marks 0

            this.menuView.RaiseOnOperatorSelected(1);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot); // triggers, 1 is the trigger

            Assert.IsNotNull(this.orchestrator.LastEnqueuedAction);
            var action = this.orchestrator.LastEnqueuedAction!.Value;
            Assert.AreEqual(PendingActionType.FocusFire, action.Type);
            Assert.AreEqual(1, action.SlotIndex);
            CollectionAssert.AreEqual(new[] { 0, 1 }, action.FocusFireParticipants);
        }

        [Test]
        public void CommandPanel_shoot_withMarkedOperators_clearsMarksAndUnmarksView()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire); // marks 0

            this.menuView.RaiseOnOperatorSelected(1);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);

            Assert.AreEqual(0, c.FocusFireMarked.Count);
            Assert.AreEqual(2, this.menuView.FocusFireMarkedCallCount); // marked(0,true) then unmarked(0,false)
            Assert.IsFalse(this.menuView.LastFocusFireMarkedValue);
            Assert.AreEqual(0, this.menuView.LastFocusFireMarkedSlot);
        }

        [Test]
        public void CommandPanel_shoot_withNoMarkedOperators_enqueuesNormalShoot()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);

            Assert.IsNotNull(this.orchestrator.LastEnqueuedAction);
            var action = this.orchestrator.LastEnqueuedAction!.Value;
            Assert.AreEqual(PendingActionType.Shoot, action.Type);
            Assert.AreEqual(0, action.SlotIndex);
        }

        [Test]
        public void BeginFocusFireConfiguration_seedsGroupStateAndEntersShotCountForFirstParticipant()
        {
            var c = BuildAndInit();
            c.BeginFocusFireConfiguration(new[] { 0, 1 });

            Assert.AreEqual(0, c.SelectedOperator);
            CollectionAssert.AreEqual(new[] { 0, 1 }, c.FocusFireParticipants);
            Assert.IsTrue(this.shotCountView.IsVisible);
        }

        [Test]
        public void ShotCountConfirm_groupFlow_loopsThroughParticipantsThenReachesTargetSelection()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            var c = BuildAndInit();
            c.BeginFocusFireConfiguration(new[] { 0, 1 });

            InvokeConfirm(c); // confirms participant 0's shot count

            Assert.AreEqual(1, c.SelectedOperator);
            Assert.AreEqual(1, c.FocusFireShotCounts[0]);
            Assert.IsTrue(this.shotCountView.IsVisible); // re-entered for participant 1

            InvokeConfirm(c); // confirms participant 1's (trigger) shot count -> TargetSelState

            Assert.AreEqual(1, c.FocusFireShotCounts[1]);
            Assert.IsTrue(this.battlefieldView.EnemyTargetVisible);
        }

        [Test]
        public void FocusFireResolution_appliesDamagePerParticipantAndPlaysSequentialBursts()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 1000);
            this.aimView.ResolveShotsForWeaponHandler = (data, count) =>
                new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 15) };

            var c = BuildAndInit();
            c.BeginFocusFireConfiguration(new[] { 0, 1 });

            InvokeConfirm(c); // participant 0's shot count
            InvokeConfirm(c); // participant 1's (trigger) shot count -> TargetSelState

            InvokeConfirm(c); // TargetSelState -> AimingState (only slot 1 is occupied)

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Head, ShotPrecision.Normal, 40) });

            InvokeConfirm(c); // dismiss aim window -> plays both bursts, finalizes

            Assert.AreEqual(2, this.battlefieldView.BurstCallCount);
            Assert.AreEqual(1, this.battlefieldView.LastBurstOperatorSlotIndex); // trigger (participant 1) fires last
            Assert.AreEqual(945, this.battlefieldView.LastDamageResult.RemainingHp); // 1000 - 15 (marked) - 40 (trigger)
            Assert.AreEqual(1, this.orchestrator.NotifyFocusFireCompletedCallCount);
            Assert.AreEqual(0, c.FocusFireParticipants.Length);
        }

        [Test]
        public void FocusFireResolution_resolvesMarkedParticipantsFromAimView()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 1000);

            var c = BuildAndInit();
            c.BeginFocusFireConfiguration(new[] { 0, 1 });

            InvokeConfirm(c);
            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Head, ShotPrecision.Normal, 40) });
            InvokeConfirm(c);

            Assert.AreEqual(1, this.aimView.ResolveShotsForWeaponCallCount); // once for the one marked participant
            Assert.AreEqual(1, this.aimView.LastResolvedShotCount);
        }

        // ── Fakes ──────────────────────────────────────────────────────

        private sealed class FakeInventoryService : IInventoryService
        {
            private readonly InventorySlot[] slots = new InventorySlot[8]; // 2 operators × 4

            public FakeInventoryService()
            {
                for (int i = 0; i < this.slots.Length; i++)
                    this.slots[i] = new InventorySlot();
            }

            public IReadOnlyList<InventorySlot> Slots    => this.slots;
            public int                          SlotCount => this.slots.Length;

            public int RemoveItemCallCount  { get; private set; }
            public int LastRemovedSlotIndex { get; private set; } = -1;

            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => true;
            public bool AddItemAuto(ItemData data, int quantity = 0)               => true;
            public void RemoveItem(int slotIndex)
            {
                this.RemoveItemCallCount++;
                this.LastRemovedSlotIndex = slotIndex;
            }
            public void MoveItem(int fromSlot, int toSlot)                         { }
            public void EquipWeapon(int slotIndex, int operatorSlot)               { }
            public void UnequipWeapon(int slotIndex)                               { }
            public int  GetEquippedWeaponIndex(int operatorSlot)                   => -1;
            public bool CanReload(int slotIndex, int operatorSlot)                 => false;
            public void ReloadOperator(int slotIndex, int operatorSlot)            { }
            public bool            TryCombine(int slotA, int slotB)          => false;
            public KeyUseOutcome   TryUseKey(string keyItemId)               => new KeyUseOutcome(KeyUseResult.NotFound, -1);
            public void            SetSlotPosition(int slotIndex, int col, int row, int rotation) { }
            public void            LoadState(InventorySlot[] slots)          { }
            public InventorySlot[] GetRawSlots()                             => this.slots;
        }

        private sealed class FakeCombatActionMenuView : ICombatActionMenuView
        {
            public event Action<int>? OnOperatorSelected;
            public event Action<int>? OnOperatorFocused;
            private readonly Dictionary<int, (int current, int max)> ammoByOperator = new();
            public bool HasSubscribers => this.OnOperatorSelected != null || this.OnOperatorFocused != null;
            public void RaiseOnOperatorSelected(int index) => this.OnOperatorSelected?.Invoke(index);
            public void FocusOperator(int index) { }
            public void ClearFocus() { }
            public void MoveSelectorTo(RectTransform anchor) { }
            public void SetOperatorAmmo(int index, int currentAmmo, int maxAmmo) =>
                this.ammoByOperator[index] = (currentAmmo, maxAmmo);
            public bool TryGetAmmo(int index, out (int current, int max) ammo) =>
                this.ammoByOperator.TryGetValue(index, out ammo);
            private readonly Dictionary<int, float> healthByOperator = new();
            public void SetOperatorHealth(int index, float hpRatio) =>
                this.healthByOperator[index] = hpRatio;
            public bool TryGetHealth(int index, out float hpRatio) =>
                this.healthByOperator.TryGetValue(index, out hpRatio);
            public void SetOperatorWeapon(int index, WeaponItem? weapon) { }
            public void SetDimmed(bool dimmed) { }
            public readonly Dictionary<int, bool> OperatorDimmedByIndex = new();
            public void SetOperatorDimmed(int index, bool dimmed) => this.OperatorDimmedByIndex[index] = dimmed;
            public int  FocusFireMarkedCallCount  { get; private set; }
            public bool LastFocusFireMarkedValue  { get; private set; }
            public int  LastFocusFireMarkedSlot   { get; private set; } = -1;
            public void SetOperatorFocusFireMarked(int index, bool marked)
            {
                this.FocusFireMarkedCallCount++;
                this.LastFocusFireMarkedValue = marked;
                this.LastFocusFireMarkedSlot  = index;
            }
            public RectTransform GetOperatorAnchor(int index) =>
                new GameObject().AddComponent<RectTransform>();
            public RectTransform GetOperatorRect(int index) =>
                new GameObject().AddComponent<RectTransform>();
            public RectTransform GetOperatorOverviewRect(int index) =>
                new GameObject().AddComponent<RectTransform>();
        }

        private sealed class FakeCommandPanelView : ICommandPanelView
        {
            public event Action<CombatCommand>?  OnCommandSelected;
            public event Action<RectTransform>?  OnEntryFocused;
            public bool IsVisible { get; private set; }
            private readonly RectTransform panelRect = new GameObject().AddComponent<RectTransform>();
            private readonly Dictionary<CombatCommand, bool> enabledByCommand = new();
            public RectTransform PanelRect => this.panelRect;
            public void Show(RectTransform _)         => this.IsVisible = true;
            public void RepositionTo(RectTransform _) { }
            public void Focus()                       { }
            public void SetCommandEnabled(CombatCommand command, bool enabled) => this.enabledByCommand[command] = enabled;
            public void SetDimmed(bool _)     { }
            public void Hide()                => this.IsVisible = false;
            public bool IsCommandEnabled(CombatCommand command) =>
                !this.enabledByCommand.TryGetValue(command, out bool enabled) || enabled;
            public void RaiseOnCommandSelected(CombatCommand cmd)
            {
                if (!this.IsCommandEnabled(cmd))
                    return;
                this.OnCommandSelected?.Invoke(cmd);
            }
        }

        private sealed class FakeCombatInventoryView : ICombatInventoryView
        {
            public event Action<int>? OnItemUsed;
            public event Action?      OnCancelled;
            public bool IsVisible             { get; private set; }
            public int  LastShownOperatorSlot { get; private set; } = -1;
            public void Show(int operatorSlot, RectTransform operatorOverviewRect)
            {
                this.LastShownOperatorSlot = operatorSlot;
                this.IsVisible             = true;
            }
            public void Hide()                          => this.IsVisible = false;
            public void RaiseOnItemUsed(int slotIndex)   => this.OnItemUsed?.Invoke(slotIndex);
            public void RaiseOnCancelled()               => this.OnCancelled?.Invoke();
        }

        private sealed class FakeShotCountView : IShotCountView
        {
            public bool IsVisible { get; private set; }
            public int Value { get; private set; } = 1;
            public int MaxValue { get; private set; } = 1;
            public void Show(RectTransform _, int initial, int max)
            {
                this.Value = Mathf.Max(1, initial);
                this.MaxValue = Mathf.Max(1, max);
                if (this.Value > this.MaxValue)
                    this.Value = this.MaxValue;
                this.IsVisible = true;
            }
            public void Hide() => this.IsVisible = false;
            public void Increment() => this.Value = Mathf.Min(this.MaxValue, this.Value + 1);
            public void Decrement() => this.Value = Mathf.Max(1, this.Value - 1);
        }

        private sealed class FakePublisher : MessagePipe.IPublisher<CombatEndedEvent>
        {
            public bool Published { get; private set; }
            public CombatEndedEvent? LastEvent { get; private set; }
            public void Publish(CombatEndedEvent message)
            {
                this.Published = true;
                this.LastEvent = message;
            }
        }

        private sealed class FakeAimView : IAimView
        {
            public event Action<ResolvedShot[]>? OnShotsResolved;
            public bool IsVisible { get; private set; }
            public AimHitMaskProfile? LastConfiguredProfile { get; private set; }
            public int LastShotCount { get; private set; } = 1;
            public bool ShowShotFeedbackCalled { get; private set; }
            public int ShowShotFeedbackCallCount { get; private set; }
            public Vector2 LastFeedbackPos { get; private set; }
            public int LastFeedbackDamage { get; private set; }
            public bool LastFeedbackIsMiss { get; private set; }
            public void ConfigureHitMask(AimHitMaskProfile? profile) => this.LastConfiguredProfile = profile;
            public void ConfigureWeapon(CrimsonDraft.Inventory.WeaponData? weaponData) { }
            public void SetShotCount(int shotCount) => this.LastShotCount = shotCount;
            public void ShowShotFeedback(Vector2 normalizedPos, int damage, bool isMiss)
            {
                this.ShowShotFeedbackCalled = true;
                this.ShowShotFeedbackCallCount++;
                this.LastFeedbackPos = normalizedPos;
                this.LastFeedbackDamage = damage;
                this.LastFeedbackIsMiss = isMiss;
            }
            public void Show()    => this.IsVisible = true;
            public void Confirm() { }
            public void Hide()    => this.IsVisible = false;
            public void FireResolvedShots(ResolvedShot[] shots) => this.OnShotsResolved?.Invoke(shots);

            public int ResolveShotsForWeaponCallCount { get; private set; }
            public CrimsonDraft.Inventory.WeaponData? LastResolvedWeaponData { get; private set; }
            public int LastResolvedShotCount { get; private set; }
            public Func<CrimsonDraft.Inventory.WeaponData?, int, ResolvedShot[]>? ResolveShotsForWeaponHandler;

            public ResolvedShot[] ResolveShotsForWeapon(CrimsonDraft.Inventory.WeaponData? weaponData, int shotCount)
            {
                this.ResolveShotsForWeaponCallCount++;
                this.LastResolvedWeaponData = weaponData;
                this.LastResolvedShotCount  = shotCount;

                if (this.ResolveShotsForWeaponHandler != null)
                    return this.ResolveShotsForWeaponHandler(weaponData, shotCount);

                var shots = new ResolvedShot[Mathf.Max(1, shotCount)];
                for (int i = 0; i < shots.Length; i++)
                    shots[i] = new ResolvedShot(i, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20);
                return shots;
            }
        }

        private sealed class FakeBattlefieldView : IBattlefieldView
        {
            private int[] occupiedSlots    = Array.Empty<int>();
            private readonly System.Collections.Generic.Dictionary<int, AimHitMaskProfile?> maskBySlot = new();
            private readonly System.Collections.Generic.Dictionary<int, int> hpBySlot = new();
            public bool   EnemyTargetVisible { get; private set; }
            public EnemyDamageResult LastDamageResult { get; private set; }
            public int BurstCallCount             { get; private set; }
            public int LastBurstOperatorSlotIndex  { get; private set; } = -1;
            public int LastBurstEnemySlotIndex     { get; private set; } = -1;
            public ResolvedShot[] LastBurstShots   { get; private set; } = Array.Empty<ResolvedShot>();
            private UniTaskCompletionSource? pendingBurstSource;
            private bool forceNextResultStaggered;
            public int LastPoiseDamageApplied { get; private set; }

            public void SetOccupiedSlots(int[] slots)
            {
                this.occupiedSlots = slots;
                foreach (int slot in slots)
                {
                    if (!this.hpBySlot.ContainsKey(slot))
                        this.hpBySlot[slot] = 100;
                }
            }
            public void SetMaskProfile(int slot, AimHitMaskProfile? profile) => this.maskBySlot[slot] = profile;
            public void SetEnemyHp(int slot, int hp) => this.hpBySlot[slot] = hp;

            public void HoldNextBurst()        => this.pendingBurstSource = new UniTaskCompletionSource();
            public void CompletePendingBurst() => this.pendingBurstSource?.TrySetResult();
            public void ForceNextDamageResultStaggered() => this.forceNextResultStaggered = true;

            public void Populate(EncounterData encounter)              { }
            public void SetOperatorIndicator(int slotIndex)            { }
            public void DimOperatorIndicator()                         { }
            public void PlayEnemyAttackFeedback(int enemySlotIndex)    { }
            public void ShowOperatorDamage(int operatorSlotIndex, int damage) { }
            public void SetEnemyTargetIndicator(int slotIndex)         => this.EnemyTargetVisible = true;
            public void HideEnemyTargetIndicator()                     => this.EnemyTargetVisible = false;
            public int[] GetOccupiedEnemySlots()                       => this.occupiedSlots;
            public AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex) =>
                this.maskBySlot.TryGetValue(slotIndex, out var profile) ? profile : null;
            public bool IsEnemyStaggered(int slotIndex) => false;
            public bool IsEnemyDead(int slotIndex) =>
                this.hpBySlot.TryGetValue(slotIndex, out int hp) && hp <= 0;
            public int TriggerEnemyStaggerCallCount { get; private set; }
            public int LastTriggerStaggerSlot       { get; private set; } = -1;
            public void TriggerEnemyStagger(int slotIndex)
            {
                this.TriggerEnemyStaggerCallCount++;
                this.LastTriggerStaggerSlot = slotIndex;
            }

            public int RecoverEnemyStaggerCallCount { get; private set; }
            public void RecoverEnemyStagger(int slotIndex) => this.RecoverEnemyStaggerCallCount++;
            public int[] NotifyActionDequeued() => Array.Empty<int>();

            public int FinalizeEnemyDeathCallCount { get; private set; }
            public int LastFinalizedDeathSlot      { get; private set; } = -1;
            public void FinalizeEnemyDeath(int slotIndex)
            {
                this.FinalizeEnemyDeathCallCount++;
                this.LastFinalizedDeathSlot = slotIndex;

                var next = new System.Collections.Generic.List<int>(this.occupiedSlots.Length);
                foreach (int slot in this.occupiedSlots)
                {
                    if (slot != slotIndex)
                        next.Add(slot);
                }
                this.occupiedSlots = next.ToArray();
            }

            public EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage)
            {
                this.LastPoiseDamageApplied = poiseDamage;
                bool staggeredThisHit = this.forceNextResultStaggered;
                this.forceNextResultStaggered = false;

                if (!this.hpBySlot.TryGetValue(slotIndex, out int hp))
                {
                    this.LastDamageResult = new EnemyDamageResult(slotIndex, 0, 0, false, staggeredThisHit);
                    return this.LastDamageResult;
                }

                int applied = Mathf.Max(0, hpDamage);
                int nextHp = Mathf.Max(0, hp - applied);
                this.hpBySlot[slotIndex] = nextHp;
                bool dead = nextHp <= 0;
                // Deliberately NOT removed from occupiedSlots here — matches real
                // BattlefieldView, which defers that to FinalizeEnemyDeath() so combat
                // can't end mid shoot-burst.

                this.LastDamageResult = new EnemyDamageResult(slotIndex, applied, nextHp, dead, staggeredThisHit);
                return this.LastDamageResult;
            }
            public bool HasAliveEnemies() => this.occupiedSlots.Length > 0;
            public UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots)
            {
                this.BurstCallCount++;
                this.LastBurstOperatorSlotIndex = operatorSlotIndex;
                this.LastBurstEnemySlotIndex = enemySlotIndex;
                this.LastBurstShots = shots;
                return this.pendingBurstSource != null ? this.pendingBurstSource.Task : UniTask.CompletedTask;
            }
#if UNITY_EDITOR || DEBUG_COMBAT
            public (int Current, int Max, bool IsDead, int Poise, bool IsStaggered) GetEnemyHpDebug(int slotIndex)
            {
                bool alive = System.Array.IndexOf(this.occupiedSlots, slotIndex) >= 0;
                int  hp    = this.hpBySlot.TryGetValue(slotIndex, out int v) ? v : 0;
                return (hp, 100, !alive, 0, false);
            }
#endif
        }

        private sealed class FakeOrchestrator : ICombatOrchestrator
        {
            public PendingAction? LastEnqueuedAction { get; private set; }
            public int EnqueueCallCount              { get; private set; }
            public void EnqueueAction(PendingAction action)
            {
                this.LastEnqueuedAction = action;
                this.EnqueueCallCount++;
            }
            public int  NotifyShootCompletedCallCount  { get; private set; }
            public void SetWaitMode(bool paused)       { }
            public bool IsOperatorReady(int slotIndex) => true;
            public void NotifyShootCompleted()         => this.NotifyShootCompletedCallCount++;
            public int NotifyEnemyStaggeredCallCount { get; private set; }
            public int LastStaggeredSlot             { get; private set; } = -1;
            public void NotifyEnemyStaggered(int enemySlot)
            {
                this.NotifyEnemyStaggeredCallCount++;
                this.LastStaggeredSlot = enemySlot;
            }
            public int MarkOperatorForFocusFireCallCount { get; private set; }
            public int LastMarkedFocusFireSlot           { get; private set; } = -1;
            public void MarkOperatorForFocusFire(int operatorSlot)
            {
                this.MarkOperatorForFocusFireCallCount++;
                this.LastMarkedFocusFireSlot = operatorSlot;
            }
            public int NotifyFocusFireCompletedCallCount { get; private set; }
            public void NotifyFocusFireCompleted()        => this.NotifyFocusFireCompletedCallCount++;
        }

        private sealed class FakeOperatorRoster : IOperatorRoster
        {
            private readonly OperatorRuntime[] slots;
            private readonly System.Collections.Generic.List<int> scratchAlive = new();
            public bool IsInitialized { get; private set; } = true;

            internal FakeOperatorRoster(int slotCount = 3, int maxHp = 100, int maxAmmo = 6)
            {
                this.slots = new OperatorRuntime[slotCount];
                for (int i = 0; i < slotCount; i++)
                {
                    this.slots[i] = new OperatorRuntime(i, null, isPresent: true, maxHp);
                    this.slots[i].SetEquippedWeapon(new FakeWeaponSlot(maxAmmo));
                }
            }

            public int Count => this.slots.Length;

            public OperatorRuntime this[int slotIndex] => this.slots[slotIndex];

            public void EnsureInitialized() => this.IsInitialized = true;

            public System.Collections.Generic.IReadOnlyList<int> GetAliveSlots()
            {
                this.scratchAlive.Clear();
                for (int i = 0; i < this.slots.Length; i++)
                    if (this.slots[i].IsAlive) this.scratchAlive.Add(i);
                return this.scratchAlive;
            }

            public int[] GetHpSnapshot() => System.Array.Empty<int>();
            public void RestoreHp(int[] snapshot) { }

            private sealed class FakeWeaponSlot : IWeaponSlot
            {
                public Caliber Caliber    => Caliber._9mm;
                public GunType GunType    => GunType.Pistols;
                public int     BaseDamage => 20;
                public int     CurrentAmmo { get; private set; }
                public int     MaxAmmo { get; }
                public int     PoiseDamage { get; }

                internal FakeWeaponSlot(int maxAmmo, int poiseDamage = 10)
                {
                    this.MaxAmmo = Mathf.Max(1, maxAmmo);
                    this.CurrentAmmo = this.MaxAmmo;
                    this.PoiseDamage = poiseDamage;
                }

                public void SetAmmo(int value) =>
                    this.CurrentAmmo = Mathf.Clamp(value, 0, this.MaxAmmo);
            }
        }
    }
}
