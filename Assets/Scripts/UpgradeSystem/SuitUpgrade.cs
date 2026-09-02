// Needed for ArgumentNullException.
using System;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// Abstract root of the UPGRADE HIERARCHY.
    /// A suit upgrade is something the player switches on for a short time, paid for with
    /// oxygen seconds. This class holds the running state and reads the upgrade's numbers
    /// from a SuitUpgradeData asset, leaving the actual effect to the subclass.
    /// </summary>
    public abstract class SuitUpgrade
    {
        // The asset holding this upgrade's id, price, duration and strength. Read-only
        // reference: the numbers are tuned in the Inspector, never rewritten at runtime,
        // so one balancing pass cannot accidentally change the game mid-match.
        private readonly SuitUpgradeData m_Data;

        // False until Activate runs, true until Deactivate runs. Guards against applying
        // the same effect twice or removing an effect that was never applied.
        private bool m_IsActive;

        // Which upgrade this is. Also the key the subclasses register their multiplier
        // under, so the owning system can later remove exactly this upgrade's effect.
        public int UpgradeId => m_Data.UpgradeId;

        // What one activation costs in oxygen seconds. UpgradeController reads this to
        // decide whether the player can afford the upgrade before calling Activate.
        public float CostOxygen => m_Data.CostOxygen;

        // How long the effect stays on. This class only reports the number: it is
        // UpgradeController that counts the time down and calls Deactivate at zero.
        public float Duration => m_Data.Duration;

        // True while the effect is applied to the player. Used by the HUD and by
        // UpgradeController.
        public bool IsActive => m_IsActive;

        // The whole settings asset, for subclasses that need more than the four values
        // above - the two boosts read EffectMultiplier through this.
        protected SuitUpgradeData Data => m_Data;

        /// <summary>
        /// Every upgrade is built from its own settings asset, so the numbers arrive from
        /// the Inspector rather than from code.
        /// </summary>
        protected SuitUpgrade(SuitUpgradeData data)
        {
            // An upgrade with no settings has no price and no duration, which would let a
            // player buy it for free and keep it forever. Fail immediately and loudly
            // rather than let that reach a match.
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "A suit upgrade needs a SuitUpgradeData asset.");
            }

            // Keep the settings asset for the lifetime of this upgrade.
            m_Data = data;

            // A newly created upgrade is always switched off.
            m_IsActive = false;
        }

        /// <summary>
        /// Switches the upgrade on for <paramref name="player"/>.
        /// Subclasses override this, apply their own effect, and call base.Activate first
        /// so the running state is recorded in one place only.
        /// </summary>
        public virtual void Activate(IUpgradeTarget player)
        {
            // Record that the effect is now on the player.
            m_IsActive = true;
        }

        /// <summary>
        /// Switches the upgrade off again and lets the subclass undo its effect.
        /// Subclasses override this, remove their own effect, and call base.Deactivate.
        /// </summary>
        public virtual void Deactivate(IUpgradeTarget player)
        {
            // Record that the effect is no longer on the player.
            m_IsActive = false;
        }
    }
}
