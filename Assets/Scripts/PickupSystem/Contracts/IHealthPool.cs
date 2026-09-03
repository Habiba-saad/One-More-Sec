// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Pickups
{
    /// <summary>
    /// Health, seen from the point of view of anything that restores it.
    /// Implemented by HealthSystem, which is another team member's class - this is the
    /// only thing MedKit and LargeMedKit need to know about it.
    ///
    /// There is no TakeDamage here on purpose: a med kit can only ever heal, and handing
    /// it a way to hurt somebody would be giving it a weapon it has no business holding.
    /// </summary>
    public interface IHealthPool
    {
        // Restores health points, capped by HealthSystem at its own maximum of 100.
        // The cap belongs there and not here: a med kit should not have to know how much
        // health the player is allowed to have.
        void Heal(float amount);
    }
}
