#nullable enable

using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class LookableTests
    {
        [Test]
        public void LookPosition_withNoOffset_returnsObjectWorldPosition()
        {
            var go       = new GameObject();
            var lookable = go.AddComponent<Lookable>();
            go.transform.position = new Vector3(1f, 2f, 3f);

            Assert.AreEqual(go.transform.position, lookable.LookPosition);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void LookPosition_withLocalOffset_returnsWorldPositionWithOffset()
        {
            var go       = new GameObject();
            var lookable = go.AddComponent<Lookable>();
            go.transform.position = new Vector3(1f, 0f, 0f);

            var so = new SerializedObject(lookable);
            so.FindProperty("offset").vector3Value = new Vector3(0f, 1f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(new Vector3(1f, 1f, 0f), lookable.LookPosition);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void LookPosition_withRotatedParent_returnsCorrectWorldPosition()
        {
            var go       = new GameObject();
            var lookable = go.AddComponent<Lookable>();
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var so = new SerializedObject(lookable);
            so.FindProperty("offset").vector3Value = new Vector3(1f, 0f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();

            var expected = go.transform.TransformPoint(new Vector3(1f, 0f, 0f));
            Assert.AreEqual(expected, lookable.LookPosition);

            Object.DestroyImmediate(go);
        }
    }
}
