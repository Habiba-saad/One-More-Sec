// ScriptableObject lives in UnityEngine, so this one file needs the Unity namespace.
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// The numbers behind one suit upgrade, stored as an asset instead of hard-coded in
    /// the class. Balancing an upgrade then means dragging a slider in the Inspector -
    /// no code change, no recompile, and no programmer needed to retune the game.
    ///
    /// One asset per upgrade: SpeedBoostData, DamageBoostData, PlayerScanData.
    /// Create them with: right click in the Project window -> Create -> One More Sec ->
    /// Suit Upgrade Data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSuitUpgradeData", menuName = "One More Sec/Suit Upgrade Data")]
    public class SuitUpgradeData : ScriptableObject
    {
        // Which upgrade these numbers belong to. Also the key the boosts register their
        // multiplier under, so every asset needs its own value: 1 speed, 2 damage, 3 scan.
        [Tooltip("1 = Speed Boost, 2 = Damage Boost, 3 = Player Scan. Must be unique.")]
        [SerializeField]
        private int m_UpgradeId = 1;

        // What one activation costs, in oxygen seconds. Cannot go below zero, because a
        // negative price would hand the player free oxygen for buying an upgrade.
        [Tooltip("Price of one activation, in oxygen seconds.")]
        [Min(0f)]
        [SerializeField]
        private float m_CostOxygen = 15f;

        // How long the effect stays on, in seconds. Kept above zero so an upgrade cannot
        // be switched on and never expire.
        [Tooltip("How many seconds the effect lasts. For Player Scan this is the scan duration.")]
        [Min(0.1f)]
        [SerializeField]
        private float m_Duration = 10f;

        // How strong the effect is, as a multiplier: 1.3 = +30%, 1.25 = +25%.
        // Speed Boost and Damage Boost read it. Player Scan ignores it, because a scan
        // does not scale a stat - it either reveals somebody or it does not.
        [Tooltip("1.3 = +30%. Used by Speed Boost and Damage Boost. Player Scan ignores it.")]
        [Min(1f)]
        [SerializeField]
        private float m_EffectMultiplier = 1.30f;

        // Read-only access to the id for the upgrade classes and the shop UI.
        public int UpgradeId => m_UpgradeId;

        // Read-only access to the price, used by UpgradeController.CanAfford.
        public float CostOxygen => m_CostOxygen;

        // Read-only access to the duration, used by UpgradeController to time the expiry.
        public float Duration => m_Duration;

        // Read-only access to the strength of the effect, used by the two boosts.
        public float EffectMultiplier => m_EffectMultiplier;
    }
}
