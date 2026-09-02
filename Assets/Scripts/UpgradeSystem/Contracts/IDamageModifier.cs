// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// Outgoing weapon damage, seen from the point of view of anything that buffs it.
    /// Implemented by CombatSystem, which is another team member's class - this is the
    /// only thing DamageBoost needs to know about it.
    ///
    /// Kept separate from IMovementModifier even though the two look alike, because the
    /// classes behind them are different: PlayerMovement has nothing to say about damage
    /// and CombatSystem has nothing to say about speed, so neither should be handed the
    /// other's methods.
    /// </summary>
    public interface IDamageModifier
    {
        // Registers a damage multiplier owned by sourceId. 1.25f means +25%.
        // Calling it again with the same id must replace the old value, not stack on it.
        void AddDamageMultiplier(int sourceId, float multiplier);

        // Removes the multiplier that sourceId registered, leaving any other source alone.
        // Must be safe to call when that source has nothing registered.
        void RemoveDamageMultiplier(int sourceId);
    }
}
