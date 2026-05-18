#nullable enable

#if UNITY_EDITOR || DEBUG_COMBAT

using System.Text;
using TMPro;
using UnityEngine;
using VContainer;

namespace CrimsonDraft.Combat
{
    public sealed class CombatDebugView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI? text;

        private ATBSystem?         atbSystem;
        private CombatActionQueue? actionQueue;
        private bool               initialized;

        [Inject]
        public void Construct(ATBSystem atbSystem, CombatActionQueue actionQueue)
        {
            this.atbSystem   = atbSystem;
            this.actionQueue = actionQueue;
            this.initialized = true;
        }

        private void Update()
        {
            if (!this.initialized || this.text == null) return;
            this.text.text = BuildDebugText();
        }

        private string BuildDebugText()
        {
            var sb = new StringBuilder();

            sb.AppendLine("[ATB ACTORS]");
            if (this.atbSystem != null)
            {
                foreach (ATBActorState actor in this.atbSystem.Actors)
                {
                    if (actor.IsDead) continue;
                    string kind  = actor.Config.Kind == ATBActorKind.Operator ? "OP" : "EN";
                    string bar   = GaugeBar(actor.Gauge);
                    string state = actor.IsReady
                        ? (actor.IsAwaitingCommand ? "READY*" : "READY")
                        : "FILLING";
                    sb.AppendLine($"  {kind}[{actor.Config.SlotIndex}] {bar} {actor.Gauge:P0} {state}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("[QUEUE]");
            if (this.actionQueue != null)
            {
                PendingAction[] pending = this.actionQueue.ToArray();
                if (pending.Length == 0)
                {
                    sb.AppendLine("  (empty)");
                }
                else
                {
                    for (int i = 0; i < pending.Length; i++)
                    {
                        PendingAction a      = pending[i];
                        string        prefix = i == 0 ? "► " : "  ";
                        sb.AppendLine($"  {prefix}[{i}] {a.Type} slot={a.SlotIndex}");
                    }
                }
            }

            return sb.ToString();
        }

        private static string GaugeBar(float gauge)
        {
            const int width  = 10;
            int       filled = Mathf.RoundToInt(gauge * width);
            return new string('█', filled) + new string('░', width - filled);
        }
    }
}

#endif
