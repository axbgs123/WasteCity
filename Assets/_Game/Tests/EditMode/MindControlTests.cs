using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class MindControlTests
    {
        [Test] public void OrdinaryLightEnemyCanBeControlledOnLowRoll(){Assert.That(MindControlModel.ShouldConvert(true,EnemyQuality.Ordinary,false,9),Is.True);Assert.That(MindControlModel.ShouldConvert(true,EnemyQuality.Ordinary,false,10),Is.False);}
        [Test] public void ResearchIsRequired(){Assert.That(MindControlModel.ShouldConvert(false,EnemyQuality.Ordinary,false,0),Is.False);}
        [Test] public void EliteAndHeavyEnemiesResistControl(){Assert.That(MindControlModel.ShouldConvert(true,EnemyQuality.Excellent,false,0),Is.False);Assert.That(MindControlModel.ShouldConvert(true,EnemyQuality.Ordinary,true,0),Is.False);}
        [Test] public void RollIsClampedToPercentRange(){Assert.That(MindControlModel.ShouldConvert(true,EnemyQuality.Ordinary,false,-5),Is.True);Assert.That(MindControlModel.ShouldConvert(true,EnemyQuality.Ordinary,false,500),Is.False);}
    }
}
