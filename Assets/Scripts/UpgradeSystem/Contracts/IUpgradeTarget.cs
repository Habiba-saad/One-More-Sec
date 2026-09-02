// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// The player, as far as the upgrade hierarchy needs to know about them.
    ///
    /// In the class diagram Activate(player) takes a PlayerController. Naming that class
    /// here would drag the whole player - input, netcode, animation - into this folder,
    /// and would stop the upgrades from compiling until somebody else finishes writing it.
    /// So the upgrades depend on this small interface instead, and PlayerController
    /// implements it with three one-line properties returning its own sub-systems.
    /// </summary>
    public interface IUpgradeTarget
    {
        // Which player this is. PlayerScan needs it so MapRevealSystem can tell the
        // scanning player apart from the opponents it is searching through.
        int PlayerId { get; }

        // The player's PlayerMovement, seen only as "something whose speed can be changed".
        // Used by SpeedBoost.
        IMovementModifier Movement { get; }

        // The player's CombatSystem, seen only as "something whose damage can be changed".
        // Used by DamageBoost.
        IDamageModifier Combat { get; }

        // The player's MapRevealSystem, seen only as "something that can reveal and hide
        // players on the minimap". Used by PlayerScan.
        IRevealService Reveal { get; }
    }
}
