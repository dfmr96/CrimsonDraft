#nullable enable

using System;

namespace CrimsonDraft.Infrastructure
{
    /// <summary>
    /// Marks "the world is being put back the way a save / earlier visit left it", as opposed to the
    /// player doing it now. Something that replays effects on load (a solved beeper receiver re-firing
    /// onCodeAccepted) wraps the replay in <see cref="Begin"/>; effects check <see cref="IsActive"/> and
    /// jump to their end state instead of animating (a safe door simply open). Main thread only.
    /// </summary>
    public static class WorldRestore
    {
        private static int depth;

        public static bool IsActive => depth > 0;

        public static IDisposable Begin()
        {
            depth++;
            return new Scope();
        }

        private sealed class Scope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (this.disposed) return;
                this.disposed = true;
                depth--;
            }
        }
    }
}
