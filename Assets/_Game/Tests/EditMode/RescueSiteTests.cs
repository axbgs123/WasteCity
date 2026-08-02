using NUnit.Framework;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class RescueSiteTests
    {
        [Test] public void RescueSitesAreDeterministicAndUnique()
        { var a=new RescueSiteModel(32,24,new WorldSeed(8128));var b=new RescueSiteModel(32,24,new WorldSeed(8128));Assert.That(a.Sites.Count,Is.EqualTo(5));for(int i=0;i<5;i++){Assert.That(a.Sites[i].X,Is.EqualTo(b.Sites[i].X));Assert.That(a.Sites[i].Y,Is.EqualTo(b.Sites[i].Y));}Assert.That(a.Sites[0].X==a.Sites[1].X&&a.Sites[0].Y==a.Sites[1].Y,Is.False);}
        [Test] public void CompletedSitesAreIdempotentAndPersisted()
        { var a=new RescueSiteModel(32,24,new WorldSeed(5));Assert.That(a.Sites[0].Complete(),Is.True);Assert.That(a.Sites[0].Complete(),Is.False);var state=a.Capture();var b=new RescueSiteModel(32,24,new WorldSeed(5));b.Restore(state);Assert.That(b.Sites[0].Completed,Is.True);}
        [Test] public void RemoteCommunicationOnlySelectsRevealedIncompleteSite(){var model=new RescueSiteModel(32,24,new WorldSeed(5));model.Sites[0].Complete();Assert.That(model.FindFirstIncomplete(site=>site==model.Sites[2]),Is.EqualTo(2));Assert.That(model.FindFirstIncomplete(_=>false),Is.EqualTo(-1));}
        [Test] public void RemoteCommunicationWaivesDelayedRescueCost(){Assert.That(RescueRules.BiomassCost(false,false),Is.EqualTo(2));Assert.That(RescueRules.BiomassCost(false,true),Is.Zero);Assert.That(RescueRules.BiomassCost(true,true),Is.EqualTo(5));}
    }
}
