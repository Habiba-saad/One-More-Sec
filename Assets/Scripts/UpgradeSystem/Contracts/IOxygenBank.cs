// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// The oxygen tank, seen from the point of view of anything that spends oxygen.
    /// Implemented by OxygenSystem, which is another team member's class - this is the
    /// only thing UpgradeController needs to know about it.
    ///
    /// In this game oxygen is both the survival timer and the currency, so a purchase is
    /// literally the player trading seconds of life for a temporary advantage. That is
    /// also why there is no AddSeconds here: the upgrade system only ever takes oxygen,
    /// and giving it back is somebody else's job (RechargeSystem, OxygenTank).
    /// </summary>
    public interface IOxygenBank
    {
        // Returns true when the tank currently holds at least this many seconds.
        // A pure question: it must never change the tank.
        bool CanSpend(float seconds);

        // Takes the seconds out of the tank and returns true when that worked.
        // It returns a bool rather than nothing because the tank keeps draining while the
        // shop panel is open, so the amount can stop being there between the check and
        // the purchase - and then the player must not get the upgrade for free.
        bool SpendSeconds(float seconds);
    }
}
