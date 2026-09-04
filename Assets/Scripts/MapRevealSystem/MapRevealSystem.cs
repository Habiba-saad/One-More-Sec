using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.MP_FPS.Upgrades;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Minimap
{
    /// <summary>
    /// What one player is allowed to see on their minimap, and the rules for putting
    /// somebody on it or taking them off again.
    ///
    /// One of these per player, on the player. It is the class the diagram gives
    /// PlayerScan to talk to, and it is deliberately the only place that decides who
    /// appears on a map: the scan asks for the nearest opponent, a later RechargeSystem
    /// will ask for a particular one, and neither of them touches the buffer itself.
    ///
    /// Server only. Where the opponents are is the server's knowledge, and a client that
    /// worked out its own reveals could simply reveal everybody.
    ///
    /// Put it on ArmaturePlayer_Rifle and ArmaturePlayer_Shotgun, next to
    /// PlayerUpgradeBridge.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapRevealSystem : GhostMonoBehaviour, IRevealService
    {
        [Header("Scanning")]
        [Tooltip("How far the scan reaches, in metres. Nobody further away can be found.")]
        [Min(1f)]
        [SerializeField]
        private float m_ScanRange = 150f;

        // Every player in the match, rebuilt when a scan runs. Cached because building an
        // EntityQuery is the expensive part and a scan can happen at any moment.
        private EntityQuery m_PlayerQuery;
        private bool m_QueryBuilt;

        /// <summary>
        /// Finds the opponent closest to <paramref name="scanner"/> and puts them on this
        /// player's map. Returns false when there is nobody to find, and then nothing was
        /// revealed and there will be nothing to hide later.
        /// </summary>
        public bool TryRevealNearestOpponent(IUpgradeTarget scanner, out int revealedPlayerId)
        {
            revealedPlayerId = -1;

            if (!IsServer("a scan"))
            {
                return false;
            }

            // Who is asking. Taken from the scanner rather than from this component, so
            // that the one player who must never be their own scan result is the one the
            // upgrade actually belongs to.
            int scannerId = scanner != null ? scanner.PlayerId : -1;

            if (!TryFindNearestOpponent(scannerId, out revealedPlayerId))
            {
                return false;
            }

            RevealPlayer(revealedPlayerId);
            return true;
        }

        /// <summary>
        /// Puts one player on this map. Public because the scan is not the only thing that
        /// will reveal somebody - a player who is recharging is meant to light up on every
        /// map in the match - and safe to call for somebody already showing.
        /// </summary>
        public void RevealPlayer(int playerId)
        {
            if (!IsServer("a reveal") || !TryGetRevealedBuffer(out var revealed))
            {
                return;
            }

            // Already there. Adding a second entry would mean the first HidePlayer left
            // the blip on the map, which is exactly the bug the scan cannot afford.
            if (IndexOf(revealed, playerId) >= 0)
            {
                return;
            }

            revealed.Add(new RevealedPlayer { NetworkId = playerId });
        }

        /// <summary>
        /// Takes one player back off this map. Safe for somebody who is not on it, which
        /// is what the interface promises - a scan can end after its target has already
        /// been eliminated.
        /// </summary>
        public void HidePlayer(int playerId)
        {
            if (!IsServer("a hide") || !TryGetRevealedBuffer(out var revealed))
            {
                return;
            }

            int index = IndexOf(revealed, playerId);
            if (index >= 0)
            {
                revealed.RemoveAt(index);
            }
        }

        /// <summary>
        /// The nearest player who is not the scanner and is inside the scan's range.
        /// </summary>
        private bool TryFindNearestOpponent(int scannerId, out int nearestId)
        {
            nearestId = -1;

            EnsureQuery();

            float3 scannerPosition = transform.position;

            // Compared squared, so the search does not pay for a square root per player
            // just to find out which of them is closest.
            float nearestDistanceSq = m_ScanRange * m_ScanRange;

            var entities = m_PlayerQuery.ToEntityArray(Allocator.Temp);

            try
            {
                var entityManager = World.EntityManager;

                foreach (var entity in entities)
                {
                    int candidateId = entityManager.GetComponentData<GhostOwner>(entity).NetworkId;

                    // Nobody scans themselves. Without this the nearest player is always
                    // the scanner, at a distance of zero.
                    if (candidateId == scannerId)
                    {
                        continue;
                    }

                    // The dead are not opponents. Their entity survives for a moment after
                    // the killing shot, and pointing the scan at a corpse would waste the
                    // whole purchase.
                    if (entityManager.GetComponentData<PredictedPlayerGhost>(entity).CurrentHealth <= 0f)
                    {
                        continue;
                    }

                    float3 candidatePosition = entityManager.GetComponentData<LocalTransform>(entity).Position;

                    // Height is dropped from the comparison: the arena has walkways above
                    // walkways, and a player one floor up is nearer on the map than one
                    // across the site, which is the distance the minimap draws.
                    float dx = candidatePosition.x - scannerPosition.x;
                    float dz = candidatePosition.z - scannerPosition.z;
                    float distanceSq = dx * dx + dz * dz;

                    if (distanceSq < nearestDistanceSq)
                    {
                        nearestDistanceSq = distanceSq;
                        nearestId = candidateId;
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }

            return nearestId >= 0;
        }

        /// <summary>
        /// Builds the query over every player in the match, once.
        /// </summary>
        private void EnsureQuery()
        {
            if (m_QueryBuilt)
            {
                return;
            }

            m_PlayerQuery = World.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PredictedPlayerGhost>(),
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<LocalTransform>());

            m_QueryBuilt = true;
        }

        /// <summary>
        /// This player's reveal list, or false when the prefab was never baked with one.
        /// </summary>
        private bool TryGetRevealedBuffer(out DynamicBuffer<RevealedPlayer> revealed)
        {
            if (!World.EntityManager.HasBuffer<RevealedPlayer>(GhostGameObject.LinkedEntity))
            {
                revealed = default;
                return false;
            }

            revealed = GetGhostDynamicBuffer<RevealedPlayer>();
            return true;
        }

        /// <summary>
        /// Where a player sits in the reveal list, or -1 when they are not on it.
        /// </summary>
        private static int IndexOf(DynamicBuffer<RevealedPlayer> revealed, int playerId)
        {
            for (int i = 0; i < revealed.Length; i++)
            {
                if (revealed[i].NetworkId == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Refuses anything not running on the server, or arriving before the ghost is
        /// linked - the same guard PlayerUpgradeBridge uses, and for the same reason.
        /// </summary>
        private bool IsServer(string what)
        {
            if (GhostGameObject == null)
            {
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
    }
}
