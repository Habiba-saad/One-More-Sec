// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// The player's weapon slot, seen from the point of view of anything that hands them
    /// a new gun. Implemented by CombatSystem, which is another team member's class.
    ///
    /// The weapon is named by its id rather than by passing a Weapon object, because
    /// Weapon belongs to the combat side of the project and this way the pickup system
    /// does not have to depend on that class at all. CombatSystem already stores a
    /// weaponId on every Weapon, so it can look the real one up.
    /// </summary>
    public interface IWeaponHolder
    {
        // Gives the player the weapon with this id, replacing whatever they are holding.
        // Replacing rather than adding is the rule for this game: the player carries one
        // weapon, so picking up a special weapon is a trade, not a collection.
        void EquipWeapon(int weaponId);
    }
}
