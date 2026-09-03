// MonoBehaviour, Debug and Time all live in the Unity namespace.
using UnityEngine;

// This project has Active Input Handling set to "Input System Package (New)", so the old
// Input.GetKeyDown would throw at runtime. Keyboard.current comes from here instead.
using UnityEngine.InputSystem;

// The three systems being tested.
using Unity.MP_FPS.Upgrades;
using Unity.MP_FPS.Pickups;
using Unity.MP_FPS.Inventory;

// Kept in its own namespace so it is obvious this is not part of the game.
namespace Unity.MP_FPS.Sandbox
{
    /// <summary>
    /// A fake player for testing the upgrade, pickup and inventory systems in an empty
    /// scene, with no netcode, no real movement and no other team member's code.
    ///
    /// It implements every interface those systems ask a player for, but instead of
    /// really changing speed or health it just writes to the Console. That is enough to
    /// prove the rules are right: the costs, the durations, the double-activation guard,
    /// and that every effect that goes on comes back off again.
    ///
    /// THROWAWAY TEST CODE - delete this whole Sandbox folder before submitting.
    /// One class implements all nine interfaces on purpose: a real player would spread
    /// them over PlayerController, OxygenSystem, HealthSystem and the rest, but for a test
    /// harness one file is easier to read and easier to delete.
    /// </summary>
    public class TestPlayerDummy : MonoBehaviour,
        IUpgradeTarget, IPickupTarget,
        IOxygenBank, IOxygenRefill, IMovementModifier, IDamageModifier, IRevealService,
        IHealthPool, IWeaponHolder
    {
        // Drag SpeedBoostData here in the Inspector.
        [Header("Drag the three upgrade assets here")]
        [SerializeField]
        private SuitUpgradeData m_SpeedBoostData;

        // Drag DamageBoostData here in the Inspector.
        [SerializeField]
        private SuitUpgradeData m_DamageBoostData;

        // Drag PlayerScanData here in the Inspector.
        [SerializeField]
        private SuitUpgradeData m_PlayerScanData;

        // Seconds of oxygen left. Starts at the 120 the design document gives a player.
        [Header("Starting state")]
        [SerializeField]
        private float m_OxygenSeconds = 120f;

        // Health points. Starts at the maximum of 100.
        [SerializeField]
        private float m_Health = 100f;

        // The system under test: the player's upgrade rules.
        private UpgradeController m_Upgrades;

        // The system under test: what the player is carrying.
        private InventoryComponent m_Inventory;

        // Pretend movement speed, so the effect of a Speed Boost is visible on screen.
        private float m_SpeedMultiplier = 1f;

        // Pretend damage multiplier, so the effect of a Damage Boost is visible.
        private float m_DamageMultiplier = 1f;

        // Which fake opponent the scan is currently showing, or -1 for nobody.
        private int m_RevealedPlayer = -1;

        // ---------- IUpgradeTarget ----------

        // Any id will do for a test with one player in the scene.
        public int PlayerId => 1;

        // This dummy is its own oxygen tank, its own movement, its own everything.
        public IOxygenBank Oxygen => this;

        public IMovementModifier Movement => this;

        public IDamageModifier Combat => this;

        public IRevealService Reveal => this;

        // ---------- IPickupTarget ----------

        public IHealthPool Health => this;

        public IOxygenRefill OxygenRefill => this;

        public IWeaponHolder Weapons => this;

        // ---------- Unity ----------

        private void Awake()
        {
            // Refuse to run with missing assets, and say exactly which one, rather than
            // throwing an unhelpful null reference on the first key press.
            if (m_SpeedBoostData == null || m_DamageBoostData == null || m_PlayerScanData == null)
            {
                Debug.LogError($"{name}: drag all three SuitUpgradeData assets into the Inspector first.", this);
                enabled = false;
                return;
            }

            // Build the real UpgradeController with the real upgrades - this is exactly
            // what PlayerController will do later.
            m_Upgrades = new UpgradeController(this, new SuitUpgrade[]
            {
                new SpeedBoost(m_SpeedBoostData),
                new DamageBoost(m_DamageBoostData),
                new PlayerScan(m_PlayerScanData)
            });

            // Build an empty inventory to drop collected items into.
            m_Inventory = new InventoryComponent();

            // Remind whoever pressed Play what the keys are.
            Debug.Log("[Test] 1 = Speed Boost, 2 = Damage Boost, 3 = Player Scan, 4 = collect nearest pickup, I = list inventory.");
        }

        private void Update()
        {
            // No keyboard attached - nothing to do, and reading Keyboard.current would
            // throw.
            if (Keyboard.current == null)
            {
                return;
            }

            // Oxygen drains at 1 per second, the way OxygenSystem will do it for real.
            // Without this the purchases would be free in practice and the test would not
            // show what running out feels like.
            m_OxygenSeconds -= Time.deltaTime;

            // This is the line PlayerController will own: it is what expires the boosts.
            m_Upgrades.Tick(Time.deltaTime);

            // Buy the Speed Boost.
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                TryBuy(0);
            }

            // Buy the Damage Boost.
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                TryBuy(1);
            }

            // Buy the Player Scan.
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                TryBuy(2);
            }

            // Collect the nearest pickup lying in the scene, without needing to walk into
            // it - handy before there is any real movement to test with.
            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                CollectNearestPickup();
            }

            // Print what the inventory is holding.
            if (Keyboard.current.iKey.wasPressedThisFrame)
            {
                LogInventory();
            }
        }

        /// <summary>
        /// Buys the upgrade at this position in the catalogue and reports what happened.
        /// </summary>
        private void TryBuy(int index)
        {
            // Pull the upgrade out of the controller's own catalogue.
            SuitUpgrade upgrade = m_Upgrades.AvailableUpgrades[index];

            // Run the real purchase rules.
            bool bought = m_Upgrades.PurchaseUpgrade(upgrade);

            // Say whether it went through, and why the oxygen level is what it is.
            Debug.Log(bought
                ? $"[Buy] {upgrade.GetType().Name} bought for {upgrade.CostOxygen}s. Oxygen left: {m_OxygenSeconds:F1}s"
                : $"[Buy] {upgrade.GetType().Name} refused (already running, or not enough oxygen). Oxygen: {m_OxygenSeconds:F1}s");
        }

        /// <summary>
        /// Finds the closest Pickup in the scene and collects it by hand.
        /// </summary>
        private void CollectNearestPickup()
        {
            // Every pickup currently in the scene. Sorting is not needed, so it is turned
            // off - this is the fast overload.
            Pickup[] pickups = FindObjectsByType<Pickup>(FindObjectsSortMode.None);

            // Nothing to collect.
            if (pickups.Length == 0)
            {
                Debug.Log("[Pickup] There are no pickups in the scene.");
                return;
            }

            // Track the best candidate found so far.
            Pickup nearest = pickups[0];

            // And how far away it is, squared - comparing squared distances avoids a
            // square root per pickup and gives the same answer.
            float nearestDistance = (pickups[0].transform.position - transform.position).sqrMagnitude;

            // Walk the rest looking for something closer.
            for (int i = 1; i < pickups.Length; i++)
            {
                // Distance from this dummy to that pickup, squared.
                float distance = (pickups[i].transform.position - transform.position).sqrMagnitude;

                // Closer than the current best, so it becomes the new best.
                if (distance < nearestDistance)
                {
                    nearest = pickups[i];
                    nearestDistance = distance;
                }
            }

            // Remember what it was before collecting, because the pickup destroys itself.
            string pickupName = nearest.GetType().Name;

            // Put it in the inventory as a record of what was picked up.
            m_Inventory.AddItem(nearest);

            // Run the real effect - this is what walking into it would do.
            nearest.OnCollected(this);

            // Report it.
            Debug.Log($"[Pickup] Collected {pickupName}.");
        }

        /// <summary>
        /// Prints every stack the inventory is holding.
        /// </summary>
        private void LogInventory()
        {
            // Nothing collected yet.
            if (m_Inventory.Slots.Count == 0)
            {
                Debug.Log("[Inventory] Empty.");
                return;
            }

            // One line per stack, showing the item and how many of it.
            for (int i = 0; i < m_Inventory.Slots.Count; i++)
            {
                InventorySlot slot = m_Inventory.Slots[i];
                Debug.Log($"[Inventory] {slot.Item.GetType().Name} x{slot.Quantity} (id {slot.Item.PickupId})");
            }
        }

        /// <summary>
        /// Draws the live state in the Game view, so the boosts can be watched going on
        /// and coming off without reading the Console.
        /// </summary>
        private void OnGUI()
        {
            // Skip when Awake bailed out on missing assets.
            if (m_Upgrades == null)
            {
                return;
            }

            // A plain block of text in the top left corner.
            GUILayout.BeginArea(new Rect(10f, 10f, 420f, 300f));

            // The two resources.
            GUILayout.Label($"Oxygen: {m_OxygenSeconds:F1}s     Health: {m_Health:F0}");

            // The two stats the boosts are supposed to change. If a boost ends and these
            // do not go back to 1.00, the remove path is broken.
            GUILayout.Label($"Speed x{m_SpeedMultiplier:F2}     Damage x{m_DamageMultiplier:F2}");

            // Who the scan is showing.
            GUILayout.Label($"Revealed player: {(m_RevealedPlayer < 0 ? "nobody" : m_RevealedPlayer.ToString())}");

            // A countdown line per running upgrade.
            for (int i = 0; i < m_Upgrades.ActiveUpgradeCount; i++)
            {
                GUILayout.Label($"ACTIVE: {m_Upgrades.GetActiveUpgrade(i).GetType().Name} - {m_Upgrades.GetRemainingSeconds(i):F1}s left");
            }

            // Close the block.
            GUILayout.EndArea();
        }

        // ---------- Fake OxygenSystem ----------

        // Can the tank pay this much?
        public bool CanSpend(float seconds) => m_OxygenSeconds >= seconds;

        public bool SpendSeconds(float seconds)
        {
            // Refuse when the tank cannot cover it, exactly as the real one must.
            if (m_OxygenSeconds < seconds)
            {
                return false;
            }

            // Take the seconds out.
            m_OxygenSeconds -= seconds;

            // Tell the caller it worked.
            return true;
        }

        public void AddSeconds(float seconds)
        {
            // Put seconds back in. No cap, which is what the design asks for.
            m_OxygenSeconds += seconds;
            Debug.Log($"[Oxygen] +{seconds}s -> {m_OxygenSeconds:F1}s");
        }

        // ---------- Fake HealthSystem ----------

        public void Heal(float amount)
        {
            // Add the health, then clamp at 100 the way HealthSystem will.
            m_Health = Mathf.Min(100f, m_Health + amount);
            Debug.Log($"[Health] +{amount} -> {m_Health:F0}");
        }

        // ---------- Fake PlayerMovement ----------

        public void AddSpeedMultiplier(int sourceId, float multiplier)
        {
            // A real PlayerMovement would combine several sources; one is enough here.
            m_SpeedMultiplier = multiplier;
            Debug.Log($"[Movement] AddSpeedMultiplier(source {sourceId}, x{multiplier:F2})");
        }

        public void RemoveSpeedMultiplier(int sourceId)
        {
            // Back to normal speed. If this line never runs, the boost leaked.
            m_SpeedMultiplier = 1f;
            Debug.Log($"[Movement] RemoveSpeedMultiplier(source {sourceId})");
        }

        // ---------- Fake CombatSystem ----------

        public void AddDamageMultiplier(int sourceId, float multiplier)
        {
            m_DamageMultiplier = multiplier;
            Debug.Log($"[Combat] AddDamageMultiplier(source {sourceId}, x{multiplier:F2})");
        }

        public void RemoveDamageMultiplier(int sourceId)
        {
            m_DamageMultiplier = 1f;
            Debug.Log($"[Combat] RemoveDamageMultiplier(source {sourceId})");
        }

        public void EquipWeapon(int weaponId)
        {
            Debug.Log($"[Combat] EquipWeapon({weaponId}) - replaces the current weapon.");
        }

        // ---------- Fake MapRevealSystem ----------

        public bool TryRevealNearestOpponent(IUpgradeTarget scanner, out int revealedPlayerId)
        {
            // There are no real opponents in a test scene, so pretend player 7 was found.
            revealedPlayerId = 7;
            m_RevealedPlayer = revealedPlayerId;
            Debug.Log($"[Reveal] TryRevealNearestOpponent -> player {revealedPlayerId}");

            // Returning true is the interesting case; flip this to false to check that the
            // scan handles finding nobody without leaving a marker behind.
            return true;
        }

        public void HidePlayer(int playerId)
        {
            // The scan ending. If this never runs, the reveal leaked.
            m_RevealedPlayer = -1;
            Debug.Log($"[Reveal] HidePlayer({playerId})");
        }
    }
}
