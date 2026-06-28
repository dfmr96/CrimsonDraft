using NUnit.Framework;
using CrimsonDraft.Infrastructure.Scenes;

namespace CrimsonDraft.Tests
{
    public sealed class EncounterContextTests
    {
        [Test]
        public void Set_withOperatorsStartFull_storesTrue()
        {
            var ctx = new EncounterContext();
            ctx.Set("enc-01", null, operatorsStartFull: true);
            Assert.IsTrue(ctx.OperatorsStartFull);
        }

        [Test]
        public void Set_withoutAdvantage_defaultsFalse()
        {
            var ctx = new EncounterContext();
            ctx.Set("enc-01", null);
            Assert.IsFalse(ctx.OperatorsStartFull);
        }

        [Test]
        public void Set_secondCallWithoutAdvantage_resetsFlagToFalse()
        {
            var ctx = new EncounterContext();
            ctx.Set("enc-01", null, operatorsStartFull: true);
            ctx.Set("enc-02", null);
            Assert.IsFalse(ctx.OperatorsStartFull);
        }

        [Test]
        public void Set_storesEncounterId()
        {
            var ctx = new EncounterContext();
            ctx.Set("my-encounter", null);
            Assert.AreEqual("my-encounter", ctx.CurrentEncounterId);
        }
    }
}
