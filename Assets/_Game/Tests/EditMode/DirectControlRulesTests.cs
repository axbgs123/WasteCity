using NUnit.Framework;
using WasteCity.City;

namespace WasteCity.Tests
{
    public sealed class DirectControlRulesTests
    {
        [TestCase(CityMode.Mobile, true, DirectControlTarget.City)]
        [TestCase(CityMode.Deploying, true, DirectControlTarget.City)]
        [TestCase(CityMode.Fortress, true, DirectControlTarget.Leader)]
        [TestCase(CityMode.Packing, true, DirectControlTarget.City)]
        [TestCase(CityMode.Fortress, false, DirectControlTarget.City)]
        public void ControlTargetMatchesApprovedState(
            CityMode mode,
            bool recruited,
            DirectControlTarget expected)
        {
            Assert.That(
                DirectControlRules.Resolve(mode, recruited),
                Is.EqualTo(expected));
        }
    }
}
