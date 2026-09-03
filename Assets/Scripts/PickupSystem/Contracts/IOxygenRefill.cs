// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// The oxygen tank, seen from the point of view of anything that refills it.
    /// Implemented by OxygenSystem, which is another team member's class - this is the
    /// only thing OxygenTank needs to know about it.
    ///
    /// Deliberately kept apart from IOxygenBank in the upgrade system, which only takes
    /// oxygen out. OxygenSystem implements both, but a pickup has no business being able
    /// to spend the player's oxygen and an upgrade has no business being able to top it
    /// up - so each side is only handed the half it needs.
    /// </summary>
    public interface IOxygenRefill
    {
        // Adds seconds to the tank. There is no maximum: in this game oxygen is the
        // survival timer, and a tank is meant to be able to push the player past the
        // 120 seconds they started with.
        void AddSeconds(float seconds);
    }
}
