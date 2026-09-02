// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// Speed Boost - the player trades oxygen seconds for a burst of movement speed.
    /// On a low gravity map, closing distance fast is worth a lot, but the seconds spent
    /// are also seconds closer to suffocating.
    ///
    /// The price, the duration and the +30% all live in the SpeedBoostData asset, so the
    /// balance can be retuned in the Inspector without touching this file.
    /// </summary>
    public class SpeedBoost : SuitUpgrade
    {
        /// <summary>
        /// Builds the boost from its settings asset (SpeedBoostData).
        /// </summary>
        public SpeedBoost(SuitUpgradeData data)
            : base(data)
        {
        }

        /// <summary>
        /// Switches the boost on: registers the speed multiplier on the player's movement.
        /// </summary>
        public override void Activate(IUpgradeTarget player)
        {
            // Applying the boost twice would register the multiplier twice, while the one
            // Deactivate that follows would only take it off once - leaving the player
            // permanently faster. So a second activation does nothing.
            if (IsActive)
            {
                return;
            }

            // Let the base class record that the upgrade is now running.
            base.Activate(player);

            // Register the multiplier from the settings asset under this upgrade's id, so
            // PlayerMovement can combine it with anything else affecting speed and later
            // remove exactly this contribution.
            player.Movement.AddSpeedMultiplier(UpgradeId, Data.EffectMultiplier);
        }

        /// <summary>
        /// Switches the boost off: takes the speed multiplier back off the player.
        /// </summary>
        public override void Deactivate(IUpgradeTarget player)
        {
            // Nothing to undo if the boost was never switched on.
            if (!IsActive)
            {
                return;
            }

            // Remove only the multiplier this upgrade added, found by its own id.
            player.Movement.RemoveSpeedMultiplier(UpgradeId);

            // Let the base class record that the upgrade has stopped running.
            base.Deactivate(player);
        }
    }
}
