// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Upgrades
{
    /// <summary>
    /// The upgrade shop screen, seen from the point of view of the controller that asks
    /// for it. Implemented by PanelManager, which is another team member's class.
    ///
    /// PanelManager also has CloseUpgradePanel, plus the victory, defeat and results
    /// screens - but none of that appears here, because UpgradeController only ever needs
    /// to ask for the panel to open. Closing it is the panel's own business, and an
    /// interface should not hand out methods its caller never uses.
    /// </summary>
    public interface IUpgradeShopView
    {
        // Shows the upgrade shop to the player.
        void OpenUpgradePanel();
    }
}
