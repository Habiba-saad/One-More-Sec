// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// The player, as far as a pickup needs to know about them.
    ///
    /// In the class diagram OnCollected() takes a PlayerController. Naming that class here
    /// would drag the whole player - input, netcode, animation - into the pickup folder,
    /// and would stop the pickups from compiling until somebody else finishes writing it.
    /// So the pickups depend on this small interface instead, and PlayerController
    /// implements it with three one-line properties returning its own sub-systems.
    ///
    /// Each property is the abstraction of exactly one sub-system in the diagram:
    ///     Health  -> HealthSystem   (MedKit, LargeMedKit)
    ///     Oxygen  -> OxygenSystem   (OxygenTank)
    ///     Weapons -> CombatSystem   (SpecialWeapon)
    /// </summary>
    public interface IPickupTarget
    {
        // The player's HealthSystem, seen only as "something that can be healed".
        IHealthPool Health { get; }

        // The player's OxygenSystem, seen only as "something that can be topped up".
        //
        // Named OxygenRefill rather than Oxygen because the upgrade system already asks
        // the player for an "Oxygen" of a different type (IOxygenBank, which spends rather
        // than refills). PlayerController implements both interfaces, and C# would force
        // it into awkward explicit implementations if the two properties shared a name.
        IOxygenRefill OxygenRefill { get; }

        // The player's CombatSystem, seen only as "something that can be handed a gun".
        IWeaponHolder Weapons { get; }
    }
}
