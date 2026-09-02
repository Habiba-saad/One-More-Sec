// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// Movement speed, seen from the point of view of anything that buffs it.
    /// Implemented by PlayerMovement, which is another team member's class - this is the
    /// only thing SpeedBoost needs to know about it.
    ///
    /// The methods are add/remove keyed by a source id rather than one
    /// SetSpeedMultiplier(float) on purpose: with a single setter, two effects running at
    /// the same time would fight each other and whichever expired first would reset the
    /// speed while the other was still supposed to be running.
    /// </summary>
    public interface IMovementModifier
    {
        // Registers a speed multiplier owned by sourceId. 1.3f means +30%.
        // Calling it again with the same id must replace the old value, not stack on it.
        void AddSpeedMultiplier(int sourceId, float multiplier);

        // Removes the multiplier that sourceId registered, leaving any other source alone.
        // Must be safe to call when that source has nothing registered.
        void RemoveSpeedMultiplier(int sourceId);
    }
}
