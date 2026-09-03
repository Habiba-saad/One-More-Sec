using UnityEngine;
using Unity.MP_FPS.Upgrades;

public class UpgradeTester : MonoBehaviour, IUpgradeShopView
{
    [Header("References")]
    [SerializeField] private UpgradeTestPlayer testPlayer;

    [Header("Upgrade Data")]
    [SerializeField] private SuitUpgradeData speedBoostData;
    [SerializeField] private SuitUpgradeData damageBoostData;
    [SerializeField] private SuitUpgradeData playerScanData;

    private UpgradeController upgradeController;

    private SpeedBoost speedBoost;
    private DamageBoost damageBoost;
    private PlayerScan playerScan;

    private void Start()
    {
        Debug.Log("======================================");
        Debug.Log("ALL UPGRADES TEST STARTED");
        Debug.Log("======================================");

        if (testPlayer == null)
        {
            Debug.LogError("Test Player is missing!");
            return;
        }

        if (speedBoostData == null ||
            damageBoostData == null ||
            playerScanData == null)
        {
            Debug.LogError("One or more Upgrade Data assets are missing!");
            return;
        }

        // Create upgrades
        speedBoost = new SpeedBoost(speedBoostData);
        damageBoost = new DamageBoost(damageBoostData);
        playerScan = new PlayerScan(playerScanData);

        // Upgrade shop catalogue
        SuitUpgrade[] catalogue =
        {
            speedBoost,
            damageBoost,
            playerScan
        };

        // Create real UpgradeController
        upgradeController = new UpgradeController(
            testPlayer,
            catalogue,
            this
        );

        Debug.Log("UpgradeController created successfully.");

        // Start tests
        TestSpeedBoost();

        // Damage starts after Speed finishes
        Invoke(nameof(TestDamageBoost), 12f);

        // Scan starts after Damage finishes
        Invoke(nameof(TestPlayerScan), 24f);

        // Final result
        Invoke(nameof(FinalResult), 31f);
    }

    private void Update()
    {
        if (upgradeController == null)
            return;

        // Required because UpgradeController is plain C#
        upgradeController.Tick(Time.deltaTime);
    }

    // ==============================
    // SPEED BOOST TEST
    // ==============================

    private void TestSpeedBoost()
    {
        Debug.Log("");
        Debug.Log("========== SPEED BOOST TEST ==========");

        bool purchased =
            upgradeController.PurchaseUpgrade(speedBoost);

        Debug.Log(
            "Speed Boost Purchase Result = " + purchased
        );
    }

    // ==============================
    // DAMAGE BOOST TEST
    // ==============================

    private void TestDamageBoost()
    {
        Debug.Log("");
        Debug.Log("========== DAMAGE BOOST TEST ==========");

        bool purchased =
            upgradeController.PurchaseUpgrade(damageBoost);

        Debug.Log(
            "Damage Boost Purchase Result = " + purchased
        );
    }

    // ==============================
    // PLAYER SCAN TEST
    // ==============================

    private void TestPlayerScan()
    {
        Debug.Log("");
        Debug.Log("========== PLAYER SCAN TEST ==========");

        bool purchased =
            upgradeController.PurchaseUpgrade(playerScan);

        Debug.Log(
            "Player Scan Purchase Result = " + purchased
        );
    }

    // ==============================
    // FINAL RESULT
    // ==============================

    private void FinalResult()
    {
        Debug.Log("");
        Debug.Log("======================================");
        Debug.Log("ALL UPGRADE TESTS FINISHED");
        Debug.Log("======================================");
    }

    // Required by IUpgradeShopView
    public void OpenUpgradePanel()
    {
        Debug.Log("Upgrade Shop Opened");
    }
}