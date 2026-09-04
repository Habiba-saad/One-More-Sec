using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

// Same namespace family as the rest of the project's gameplay code (Unity.MP_FPS.*).
namespace Unity.MP_FPS.Minimap
{
    /// <summary>
    /// The round minimap in the bottom right corner: where this player is facing, and
    /// where the opponents their suit has found are standing.
    ///
    /// It draws positions rather than a picture of the level. A second camera rendering
    /// the arena from above would cost a full extra render every frame and would have to
    /// be re-aimed for every map the game ships with, while a dot on a disc needs neither
    /// - and the disc can be given the real map as a background later without a line of
    /// this file changing.
    ///
    /// North stays up. The arrow in the middle turns instead, so a blip that is north east
    /// of the player is drawn north east all the time and the map does not swing about
    /// under them while they are looking around in a firefight.
    ///
    /// It shows opponents the server has revealed to this player and nobody else. Drawing
    /// everybody would make PlayerScan pointless, and this client is never told where the
    /// others are anyway - see RevealedPlayer.
    ///
    /// Put it on the same GameObject as InGameHUD, which is where the UIDocument lives.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class MinimapController : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Width of the map circle, in pixels.")]
        [SerializeField]
        private float m_Diameter = 190f;

        [Tooltip("Distance from the right edge of the screen, in pixels. Also the fallback if the ammo readout cannot be found.")]
        [SerializeField]
        private float m_ScreenMargin = 20f;

        [Tooltip("Gap left between the map and the ammo readout it sits above, in pixels.")]
        [SerializeField]
        private float m_GapAboveAmmo = 20f;

        [Header("Scale")]
        [Tooltip("How many metres the edge of the circle is away from the player.")]
        [Min(1f)]
        [SerializeField]
        private float m_RangeMetres = 150f;

        [Header("Look")]
        [Tooltip("Optional picture of the arena, drawn inside the circle.")]
        [SerializeField]
        private Texture2D m_MapTexture;

        [SerializeField]
        private Color m_SelfColor = new Color(0.45f, 0.95f, 1f);

        [SerializeField]
        private Color m_RevealedColor = new Color(1f, 0.35f, 0.35f);

        // The disc, and the pivot the facing pointer hangs off.
        private VisualElement m_Map;
        private VisualElement m_Self;

        // The ammo readout the map keeps clear of, and where the map was last put so the
        // layout is only dirtied when it really moves.
        private VisualElement m_AmmoAnchor;
        private float m_AppliedRight = float.NaN;
        private float m_AppliedBottom = float.NaN;

        // One dot per revealed opponent. Kept and reused rather than created and thrown
        // away every frame, because a blip appearing costs an element and a layout pass
        // and there are only ever a handful of them.
        private readonly List<VisualElement> m_Blips = new List<VisualElement>();

        // Where every player in the match is, by network id. Rebuilt each frame from the
        // ghosts this client is holding.
        private readonly Dictionary<int, float3> m_PlayerPositions = new Dictionary<int, float3>();

        // Who this player is allowed to see this frame.
        private readonly List<int> m_RevealedIds = new List<int>();

        private World m_ClientWorld;
        private EntityManager m_EntityManager;
        private EntityQuery m_LocalPlayerQuery;
        private EntityQuery m_AllPlayersQuery;

        private const float k_BlipSize = 10f;
        private const float k_SelfSize = 12f;
        private const float k_SelfDotSize = 9f;

        private void OnEnable()
        {
            BuildMap();
        }

        private void OnDisable()
        {
            m_Map?.RemoveFromHierarchy();
            m_Map = null;
            m_Self = null;
            m_Blips.Clear();
        }

        /// <summary>
        /// Creates the disc and the player's arrow once.
        /// </summary>
        private void BuildMap()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null)
            {
                return;
            }

            m_Map = new VisualElement { name = "minimap" };
            m_Map.pickingMode = PickingMode.Ignore;

            var style = m_Map.style;
            style.position = Position.Absolute;
            style.right = m_ScreenMargin;
            style.bottom = m_ScreenMargin;
            style.width = m_Diameter;
            style.height = m_Diameter;
            style.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.78f);

            if (m_MapTexture != null)
            {
                style.backgroundImage = new StyleBackground(m_MapTexture);
            }

            // A radius of half the width is what turns the square into a circle, and the
            // clipping is what keeps a blip near the edge inside it rather than sitting on
            // the corner where the circle is not.
            SetRadius(m_Map, m_Diameter * 0.5f);
            SetBorder(m_Map, new Color(1f, 1f, 1f, 0.25f), 2f);
            style.overflow = Overflow.Hidden;

            float centre = m_Diameter * 0.5f;

            // This player, drawn as two separate things on purpose. A single arrow that
            // spun on the spot was hard to read: the whole shape moved, so there was
            // nothing standing still to read the direction against.
            //
            // So the dot never moves - it is always the player, always dead centre - and
            // only the pointer swings around it. What turns is then obvious, because
            // something next to it is holding still.
            var dot = new VisualElement { name = "minimap-self-dot" };
            dot.pickingMode = PickingMode.Ignore;
            dot.style.position = Position.Absolute;
            dot.style.width = k_SelfDotSize;
            dot.style.height = k_SelfDotSize;
            dot.style.left = centre - k_SelfDotSize * 0.5f;
            dot.style.top = centre - k_SelfDotSize * 0.5f;
            dot.style.backgroundColor = m_SelfColor;
            SetRadius(dot, k_SelfDotSize * 0.5f);
            m_Map.Add(dot);

            // A point of zero size sitting exactly at the centre. Rotating it turns
            // whatever hangs off it around that point, which is how the pointer orbits the
            // dot rather than spinning about its own middle.
            m_Self = new VisualElement { name = "minimap-self-pivot" };
            m_Self.pickingMode = PickingMode.Ignore;
            m_Self.style.position = Position.Absolute;
            m_Self.style.left = centre;
            m_Self.style.top = centre;
            m_Self.style.width = 0f;
            m_Self.style.height = 0f;

            // The pointer itself: a triangle made out of borders, because UI Toolkit has
            // no shape to draw. Placed above the pivot, so at a yaw of zero it points
            // north, which is up.
            var pointer = new VisualElement { name = "minimap-self-pointer" };
            pointer.pickingMode = PickingMode.Ignore;
            pointer.style.position = Position.Absolute;
            pointer.style.width = 0f;
            pointer.style.height = 0f;
            pointer.style.left = -k_SelfSize * 0.4f;
            pointer.style.top = -(k_SelfDotSize * 0.5f + k_SelfSize + 3f);
            pointer.style.borderLeftWidth = k_SelfSize * 0.4f;
            pointer.style.borderRightWidth = k_SelfSize * 0.4f;
            pointer.style.borderBottomWidth = k_SelfSize;
            pointer.style.borderLeftColor = Color.clear;
            pointer.style.borderRightColor = Color.clear;
            pointer.style.borderBottomColor = m_SelfColor;
            m_Self.Add(pointer);

            m_Map.Add(m_Self);

            // The ammo readout shares this corner. Found by the name it carries in
            // PlayerHUD.uxml and only ever read from - the map moves itself out of the way
            // rather than moving somebody else's element.
            m_AmmoAnchor = root.Q<VisualElement>("weapon-info-container");

            root.Add(m_Map);
        }

        private void LateUpdate()
        {
            if (m_Map == null)
            {
                return;
            }

            // Nothing to draw without a player of our own: the menu, the loading screen,
            // or the moment after being killed.
            if (!TryReadWorld(out float3 selfPosition, out float selfYawDegrees))
            {
                m_Map.style.display = DisplayStyle.None;
                return;
            }

            m_Map.style.display = DisplayStyle.Flex;
            KeepAboveAmmoBar();

            DrawSelf(selfYawDegrees);
            DrawRevealed(selfPosition);
        }

        /// <summary>
        /// Collects this frame's positions and reveals. Returns false when this client has
        /// no player on the field.
        /// </summary>
        private bool TryReadWorld(out float3 selfPosition, out float selfYawDegrees)
        {
            selfPosition = float3.zero;
            selfYawDegrees = 0f;

            if (m_ClientWorld == null || !m_ClientWorld.IsCreated)
            {
                InitializeEcs();
            }

            if (m_ClientWorld == null)
            {
                return false;
            }

            // Asked for as a list rather than as a singleton: GhostOwnerIsLocal is an
            // enableable component, and Entities refuses GetSingletonEntity on a query
            // holding one.
            var localPlayers = m_LocalPlayerQuery.ToEntityArray(Allocator.Temp);

            try
            {
                if (localPlayers.Length == 0)
                {
                    return false;
                }

                var self = localPlayers[0];

                selfPosition = m_EntityManager.GetComponentData<LocalTransform>(self).Position;
                selfYawDegrees = m_EntityManager.GetComponentData<PredictedPlayerGhost>(self)
                    .ControllerState.YawDegrees;

                ReadRevealedIds(self);
            }
            finally
            {
                localPlayers.Dispose();
            }

            ReadPlayerPositions();
            return true;
        }

        /// <summary>
        /// Reads the ids the server has revealed to this player.
        /// </summary>
        private void ReadRevealedIds(Entity self)
        {
            m_RevealedIds.Clear();

            // A player ghost baked before the buffer existed simply has nobody revealed,
            // which is the honest thing to draw rather than an error.
            if (!m_EntityManager.HasBuffer<RevealedPlayer>(self))
            {
                return;
            }

            var revealed = m_EntityManager.GetBuffer<RevealedPlayer>(self, true);
            for (int i = 0; i < revealed.Length; i++)
            {
                m_RevealedIds.Add(revealed[i].NetworkId);
            }
        }

        /// <summary>
        /// Reads where every player this client knows about is standing.
        /// </summary>
        private void ReadPlayerPositions()
        {
            m_PlayerPositions.Clear();

            // Skipped entirely when nobody is revealed, which is the usual case: there is
            // no point walking every ghost in the match to answer a question nobody asked.
            if (m_RevealedIds.Count == 0)
            {
                return;
            }

            var players = m_AllPlayersQuery.ToEntityArray(Allocator.Temp);

            try
            {
                foreach (var player in players)
                {
                    int networkId = m_EntityManager.GetComponentData<GhostOwner>(player).NetworkId;
                    m_PlayerPositions[networkId] = m_EntityManager.GetComponentData<LocalTransform>(player).Position;
                }
            }
            finally
            {
                players.Dispose();
            }
        }

        /// <summary>
        /// Swings the pointer round to wherever the player is looking. The dot underneath
        /// it is fixed and is not touched here.
        /// </summary>
        private void DrawSelf(float yawDegrees)
        {
            // The pointer is built pointing north, which is where a yaw of zero looks, so
            // the angle goes in as it is.
            m_Self.style.rotate = new StyleRotate(new Rotate(new Angle(yawDegrees, AngleUnit.Degree)));
        }

        /// <summary>
        /// Slides the map up so it sits on top of the ammo readout rather than over it.
        ///
        /// Measured every frame rather than written down as a number, for the same reason
        /// the shop panel measures the health bar: that corner of the HUD belongs to the
        /// rest of the team, and the map should follow it if it moves.
        /// </summary>
        private void KeepAboveAmmoBar()
        {
            float right = m_ScreenMargin;
            float bottom = m_ScreenMargin;

            var parent = m_Map.parent;
            if (m_AmmoAnchor != null && parent != null)
            {
                var ammo = m_AmmoAnchor.worldBound;
                var area = parent.worldBound;

                // The first frames run before UI Toolkit has laid anything out, and the
                // empty rectangle it reports then would throw the map into the corner.
                if (ammo.height > 0f && area.height > 0f)
                {
                    // Both rectangles are in screen space, so they are brought back into
                    // the coordinates the map is positioned in before being used.
                    right = area.xMax - ammo.xMax;
                    bottom = area.yMax - ammo.yMin + m_GapAboveAmmo;
                }
            }

            // Writing a style is what marks the element for a fresh layout pass, so it is
            // only worth doing when a value actually changed.
            if (!Mathf.Approximately(right, m_AppliedRight))
            {
                m_Map.style.right = right;
                m_AppliedRight = right;
            }

            if (!Mathf.Approximately(bottom, m_AppliedBottom))
            {
                m_Map.style.bottom = bottom;
                m_AppliedBottom = bottom;
            }
        }

        /// <summary>
        /// Puts a dot on the map for every opponent the suit has found.
        /// </summary>
        private void DrawRevealed(float3 selfPosition)
        {
            float centre = m_Diameter * 0.5f;

            // How many pixels one metre is worth. The edge of the circle is the range, so
            // everything in between falls out of one multiplication.
            float scale = centre / m_RangeMetres;

            int drawn = 0;

            foreach (int revealedId in m_RevealedIds)
            {
                // Revealed but not yet received - a ghost can arrive a snapshot after the
                // id that names it. There is nowhere to draw them until it does.
                if (!m_PlayerPositions.TryGetValue(revealedId, out float3 position))
                {
                    continue;
                }

                float dx = (position.x - selfPosition.x) * scale;

                // Screen y grows downwards while world z grows forwards, so north on the
                // map is a negative offset. Getting this backwards is what puts a blip
                // behind the player when they are in front of them.
                float dy = -(position.z - selfPosition.z) * scale;

                // Someone beyond the range is pinned to the rim rather than dropped. The
                // player paid for a direction, and "that way, far off" is still the answer
                // they bought.
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float limit = centre - k_BlipSize;
                if (distance > limit && distance > 0f)
                {
                    dx *= limit / distance;
                    dy *= limit / distance;
                }

                var blip = GetBlip(drawn);
                blip.style.display = DisplayStyle.Flex;
                blip.style.left = centre + dx - k_BlipSize * 0.5f;
                blip.style.top = centre + dy - k_BlipSize * 0.5f;
                drawn++;
            }

            // Anything left over from a frame when more people were visible.
            for (int i = drawn; i < m_Blips.Count; i++)
            {
                m_Blips[i].style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// The dot at this position in the pool, creating it the first time it is needed.
        /// </summary>
        private VisualElement GetBlip(int index)
        {
            while (m_Blips.Count <= index)
            {
                var blip = new VisualElement { name = "minimap-blip" };
                blip.pickingMode = PickingMode.Ignore;
                blip.style.position = Position.Absolute;
                blip.style.width = k_BlipSize;
                blip.style.height = k_BlipSize;
                blip.style.backgroundColor = m_RevealedColor;
                SetRadius(blip, k_BlipSize * 0.5f);

                m_Map.Add(blip);
                m_Blips.Add(blip);
            }

            return m_Blips[index];
        }

        /// <summary>
        /// Finds the client world and the two queries this map reads from.
        /// </summary>
        private void InitializeEcs()
        {
            m_ClientWorld = null;
            m_EntityManager = default;

            foreach (var world in World.All)
            {
                if (world.IsClient())
                {
                    m_ClientWorld = world;
                    m_EntityManager = world.EntityManager;
                    break;
                }
            }

            if (m_ClientWorld == null)
            {
                return;
            }

            m_LocalPlayerQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PredictedPlayerGhost>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());

            m_AllPlayersQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PredictedPlayerGhost>(),
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        /// <summary>
        /// UI Toolkit keeps a separate radius per corner, so a circle is four lines.
        /// </summary>
        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        /// <summary>
        /// And a separate width and colour per edge.
        /// </summary>
        private static void SetBorder(VisualElement element, Color colour, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;

            element.style.borderTopColor = colour;
            element.style.borderRightColor = colour;
            element.style.borderBottomColor = colour;
            element.style.borderLeftColor = colour;
        }
    }
}
