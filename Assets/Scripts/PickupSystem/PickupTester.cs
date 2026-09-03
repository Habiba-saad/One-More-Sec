using UnityEngine;
using Unity.MP_FPS.Pickups;

public class PickupTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UpgradeTestPlayer testPlayer;

    [Header("Pickup Prefabs")]
    [SerializeField] private MedKit medKitPrefab;
    [SerializeField] private LargeMedKit largeMedKitPrefab;
    [SerializeField] private OxygenTank oxygenTankPrefab;
    [SerializeField] private SpecialWeapon specialWeaponPrefab;

    private void Start()
    {
        Debug.Log("======================================");
        Debug.Log("ALL PICKUP TESTS STARTED");
        Debug.Log("======================================");

        if (testPlayer == null)
        {
            Debug.LogError("Test Player is missing!");
            return;
        }

        Invoke(nameof(TestMedKit), 1f);
        Invoke(nameof(TestLargeMedKit), 3f);
        Invoke(nameof(TestOxygenTank), 5f);
        Invoke(nameof(TestSpecialWeapon), 7f);
        Invoke(nameof(FinalResult), 9f);
    }

    private void TestMedKit()
    {
        Debug.Log("");
        Debug.Log("========== MED KIT TEST ==========");

        if (medKitPrefab == null)
        {
            Debug.LogError("MedKit Prefab is missing!");
            return;
        }

        MedKit medKit = Instantiate(medKitPrefab);

        medKit.OnCollected(testPlayer);

        Destroy(medKit.gameObject);
    }

    private void TestLargeMedKit()
    {
        Debug.Log("");
        Debug.Log("========== LARGE MED KIT TEST ==========");

        if (largeMedKitPrefab == null)
        {
            Debug.LogError("LargeMedKit Prefab is missing!");
            return;
        }

        LargeMedKit largeMedKit = Instantiate(largeMedKitPrefab);

        largeMedKit.OnCollected(testPlayer);

        Destroy(largeMedKit.gameObject);
    }

    private void TestOxygenTank()
    {
        Debug.Log("");
        Debug.Log("========== OXYGEN TANK TEST ==========");

        if (oxygenTankPrefab == null)
        {
            Debug.LogError("OxygenTank Prefab is missing!");
            return;
        }

        OxygenTank tank = Instantiate(oxygenTankPrefab);

        tank.OnCollected(testPlayer);

        Destroy(tank.gameObject);
    }

    private void TestSpecialWeapon()
    {
        Debug.Log("");
        Debug.Log("========== SPECIAL WEAPON TEST ==========");

        if (specialWeaponPrefab == null)
        {
            Debug.LogError("SpecialWeapon Prefab is missing!");
            return;
        }

        SpecialWeapon weapon = Instantiate(specialWeaponPrefab);

        weapon.OnCollected(testPlayer);

        Destroy(weapon.gameObject);
    }

    private void FinalResult()
    {
        Debug.Log("");
        Debug.Log("======================================");
        Debug.Log("ALL PICKUP TESTS FINISHED");
        Debug.Log("======================================");
    }
}