// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// Revealing an opponent on the minimap, seen from the point of view of the scan.
    /// Implemented by MapRevealSystem, which is another team member's class and is also
    /// the class that talks to MinimapController - this is the only thing PlayerScan
    /// needs to know about it.
    ///
    /// There is no "clear all reveals" method here on purpose: reveals have more than one
    /// source in this game, since RechargeSystem also reveals a player while they are
    /// recharging, and the scan must only undo the reveal it created itself.
    /// </summary>
    public interface IRevealService
    {
        // Finds the opponent closest to the scanner and reveals them on the minimap.
        // Returns true and sets revealedPlayerId when somebody was revealed.
        // Returns false when there is nobody to reveal - everyone else eliminated, or out
        // of range - and then revealedPlayerId means nothing and there is nothing to undo.
        bool TryRevealNearestOpponent(IUpgradeTarget scanner, out int revealedPlayerId);

        // Hides a player that was revealed earlier.
        // Must be safe to call for somebody already hidden or already eliminated, because
        // the scan can end after its target has left the round.
        void HidePlayer(int playerId);
    }
}
