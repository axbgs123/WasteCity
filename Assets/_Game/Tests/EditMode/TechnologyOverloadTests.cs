using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class TechnologyOverloadTests
    {
        [Test]
        public void LockedTechnologyOverloadCannotActivate()
        {
            var model = new TechnologyOverloadModel();

            Assert.That(model.TryActivate(false), Is.False);
            Assert.That(model.Phase, Is.EqualTo(TechnologyOverloadPhase.Ready));
        }

        [Test]
        public void TechnologyOverloadBoostsThenLocksAndCoolsDown()
        {
            var model = new TechnologyOverloadModel();

            Assert.That(model.TryActivate(true), Is.True);
            Assert.That(model.FireRateMultiplier, Is.EqualTo(2f));
            Assert.That(model.DamageMultiplier(DamageType.Energy), Is.EqualTo(1.3f));
            Assert.That(model.DamageMultiplier(DamageType.Physical), Is.EqualTo(1f));
            model.Tick(5f);
            Assert.That(model.Phase, Is.EqualTo(TechnologyOverloadPhase.Lockout));
            Assert.That(model.FireRateMultiplier, Is.Zero);
            model.Tick(3f);
            Assert.That(model.Phase, Is.EqualTo(TechnologyOverloadPhase.Cooldown));
            model.Tick(22f);
            Assert.That(model.Phase, Is.EqualTo(TechnologyOverloadPhase.Ready));
        }

        [Test]
        public void LargeTickConsumesEveryPhase()
        {
            var model = new TechnologyOverloadModel();
            model.TryActivate(true);

            model.Tick(30f);

            Assert.That(model.Phase, Is.EqualTo(TechnologyOverloadPhase.Ready));
            Assert.That(model.CooldownRemaining, Is.Zero);
            Assert.That(model.BoostRemaining, Is.Zero);
            Assert.That(model.LockoutRemaining, Is.Zero);
        }

        [Test]
        public void NegativeTickDoesNotAdvanceState()
        {
            var model = new TechnologyOverloadModel();
            model.TryActivate(true);

            model.Tick(-5f);

            Assert.That(model.BoostRemaining, Is.EqualTo(5f));
            Assert.That(model.CooldownRemaining, Is.EqualTo(30f));
        }

        [Test]
        public void RepeatActivationIsRejectedDuringCooldown()
        {
            var model = new TechnologyOverloadModel();

            Assert.That(model.TryActivate(true), Is.True);
            Assert.That(model.TryActivate(true), Is.False);
        }

        [Test]
        public void RestorePreservesUnlockedStateAndClearsLockedState()
        {
            var unlocked = new TechnologyOverloadModel();
            unlocked.Restore(true, 18f, 2f, 0f);
            var locked = new TechnologyOverloadModel();
            locked.Restore(false, 18f, 2f, 1f);

            Assert.That(unlocked.CooldownRemaining, Is.EqualTo(18f));
            Assert.That(unlocked.BoostRemaining, Is.EqualTo(2f));
            Assert.That(unlocked.Phase, Is.EqualTo(TechnologyOverloadPhase.Boosting));
            Assert.That(locked.CooldownRemaining, Is.Zero);
            Assert.That(locked.BoostRemaining, Is.Zero);
            Assert.That(locked.LockoutRemaining, Is.Zero);
        }

        [Test]
        public void ModifierCompositionUsesStrongestBoostWithoutMultiplying()
        {
            Assert.That(TurretCombatModifierRules.ResolveFireRate(1.75f, 2f), Is.EqualTo(2f));
            Assert.That(TurretCombatModifierRules.ResolveFireRate(1.35f, 1f), Is.EqualTo(1.35f));
        }

        [Test]
        public void ModifierCompositionHonorsEitherLockout()
        {
            Assert.That(TurretCombatModifierRules.ResolveFireRate(0f, 2f), Is.Zero);
            Assert.That(TurretCombatModifierRules.ResolveFireRate(1.75f, 0f), Is.Zero);
        }

        [Test]
        public void ModifierDamageOnlyAppliesToEnergy()
        {
            Assert.That(TurretCombatModifierRules.ResolveDamage(DamageType.Energy, 1.3f), Is.EqualTo(1.3f));
            Assert.That(TurretCombatModifierRules.ResolveDamage(DamageType.Physical, 1.3f), Is.EqualTo(1f));
            Assert.That(TurretCombatModifierRules.ResolveDamage(DamageType.Biological, 1.3f), Is.EqualTo(1f));
        }
    }
}
