#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class SavePointInteractableTests
    {
        private const string MissingItemNode = "save_no_ticker_tape";
        private const string PromptNode      = "save_use_ticker_tape_prompt";
        private const string SendCommand     = "send_report";

        private GameObject go = null!;

        [TearDown]
        public void TearDown()
        {
            if (this.go != null) UnityEngine.Object.DestroyImmediate(this.go);
        }

        private SavePointInteractable MakeSavePoint(ItemData? requiredItem)
        {
            this.go = new GameObject("SavePoint");
            var savePoint = this.go.AddComponent<SavePointInteractable>();
            typeof(SavePointInteractable).GetField("requiredItem", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(savePoint, requiredItem);
            var dialogueField = typeof(SavePointInteractable).GetField("missingItemDialogue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var dialogue      = (DialogueReference)dialogueField.GetValue(savePoint)!;
            dialogue.nodeName = MissingItemNode;
            var promptField = typeof(SavePointInteractable).GetField("promptDialogue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ((DialogueReference)promptField.GetValue(savePoint)!).nodeName = PromptNode;
            return savePoint;
        }

        private static InteractionContext MakeContext(
            FakeInventoryService inventory, FakeDialogue dialogue, FakeSaveController save, FakeDialogue? prompt = null)
            => new(inventory, null!, dialogue, null!, prompt ?? new FakeDialogue(), null!, null!, null!, save, null!, null!);

        // Plays the Yes/No prompt to the end: "Yes" runs the command, then the dialogue completes.
        private static void Answer(FakeDialogue prompt, bool yes)
        {
            if (yes) prompt.LastCommands![SendCommand]();
            prompt.LastOnComplete!();
        }

        [Test]
        public void Interact_withoutRequiredItem_opensSaveForFree()
        {
            var inventory = new FakeInventoryService();
            var save      = new FakeSaveController();

            MakeSavePoint(requiredItem: null).Interact(MakeContext(inventory, new FakeDialogue(), save));
            save.LastOnSaved?.Invoke();

            Assert.AreEqual(1, save.OpenCount);
            Assert.AreEqual(0, inventory.ConsumedOne.Count);
        }

        [Test]
        public void Interact_requiredItemMissing_showsMessage_andDoesNotOpen()
        {
            var tape     = Consumable(stackable: true, maxStack: 5, id: "ticker_tape");
            var dialogue = new FakeDialogue();
            var save     = new FakeSaveController();

            MakeSavePoint(tape).Interact(MakeContext(new FakeInventoryService(), dialogue, save));

            Assert.AreEqual(0, save.OpenCount);
            Assert.AreEqual(MissingItemNode, dialogue.LastNodeName);
        }

        [Test]
        public void Interact_requiredItemCarried_asksBeforeOpening()
        {
            var tape   = Consumable(stackable: true, maxStack: 5, id: "ticker_tape");
            var prompt = new FakeDialogue();
            var save   = new FakeSaveController();

            MakeSavePoint(tape).Interact(MakeContext(new FakeInventoryService(ownedIds: "ticker_tape"), new FakeDialogue(), save, prompt));

            Assert.AreEqual(PromptNode, prompt.LastNodeName);
            Assert.AreEqual(0, save.OpenCount, "the save menu waits for the player's answer");
        }

        [Test]
        public void AnsweringYes_opensSave_withoutSpendingItYet()
        {
            var tape      = Consumable(stackable: true, maxStack: 5, id: "ticker_tape");
            var inventory = new FakeInventoryService(ownedIds: "ticker_tape");
            var prompt    = new FakeDialogue();
            var save      = new FakeSaveController();

            MakeSavePoint(tape).Interact(MakeContext(inventory, new FakeDialogue(), save, prompt));
            Answer(prompt, yes: true);

            Assert.AreEqual(1, save.OpenCount);
            Assert.AreEqual(0, inventory.ConsumedOne.Count, "closing the save menu without saving must not cost a tape");
        }

        [Test]
        public void AnsweringNo_doesNotOpenSave()
        {
            var tape      = Consumable(stackable: true, maxStack: 5, id: "ticker_tape");
            var inventory = new FakeInventoryService(ownedIds: "ticker_tape");
            var prompt    = new FakeDialogue();
            var save      = new FakeSaveController();

            MakeSavePoint(tape).Interact(MakeContext(inventory, new FakeDialogue(), save, prompt));
            Answer(prompt, yes: false);

            Assert.AreEqual(0, save.OpenCount);
            Assert.AreEqual(0, inventory.ConsumedOne.Count);
        }

        [Test]
        public void SavingToASlot_spendsOneRequiredItem()
        {
            var tape      = Consumable(stackable: true, maxStack: 5, id: "ticker_tape");
            var inventory = new FakeInventoryService(ownedIds: "ticker_tape");
            var prompt    = new FakeDialogue();
            var save      = new FakeSaveController();

            MakeSavePoint(tape).Interact(MakeContext(inventory, new FakeDialogue(), save, prompt));
            Answer(prompt, yes: true);
            save.LastOnSaved!.Invoke();

            CollectionAssert.AreEqual(new[] { tape }, inventory.ConsumedOne);
        }

        private sealed class FakeSaveController : ISaveController
        {
            public int     OpenCount   { get; private set; }
            public Action? LastOnSaved { get; private set; }

            public void Open(Action? onSaved = null)
            {
                this.OpenCount++;
                this.LastOnSaved = onSaved;
            }
        }

        private sealed class FakeDialogue : IPickupDialogueService
        {
            public bool                                 IsRunning      => false;
            public string?                              LastNodeName   { get; private set; }
            public Action?                              LastOnComplete { get; private set; }
            public IReadOnlyDictionary<string, Action>? LastCommands   { get; private set; }

            public void StartDialogue(
                string                               nodeName,
                IReadOnlyDictionary<string, object>? variables  = null,
                Action?                              onComplete = null,
                IReadOnlyDictionary<string, Action>? commands   = null)
            {
                this.LastNodeName   = nodeName;
                this.LastOnComplete = onComplete;
                this.LastCommands   = commands;
            }

            public void SetVariable(string name, object value) { }
        }
    }
}
