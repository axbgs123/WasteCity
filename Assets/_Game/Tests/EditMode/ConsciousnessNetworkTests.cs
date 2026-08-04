using NUnit.Framework;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class ConsciousnessNetworkTests
    {
        [TestCase(true, 1, true)]
        [TestCase(true, 2, true)]
        [TestCase(false, 1, false)]
        [TestCase(true, 0, false)]
        [TestCase(true, -1, false)]
        public void RemoteLinkRequiresResearchAndAnOperationalNetwork(
            bool researchCompleted,
            int operationalNetworks,
            bool expected)
        {
            Assert.That(
                ConsciousnessNetworkRules.RemoteLinkAvailable(
                    researchCompleted,
                    operationalNetworks),
                Is.EqualTo(expected));
        }
    }
}
