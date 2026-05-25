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

        [Test]
        public void Confirm_EmptySequence_DoesNothing()
        {
            _decoder.Confirm();
            Assert.AreEqual(0, _decoder.Word.Count);
            Assert.AreEqual("", _decoder.CurrentSequence);
        }

        [Test]
        public void Confirm_InvalidSequence_DoesNothing()
        {
            _decoder.InputDot();
            _decoder.InputDot();
            _decoder.InputDot();
            _decoder.InputDot();
            _decoder.InputDot();
            _decoder.Confirm();
            Assert.AreEqual(0, _decoder.Word.Count);
            Assert.AreEqual(".....", _decoder.CurrentSequence);
        }

        [Test]
        public void Confirm_MultipleLetters_BuildsWord()
        {
            _decoder.InputDot();
            _decoder.Confirm();
            _decoder.InputDash();
            _decoder.InputDot();
            _decoder.Confirm();
            Assert.AreEqual(2, _decoder.Word.Count);
            Assert.AreEqual('E', _decoder.Word[0]);
            Assert.AreEqual('N', _decoder.Word[1]);
        }

        [Test]
        public void GetWord_ReturnsConfirmedLettersAsString()
        {
            _decoder.InputDot();
            _decoder.Confirm();
            _decoder.InputDash();
            _decoder.InputDot();
            _decoder.Confirm();
            Assert.AreEqual("EN", _decoder.GetWord());
        }

        [Test]
        public void Backspace_WithCurrentSequenceNonEmpty_ClearsCurrentSequence()
        {
            _decoder.InputDot();
            _decoder.InputDot();
            _decoder.Backspace();
            Assert.AreEqual("", _decoder.CurrentSequence);
            Assert.AreEqual(0, _decoder.Word.Count);
        }

        [Test]
        public void Backspace_WithCurrentSequenceEmpty_RemovesLastWordLetter()
        {
            _decoder.InputDot();
            _decoder.Confirm();
            _decoder.InputDash();
            _decoder.Confirm();
            _decoder.Backspace();
            Assert.AreEqual("E", _decoder.GetWord());
        }

        [Test]
        public void Backspace_AllEmpty_DoesNothing()
        {
            Assert.DoesNotThrow(() => _decoder.Backspace());
            Assert.AreEqual("", _decoder.CurrentSequence);
            Assert.AreEqual("", _decoder.GetWord());
        }

        [Test]
        public void Reset_ClearsCurrentSequenceAndWord()
        {
            _decoder.InputDot();
            _decoder.InputDash();
            _decoder.Confirm();
            _decoder.InputDot();
            _decoder.Reset();
            Assert.AreEqual("", _decoder.CurrentSequence);
            Assert.AreEqual("", _decoder.GetWord());
        }
    }
}
