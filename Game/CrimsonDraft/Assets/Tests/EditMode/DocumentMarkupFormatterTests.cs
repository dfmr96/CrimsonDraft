#nullable enable

using NUnit.Framework;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Tests
{
    public sealed class DocumentMarkupFormatterTests
    {
        [Test]
        public void Format_whenKnownMarker_wrapsTextWithPaletteStyle()
        {
            var palette = ScriptableObject.CreateInstance<MarkupPalette>();
            palette.BasicMarkers.Add(new MarkupPalette.BasicMarker
            {
                Marker = "place",
                CustomColor = true,
                Color = new Color(0.58f, 0.68f, 0.78f, 1f)
            });

            string result = DocumentMarkupFormatter.Format(
                "A sign reads [place]Kitchen[/place].",
                palette);

            Assert.AreEqual("A sign reads <color=#94ADC7FF>Kitchen</color>.", result);
        }

        [Test]
        public void Format_whenUnknownMarker_leavesTextUnchanged()
        {
            var palette = ScriptableObject.CreateInstance<MarkupPalette>();

            string input = "Use the [unknown]strange key[/unknown].";
            string result = DocumentMarkupFormatter.Format(input, palette);

            Assert.AreEqual(input, result);
        }

        [Test]
        public void Format_whenPaletteMissing_leavesTextUnchanged()
        {
            string input = "A [place]room[/place].";

            string result = DocumentMarkupFormatter.Format(input, null);

            Assert.AreEqual(input, result);
        }
    }
}
