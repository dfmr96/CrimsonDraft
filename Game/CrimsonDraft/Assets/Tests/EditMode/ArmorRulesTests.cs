#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class ArmorRulesTests
    {
        [Test]
        public void IsBlocked_miss_neverBlocks() =>
            Assert.IsFalse(ArmorRules.IsBlocked(ShotZone.Miss, covered: true, OverlayKind.Armor, 0.25f));

        [Test]
        public void IsBlocked_uncovered_doesNotBlock() =>
            Assert.IsFalse(ArmorRules.IsBlocked(ShotZone.Torso, covered: false, OverlayKind.Armor, 0.25f));

        [Test]
        public void IsBlocked_noOverlay_doesNotBlock() =>
            Assert.IsFalse(ArmorRules.IsBlocked(ShotZone.Torso, covered: true, kind: null, 0.25f));

        [Test]
        public void IsBlocked_fullPenetrationWeapon_doesNotBlock() =>
            Assert.IsFalse(ArmorRules.IsBlocked(ShotZone.Torso, covered: true, OverlayKind.Armor, 1f));

        [Test]
        public void IsBlocked_coveredArmorWithReducedMultiplier_blocks() =>
            Assert.IsTrue(ArmorRules.IsBlocked(ShotZone.Torso, covered: true, OverlayKind.Armor, 0.25f));

        [Test]
        public void IsBlocked_anyBodyZoneUnderArmor_blocks() =>
            Assert.IsTrue(ArmorRules.IsBlocked(ShotZone.Arms, covered: true, OverlayKind.Armor, 0.5f));

        [Test]
        public void WeaponData_defaultArmorDamageMultiplier_isQuarter()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            Assert.AreEqual(0.25f, weapon.ArmorDamageMultiplier, 1e-6f);
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void MeleeWeaponData_defaultArmorDamageMultiplier_isQuarter()
        {
            var melee = ScriptableObject.CreateInstance<MeleeWeaponData>();
            Assert.AreEqual(0.25f, melee.ArmorDamageMultiplier, 1e-6f);
            Object.DestroyImmediate(melee);
        }

        [Test]
        public void ResolvedShot_defaultsToNotArmorBlocked()
        {
            var shot = new ResolvedShot(0, 0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20);
            Assert.IsFalse(shot.ArmorBlocked);
        }

        [Test]
        public void ResolvedShot_carriesArmorBlocked()
        {
            var shot = new ResolvedShot(0, 0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 5, armorBlocked: true);
            Assert.IsTrue(shot.ArmorBlocked);
        }
    }
}
