#nullable enable

using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Decides how many pellet positions a single fired bullet produces, and where each is
    // placed in aim-space, before zone-sampling/damage happens. Selected once per weapon
    // configuration (by GunType), not per bullet - see AimViewController.ConfigureWeapon.
    public interface IShotResolutionStrategy
    {
        Vector2[] GetPelletLocalPositions(
            Vector2 confirmedLocalPos,
            Vector2 firstShotLocal,
            int bulletIndex,
            float perBulletYOffset,
            BurstPatternData? burstPattern,
            int pelletCount);
    }
}
