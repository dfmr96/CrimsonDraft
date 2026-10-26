//-----------------------------------------------------------------------
// File:    CRTHistory.cs  (CrimsonDraft addition)
//-----------------------------------------------------------------------
namespace ColbyO.VNTG.CRT
{
    /// <summary>When a camera's CRT history (the previous frame used to emulate refresh rate and
    /// interlacing) no longer matches what is on screen.</summary>
    public static class CRTHistory
    {
        /// <summary>True when the camera did not render on the previous frame (or never did): it was
        /// switched off, so its history holds an old frame and must be fully refreshed instead of shown.</summary>
        public static bool IsStale(int lastRenderedFrame, int currentFrame)
            => lastRenderedFrame < 0 || currentFrame - lastRenderedFrame > 1;
    }
}
