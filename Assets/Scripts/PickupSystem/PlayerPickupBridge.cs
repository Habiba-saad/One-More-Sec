// The bridge sits on the player prefab, so this file needs the Unity namespace.
using Unity.MP_FPS.Oxygen;
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// The real player, wearing the face the pickups expect.
    ///
    /// The pickup hierarchy was written against IPickupTarget and its three sub-system
    /// interfaces, for a player that was going to be a MonoBehaviour holding a
    /// HealthSystem, an OxygenSystem and a CombatSystem. This project put all three of
    /// those inside ECS components instead - health, ammo and the equipped weapon on
    /// PredictedPlayerGhost, air on PlayerOxygen - so nothing in the running game
    /// implements the interfaces the pickups ask for, and every OnCollected would find
    /// null and give up.
    ///
    /// Rather than rewrite five pickup classes around ECS, this one class answers all four
    /// contracts and turns them into component writes. That is the whole point of having
    /// depended on interfaces in the first place: not one of the pickups ever learns that
    /// the player moved into ECS.
    ///
    /// A GhostMonoBehaviour rather than a plain one, because that is what hands it a linked
    /// entity to read and write, and a Role to check before writing anything. Put it on
    /// ArmaturePlayer_Rifle and ArmaturePlayer_Shotgun, next to PlayerGhost.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerPickupBridge : GhostMonoBehaviour,
        IPickupTarget, IHealthPool, IOxygenRefill, IWeaponHolder
    {
        // One object answers all three, because in this game one entity holds all three.
        // The interfaces stay separate regardless: a med kit handed only IHealthPool still
        // has no way of reaching the weapons.
        public IHealthPool Health => this;
        public IOxygenRefill OxygenRefill => this;
        public IWeaponHolder Weapons => this;

        /// <summary>
        /// Refuses any write that is not happening on the server, or that arrives before
        /// the ghost has been linked.
        ///
        /// Everything below changes state the server owns. A client that healed itself
        /// would be overwritten by the next snapshot at best and be cheating at worst, so
        /// the guard sits here once instead of being remembered in three separate places.
        /// </summary>
        private bool CanWrite(string what)
        {
            if (GhostGameObject == null)
            {
                // Collected in the same frame the player spawned, before OnGhostLinked.
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

        // ---------- IHealthPool ----------

        /// <summary>
        /// Restores health, up to the maximum the player entity carries.
        /// </summary>
        public void Heal(float amount)
        {
            if (!CanWrite("a heal") || !GhostHasComponent<PredictedPlayerGhost>())
            {
                return;
            }

            ReadGhostComponentData<PredictedPlayerGhost>(out var ghost);

            // A player on zero health is dead and waiting for ServerGameSystem to respawn
            // them. Healing them here would leave a corpse standing up mid-respawn.
            if (ghost.CurrentHealth <= 0f)
            {
                return;
            }

            // Capped here because this is where MaxHealth lives - a med kit has no business
            // knowing what the ceiling is, which is exactly what IHealthPool says.
            ghost.CurrentHealth = Mathf.Min(ghost.CurrentHealth + amount, ghost.MaxHealth);

            WriteGhostComponentData(ghost);
        }

        // ---------- IOxygenRefill ----------

        /// <summary>
        /// Adds seconds to the tank.
        /// </summary>
        public void AddSeconds(float seconds)
        {
            if (!CanWrite("an oxygen refill") || !GhostHasComponent<PlayerOxygen>())
            {
                return;
            }

            ReadGhostComponentData<PlayerOxygen>(out var oxygen);

            // Uncapped on purpose, and for the same reason recharging is - MaxSeconds is
            // only the full value of the HUD bar, not a limit. Banking more air than the
            // suit started with is what makes a tank worth crossing the map for.
            oxygen.Seconds += seconds;

            WriteGhostComponentData(oxygen);
        }

        // ---------- IWeaponHolder ----------

        /// <summary>
        /// Swaps the player's weapon for the one with this id, handed over loaded.
        /// </summary>
        public void EquipWeapon(int weaponId)
        {
            if (!CanWrite("a weapon swap") || !GhostHasComponent<PredictedPlayerGhost>())
            {
                return;
            }

            if (weaponId < 0 || WeaponManager.Instance == null)
            {
                Debug.LogError($"{name}: no weapon registry, so weapon {weaponId.ToString()} was not equipped.", this);
                return;
            }

            // Looked up before anything is written, so that a bad id on a prefab leaves the
            // player holding the gun they already had rather than an id that resolves to
            // nothing the next time they pull the trigger.
            var weaponData = WeaponManager.Instance.WeaponRegistry.GetWeaponData((uint)weaponId);
            if (weaponData == null)
            {
                Debug.LogError($"{name}: weapon id {weaponId.ToString()} is not in the registry.", this);
                return;
            }

            ReadGhostComponentData<PredictedPlayerGhost>(out var ghost);

            ghost.EquippedWeaponID = (uint)weaponId;

            // Handed over loaded, and with any reload in progress abandoned: the reload
            // that was running belonged to the gun the player just dropped.
            ghost.CurrentAmmo = weaponData.MagazineSize;
            ghost.ReloadTimer = 0f;
            ghost.ControllerState.IsReloadingState = false;

            // Ready to fire immediately. The cooldown counts up towards CooldownInMs in
            // ServerPlayerMovementSystem, so starting it there means no dead first shot.
            ghost.WeaponCooldown = weaponData.CooldownInMs;

            WriteGhostComponentData(ghost);
        }
    }
}
