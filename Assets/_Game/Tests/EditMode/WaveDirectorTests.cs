using NUnit.Framework;
using WasteCity.Combat;
using System.Collections.Generic;
using System.Linq;

namespace WasteCity.Tests
{
    public sealed class WaveDirectorTests
    {
        [Test] public void FormalWaveCountsMatchBaseline(){Assert.That(WaveCatalog.Tutorial.TotalCount,Is.EqualTo(8));Assert.That(WaveCatalog.Directed.TotalCount,Is.EqualTo(22));Assert.That(WaveCatalog.HighRisk.TotalCount,Is.EqualTo(35));}
        [Test] public void SchedulingSameThresholdIsIdempotent(){var model=new WaveDirectorModel();Assert.That(model.Schedule(30),Is.True);Assert.That(model.Schedule(30),Is.False);}
        [Test] public void WarningMustFinishBeforeSpawning(){var model=new WaveDirectorModel();var output=new List<EnemyArchetype>();model.Schedule(30);model.Tick(59,output);Assert.That(output,Is.Empty);Assert.That(model.Phase,Is.EqualTo(WavePhase.Warning));model.Tick(4,output);Assert.That(output,Is.Not.Empty);}
        [Test] public void DirectedWaveSpawnsExactInterleavedComposition(){var model=new WaveDirectorModel();var output=new List<EnemyArchetype>();model.Schedule(30);model.Tick(200,output);Assert.That(output.Count(value=>value==EnemyArchetype.Gnawer),Is.EqualTo(18));Assert.That(output.Count(value=>value==EnemyArchetype.CrystalBeast),Is.EqualTo(4));Assert.That(model.Phase,Is.EqualTo(WavePhase.Active));}
        [Test] public void QueuedWaveWaitsForNinetyPercentClear(){var model=new WaveDirectorModel();var output=new List<EnemyArchetype>();model.Schedule(30);model.Schedule(60);model.Tick(200,output);for(int i=0;i<19;i++)Assert.That(model.RegisterDefeat(30),Is.False);Assert.That(model.Current.Trigger,Is.EqualTo(30));Assert.That(model.RegisterDefeat(30),Is.True);Assert.That(model.Current.Trigger,Is.EqualTo(60));Assert.That(model.RegisterDefeat(30),Is.False);Assert.That(model.Phase,Is.EqualTo(WavePhase.Warning));}
        [Test] public void WaveSnapshotRestoresWarningQueueAndProgress(){var model=new WaveDirectorModel();model.Schedule(30);model.Schedule(60);var output=new List<EnemyArchetype>();model.Tick(25,output);var restored=new WaveDirectorModel();restored.Restore(model.Capture());Assert.That(restored.Current.Trigger,Is.EqualTo(30));Assert.That(restored.WarningRemaining,Is.EqualTo(35));Assert.That(restored.PendingWaveCount,Is.EqualTo(1));Assert.That(restored.Schedule(30),Is.False);}
    }
}
