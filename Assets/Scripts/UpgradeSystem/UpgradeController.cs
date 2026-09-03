// Needed for ArgumentNullException.
using System;

// Needed for List and IReadOnlyList.
using System.Collections.Generic;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// One player's suit upgrades: what they are allowed to buy, what is running right
    /// now, and the rules for moving an upgrade between those two lists.
    ///
    /// The controller never asks what kind of upgrade it is holding - no type checks, no
    /// switch on the id, it only ever sees a SuitUpgrade. That is what lets a fourth
    /// upgrade be added later without a single line changing in this file.
    ///
    /// It is plain C# and not a MonoBehaviour: it does not sit on a GameObject and does
    /// not want Unity's callbacks. PlayerController is the MonoBehaviour that owns one of
    /// these and feeds it deltaTime from its own Update.
    /// </summary>
    public class UpgradeController
    {
        /// <summary>
        /// One upgrade that is currently running, together with the time it has left.
        ///
        /// The countdown is kept here and not on SuitUpgrade on purpose: the class
        /// diagram gives SuitUpgrade a duration but no timer, so the upgrades stay pure
        /// descriptions of an effect and the controller is the only thing that watches
        /// the clock.
        /// </summary>
        private struct RunningUpgrade
        {
            // The upgrade that is switched on.
            public SuitUpgrade Upgrade;

            // How many seconds are left before it wears off.
            public float RemainingSeconds;
        }

        // The player these upgrades belong to, and the object handed to Activate and
        // Deactivate. Set once at construction and never swapped, because an upgrade must
        // always be removed from the same player it was applied to.
        private readonly IUpgradeTarget m_Player;

        // The shop screen. Allowed to be null: a headless server still runs the purchase
        // rules but has no UI to show.
        private readonly IUpgradeShopView m_ShopView;

        // Everything this player is allowed to buy. Built once at construction.
        private readonly List<SuitUpgrade> m_AvailableUpgrades;

        // Everything running on this player right now, with each one's remaining time.
        private readonly List<RunningUpgrade> m_ActiveUpgrades = new List<RunningUpgrade>();

        // Read-only view of the catalogue, for the shop UI to draw buttons from.
        public IReadOnlyList<SuitUpgrade> AvailableUpgrades => m_AvailableUpgrades;

        // How many upgrades are running. The HUD walks the list with the two getters
        // below rather than being handed the internal list to poke at.
        public int ActiveUpgradeCount => m_ActiveUpgrades.Count;

        /// <param name="player">The player who owns these upgrades.</param>
        /// <param name="availableUpgrades">
        /// This player's own upgrade objects. Each one remembers whether it is running, so
        /// two players must never be given the same instance.
        /// </param>
        /// <param name="shopView">The upgrade panel, or null when there is no UI.</param>
        public UpgradeController(IUpgradeTarget player, IEnumerable<SuitUpgrade> availableUpgrades, IUpgradeShopView shopView = null)
        {
            // Without a player there is nobody to charge and nobody to apply effects to.
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            // A null catalogue is a wiring mistake: an empty one is how you say "this
            // player can buy nothing".
            if (availableUpgrades == null)
            {
                throw new ArgumentNullException(nameof(availableUpgrades));
            }

            // Remember who these upgrades belong to.
            m_Player = player;

            // Copy the catalogue into our own list, so whoever passed it in cannot change
            // what this player is allowed to buy after the fact.
            m_AvailableUpgrades = new List<SuitUpgrade>(availableUpgrades);

            // Keep the panel, which is allowed to be null.
            m_ShopView = shopView;
        }

        /// <summary>
        /// Shows the upgrade shop to the player.
        /// </summary>
        public void OpenShop()
        {
            // Silently do nothing when there is no UI, so the same controller can run on
            // the server without a special case at every call site.
            if (m_ShopView != null)
            {
                m_ShopView.OpenUpgradePanel();
            }
        }

        /// <summary>
        /// True when the tank currently holds this upgrade's price. The shop uses it to
        /// grey out what the player cannot afford; the real check still happens inside
        /// PurchaseUpgrade, because oxygen keeps draining while the panel is open.
        /// </summary>
        public bool CanAfford(SuitUpgrade upgrade)
        {
            // Asking about nothing is a programming mistake, not a "no".
            if (upgrade == null)
            {
                throw new ArgumentNullException(nameof(upgrade));
            }

            // No tank means nothing can be afforded. Ask the tank whether the price fits.
            return m_Player.Oxygen != null && m_Player.Oxygen.CanSpend(upgrade.CostOxygen);
        }

        /// <summary>
        /// Charges the player and switches the upgrade on. Returns false when the purchase
        /// did not happen - not on the list, already running, or not enough oxygen - and
        /// in every one of those cases nothing was charged.
        ///
        /// The class diagram shows this method returning nothing. It returns a bool here
        /// because the shop button has to know whether to play the "bought" feedback or
        /// the "denied" feedback, and it should not have to re-run the rules to find out.
        /// </summary>
        public bool PurchaseUpgrade(SuitUpgrade upgrade)
        {
            // Buying nothing is a programming mistake.
            if (upgrade == null)
            {
                throw new ArgumentNullException(nameof(upgrade));
            }

            // Refuse anything that is not in this player's catalogue, so a stale UI button
            // or a bad network message cannot grant an upgrade the player never had.
            if (!m_AvailableUpgrades.Contains(upgrade))
            {
                return false;
            }

            // Refuse to re-buy something already running. Charging again would burn
            // oxygen for almost no extra uptime, which is harsh when the oxygen is also
            // the player's life.
            if (upgrade.IsActive)
            {
                return false;
            }

            // A player with no tank cannot be charged at all - that is a broken prefab,
            // not a failed purchase, so it should be loud.
            if (m_Player.Oxygen == null)
            {
                throw new InvalidOperationException("UpgradeController cannot charge a player with no oxygen tank.");
            }

            // Take the oxygen first. If the tank drained while the panel was open the
            // withdrawal fails here, and the player does not walk away with a free boost.
            if (!m_Player.Oxygen.SpendSeconds(upgrade.CostOxygen))
            {
                return false;
            }

            // Paid for, so switch it on.
            ActivateUpgrade(upgrade);

            // Tell the caller the purchase went through.
            return true;
        }

        /// <summary>
        /// Switches an upgrade on without charging for it. Kept separate from
        /// PurchaseUpgrade so that paying and applying stay two decisions - a future
        /// pickup or a debug key can grant an effect without touching the tank.
        /// </summary>
        public void ActivateUpgrade(SuitUpgrade upgrade)
        {
            // Activating nothing is a programming mistake.
            if (upgrade == null)
            {
                throw new ArgumentNullException(nameof(upgrade));
            }

            // Already running: leave it alone rather than restarting its timer, so that a
            // stray second call cannot quietly extend an effect the player did not pay for.
            if (IndexOfActive(upgrade) >= 0)
            {
                return;
            }

            // Let the upgrade apply its own effect to the player.
            upgrade.Activate(m_Player);

            // Start its countdown from the duration in its settings asset.
            m_ActiveUpgrades.Add(new RunningUpgrade
            {
                Upgrade = upgrade,
                RemainingSeconds = upgrade.Duration
            });
        }

        /// <summary>
        /// Switches an upgrade off and lets it undo its effect. Safe to call for an
        /// upgrade that is not running.
        /// </summary>
        public void RemoveUpgrade(SuitUpgrade upgrade)
        {
            // Removing nothing is a programming mistake.
            if (upgrade == null)
            {
                throw new ArgumentNullException(nameof(upgrade));
            }

            // Find it among the running upgrades.
            int index = IndexOfActive(upgrade);

            // Not running, so there is nothing to undo.
            if (index < 0)
            {
                return;
            }

            // Take it off the running list before undoing it, so the effect can never be
            // undone twice if Deactivate somehow calls back into this controller.
            m_ActiveUpgrades.RemoveAt(index);

            // Let the upgrade take its own effect back off the player.
            upgrade.Deactivate(m_Player);
        }

        /// <summary>
        /// Counts every running upgrade down and switches off the ones that have expired.
        /// PlayerController calls this once per frame from its Update.
        ///
        /// This method is not in the class diagram. It has to exist, because the diagram
        /// gives every upgrade a duration but nothing that watches it - without a
        /// countdown a boost would stay on for the rest of the match.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Walk backwards, because expired upgrades are removed from the list we are
            // walking and removing from the front would skip the next entry.
            for (int i = m_ActiveUpgrades.Count - 1; i >= 0; i--)
            {
                // Take a copy of the entry. RunningUpgrade is a struct, so this really is
                // a copy and not a reference into the list.
                RunningUpgrade running = m_ActiveUpgrades[i];

                // Age it by one frame.
                running.RemainingSeconds -= deltaTime;

                // Still has time left, so keep it running.
                if (running.RemainingSeconds > 0f)
                {
                    // Write the copy back into the list, otherwise the new remaining time
                    // is thrown away and the upgrade never expires. This is the usual trap
                    // with structs stored in a List.
                    m_ActiveUpgrades[i] = running;

                    // Move on to the next running upgrade.
                    continue;
                }

                // Time is up: take it off the running list first.
                m_ActiveUpgrades.RemoveAt(i);

                // Then let it undo its effect on the player.
                running.Upgrade.Deactivate(m_Player);
            }
        }

        /// <summary>
        /// Switches off everything that is running.
        ///
        /// Also not in the class diagram, and also necessary: when the player is
        /// eliminated, the round ends or they disconnect, any speed or damage multiplier
        /// still registered on their movement and combat would otherwise outlive the
        /// upgrade that put it there.
        /// </summary>
        public void RemoveAllUpgrades()
        {
            // Walk backwards for the same reason as Tick - the list shrinks as we go.
            for (int i = m_ActiveUpgrades.Count - 1; i >= 0; i--)
            {
                // Remember the upgrade before its entry disappears.
                SuitUpgrade upgrade = m_ActiveUpgrades[i].Upgrade;

                // Take it off the running list.
                m_ActiveUpgrades.RemoveAt(i);

                // Let it undo its effect.
                upgrade.Deactivate(m_Player);
            }
        }

        /// <summary>
        /// The upgrade running at this position, for the HUD to draw. Index runs from 0 to
        /// ActiveUpgradeCount - 1.
        /// </summary>
        public SuitUpgrade GetActiveUpgrade(int index)
        {
            // Reading straight out of the list: an index outside the range is a caller
            // mistake and the list will say so.
            return m_ActiveUpgrades[index].Upgrade;
        }

        /// <summary>
        /// How many seconds the upgrade at this position has left, for the HUD countdown.
        /// </summary>
        public float GetRemainingSeconds(int index)
        {
            // Same as above: the list itself guards the range.
            return m_ActiveUpgrades[index].RemainingSeconds;
        }

        /// <summary>
        /// Where an upgrade sits in the running list, or -1 when it is not running.
        /// </summary>
        private int IndexOfActive(SuitUpgrade upgrade)
        {
            // A plain loop rather than List.FindIndex, because this runs every time an
            // upgrade is bought or removed and a loop allocates nothing.
            for (int i = 0; i < m_ActiveUpgrades.Count; i++)
            {
                // Compare by reference: each player has their own upgrade objects, so the
                // same object is the same upgrade.
                if (m_ActiveUpgrades[i].Upgrade == upgrade)
                {
                    return i;
                }
            }

            // Not found among the running upgrades.
            return -1;
        }
    }
}
