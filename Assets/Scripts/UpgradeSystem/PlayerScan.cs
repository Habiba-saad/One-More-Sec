// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// Player Scan - the player trades oxygen seconds to see the nearest opponent on the
    /// minimap for a few seconds. Unlike the two boosts it does not change a stat, it
    /// changes what the player can see: the cheapest way to find somebody when the round
    /// is running down.
    ///
    /// The price and the scan duration live in the PlayerScanData asset. That asset's
    /// EffectMultiplier is ignored here, because a scan either finds somebody or it does
    /// not - there is no strength to scale.
    /// </summary>
    public class PlayerScan : SuitUpgrade
    {
        // Id value that means "this scan is not showing anybody".
        private const int NoPlayerRevealed = -1;

        // The opponent this scan revealed. Remembered so that Deactivate can hide exactly
        // that one player again instead of clearing every reveal on the map.
        private int m_RevealedPlayerId = NoPlayerRevealed;

        /// <summary>
        /// Builds the scan from its settings asset (PlayerScanData).
        /// </summary>
        public PlayerScan(SuitUpgradeData data)
            : base(data)
        {
        }

        /// <summary>
        /// Runs the scan: finds the closest opponent and reveals them on the minimap.
        /// </summary>
        public override void Activate(IUpgradeTarget player)
        {
            // Scanning again while a scan is already running would overwrite the stored id
            // and leave the first revealed opponent visible forever. So it does nothing.
            if (IsActive)
            {
                return;
            }

            // Let the base class record that the upgrade is now running.
            base.Activate(player);

            // Ask MapRevealSystem for the opponent nearest to this player and reveal them.
            // The target is picked once, here, and not re-checked every frame: a scan that
            // silently jumped to a new opponent as people moved would turn a short
            // purchase into a permanent tracker.
            if (!player.Reveal.TryRevealNearestOpponent(player, out m_RevealedPlayerId))
            {
                // Nobody was in range - remember that there is nothing to hide later. The
                // oxygen is still spent: the player paid to look, not to be guaranteed a
                // target.
                m_RevealedPlayerId = NoPlayerRevealed;
            }
        }

        /// <summary>
        /// Ends the scan: hides the opponent it revealed.
        ///
        /// The class diagram shows only Activate() on PlayerScan because the scan is the
        /// one upgrade with no stat to roll back. It still has to end when its duration
        /// runs out, so Deactivate is overridden here rather than left doing nothing.
        /// </summary>
        public override void Deactivate(IUpgradeTarget player)
        {
            // Nothing to undo if the scan was never run.
            if (!IsActive)
            {
                return;
            }

            // Only hide somebody if the scan actually found a target.
            if (m_RevealedPlayerId != NoPlayerRevealed)
            {
                // Hide only the player this scan revealed. RechargeSystem also reveals
                // players while they recharge, and clearing every reveal on the map here
                // would cancel a reveal that is not ours to cancel.
                player.Reveal.HidePlayer(m_RevealedPlayerId);

                // Forget the target so a later scan starts from a clean state.
                m_RevealedPlayerId = NoPlayerRevealed;
            }

            // Let the base class record that the upgrade has stopped running.
            base.Deactivate(player);
        }
    }
}
