using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Oxygen
{
    /// <summary>
    /// Shows the local player's remaining air.
    ///
    /// Deliberately a small separate component that builds its label in code instead of an
    /// addition to InGameHUD and its UXML: oxygen has no agreed place in the HUD layout yet,
    /// and this way the readout can be deleted in one step once PanelManager and the real
    /// HUD design exist. Put it on the same GameObject as InGameHUD, which is where the
    /// UIDocument lives.
    ///
    /// Purely a display. The value it shows is owned by the server - see PlayerOxygen.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class OxygenHud : MonoBehaviour
    {
        private static readonly Color k_NormalColor = new Color(0.6f, 0.85f, 1f);
        private static readonly Color k_SuffocatingColor = new Color(1f, 0.25f, 0.25f);
        private static readonly Color k_RechargingColor = new Color(0.4f, 1f, 0.5f);

        // Below this many seconds the readout turns red as a warning.
        private const float k_WarningSeconds = 10f;

        private Label m_OxygenLabel;

        private World m_ClientWorld;
        private EntityManager m_EntityManager;
        private EntityQuery m_LocalPlayerQuery;

        private void OnEnable()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;
            if (rootElement == null)
            {
                return;
            }

            m_OxygenLabel = new Label { name = "oxygen-label" };

            // Styled here rather than in a stylesheet so that no .uxml or .uss asset has to
            // be edited to try this out.
            var style = m_OxygenLabel.style;
            style.position = Position.Absolute;
            style.top = 24f;
            style.left = 0f;
            style.right = 0f;
            style.unityTextAlign = TextAnchor.UpperCenter;
            style.fontSize = 26f;
            style.color = k_NormalColor;

            rootElement.Add(m_OxygenLabel);
        }

        private void OnDisable()
        {
            // Remove the label again, otherwise re-enabling would stack a second one on top.
            m_OxygenLabel?.RemoveFromHierarchy();
            m_OxygenLabel = null;
        }

        private void InitializeEcs()
        {
            foreach (var world in World.All)
            {
                if (world.IsClient())
                {
                    m_ClientWorld = world;
                    m_EntityManager = world.EntityManager;
                    break;
                }
            }

            if (m_ClientWorld != null)
            {
                // The one entity that both carries oxygen and belongs to this client.
                m_LocalPlayerQuery = m_EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerOxygen>(),
                    ComponentType.ReadOnly<GhostOwnerIsLocal>()
                );
            }
        }

        private void LateUpdate()
        {
            if (m_OxygenLabel == null)
            {
                return;
            }

            if (GameSettings.Instance == null ||
                GameSettings.Instance.GameState != GlobalGameState.InGame)
            {
                m_OxygenLabel.style.display = DisplayStyle.None;
                return;
            }

            // The world is torn down when returning to the menu, so it may need finding again.
            if (m_ClientWorld == null || !m_ClientWorld.IsCreated)
            {
                InitializeEcs();
            }

            if (m_ClientWorld == null || m_LocalPlayerQuery == default)
            {
                m_OxygenLabel.style.display = DisplayStyle.None;
                return;
            }

            // GetSingletonEntity refuses to run on a query holding an enableable component,
            // and GhostOwnerIsLocal is one, so the match is fetched as a list instead. An
            // empty list means the player is dead or has not spawned yet.
            using var localPlayers = m_LocalPlayerQuery.ToEntityArray(Allocator.Temp);
            if (localPlayers.Length == 0)
            {
                m_OxygenLabel.style.display = DisplayStyle.None;
                return;
            }

            var playerEntity = localPlayers[0];
            var oxygen = m_EntityManager.GetComponentData<PlayerOxygen>(playerEntity);

            // Read off the predicted ghost rather than replicated separately: the local
            // player predicts its own controller state, so this reacts on the press
            // instead of waiting for the next snapshot to come back from the server.
            bool isRecharging =
                m_EntityManager.HasComponent<PredictedPlayerGhost>(playerEntity) &&
                m_EntityManager.GetComponentData<PredictedPlayerGhost>(playerEntity)
                    .ControllerState.IsRecharging;

            m_OxygenLabel.style.display = DisplayStyle.Flex;

            // Rounded up, so the readout only shows 0 once the air has genuinely run out.
            int secondsLeft = Mathf.CeilToInt(oxygen.Seconds);

            if (isRecharging)
            {
                m_OxygenLabel.text = $"O2  {secondsLeft.ToString()}s  CHARGING";
                m_OxygenLabel.style.color = k_RechargingColor;
                return;
            }

            m_OxygenLabel.text = oxygen.IsSuffocating
                ? "NO OXYGEN"
                : $"O2  {secondsLeft.ToString()}s";

            m_OxygenLabel.style.color = oxygen.Seconds <= k_WarningSeconds
                ? k_SuffocatingColor
                : k_NormalColor;
        }
    }
}
