#nullable enable

using DG.Tweening;
using UnityEngine.Rendering;

namespace CrimsonDraft.Navigation.UI
{
    // Shared show/hide fade for a Volume's weight -- any UI panel that wants to bring in a
    // post-process look while it's open (Inventory, pickup preview, Inspect, Pause menu) calls
    // this instead of duplicating the tween. Runs on unscaled time so it still animates while
    // paused (Time.timeScale == 0).
    public static class VolumeFader
    {
        public static void Fade(Volume? volume, bool show, float duration)
        {
            if (volume == null) return;

            float target = show ? 1f : 0f;
            if (show) volume.gameObject.SetActive(true);

            DOTween.Kill(volume);
            DOTween.To(
                    () => volume.weight,
                    x  => volume.weight = x,
                    target,
                    duration)
                .SetTarget(volume)
                .SetUpdate(true)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    if (!show) volume.gameObject.SetActive(false);
                });
        }
    }
}
