#nullable enable

using NUnit.Framework;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class MorseDecoderTests
    {
        private MorseDecoder _decoder = null!;

        [SetUp]
        public void SetUp() => _decoder = new MorseDecoder();

        [Test]
        public void InputDot_AppendsDotToCurrentSequence()
        {
            _decoder.InputDot();
            Assert.AreEqual(".", _decoder.CurrentSequence);
        }

        [Test]
        public void InputDash_AppendsDashToCurrentSequence()
        {
            _decoder.InputDash();
            Assert.AreEqual("-", _decoder.CurrentSequence);
        }

        [Test]
        public void MultipleInputs_BuildSequenceInOrder()
        {
            _decoder.InputDot();
            _decoder.InputDash();
            _decoder.InputDot();
            Assert.AreEqual(".-.", _decoder.CurrentSequence);
        }
    }
}
