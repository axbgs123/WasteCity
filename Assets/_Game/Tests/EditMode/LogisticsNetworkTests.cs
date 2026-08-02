using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Population;
using UnityEngine;

namespace WasteCity.Tests
{
    public sealed class LogisticsNetworkTests
    {
        [Test] public void BuildingWithinEightCellsConnectsToCore(){var m=new LogisticsNetworkModel();m.Rebuild(new[]{new LogisticsPoint("a",16,6)});Assert.That(m.IsConnected("a"),Is.True);}
        [Test] public void ChainExtendsLogisticsBeyondCoreRange(){var m=new LogisticsNetworkModel();m.Rebuild(new[]{new LogisticsPoint("a",16,6),new LogisticsPoint("b",24,6)});Assert.That(m.IsConnected("b"),Is.True);}
        [Test] public void GapLargerThanEightCellsDisconnectsBuilding(){var m=new LogisticsNetworkModel();m.Rebuild(new[]{new LogisticsPoint("a",17,6)});Assert.That(m.IsConnected("a"),Is.False);}
        [Test] public void DisconnectedIslandDoesNotBecomeConnectedInternally(){var m=new LogisticsNetworkModel();m.Rebuild(new[]{new LogisticsPoint("a",20,20),new LogisticsPoint("b",24,20)});Assert.That(m.IsConnected("a"),Is.False);Assert.That(m.IsConnected("b"),Is.False);}
        [Test] public void RuntimeRangeExpansionReconnectsDistantBuilding(){var m=new LogisticsNetworkModel();var points=new[]{new LogisticsPoint("a",20,6)};m.Rebuild(points);Assert.That(m.IsConnected("a"),Is.False);m.SetRange(12);m.Rebuild(points);Assert.That(m.IsConnected("a"),Is.True);}
        [Test] public void DisconnectingCompletedHousingSuspendsCapacityEffect()
        {var services=new GameObject("services");var economy=services.AddComponent<FormalEconomyController>();var population=services.AddComponent<FormalPopulationController>();var item=new GameObject("housing");item.AddComponent<HealthComponent>();var runtime=item.AddComponent<BuildingRuntime>();runtime.Configure(BuildingCatalog.All[1],economy,population,population);runtime.RestoreState(250,0);Assert.That(population.Model.Capacity,Is.EqualTo(200));runtime.SetLogistics(false);Assert.That(population.Model.Capacity,Is.EqualTo(150));runtime.SetLogistics(true);Assert.That(population.Model.Capacity,Is.EqualTo(200));Object.DestroyImmediate(item);Object.DestroyImmediate(services);}
    }
}
