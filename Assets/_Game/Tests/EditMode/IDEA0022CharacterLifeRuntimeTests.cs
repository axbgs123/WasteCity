using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;

namespace WasteCity.Tests.EditMode
{
    public sealed class IDEA0022CharacterLifeRuntimeTests
    {
        [Test]
        public void ContactRescueReservesBiomassInterruptsAndRecovers()
        {
            var target = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            Assert.That(target.TryApplyDamage(1000, "combat.enemy.hit", out bool downed), Is.True);
            Assert.That(downed, Is.True);
            Assert.That(target.State, Is.EqualTo(CharacterLifeState.Downed));
            Assert.That(target.DownedRemainingSeconds, Is.EqualTo(60f));

            Assert.That(target.TryBeginRescue(
                CharacterRescueMethod.CharacterContact,
                CharacterCatalog.LinXi.Id.Value,
                2,
                out int reserved,
                out string error), Is.True, error);
            Assert.That(reserved, Is.EqualTo(2));

            CharacterLifeTickResult paused = target.Tick(
                3f,
                paused: true,
                rescueInRange: true,
                rescuerWasHit: false);
            Assert.That(paused.Kind, Is.EqualTo(CharacterLifeTickKind.None));
            Assert.That(target.RescueRemainingSeconds, Is.EqualTo(8f));

            target.Tick(3f, false, true, false);
            CharacterLifeTickResult interrupted = target.Tick(1f, false, true, true);
            Assert.That(interrupted.Kind, Is.EqualTo(CharacterLifeTickKind.RescueInterrupted));
            Assert.That(interrupted.ReleasedBiomass, Is.EqualTo(2));
            Assert.That(target.HasActiveRescue, Is.False);

            Assert.That(target.TryBeginRescue(
                CharacterRescueMethod.CharacterContact,
                CharacterCatalog.LinXi.Id.Value,
                2,
                out reserved,
                out error), Is.True, error);
            CharacterLifeTickResult rescued = target.Tick(8f, false, true, false);
            Assert.That(rescued.Kind, Is.EqualTo(CharacterLifeTickKind.RescueCompleted));
            Assert.That(rescued.ConsumedBiomass, Is.EqualTo(2));
            Assert.That(target.State, Is.EqualTo(CharacterLifeState.Recovering));
            Assert.That(target.RecoveryRemainingSeconds, Is.EqualTo(30f));

            target.Tick(30f, false, true, false);
            Assert.That(target.State, Is.EqualTo(CharacterLifeState.Active));
            Assert.That(target.CurrentHealth, Is.GreaterThan(0));
        }

        [Test]
        public void CityMedicalRescueTakesFourSecondsAndLeavingRangeInterrupts()
        {
            var target = new CharacterLifeRuntime(CharacterCatalog.HanGu);
            target.TryApplyDamage(1000, "combat.enemy.hit", out _);
            Assert.That(target.TryBeginRescue(
                CharacterRescueMethod.CityMedical,
                "core.city.000001",
                2,
                out _,
                out string error), Is.True, error);

            CharacterLifeTickResult interrupted = target.Tick(1f, false, false, false);
            Assert.That(interrupted.Kind, Is.EqualTo(CharacterLifeTickKind.RescueInterrupted));
            Assert.That(interrupted.InterruptionReason,
                Is.EqualTo(CharacterRescueInterruptionReason.LeftRange));

            Assert.That(target.TryBeginRescue(
                CharacterRescueMethod.CityMedical,
                "core.city.000001",
                2,
                out _,
                out error), Is.True, error);
            target.Tick(4f, false, true, false);
            Assert.That(target.State, Is.EqualTo(CharacterLifeState.Recovering));
        }

        [Test]
        public void DelayedRescueLeavesPermanentInjury()
        {
            var target = new CharacterLifeRuntime(CharacterCatalog.LinXi);
            target.TryApplyDamage(1000, "combat.enemy.hit", out _);
            target.Tick(31f, false, true, false);
            target.TryBeginRescue(
                CharacterRescueMethod.CityMedical,
                "core.city.000001",
                2,
                out _,
                out _);
            target.Tick(4f, false, true, false);

            Assert.That(target.HasPermanentInjury, Is.True);
            Assert.That(target.PermanentInjuryIds,
                Does.Contain(CharacterLifeRuntime.DelayedRescueInjuryId));
        }

        [Test]
        public void PermanentInjuryRequiresLessThanThirtySecondsRemaining()
        {
            var boundary = new CharacterLifeRuntime(CharacterCatalog.LinXi);
            boundary.TryApplyDamage(1000, "combat.enemy.hit", out _);
            boundary.Tick(26f, false, true, false);
            boundary.TryBeginRescue(
                CharacterRescueMethod.CityMedical,
                CharacterCatalog.MainCityId,
                2,
                out _,
                out _);
            boundary.Tick(4f, false, true, false);
            Assert.That(boundary.HasPermanentInjury, Is.False);

            var delayed = new CharacterLifeRuntime(CharacterCatalog.LinXi);
            delayed.TryApplyDamage(1000, "combat.enemy.hit", out _);
            delayed.Tick(26.01f, false, true, false);
            delayed.TryBeginRescue(
                CharacterRescueMethod.CityMedical,
                CharacterCatalog.MainCityId,
                2,
                out _,
                out _);
            delayed.Tick(4f, false, true, false);
            Assert.That(delayed.PermanentInjuryIds,
                Does.Contain("core.injury.slow-reaction"));
        }

        [Test]
        public void RescueTimeoutCreatesRecoverableCorpseAndEquipment()
        {
            var target = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            target.SetPosition("core.city.000001", 14, 9);
            target.TryApplyDamage(1000, "combat.enemy.hit", out _);

            CharacterLifeTickResult died = target.Tick(60f, false, true, false);
            Assert.That(died.Kind, Is.EqualTo(CharacterLifeTickKind.Died));
            Assert.That(target.State, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(target.Corpse, Is.Not.Null);
            Assert.That(target.Corpse.X, Is.EqualTo(14));
            Assert.That(target.Corpse.EquipmentIds, Is.Not.Empty);

            Assert.That(target.TryRecoverCorpse(out string[] recovered), Is.True);
            Assert.That(recovered, Is.Not.Empty);
            Assert.That(target.Corpse.IsRecovered, Is.True);
            Assert.That(target.TryRecoverCorpse(out _), Is.False);
        }

        [Test]
        public void DeathDuringLateRescueReleasesReservedBiomass()
        {
            var target = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            target.TryApplyDamage(1000, "combat.enemy.hit", out _);
            target.Tick(57f, false, true, false);
            target.TryBeginRescue(
                CharacterRescueMethod.CharacterContact,
                CharacterCatalog.LinXiId,
                2,
                out _,
                out _);

            CharacterLifeTickResult died = target.Tick(3f, false, true, false);
            Assert.That(died.Kind, Is.EqualTo(CharacterLifeTickKind.Died));
            Assert.That(died.ReleasedBiomass, Is.EqualTo(2));
        }

        [Test]
        public void RescueRequiresTwoBiomassAndRejectsSelfContact()
        {
            var target = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            target.TryApplyDamage(1000, "combat.enemy.hit", out _);

            Assert.That(target.TryBeginRescue(
                CharacterRescueMethod.CharacterContact,
                CharacterCatalog.LinXi.Id.Value,
                1,
                out int reserve,
                out _), Is.False);
            Assert.That(reserve, Is.Zero);
            Assert.That(target.TryBeginRescue(
                CharacterRescueMethod.CharacterContact,
                CharacterCatalog.CenJin.Id.Value,
                2,
                out _,
                out _), Is.False);
            Assert.That(ResourceIds.Biomass,
                Is.EqualTo(CharacterLifeRuntime.RescueResourceId));
        }

        [Test]
        public void CaptureRestorePreservesActiveRescueAndRejectsInvalidAtomically()
        {
            var target = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            target.SetPosition("core.city.000001", 7, 11);
            target.AdjustLoyalty(-9);
            target.TryApplyDamage(1000, "combat.enemy.hit", out _);
            target.Tick(13f, false, true, false);
            target.TryBeginRescue(
                CharacterRescueMethod.CharacterContact,
                CharacterCatalog.LinXi.Id.Value,
                2,
                out _,
                out _);
            target.Tick(2.5f, false, true, false);
            CharacterLifeSnapshot saved = target.Capture();

            target.Tick(5.5f, false, true, false);
            Assert.That(target.State, Is.EqualTo(CharacterLifeState.Recovering));
            Assert.That(target.TryRestore(saved, out string error), Is.True, error);
            Assert.That(target.State, Is.EqualTo(CharacterLifeState.Downed));
            Assert.That(target.DownedRemainingSeconds,
                Is.EqualTo(saved.DownedRemainingSeconds));
            Assert.That(target.RescueRemainingSeconds,
                Is.EqualTo(saved.Rescue.RemainingSeconds));
            Assert.That(target.Loyalty, Is.EqualTo(saved.Loyalty));
            Assert.That(target.X, Is.EqualTo(7));
            Assert.That(target.Y, Is.EqualTo(11));

            CharacterLifeSnapshot beforeInvalid = target.Capture();
            var invalid = new CharacterLifeSnapshot(
                saved.CharacterId,
                CharacterLifeState.Downed,
                0,
                saved.Loyalty,
                saved.AssignedSettlementId,
                saved.X,
                saved.Y,
                -1f,
                0f,
                saved.DownedElapsedSeconds,
                saved.DownCount,
                saved.DownedCauseId,
                saved.Rescue,
                saved.PermanentInjuryIds,
                saved.EquipmentIds,
                saved.Corpse);
            Assert.That(target.TryRestore(invalid, out _), Is.False);
            AssertCharacterSnapshotsEqual(beforeInvalid, target.Capture());
        }

        [Test]
        public void CaptureRestorePreservesRecoveryInjuryAndRecoveredCorpse()
        {
            var recovering = new CharacterLifeRuntime(CharacterCatalog.LinXi);
            recovering.TryApplyDamage(1000, "combat.enemy.hit", out _);
            recovering.Tick(31f, false, true, false);
            recovering.TryBeginRescue(
                CharacterRescueMethod.CityMedical,
                CharacterCatalog.MainCityId,
                2,
                out _,
                out _);
            recovering.Tick(4f, false, true, false);
            recovering.Tick(7f, false, true, false);
            CharacterLifeSnapshot recovery = recovering.Capture();
            recovering.Tick(23f, false, true, false);
            Assert.That(recovering.TryRestore(recovery, out string error),
                Is.True, error);
            Assert.That(recovering.State, Is.EqualTo(CharacterLifeState.Recovering));
            Assert.That(recovering.RecoveryRemainingSeconds, Is.EqualTo(23f));
            Assert.That(recovering.PermanentInjuryIds,
                Does.Contain(CharacterLifeRuntime.DelayedRescueInjuryId));

            var dead = new CharacterLifeRuntime(CharacterCatalog.HanGu);
            dead.SetPosition(string.Empty, 20, 6);
            dead.TryApplyDamage(1000, "combat.enemy.hit", out _);
            dead.Tick(60f, false, true, false);
            dead.TryRecoverCorpse(out _);
            CharacterLifeSnapshot corpse = dead.Capture();
            var restored = new CharacterLifeRuntime(CharacterCatalog.HanGu);
            Assert.That(restored.TryRestore(corpse, out error), Is.True, error);
            Assert.That(restored.State, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(restored.Corpse.IsRecovered, Is.True);
            Assert.That(restored.Corpse.EquipmentIds,
                Is.EqualTo(corpse.Corpse.EquipmentIds));
        }

        [Test]
        public void FormalCharacterContactRequiresActiveSourceWithinOnePointFive()
        {
            var target = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            var rescuer = new CharacterLifeRuntime(CharacterCatalog.LinXi);
            target.SetPosition(string.Empty, 10, 10);
            rescuer.SetPosition(string.Empty, 11, 11);
            target.TryApplyDamageAtRuleTick(
                1000,
                "combat.enemy.hit",
                4ul,
                out _);

            CharacterRescueValidity valid =
                CharacterRescueRules.EvaluateCharacterContact(
                    target,
                    rescuer,
                    5ul);
            Assert.That(valid.IsValid, Is.True);
            Assert.That(valid.Distance, Is.LessThanOrEqualTo(1.5f));
            Assert.That(target.TryBeginCharacterContactRescue(
                rescuer,
                5ul,
                2,
                out int reserve,
                out string error), Is.True, error);
            Assert.That(reserve, Is.EqualTo(2));

            CharacterLifeTickResult interrupted = target.TickFormalRescue(
                1f,
                false,
                rescuer,
                null,
                0,
                0,
                6ul);
            Assert.That(interrupted.Kind, Is.EqualTo(CharacterLifeTickKind.None));

            rescuer.SetPosition(string.Empty, 12, 10);
            interrupted = target.TickFormalRescue(
                1f,
                false,
                rescuer,
                null,
                0,
                0,
                7ul);
            Assert.That(interrupted.Kind,
                Is.EqualTo(CharacterLifeTickKind.RescueInterrupted));
            Assert.That(interrupted.InterruptionReason,
                Is.EqualTo(CharacterRescueInterruptionReason.LeftRange));
            Assert.That(interrupted.ReleasedBiomass, Is.EqualTo(2));

            var inactive = new CharacterLifeRuntime(CharacterCatalog.HanGu);
            inactive.SetPosition(string.Empty, 10, 11);
            inactive.TryApplyDamageAtRuleTick(
                1000,
                "combat.enemy.hit",
                7ul,
                out _);
            CharacterRescueValidity invalid =
                CharacterRescueRules.EvaluateCharacterContact(
                    target,
                    inactive,
                    8ul);
            Assert.That(invalid.Code,
                Is.EqualTo(CharacterRescueValidityCode.SourceNotActive));
        }

        [Test]
        public void RescuerDamageRevisionInterruptsSameTickAndRefundsReservation()
        {
            var target = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            var rescuer = new CharacterLifeRuntime(CharacterCatalog.LinXi);
            target.SetPosition(string.Empty, 3, 3);
            rescuer.SetPosition(string.Empty, 4, 3);
            target.TryApplyDamageAtRuleTick(
                1000,
                "combat.enemy.hit",
                10ul,
                out _);
            target.TryBeginCharacterContactRescue(
                rescuer,
                11ul,
                2,
                out _,
                out _);

            ulong beforeRevision = rescuer.DamageRevision;
            Assert.That(rescuer.TryApplyDamageAtRuleTick(
                1,
                "combat.enemy.hit",
                12ul,
                out _), Is.True);
            Assert.That(rescuer.DamageRevision, Is.EqualTo(beforeRevision + 1ul));
            Assert.That(rescuer.WasDamagedAtRuleTick(12ul), Is.True);

            CharacterLifeTickResult result = target.TickFormalRescue(
                1f,
                false,
                rescuer,
                null,
                0,
                0,
                12ul);
            Assert.That(result.Kind,
                Is.EqualTo(CharacterLifeTickKind.RescueInterrupted));
            Assert.That(result.InterruptionReason,
                Is.EqualTo(CharacterRescueInterruptionReason.RescuerDamaged));
            Assert.That(result.ReleasedBiomass, Is.EqualTo(2));
        }

        [Test]
        public void FormalCityMedicalRequiresTargetWithinThreeCellsAndCanComplete()
        {
            var target = new CharacterLifeRuntime(CharacterCatalog.HanGu);
            target.SetPosition(string.Empty, 20, 20);
            target.TryApplyDamageAtRuleTick(
                1000,
                "combat.enemy.hit",
                20ul,
                out _);

            Assert.That(CharacterRescueRules.EvaluateCityMedical(
                    target,
                    CharacterCatalog.MainCityId,
                    23,
                    20).IsValid,
                Is.True);
            Assert.That(target.TryBeginCityMedicalRescue(
                CharacterCatalog.MainCityId,
                23,
                20,
                2,
                out _,
                out string error), Is.True, error);
            CharacterLifeTickResult completed = target.TickFormalRescue(
                4f,
                false,
                null,
                CharacterCatalog.MainCityId,
                23,
                20,
                21ul);
            Assert.That(completed.Kind,
                Is.EqualTo(CharacterLifeTickKind.RescueCompleted));

            var tooFar = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            tooFar.SetPosition(string.Empty, 20, 20);
            tooFar.TryApplyDamageAtRuleTick(
                1000,
                "combat.enemy.hit",
                20ul,
                out _);
            CharacterRescueValidity invalid =
                CharacterRescueRules.EvaluateCityMedical(
                    tooFar,
                    CharacterCatalog.MainCityId,
                    24,
                    20);
            Assert.That(invalid.Code,
                Is.EqualTo(CharacterRescueValidityCode.CityOutOfRange));
            Assert.That(tooFar.TryBeginCityMedicalRescue(
                CharacterCatalog.MainCityId,
                24,
                20,
                2,
                out _,
                out _), Is.False);
        }

        private static void AssertCharacterSnapshotsEqual(
            CharacterLifeSnapshot expected,
            CharacterLifeSnapshot actual)
        {
            Assert.That(actual.CharacterId, Is.EqualTo(expected.CharacterId));
            Assert.That(actual.State, Is.EqualTo(expected.State));
            Assert.That(actual.CurrentHealth, Is.EqualTo(expected.CurrentHealth));
            Assert.That(actual.DownedRemainingSeconds,
                Is.EqualTo(expected.DownedRemainingSeconds));
            Assert.That(actual.RecoveryRemainingSeconds,
                Is.EqualTo(expected.RecoveryRemainingSeconds));
            Assert.That(actual.Rescue?.RemainingSeconds,
                Is.EqualTo(expected.Rescue?.RemainingSeconds));
            Assert.That(actual.PermanentInjuryIds,
                Is.EqualTo(expected.PermanentInjuryIds));
            Assert.That(actual.DamageRevision,
                Is.EqualTo(expected.DamageRevision));
            Assert.That(actual.LastDamageRuleTick,
                Is.EqualTo(expected.LastDamageRuleTick));
        }
    }
}
