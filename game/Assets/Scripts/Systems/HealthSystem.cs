// HealthSystem — pure C# player-HP contact/death math (gdd HP・被弾・死亡判定 P-01,P-03; S-08).
// Engine-independent (rules/unity-code.md #3): Vector3/Mathf are value types, not scene API.
// Components/HealthComponent drives this every frame: reads player/enemy world positions +
// Components/PlayerController.IsDashInvulnerable (scene state), calls the pure functions below, and
// applies the returned damage through the existing EntityState.ApplyDamage reducer (Types.cs — the
// same reducer Components/EnemyAgent already uses for enemy HP; no duplicate damage-application logic).
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class HealthSystem
    {
        /// <summary>True when the two collision circles overlap on the XZ plane (gdd 接触判定:
        /// PLAYER_COLLISION_RADIUS＋ENEMY_COLLISION_RADIUS 以内). Y is ignored — アリーナは XZ 平面
        /// (conventions.md §3).</summary>
        public static bool IsContacting(Vector3 playerPosition, Vector3 enemyPosition, float radiusSum)
        {
            float dx = playerPosition.x - enemyPosition.x;
            float dz = playerPosition.z - enemyPosition.z;
            float distanceSq = dx * dx + dz * dz;
            return distanceSq <= radiusSum * radiusSum;
        }

        /// <summary>Result of evaluating one contacting enemy's contact-damage cooldown for one frame.</summary>
        public readonly struct ContactEvaluation
        {
            public readonly bool ShouldApplyDamage;
            public readonly float CooldownRemaining;

            public ContactEvaluation(bool shouldApplyDamage, float cooldownRemaining)
            {
                ShouldApplyDamage = shouldApplyDamage;
                CooldownRemaining = cooldownRemaining;
            }
        }

        /// <summary>
        /// Per-enemy contact-damage cooldown (gdd 数値表 ENEMY_CONTACT_COOLDOWN: 「同一敵からの連続被弾間隔」
        /// — tracked per contacting enemy, not globally, so simultaneous contact from multiple enemies
        /// each ticks its own cooldown independently, matching gdd 敵・障害物「複数同時接触で一気に脅威化」).
        /// The cooldown always ticks down by <paramref name="deltaTime"/> (rule 2: delta-time 必須),
        /// regardless of contact/invulnerability, so a re-approach after losing contact doesn't get an
        /// unfair immediate hit the instant it reconnects. Damage is applied only when: currently
        /// contacting, NOT invulnerable (dash無敵窓中は被弾しない — gdd 決定), and the cooldown has fully
        /// elapsed — at which point it resets to ENEMY_CONTACT_COOLDOWN for the next interval.
        /// </summary>
        public static ContactEvaluation EvaluateContact(
            bool isContacting, float cooldownRemaining, float deltaTime, bool isInvulnerable)
        {
            float ticked = Mathf.Max(0f, cooldownRemaining - deltaTime);
            if (!isContacting || isInvulnerable || ticked > 0f)
            {
                return new ContactEvaluation(false, ticked);
            }
            return new ContactEvaluation(true, GameConfig.Enemy.ContactCooldown);
        }
    }
}
