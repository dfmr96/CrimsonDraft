#nullable enable

using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NaughtyAttributes;
#endif

namespace CrimsonDraft.UI
{
    public class TestPickupItem : MonoBehaviour
    {
        [SerializeField] private ItemData           item    = null!;
        [SerializeField] private InventoryPopulator spawner = null!;

        [SerializeField] private DialogueRunner? runner;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Testing")]
        [SerializeField] private bool forceNoSpace = false;
#endif

        private bool commandRegistered;

        public void TriggerPickup()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[TestPickupItem] Debe estar en Play Mode.");
                return;
            }

            if (this.runner == null) this.runner = FindAnyObjectByType<DialogueRunner>();

            if (this.runner == null)
            {
                Debug.LogError("[TestPickupItem] DialogueRunner no encontrado.");
                return;
            }

            var storage = this.runner.VariableStorage;

            string itemName = !string.IsNullOrEmpty(item.SecondaryName) ? item.SecondaryName : item.DisplayName;
            storage.SetValue("$item_name", itemName);

            if (commandRegistered) this.runner.RemoveCommandHandler("try_pickup");
            this.runner.AddCommandHandler("try_pickup", OnTryPickup);
            commandRegistered = true;
            this.runner.onDialogueComplete.RemoveListener(OnDialogueComplete);
            this.runner.onDialogueComplete.AddListener(OnDialogueComplete);

            _ = this.runner.StartDialogue("pickup_prompt");
        }

        private void OnTryPickup()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool hasSpace = !forceNoSpace && spawner.HasSpace(item);
#else
            bool hasSpace = spawner.HasSpace(item);
#endif
            this.runner!.VariableStorage.SetValue("$pickup_success", hasSpace);
            if (hasSpace) spawner.Spawn(item);
        }

        private void OnDialogueComplete()
        {
            this.runner!.RemoveCommandHandler("try_pickup");
            commandRegistered = false;
            this.runner!.onDialogueComplete.RemoveListener(OnDialogueComplete);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Button("Trigger Pickup")]
        void EditorTrigger() => TriggerPickup();
#endif
    }
}
