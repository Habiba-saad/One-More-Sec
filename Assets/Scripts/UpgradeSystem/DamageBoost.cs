// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// Damage Boost - the player trades oxygen seconds for extra weapon damage.
    /// Usually the most expensive of the three upgrades, because it is the one that ends
    /// rounds: it is what turns a five shot kill into a four shot kill.
    ///
    /// The price, the duration and the +25% all live in the DamageBoostData asset, so the
    /// balance can be retuned in the Inspector without touching this file.
    /// </summary>
    public class DamageBoost : SuitUpgrade
    {
        /// <summary>
        /// Builds the boost from its settings asset (DamageBoostData).
        /// </summary>
        public DamageBoost(SuitUpgradeData data)
            : base(data)
        {
        }

        /// <summary>
        /// Switches the boost on: registers the damage multiplier on the player's combat.
        /// </summary>
        public override void Activate(IUpgradeTarget player)
        {
            // Applying the boost twice would register the multiplier twice, while the one
            // Deactivate that follows would only take it off once - leaving the player
            // permanently stronger. So a second activation does nothing.
            if (IsActive)
            {
                return;
            }

            // Let the base class record that the upgrade is now running.
            base.Activate(player);

            // Register the multiplier from the settings asset under this upgrade's id.
            // CombatSystem reads it at the moment a shot is fired, so a bullet already in
            // the air keeps the damage it was fired with even if the boost expires while
            // it is still travelling.
            player.Combat.AddDamageMultiplier(UpgradeId, Data.EffectMultiplier);
        }

        /// <summary>
        /// Switches the boost off: takes the damage multiplier back off the player.
        /// </summary>
        public override void Deactivate(IUpgradeTarget player)
        {
            // Nothing to undo if the boost was never switched on.
            if (!IsActive)
            {
                return;
            }

            // Remove only the multiplier this upgrade added, found by its own id.
            player.Combat.RemoveDamageMultiplier(UpgradeId);

            // Let the base class record that the upgrade has stopped running.
            base.Deactivate(player);
        }
    }
}
