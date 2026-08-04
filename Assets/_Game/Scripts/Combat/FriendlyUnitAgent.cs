using System.Collections.Generic;
using UnityEngine;
using WasteCity.Research;

namespace WasteCity.Combat
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class FriendlyUnitAgent : MonoBehaviour
    {
        public const float GuardRadius = 9f;
        public const float LeashRadius = 13f;

        private readonly BuildingRegenerationModel regeneration = new BuildingRegenerationModel();
        private HealthComponent health;
        private Transform city;
        private FriendlyUnitCommandModel commands;
        private FriendlyUnitKind kind;
        private ResearchController research;
        private float moveSpeed;
        private float damagePerSecond;
        private DamageType damageType;
        private FriendlyTacticalProfile tacticalProfile;
        private PlaceholderEnemy currentTarget;
        private float attackRemainder;
        private bool lossReported;

        public HealthComponent Health => health;
        public FriendlyUnitKind Kind => kind;

        public void Configure(
            HealthComponent unitHealth,
            Transform cityTransform,
            FriendlyUnitCommandModel commandModel,
            FriendlyUnitKind unitKind,
            ResearchController researchController,
            float configuredMoveSpeed,
            float attackRange,
            float configuredDamagePerSecond,
            DamageType configuredDamageType,
            float arrivalTolerance)
        {
            health = unitHealth;
            city = cityTransform;
            commands = commandModel ?? new FriendlyUnitCommandModel();
            kind = unitKind;
            research = researchController;
            moveSpeed = configuredMoveSpeed;
            damagePerSecond = configuredDamagePerSecond;
            damageType = configuredDamageType;
            tacticalProfile = new FriendlyTacticalProfile(GuardRadius, LeashRadius, arrivalTolerance, attackRange);
            health.Value.Died += ReportLoss;
        }

        private void Update()
        {
            if (health == null || health.Value.IsDead || city == null) return;
            if (research != null)
                regeneration.Tick(Time.deltaTime, false, research.HasTissueRegeneration, false, health.Value, null);

            FriendlyRallyPoint rally = commands.ResolveRally(city.position.x, city.position.y);
            PlaceholderEnemy[] enemies = Object.FindObjectsOfType<PlaceholderEnemy>();
            var candidates = new FriendlyTargetCandidate[enemies.Length];
            var byId = new Dictionary<int, PlaceholderEnemy>(enemies.Length);
            for (int index = 0; index < enemies.Length; index++)
            {
                PlaceholderEnemy enemy = enemies[index];
                int id = enemy.GetInstanceID();
                candidates[index] = new FriendlyTargetCandidate(
                    id,
                    enemy.transform.position.x,
                    enemy.transform.position.y,
                    enemy.Health != null && !enemy.Health.Value.IsDead,
                    enemy.IsControlled);
                byId[id] = enemy;
            }

            int currentId = currentTarget == null ? 0 : currentTarget.GetInstanceID();
            FriendlyUnitDecision decision = FriendlyUnitTacticalRules.Decide(
                transform.position.x,
                transform.position.y,
                rally.X,
                rally.Y,
                tacticalProfile,
                candidates,
                currentId);

            if (!decision.HasTarget || !byId.TryGetValue(decision.TargetId, out currentTarget))
                currentTarget = null;

            switch (decision.Type)
            {
                case FriendlyUnitDecisionType.ReturnToRally:
                    transform.position = Vector2.MoveTowards(transform.position, new Vector2(rally.X, rally.Y), moveSpeed * Time.deltaTime);
                    break;
                case FriendlyUnitDecisionType.Chase:
                    transform.position = Vector2.MoveTowards(transform.position, currentTarget.transform.position, moveSpeed * Time.deltaTime);
                    break;
                case FriendlyUnitDecisionType.Attack:
                    Attack(currentTarget);
                    break;
            }
        }

        private void Attack(PlaceholderEnemy target)
        {
            if (target == null || target.Health == null || target.Health.Value.IsDead) return;
            attackRemainder += damagePerSecond * Time.deltaTime;
            int damage = Mathf.FloorToInt(attackRemainder);
            if (damage <= 0) return;
            target.Health.Value.Apply(damage, damageType, target.Health.Armor);
            attackRemainder -= damage;
        }

        private void ReportLoss()
        {
            if (lossReported) return;
            lossReported = true;
            commands.RecordLoss(kind);
        }
    }
}
