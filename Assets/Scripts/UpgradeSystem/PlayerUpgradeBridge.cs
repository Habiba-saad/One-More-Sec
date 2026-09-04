// The bridge sits on the player prefab, so this file needs the Unity namespace.
using System.Collections.Generic;
using Unity.Entities;
using Unity.MP_FPS.Oxygen;
using Unity.NetCode;
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// The real player, wearing the face the suit upgrades expect - the upgrade side's
    /// twin of PlayerPickupBridge, and kept apart from it for the same reason the
    /// interfaces are: a pickup must not be able to spend oxygen, and an upgrade must not
    /// be able to refill it.
    ///
    /// In the class diagram the upgrades talk to PlayerMovement and to CombatSystem. This
    /// class is what stands in for both of them here: neither exists as a MonoBehaviour in
    /// this project, because movement and shooting are DOTS systems, so the bridge adapts
    /// those systems to the two small interfaces the upgrades were written against.
    ///
    /// It owns this player's UpgradeController, because the controller is plain C# with no
    /// Update of its own and something has to feed it the clock. Server only - buying
    /// spends oxygen and applying changes stats, and the server owns both.
    ///
    /// Put it on ArmaturePlayer_Rifle and ArmaturePlayer_Shotgun, next to PlayerGhost, and
    /// drag SpeedBoostData and DamageBoostData into the fields below.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerUpgradeBridge : GhostMonoBehaviour,
        IUpgradeTarget, IOxygenBank, IMovementModifier, IDamageModifier, IUpdateServer
    {
        /// <summary>
        /// Every multiplier currently registered on one stat, keyed by whatever registered
        /// it, together with the single number they come to.
        ///
        /// A set rather than one float, so two effects can run at once and the first to
        /// expire removes only its own share - the whole reason IMovementModifier and
        /// IDamageModifier are add/remove instead of plain setters. One instance per stat,
        /// so a speed source can never be removed by a damage source that happens to share
        /// its id, and so both stats get the same rules from one place instead of two
        /// copies of them.
        /// </summary>
        private sealed class MultiplierSet
        {
            // What each source has registered. Never exposed, so the only way in or out is
            // through the two methods below.
            private readonly Dictionary<int, float> m_Values = new Dictionary<int, float>();

            /// <summary>
            /// Registers the multiplier owned by <paramref name="sourceId"/>, replacing
            /// whatever that source had registered before rather than stacking on it.
            /// </summary>
            public void Set(int sourceId, float multiplier)
            {
                m_Values[sourceId] = multiplier;
            }

            /// <summary>
            /// Removes only what <paramref name="sourceId"/> registered, and says whether
            /// there was anything to remove. Safe for a source that registered nothing,
            /// which is what both interfaces promise.
            /// </summary>
            public bool Remove(int sourceId)
            {
                return m_Values.Remove(sourceId);
            }

            /// <summary>
            /// Every registered multiplier multiplied together.
            /// </summary>
            public float Combined
            {
                get
                {
                    // Multiplied rather than summed, so two +30% boosts come out at +69%
                    // instead of +60%, and so an empty set lands on exactly 1.
                    float combined = 1f;

                    foreach (var multiplier in m_Values.Values)
                    {
                        combined *= multiplier;
                    }

                    return combined;
                }
            }
        }

        [Header("Upgrade Data")]
        [Tooltip("The SpeedBoostData asset. Left empty, this player cannot buy the speed boost.")]
        [SerializeField]
        private SuitUpgradeData m_SpeedBoostData;

        [Tooltip("The DamageBoostData asset. Left empty, this player cannot buy the damage boost.")]
        [SerializeField]
        private SuitUpgradeData m_DamageBoostData;

        [Tooltip("The PlayerScanData asset. Left empty, this player cannot buy the scan.")]
        [SerializeField]
        private SuitUpgradeData m_PlayerScanData;

        // This player's own upgrade objects, built once on the server. Never shared with
        // another player: a SuitUpgrade remembers whether it is running, so two players
        // holding one instance would fight over that flag.
        private UpgradeController m_Controller;
        private SpeedBoost m_SpeedBoost;
        private DamageBoost m_DamageBoost;
        private PlayerScan m_PlayerScan;

        // The map service living next to this component, found once through its interface.
        private IRevealService m_Reveal;

        // Everything currently multiplying this player's move speed.
        private readonly MultiplierSet m_SpeedMultipliers = new MultiplierSet();

        // Everything currently multiplying the damage this player deals.
        private readonly MultiplierSet m_DamageMultipliers = new MultiplierSet();

        // ---------- IUpgradeTarget ----------

        // The network id, which is what the rest of the game already calls a player.
        public int PlayerId =>
            GhostGameObject != null && GhostHasComponent<GhostOwner>()
                ? ReadGhostComponentData<GhostOwner>().NetworkId
                : -1;

        public IOxygenBank Oxygen => this;
        public IMovementModifier Movement => this;
        public IDamageModifier Combat => this;

        // The one sub-system this bridge does not play itself, because revealing somebody
        // needs to know where every player in the match is standing and that is nothing to
        // do with adapting this player to the upgrades.
        //
        // Asked for by its interface rather than by its class, so this file still knows
        // nothing about MapRevealSystem beyond the contract in Contracts/ - Unity resolves
        // an interface to whichever component on this GameObject implements it.
        public IRevealService Reveal
        {
            get
            {
                // Looked up again while it is missing rather than remembered as missing,
                // so adding the component to a prefab mid-session starts working at once.
                if (m_Reveal == null)
                {
                    m_Reveal = GetComponent<IRevealService>();
                }

                return m_Reveal;
            }
        }

        // ---------- IOxygenBank ----------

        /// <summary>
        /// True when the tank currently holds this many seconds. Asks, changes nothing.
        /// </summary>
        public bool CanSpend(float seconds)
        {
            if (GhostGameObject == null || !GhostHasComponent<PlayerOxygen>())
            {
                return false;
            }

            ReadGhostComponentData<PlayerOxygen>(out var oxygen);
            return oxygen.Seconds >= seconds;
        }

        /// <summary>
        /// Takes the seconds out of the tank, or refuses and takes nothing.
        /// </summary>
        public bool SpendSeconds(float seconds)
        {
            if (!IsServer("an oxygen withdrawal") || !GhostHasComponent<PlayerOxygen>())
            {
                return false;
            }

            ReadGhostComponentData<PlayerOxygen>(out var oxygen);

            // Re-checked rather than trusted from CanSpend, because the tank keeps
            // draining while the shop is open - see the note on IOxygenBank.
            if (oxygen.Seconds < seconds)
            {
                return false;
            }

            oxygen.Seconds -= seconds;
            WriteGhostComponentData(oxygen);
            return true;
        }

        // ---------- IMovementModifier ----------

        /// <summary>
        /// Registers a speed multiplier owned by <paramref name="sourceId"/>.
        /// </summary>
        public void AddSpeedMultiplier(int sourceId, float multiplier)
        {
            if (!IsServer("a speed multiplier"))
            {
                return;
            }

            m_SpeedMultipliers.Set(sourceId, multiplier);
            WriteMultipliersToGhost();
        }

        /// <summary>
        /// Removes only the multiplier <paramref name="sourceId"/> registered.
        /// </summary>
        public void RemoveSpeedMultiplier(int sourceId)
        {
            if (!IsServer("a speed multiplier removal"))
            {
                return;
            }

            // Removing a source that never registered has to be safe, per the interface,
            // so the return value is the only thing deciding whether a rewrite is needed.
            if (m_SpeedMultipliers.Remove(sourceId))
            {
                WriteMultipliersToGhost();
            }
        }

        // ---------- IDamageModifier ----------

        /// <summary>
        /// Registers a damage multiplier owned by <paramref name="sourceId"/>.
        /// </summary>
        public void AddDamageMultiplier(int sourceId, float multiplier)
        {
            if (!IsServer("a damage multiplier"))
            {
                return;
            }

            m_DamageMultipliers.Set(sourceId, multiplier);
            WriteMultipliersToGhost();
        }

        /// <summary>
        /// Removes only the multiplier <paramref name="sourceId"/> registered.
        /// </summary>
        public void RemoveDamageMultiplier(int sourceId)
        {
            if (!IsServer("a damage multiplier removal"))
            {
                return;
            }

            if (m_DamageMultipliers.Remove(sourceId))
            {
                WriteMultipliersToGhost();
            }
        }

        // ---------- writing to the ghost ----------

        /// <summary>
        /// Puts both combined multipliers on the ghost, where the shooting code and the
        /// client's movement prediction read them.
        ///
        /// One read-modify-write for the two of them rather than one each: they live in
        /// the same component, so writing them together halves the work and makes it
        /// impossible to update one and forget the other.
        /// </summary>
        private void WriteMultipliersToGhost()
        {
            if (!GhostHasComponent<PredictedPlayerGhost>())
            {
                return;
            }

            ReadGhostComponentData<PredictedPlayerGhost>(out var ghost);
            ghost.SpeedMultiplier = m_SpeedMultipliers.Combined;
            ghost.DamageMultiplier = m_DamageMultipliers.Combined;
            WriteGhostComponentData(ghost);
        }

        // ---------- server tick ----------

        /// <summary>
        /// Reads the purchase input and ages whatever is currently switched on.
        /// </summary>
        public void UpdateServer(float deltaTime)
        {
            EnsureController();

            if (m_Controller == null)
            {
                return;
            }

            // The input the server last accepted for this player, which is the only copy
            // of it the server is allowed to act on.
            var playerGhost = GetComponent<PlayerGhost>();
            if (playerGhost != null)
            {
                var input = playerGhost.ServerMovementInput;
                TryPurchase(input.BuySpeedBoost, m_SpeedBoost, "speed boost");
                TryPurchase(input.BuyDamageBoost, m_DamageBoost, "damage boost");
                TryPurchase(input.BuyPlayerScan, m_PlayerScan, "player scan");
            }

            // Ages the running upgrades and deactivates whatever reached zero.
            m_Controller.Tick(deltaTime);

            // Then tell the owning client what it should be drawing.
            PublishActiveUpgrades();
        }

        /// <summary>
        /// Copies what the controller is running into the ghost buffer the owning client's
        /// shop panel reads.
        ///
        /// Rewritten from scratch every tick rather than patched when something is bought
        /// or expires. With at most a handful of entries that costs nothing, and it means
        /// the buffer cannot drift out of step with the controller - there is no path
        /// where an upgrade ends and the panel is left showing a bar that never empties.
        /// </summary>
        private void PublishActiveUpgrades()
        {
            // The buffer is baked onto the player prefab. A player without it is a prefab
            // that was never re-baked, and asking for it anyway would throw.
            if (GhostGameObject == null ||
                !World.EntityManager.HasBuffer<ActiveUpgradeStatus>(GhostGameObject.LinkedEntity))
            {
                return;
            }

            var statuses = GetGhostDynamicBuffer<ActiveUpgradeStatus>();
            statuses.Clear();

            for (int i = 0; i < m_Controller.ActiveUpgradeCount; i++)
            {
                statuses.Add(new ActiveUpgradeStatus
                {
                    UpgradeId = m_Controller.GetActiveUpgrade(i).UpgradeId,
                    RemainingSeconds = m_Controller.GetRemainingSeconds(i)
                });
            }
        }

        /// <summary>
        /// Buys one upgrade when its key was pressed this tick and this player has it at
        /// all. One method for both boosts, so a third upgrade costs one line up there
        /// rather than another copy of the rules down here.
        /// </summary>
        private void TryPurchase(bool wanted, SuitUpgrade upgrade, string label)
        {
            if (!wanted)
            {
                return;
            }

            // Pressed the key for something this player does not have. Said out loud
            // rather than ignored: a missing data asset or a missing MapRevealSystem looks
            // exactly like a dead key from the player's side, and silence there costs an
            // afternoon of wondering why nothing happens.
            if (upgrade == null)
            {
                Debug.LogWarning($"{name}: {label} was asked for, but this player has no such upgrade - check its data asset on the prefab.", this);
                return;
            }

            // Every rule about whether this is allowed - in the catalogue, not already
            // running, affordable - belongs to the controller, so there is nothing to
            // check here beyond reporting what it decided.
            bool bought = m_Controller.PurchaseUpgrade(upgrade);
            Debug.Log($"[Server] {name}: {label} purchase {(bought ? "accepted" : "refused")}.", this);
        }

        /// <summary>
        /// Builds this player's upgrades the first time they are needed.
        /// </summary>
        private void EnsureController()
        {
            if (m_Controller != null)
            {
                return;
            }

            var catalogue = new List<SuitUpgrade>();

            if (m_SpeedBoostData != null)
            {
                m_SpeedBoost = new SpeedBoost(m_SpeedBoostData);
                catalogue.Add(m_SpeedBoost);
            }

            if (m_DamageBoostData != null)
            {
                m_DamageBoost = new DamageBoost(m_DamageBoostData);
                catalogue.Add(m_DamageBoost);
            }

            // The scan is only offered when there is something to reveal onto. Without a
            // map service it would throw the moment it was bought, and a player who cannot
            // be shown anything is better off keeping their oxygen.
            if (m_PlayerScanData != null && Reveal != null)
            {
                m_PlayerScan = new PlayerScan(m_PlayerScanData);
                catalogue.Add(m_PlayerScan);
            }
            else if (m_PlayerScanData != null)
            {
                Debug.LogWarning($"{name}: PlayerScanData is assigned but no MapRevealSystem sits on this player, so the scan cannot be bought.", this);
            }

            // An empty catalogue means every data field was left empty on the prefab,
            // which is a wiring mistake rather than a player who chose to buy nothing.
            if (catalogue.Count == 0)
            {
                Debug.LogWarning($"{name}: no upgrade data assigned, so this player can buy nothing.", this);
            }

            // No shop view yet. The controller is built to accept null there, which is
            // exactly the headless case its own comment describes.
            m_Controller = new UpgradeController(this, catalogue);
        }

        /// <summary>
        /// Refuses anything not running on the server, or arriving before the ghost is
        /// linked. Every write above is to state the server owns.
        /// </summary>
        private bool IsServer(string what)
        {
            if (GhostGameObject == null)
            {
                Debug.LogWarning($"{name}: ignored {what} because the ghost is not linked yet.", this);
                return false;
            }

            if (Role != MultiplayerRole.Server)
            {
                Debug.LogWarning($"{name}: ignored {what} because this is the {Role.ToString()} copy of the player.", this);
                return false;
            }

            return true;
        }
    }
}
