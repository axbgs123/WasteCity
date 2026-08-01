using NUnit.Framework;
using WasteCity.City;

namespace WasteCity.Tests
{
    public sealed class CityOperationalTests
    {
        [Test] public void LongWorkOnlyRunsInFortressMode(){Assert.That(CityOperationalRules.LongWorkAllowed(CityMode.Mobile),Is.False);Assert.That(CityOperationalRules.LongWorkAllowed(CityMode.Deploying),Is.False);Assert.That(CityOperationalRules.LongWorkAllowed(CityMode.Fortress),Is.True);Assert.That(CityOperationalRules.LongWorkAllowed(CityMode.Packing),Is.False);}
        [Test] public void FortressProvidesCentralizedProductionAndDefenseBonus(){Assert.That(CityOperationalRules.ProductionMultiplier(CityMode.Fortress),Is.EqualTo(1.25f));Assert.That(CityOperationalRules.DefenseMultiplier(CityMode.Fortress),Is.EqualTo(1.25f));Assert.That(CityOperationalRules.ProductionMultiplier(CityMode.Mobile),Is.EqualTo(1f));}
        [Test] public void DeploymentIntermediateStateCanBeRestored(){var model=new CityDeploymentModel(3,5);model.Restore(CityMode.Packing,2.5f);Assert.That(model.Mode,Is.EqualTo(CityMode.Packing));Assert.That(model.Remaining,Is.EqualTo(2.5f));model.Tick(2.5f);Assert.That(model.Mode,Is.EqualTo(CityMode.Mobile));}
    }
}
