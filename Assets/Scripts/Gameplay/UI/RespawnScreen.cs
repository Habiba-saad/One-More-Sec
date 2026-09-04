using Unity.Entities;
using Unity.MP_FPS.Match;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.MP_FPS.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class RespawnScreen : MonoBehaviour
    {
        public Camera RespawnCamera;
        private VisualElement m_RespawnScreen;
        private Label m_RespawnTimerLabel;

        private World m_ClientWorld;
        private EntityManager m_EntityManager;
        private EntityQuery m_LocalPlayerQuery;

        private float m_RespawnCountdown;
        private const float RESPAWN_DURATION = 5.0f;

        // The match, for the message this screen shows. A player is no longer waiting a
        // fixed five seconds to come back: they are waiting for the round they were
        // knocked out of to finish, and only the match knows how long that is.
        private readonly ClientMatchStateReader m_Match = new ClientMatchStateReader();

        private void Awake()
        {
            RespawnCamera.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            m_RespawnScreen = GetComponent<UIDocument>().rootVisualElement;
            m_RespawnTimerLabel = m_RespawnScreen.Q<Label>("RespawnMessage");
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

            if (m_EntityManager != null)
            {
                m_LocalPlayerQuery = m_EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PredictedPlayerGhost>(),
                    ComponentType.ReadOnly<GhostOwnerIsLocal>()
                );
            }
        }

        void LateUpdate()
        {
            if (GameSettings.Instance.GameState != GlobalGameState.InGame)
            {
                m_RespawnScreen.style.display = DisplayStyle.None;
                return;
            }

            if (m_ClientWorld == null || !m_ClientWorld.IsCreated)
            {
                InitializeEcs();
                if (m_ClientWorld == null) return;
            }

            bool isPlayerAlive = m_LocalPlayerQuery.HasSingleton<PredictedPlayerGhost>();

            if (isPlayerAlive)
            {
                RespawnCamera.gameObject.SetActive(false);
                // Player is alive, hide the respawn screen
                if (m_RespawnScreen.style.display == DisplayStyle.Flex)
                {
                    m_RespawnScreen.style.display = DisplayStyle.None;
                }
            }
            else
            {
                RespawnCamera.gameObject.SetActive(true);
                // Player is dead, show the respawn screen and update the timer
                if (m_RespawnScreen.style.display == DisplayStyle.None)
                {
                    // This is the first frame death is detected, start the countdown
                    m_RespawnCountdown = RESPAWN_DURATION;
                    m_RespawnScreen.style.display = DisplayStyle.Flex;
                }

                m_RespawnCountdown -= Time.deltaTime;
                if (m_RespawnCountdown < 0)
                {
                    m_RespawnCountdown = 0;
                }

                m_RespawnTimerLabel.text = BuildWaitingMessage();
            }
        }

        /// <summary>
        /// What a player who is out of the round is told.
        ///
        /// There is no respawn timer to show any more: being killed puts a player out for
        /// the rest of the round, so the honest thing to show is what they are actually
        /// waiting for - the round to finish, and then the next one to begin.
        /// </summary>
        private string BuildWaitingMessage()
        {
            // No match running - a test scene, or the manager ghost has not arrived yet.
            // The old countdown is kept for that case so the screen never sits blank.
            if (!m_Match.TryRead(out var match))
            {
                return $"RESPAWNING IN {Mathf.CeilToInt(m_RespawnCountdown).ToString()}";
            }

            switch (match.State)
            {
                case MatchState.RoundInProgress:
                    // How long is left of the round is deliberately not shown here. It is
                    // already on the HUD strip, and a second copy of the same clock next
                    // to the word "eliminated" reads as a countdown to coming back, which
                    // it is not.
                    return "ELIMINATED - WAITING FOR THE ROUND TO END";

                case MatchState.RoundEnded:
                {
                    string seconds = Mathf.CeilToInt(match.StateSeconds).ToString();

                    // Nobody took it: the clock ran out with more than one player still
                    // standing. Said out loud, because a player who survived to the end
                    // would otherwise assume they had won it.
                    if (match.LastRoundWinnerId < 0)
                    {
                        return $"TIME UP - NOBODY WON THE ROUND - NEXT ROUND IN {seconds}";
                    }

                    if (match.LastRoundWinnerId == m_Match.LocalNetworkId)
                    {
                        return $"YOU WON THE ROUND - NEXT ROUND IN {seconds}";
                    }

                    return $"NEXT ROUND IN {seconds}";
                }

                case MatchState.RoundStarting:
                    return $"ROUND {match.CurrentRound.ToString()} STARTING";

                case MatchState.MatchEnded:
                    // The results board covers this screen at this point, so the line
                    // underneath it only has to not contradict it.
                    return "MATCH OVER";

                default:
                    return "WAITING FOR PLAYERS";
            }
        }
    }
}