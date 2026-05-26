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

        private ATBSystem?          atbSystem;
        private CombatActionQueue?  actionQueue;
        private CombatOrchestrator? orchestrator;
        private bool                initialized;

        [Inject]
        public void Construct(ATBSystem atbSystem, CombatActionQueue actionQueue, CombatOrchestrator orchestrator)
        {
            this.atbSystem    = atbSystem;
            this.actionQueue  = actionQueue;
            this.orchestrator = orchestrator;
            this.initialized  = true;
        }

        private void Update()
        {
            if (!this.initialized || this.text == null) return;
            this.text.text = BuildDebugText();
        }

        private string BuildDebugText()
        {
            var sb = new StringBuilder();

            PendingAction[] pending       = this.actionQueue?.ToArray() ?? System.Array.Empty<PendingAction>();
            int             activeEnemySlot = pending.Length > 0 && pending[0].Type == PendingActionType.EnemyAttack
                                              ? pending[0].SlotIndex : -1;

            sb.AppendLine("[ATB ACTORS]");
            if (this.atbSystem != null)
            {
                foreach (ATBActorState actor in this.atbSystem.Actors)
                {
                    if (actor.IsDead) continue;
                    string kind  = actor.Config.Kind == ATBActorKind.Operator ? "OP" : "EN";
                    string bar   = GaugeBar(actor.Gauge);
                    string state = actor.IsFrozen
                        ? "ACTING"
                        : (actor.IsReady ? (actor.IsAwaitingCommand ? "READY*" : "READY") : "FILLING");
                    string line  = $"  {kind}[{actor.Config.SlotIndex}] {bar} {actor.Gauge:P0} {state}";

                    bool isActive = actor.Config.Kind == ATBActorKind.Enemy &&
                                    actor.Config.SlotIndex == activeEnemySlot;
                    sb.AppendLine(isActive ? $"<color=#FF8C00>{line}</color>" : line);
                }
            }

            sb.AppendLine();
            sb.AppendLine("[QUEUE]");
            {
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

            if (this.orchestrator != null)
            {
                float total     = this.orchestrator.AnimationLockDuration;
                float remaining = this.orchestrator.AnimationLockRemaining;
                if (total > 0f && remaining > 0f)
                {
                    float progress = 1f - remaining / total;
                    string bar     = GaugeBar(progress);
                    string label   = pending.Length > 0 ? pending[0].Type.ToString() : "?";
                    sb.AppendLine();
                    sb.AppendLine($"[ANIM] {label}");
                    sb.AppendLine($"  {bar} {remaining:F2}s / {total:F2}s");
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
