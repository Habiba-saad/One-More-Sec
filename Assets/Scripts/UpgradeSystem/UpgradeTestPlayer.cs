using UnityEngine;
using Unity.MP_FPS.Upgrades;
using Unity.MP_FPS.Pickups;

public class UpgradeTestPlayer : MonoBehaviour,
    IUpgradeTarget,
    IPickupTarget,
    IOxygenBank,
    IOxygenRefill,
    IMovementModifier,
    IDamageModifier,
    IRevealService,
    IHealthPool,
    IWeaponHolder
{
    [Header("Test Values")]
    [SerializeField] private float oxygen = 120f;
    [SerializeField] private float health = 100f;
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField] private float damageMultiplier = 1f;

    public int PlayerId => 1;

    public IOxygenBank Oxygen => this;
    public IMovementModifier Movement => this;
    public IDamageModifier Combat => this;
    public IRevealService Reveal => this;

    public IHealthPool Health => this;
    public IOxygenRefill OxygenRefill => this;
    public IWeaponHolder Weapons => this;


    // =========================
    // Oxygen
    // =========================

    public bool CanSpend(float seconds)
    {
        return oxygen >= seconds;
    }

    public bool SpendSeconds(float seconds)
    {
        if (!CanSpend(seconds))
            return false;

        oxygen -= seconds;

        Debug.Log("Oxygen after purchase: " + oxygen);

        return true;
    }

    public void AddSeconds(float seconds)
    {
        oxygen += seconds;

        Debug.Log("Oxygen after refill: " + oxygen);
    }


    // =========================
    // Movement
    // =========================

    public void AddSpeedMultiplier(int sourceId, float multiplier)
    {
        speedMultiplier *= multiplier;

        Debug.Log(
            $"Speed Boost ON | Multiplier = {speedMultiplier}"
        );
    }

    public void RemoveSpeedMultiplier(int sourceId)
    {
        speedMultiplier = 1f;

        Debug.Log("Speed Boost OFF | Speed back to normal");
    }


    // =========================
    // Damage
    // =========================

    public void AddDamageMultiplier(int sourceId, float multiplier)
    {
        damageMultiplier *= multiplier;

        Debug.Log(
            $"Damage Boost ON | Multiplier = {damageMultiplier}"
        );
    }

    public void RemoveDamageMultiplier(int sourceId)
    {
        damageMultiplier = 1f;

        Debug.Log("Damage Boost OFF | Damage back to normal");
    }


    // =========================
    // Scan
    // =========================

    public bool TryRevealNearestOpponent(
        IUpgradeTarget scanner,
        out int revealedPlayerId)
    {
        revealedPlayerId = 2;

        Debug.Log("Player Scan ON | Revealed Player ID = 2");

        return true;
    }

    public void HidePlayer(int playerId)
    {
        Debug.Log(
            "Player Scan OFF | Hidden Player ID = " + playerId
        );
    }


    // =========================
    // Health
    // =========================

    public void Heal(float amount)
    {
        health += amount;

        if (health > 100f)
            health = 100f;

        Debug.Log("Health = " + health);
    }


    // =========================
    // Weapon
    // =========================

    public void EquipWeapon(int weaponId)
    {
        Debug.Log(
            "Equipped Special Weapon ID = " + weaponId
        );
    }
}