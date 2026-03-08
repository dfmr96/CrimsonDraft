#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Combat;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class CombatMenuControllerTests
    {
        private FakeCombatActionMenuView menuView        = null!;
        private FakeCommandPanelView     commandPanel    = null!;
        private FakeSubPanelView         subPanel        = null!;
        private FakeShotCountView        shotCountView   = null!;
        private FakePublisher            publisher       = null!;
        private FakeAimView              aimView         = null!;
        private FakeBattlefieldView      battlefieldView = null!;
        private FakeOperatorRoster       roster          = null!;
        private FakeInventoryService     inventory       = null!;

        [SetUp]
        public void SetUp()
        {
            this.menuView        = new FakeCombatActionMenuView();
            this.commandPanel    = new FakeCommandPanelView();
            this.subPanel        = new FakeSubPanelView();
            this.shotCountView   = new FakeShotCountView();
            this.publisher       = new FakePublisher();
            this.aimView         = new FakeAimView();
            this.battlefieldView = new FakeBattlefieldView();
            this.roster          = new FakeOperatorRoster();
            this.inventory       = new FakeInventoryService();
        }

        private CombatMenuController BuildAndInit(FakeInventoryService? inv = null)
        {
            var controller = new CombatMenuController(
                this.menuView, this.commandPanel, this.subPanel, this.shotCountView, this.publisher, this.aimView, this.battlefieldView, this.roster,
                inv ?? this.inventory);
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
        public void CommandSelected_Reload_doesNotShowSubPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);
            Assert.IsFalse(this.subPanel.IsVisible);
        }

        [Test]
        public void CommandSelected_Items_showsSubPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Items);
            Assert.IsTrue(this.subPanel.IsVisible);
        }

        [Test]
        public void CommandSelected_Defend_showsSubPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Defend);
            Assert.IsTrue(this.subPanel.IsVisible);
        }

        [Test]
        public void Cancel_inSubPanel_hidesSubPanel_commandPanelRemains()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Items);
            c.HandleCancelPressed();
            Assert.IsFalse(this.subPanel.IsVisible);
            Assert.IsTrue(this.commandPanel.IsVisible);
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
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            Assert.IsTrue(this.shotCountView.IsVisible);
            Assert.AreEqual(1, this.shotCountView.Value);
        }

        [Test]
        public void ShootCommand_doesNotShowSubPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            Assert.IsFalse(this.subPanel.IsVisible);
        }

        [Test]
        public void ShotCount_cancel_returnsToCommandPanel()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);

            c.HandleCancelPressed();

            Assert.IsFalse(this.shotCountView.IsVisible);
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void ShotCount_confirm_usesSelectedValueAsAimShotCount()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);

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
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Miss, 0) });
            Assert.IsTrue(this.aimView.IsVisible);
        }

        [Test]
        public void ShotFired_keepsCommandPanelVisibleUntilExtraConfirm()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Miss, 0) });
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void ShotFired_extraConfirm_closesAimAndCommandPanel()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Miss, 0) });
            InvokeConfirm(c);

            Assert.IsFalse(this.aimView.IsVisible);
            Assert.IsFalse(this.commandPanel.IsVisible);
        }

        [Test]
        public void Reload_doesNotRefillAmmo_andShootRemainsUnavailable()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);

            for (int i = 0; i < 6; i++)
            {
                this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
                InvokeConfirm(c);
                this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Miss, 0) });
                InvokeConfirm(c);
                this.menuView.RaiseOnOperatorSelected(0);
            }

            Assert.IsFalse(this.commandPanel.IsCommandEnabled(CombatCommand.Shoot));

            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);
            this.menuView.RaiseOnOperatorSelected(0);

            Assert.IsFalse(this.commandPanel.IsCommandEnabled(CombatCommand.Shoot));
        }

        [Test]
        public void ShotsResolved_hit_appliesDamageUsingShotPayload()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);

            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, 20) });

            Assert.AreEqual(20, this.battlefieldView.LastDamageResult.DamageApplied);
            Assert.AreEqual(80, this.battlefieldView.LastDamageResult.RemainingHp);
        }

        [Test]
        public void ShotsResolved_miss_appliesZeroDamage()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);

            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, new Vector2(0.25f, 0.75f), ShotZone.Miss, 0) });

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
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);

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
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
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
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            InvokeConfirm(c);
            InvokeConfirm(c);

            Assert.AreSame(expected, this.aimView.LastConfiguredProfile);
        }

        [Test]
        public void ComputeShotDamage_head_returns40()
        {
            Assert.AreEqual(40, CombatMenuController.ComputeShotDamage(ShotZone.Head));
        }

        [Test]
        public void ComputeShotDamage_torso_returns20()
        {
            Assert.AreEqual(20, CombatMenuController.ComputeShotDamage(ShotZone.Torso));
        }

        [Test]
        public void ComputeShotDamage_arms_returns14()
        {
            Assert.AreEqual(14, CombatMenuController.ComputeShotDamage(ShotZone.Arms));
        }

        [Test]
        public void ComputeShotDamage_legs_returns16()
        {
            Assert.AreEqual(16, CombatMenuController.ComputeShotDamage(ShotZone.Legs));
        }

        [Test]
        public void ComputeShotDamage_miss_returns0()
        {
            Assert.AreEqual(0, CombatMenuController.ComputeShotDamage(ShotZone.Miss));
        }

        [Test]
        public void ShotFired_appliesDamageToSelectedEnemy()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, 20) });

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
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, 20) });

            Assert.IsTrue(this.battlefieldView.LastDamageResult.IsDead);
            Assert.AreEqual(0, this.battlefieldView.LastDamageResult.RemainingHp);
            Assert.IsFalse(this.battlefieldView.HasAliveEnemies());
        }

        [Test]
        public void ShotFired_whenAllEnemiesDead_publishesVictoryTrue()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 10);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
            InvokeConfirm(c);

            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Head, 40) });

            Assert.IsTrue(this.publisher.Published);
            Assert.NotNull(this.publisher.LastEvent);
            Assert.IsTrue(this.publisher.LastEvent!.Value.Victory);
        }

        // ── Fakes ──────────────────────────────────────────────────────

        private sealed class FakeInventoryService : IInventoryService
        {
            private readonly List<InventoryItem>   items       = new();
            private readonly Dictionary<int, bool> canReloadBy = new();

            public IReadOnlyList<InventoryItem> Items => this.items;
            public int ReloadCallCount  { get; private set; }
            public int LastAmmoBoxIndex { get; private set; } = -1;
            public int LastOperatorSlot { get; private set; } = -1;

            public void AddItem(ItemData data, int quantity = 0)        { }
            public void EquipWeapon(int itemIndex, int operatorSlot)    { }
            public void UnequipWeapon(int itemIndex)                    { }
            public int  GetEquippedWeaponIndex(int operatorSlot)        => -1;

            public bool CanReload(int ammoBoxIndex, int operatorSlot)
                => this.canReloadBy.TryGetValue(ammoBoxIndex, out bool v) && v;

            public void ReloadOperator(int ammoBoxIndex, int operatorSlot)
            {
                this.ReloadCallCount++;
                this.LastAmmoBoxIndex = ammoBoxIndex;
                this.LastOperatorSlot = operatorSlot;
            }

            /// <summary>Registers an ammo box item at the next index. canReload controls CanReload result.</summary>
            public void RegisterBox(AmmoBoxItem box, bool canReload)
            {
                int idx = this.items.Count;
                this.items.Add(box);
                this.canReloadBy[idx] = canReload;
            }
        }

        private sealed class FakeCombatActionMenuView : ICombatActionMenuView
        {
            public event Action<int>? OnOperatorSelected;
            public event Action<int>? OnOperatorFocused;
            private readonly Dictionary<int, (int current, int max)> ammoByOperator = new();
            public bool HasSubscribers => this.OnOperatorSelected != null || this.OnOperatorFocused != null;
            public void RaiseOnOperatorSelected(int index) => this.OnOperatorSelected?.Invoke(index);
            public void FocusOperator(int index) { }
            public void MoveSelectorTo(RectTransform anchor) { }
            public void SetOperatorAmmo(int index, int currentAmmo, int maxAmmo) =>
                this.ammoByOperator[index] = (currentAmmo, maxAmmo);
            public bool TryGetAmmo(int index, out (int current, int max) ammo) =>
                this.ammoByOperator.TryGetValue(index, out ammo);
            public void SetDimmed(bool dimmed) { }
            public RectTransform GetOperatorAnchor(int index) =>
                new GameObject().AddComponent<RectTransform>();
            public RectTransform GetOperatorRect(int index) =>
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
            public void Show(RectTransform _) => this.IsVisible = true;
            public void Focus()               { }
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

        private sealed class FakeSubPanelView : ISubPanelView
        {
            public event Action<int>?           OnItemSelected;
            public event Action<RectTransform>? OnEntryFocused;
            public bool IsVisible { get; private set; }
            public void Show(SubPanelItem[] _, RectTransform __) => this.IsVisible = true;
            public void Hide()                                    => this.IsVisible = false;
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
        }

        private sealed class FakeBattlefieldView : IBattlefieldView
        {
            private int[] occupiedSlots    = Array.Empty<int>();
            private readonly System.Collections.Generic.Dictionary<int, AimHitMaskProfile?> maskBySlot = new();
            private readonly System.Collections.Generic.Dictionary<int, int> hpBySlot = new();
            public bool   EnemyTargetVisible { get; private set; }
            public EnemyDamageResult LastDamageResult { get; private set; }

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
            public EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int damage)
            {
                if (!this.hpBySlot.TryGetValue(slotIndex, out int hp))
                {
                    this.LastDamageResult = new EnemyDamageResult(slotIndex, 0, 0, false);
                    return this.LastDamageResult;
                }

                int applied = Mathf.Max(0, damage);
                int nextHp = Mathf.Max(0, hp - applied);
                this.hpBySlot[slotIndex] = nextHp;
                bool dead = nextHp <= 0;
                if (dead)
                {
                    var next = new System.Collections.Generic.List<int>(this.occupiedSlots.Length);
                    foreach (int slot in this.occupiedSlots)
                    {
                        if (slot != slotIndex)
                            next.Add(slot);
                    }
                    this.occupiedSlots = next.ToArray();
                }

                this.LastDamageResult = new EnemyDamageResult(slotIndex, applied, nextHp, dead);
                return this.LastDamageResult;
            }
            public bool HasAliveEnemies() => this.occupiedSlots.Length > 0;
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

            private sealed class FakeWeaponSlot : IWeaponSlot
            {
                public string Caliber => "9mm";
                public int CurrentAmmo { get; private set; }
                public int MaxAmmo { get; }

                internal FakeWeaponSlot(int maxAmmo)
                {
                    this.MaxAmmo = Mathf.Max(1, maxAmmo);
                    this.CurrentAmmo = this.MaxAmmo;
                }

                public void SetAmmo(int value) =>
                    this.CurrentAmmo = Mathf.Clamp(value, 0, this.MaxAmmo);
            }
        }
    }
}
