#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;

namespace CrimsonDraft.Tests
{
    public sealed class SaveSlotNavigatorTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var view in UnityEngine.Object.FindObjectsByType<SaveSlotListView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            foreach (var row in UnityEngine.Object.FindObjectsByType<SaveSlotRow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(row.gameObject);
        }

        private static SaveSlotListView MakeView()
        {
            var rowGo = new GameObject("SaveSlotRow");
            var rowLabelGo = new GameObject("Label");
            rowLabelGo.transform.SetParent(rowGo.transform);
            var rowLabel = rowLabelGo.AddComponent<TMPro.TextMeshProUGUI>();
            var rowScript = rowGo.AddComponent<SaveSlotRow>();
            var rowSo = new UnityEditor.SerializedObject(rowScript);
            rowSo.FindProperty("label").objectReferenceValue = rowLabel;
            rowSo.ApplyModifiedPropertiesWithoutUndo();

            var go = new GameObject("SaveSlotListView");
            var view = go.AddComponent<SaveSlotListView>();
            var confirmLabelGo = new GameObject("ConfirmLabel");
            confirmLabelGo.transform.SetParent(go.transform);
            var confirmLabel = confirmLabelGo.AddComponent<TMPro.TextMeshProUGUI>();

            var so = new UnityEditor.SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = go;
            so.FindProperty("slotListParent").objectReferenceValue = go.transform;
            so.FindProperty("slotRowPrefab").objectReferenceValue = rowScript;
            so.FindProperty("confirmPanel").objectReferenceValue = go;
            so.FindProperty("confirmLabel").objectReferenceValue = confirmLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static List<SaveSlotSummary> MakeSlots(params bool[] emptyFlags)
        {
            var list = new List<SaveSlotSummary>();
            for (int i = 0; i < emptyFlags.Length; i++)
                list.Add(new SaveSlotSummary { slot = i, isEmpty = emptyFlags[i] });
            return list;
        }

        [Test]
        public void HandleNavigate_movesCursorForwardAndWraps()
        {
            var view = MakeView();
            int? confirmed = null;
            var nav = new SaveSlotNavigator(view, "Save to", slot => confirmed = slot);
            nav.Open(MakeSlots(true, true, true));

            nav.HandleNavigate(new Vector2(0, -1)); // down -> next
            nav.HandleNavigate(new Vector2(0, -1));
            nav.HandleNavigate(new Vector2(0, -1)); // wraps back to 0

            nav.HandleConfirm();
            nav.HandleConfirm();

            Assert.AreEqual(0, confirmed);
        }

        [Test]
        public void HandleConfirm_onFirstPress_entersConfirmState_withoutInvokingCallback()
        {
            var view = MakeView();
            bool invoked = false;
            var nav = new SaveSlotNavigator(view, "Save to", _ => invoked = true);
            nav.Open(MakeSlots(true));

            nav.HandleConfirm();

            Assert.IsFalse(invoked, "first confirm should only show the confirmation panel");
        }

        [Test]
        public void HandleConfirm_onSecondPress_invokesCallback_andCloses()
        {
            var view = MakeView();
            int? confirmedSlot = null;
            var nav = new SaveSlotNavigator(view, "Save to", slot => confirmedSlot = slot);
            nav.Open(MakeSlots(true, true));
            nav.HandleNavigate(new Vector2(0, -1)); // cursor -> slot 1

            nav.HandleConfirm();
            nav.HandleConfirm();

            Assert.AreEqual(1, confirmedSlot);
            Assert.IsFalse(nav.IsOpen);
        }

        [Test]
        public void HandleBack_duringConfirm_returnsToListWithoutInvokingCallback()
        {
            var view = MakeView();
            bool invoked = false;
            var nav = new SaveSlotNavigator(view, "Save to", _ => invoked = true);
            nav.Open(MakeSlots(true));

            nav.HandleConfirm(); // enters confirm state
            nav.HandleBack();    // cancels back to list

            Assert.IsFalse(invoked);
            Assert.IsTrue(nav.IsOpen, "back during confirm should not close the whole navigator");
        }

        [Test]
        public void HandleBack_onList_closesNavigator()
        {
            var view = MakeView();
            var nav = new SaveSlotNavigator(view, "Save to", _ => { });
            nav.Open(MakeSlots(true));

            nav.HandleBack();

            Assert.IsFalse(nav.IsOpen);
        }

        [Test]
        public void HandleConfirm_whenCanConfirmReturnsFalse_doesNothing()
        {
            var view = MakeView();
            bool invoked = false;
            var nav = new SaveSlotNavigator(view, "Load", _ => invoked = true, canConfirm: summary => !summary.isEmpty);
            nav.Open(MakeSlots(true)); // slot 0 is empty

            nav.HandleConfirm();
            nav.HandleConfirm();

            Assert.IsFalse(invoked, "confirming an empty slot in Load mode must be a no-op");
            Assert.IsTrue(nav.IsOpen, "navigator should remain open, not silently close");
        }

        [Test]
        public void Close_invokesOnClosedCallback()
        {
            var view = MakeView();
            bool closed = false;
            var nav = new SaveSlotNavigator(view, "Save to", _ => { }, onClosed: () => closed = true);
            nav.Open(MakeSlots(true));

            nav.HandleBack();

            Assert.IsTrue(closed);
        }

        [Test]
        public void HandleNavigate_whenNotOpen_doesNothing()
        {
            var view = MakeView();
            var nav = new SaveSlotNavigator(view, "Save to", _ => { });

            // Not opened -- should not throw despite no slots being set.
            Assert.DoesNotThrow(() => nav.HandleNavigate(new Vector2(0, -1)));
        }
    }
}
