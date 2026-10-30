#nullable enable

#if UNITY_EDITOR

namespace CrimsonDraft.Infrastructure.DebugMenu
{
    /// <summary>Which entry of the menu is highlighted. Keeps the index valid as entries come and go.</summary>
    public sealed class DebugMenuSelection
    {
        private readonly IDebugMenu menu;
        private int index;

        public DebugMenuSelection(IDebugMenu menu)
        {
            this.menu = menu;
        }

        public int Index
        {
            get
            {
                Clamp();
                return this.index;
            }
        }

        public DebugMenuEntry? Current
        {
            get
            {
                var entries = this.menu.Entries;
                return entries.Count == 0 ? null : entries[Index];
            }
        }

        /// <summary>Moves by <paramref name="delta"/> rows, wrapping around the ends.</summary>
        public void Move(int delta)
        {
            int count = this.menu.Entries.Count;
            if (count == 0) return;
            this.index = ((Index + delta) % count + count) % count;
        }

        public void ActivateCurrent() => Current?.Activate();

        /// <summary>Left/Right on the highlighted entry: cycles a choice, ignored by the others.</summary>
        public void CycleCurrent(int delta) => Current?.Cycle(delta);

        private void Clamp()
        {
            int count = this.menu.Entries.Count;
            if (this.index >= count) this.index = count - 1;
            if (this.index < 0) this.index = 0;
        }
    }
}

#endif
