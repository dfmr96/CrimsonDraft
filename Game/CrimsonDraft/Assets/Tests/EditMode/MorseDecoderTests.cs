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

        [Test]
        public void Confirm_DotDash_AddsLetterA()
        {
            _decoder.InputDot();
            _decoder.InputDash();
            _decoder.Confirm();
            Assert.AreEqual(1, _decoder.Word.Count);
            Assert.AreEqual('A', _decoder.Word[0]);
        }

        [Test]
        public void Confirm_ClearsCurrentSequence()
        {
            _decoder.InputDot();
            _decoder.InputDash();
            _decoder.Confirm();
            Assert.AreEqual("", _decoder.CurrentSequence);
        }

        [Test]
        public void Confirm_SingleDash_AddsLetterT()
        {
            _decoder.InputDash();
            _decoder.Confirm();
            Assert.AreEqual('T', _decoder.Word[0]);
        }

        [Test]
        public void Confirm_ThreeDots_AddsLetterS()
        {
            _decoder.InputDot();
            _decoder.InputDot();
            _decoder.InputDot();
            _decoder.Confirm();
            Assert.AreEqual('S', _decoder.Word[0]);
        }
    }
}
