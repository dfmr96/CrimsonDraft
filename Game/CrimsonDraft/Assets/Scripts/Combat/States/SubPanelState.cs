#nullable enable

namespace CrimsonDraft.Combat
{
    internal sealed class SubPanelState : ICombatMenuState
    {
        private readonly CombatMenuController  context;
        private readonly ISubPanelView         subPanel;

        internal SubPanelState(
            CombatMenuController  context,
            ISubPanelView         subPanel)
        {
            this.context  = context;
            this.subPanel = subPanel;
        }

        public void OnCancel()
        {
            this.subPanel.Hide();
            this.context.TransitionTo(this.context.CommandPanelState);
        }

        public void OnItemSelected(int index)
        {
            int[] indices = this.context.ReloadAmmoBoxIndices;
            if (index >= indices.Length) return;

            int op = this.context.SelectedOperator;
            this.context.Orchestrator.EnqueueAction(PendingAction.Reload(op, indices[index]));

            this.subPanel.Hide();
            this.context.TransitionTo(this.context.OperatorSelState);
        }
    }
}
