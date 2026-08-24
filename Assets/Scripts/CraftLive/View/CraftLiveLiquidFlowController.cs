using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveLiquidFlowController :
        MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad2Bindings bindings;
        [SerializeField] private GameObject liquidDropPrefab;

        [Header("Flow Animation")]
        [SerializeField, Min(1)] private int dropCount = 6;
        [SerializeField, Min(0.1f)] private float flowDuration = 2.4f;
        [SerializeField, Min(0f)] private float dropSpacingSeconds = 0.12f;
        [SerializeField, Min(0f)] private float surfaceOffset = 0.14f;

        [Header("Persistent Groove Fill")]
        [SerializeField, Min(4)] private int trailSampleCount = 40;
        [SerializeField, Min(0.02f)] private float trailRadius = 0.11f;

        [SerializeField] private UnityEvent<Color> onFlowStarted;
        [SerializeField] private UnityEvent<float> onFlowProgress;
        [SerializeField] private UnityEvent onFlowCompleted;

        private sealed class PersistentFill
        {
            public GameObject Root;
            public GameObject GuideChannel;
            public Vector3 GuideChannelPosition;
            public Quaternion GuideChannelRotation;
            public Vector3 GuideChannelScale;
            public GameObject Rim;
            public Material RimMaterial;
            public Mesh RimMesh;
            public readonly List<GameObject> Trail =
                new List<GameObject>();
        }

        private static readonly CraftLiveSlotId[] Slots =
        {
            CraftLiveSlotId.Top,
            CraftLiveSlotId.Left,
            CraftLiveSlotId.Right,
            CraftLiveSlotId.Bottom,
            CraftLiveSlotId.Skill,
            CraftLiveSlotId.Attribute
        };

        private readonly List<GameObject> activeDrops =
            new List<GameObject>();
        private readonly Dictionary<CraftLiveSlotId, PersistentFill>
            persistentFills =
                new Dictionary<CraftLiveSlotId, PersistentFill>();
        private readonly Dictionary<CraftLiveSlotId, string>
            persistentMaterialIds =
                new Dictionary<CraftLiveSlotId, string>();

        private int observedGroupGeneration = -1;
        private int handledTransferGeneration = -1;
        private int handledTransferSerial = -1;
        private int activeTransferGeneration = -1;
        private int activeTransferSerial = -1;
        private int completedTransferGeneration = -1;
        private int completedTransferSerial = -1;
        private bool isResettingFlowLifecycle;
        private bool externallySequenced;
        private bool externalFlowActive;
        private Coroutine flowRoutine;

        public bool HasCompletedFlow(
            int groupGeneration,
            int transferSerial)
        {
            return transferSerial > 0 &&
                   completedTransferGeneration == groupGeneration &&
                   completedTransferSerial == transferSerial;
        }

        public bool HasCompletedFlow(int transferSerial)
        {
            return HasCompletedFlow(
                observedGroupGeneration,
                transferSerial);
        }

        public static bool ShouldStartAutomaticFlow(
            bool isExternallySequenced,
            CraftLivePlacementStatus status,
            int groupGeneration,
            int transferSerial,
            int handledGroupGeneration,
            int handledTransferSerial,
            bool hasActiveRoutine)
        {
            return !isExternallySequenced &&
                   status == CraftLivePlacementStatus.PlacementComplete &&
                   (groupGeneration != handledGroupGeneration ||
                    transferSerial != handledTransferSerial) &&
                   !hasActiveRoutine;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (session != null)
            {
                session.StateChanged -= Refresh;
                session.StateChanged += Refresh;
                Refresh(session.State);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= Refresh;
            }

            ResetFlowLifecycle(-1);
        }

        public void Configure(CraftLivePad2Bindings targetBindings)
        {
            bindings = targetBindings;
            ResolveReferences();
        }

        /// <summary>
        /// When Pad 2 owns the placement sequence, state changes must not
        /// independently start a second light coroutine. The receiver calls
        /// the same single-item flow method explicitly after each landing.
        /// </summary>
        public void SetExternalSequencing(bool value)
        {
            externallySequenced = value;
            if (!value && isActiveAndEnabled && session != null)
            {
                Refresh(session.State);
            }
        }

        public IEnumerator PlaySinglePlacementFlow(
            CraftLiveRoomState snapshot,
            int groupGeneration,
            int transferSerial)
        {
            if (snapshot == null)
            {
                yield break;
            }

            // A scene can enable the flow component before the receiver in
            // the same frame. If an automatic pass already owns the current
            // transfer, await it instead of skipping the light and advancing
            // the queue. Different transfers are never run concurrently.
            while (externalFlowActive || flowRoutine != null)
            {
                if (!IsCurrentTransfer(
                        groupGeneration,
                        transferSerial,
                        CraftLivePlacementStatus.PlacementComplete))
                {
                    yield break;
                }

                yield return null;
            }

            if (HasCompletedFlow(groupGeneration, transferSerial) ||
                !IsCurrentTransfer(
                    groupGeneration,
                    transferSerial,
                    CraftLivePlacementStatus.PlacementComplete))
            {
                yield break;
            }

            externalFlowActive = true;
            handledTransferGeneration = groupGeneration;
            handledTransferSerial = transferSerial;
            activeTransferGeneration = groupGeneration;
            activeTransferSerial = transferSerial;
            try
            {
                yield return RunFlowLifecycle(
                    snapshot,
                    groupGeneration,
                    transferSerial,
                    false);
            }
            finally
            {
                externalFlowActive = false;
            }
        }

        public static Vector3 EvaluatePath(
            Vector3 start,
            Vector3 end,
            float normalized,
            float wave)
        {
            return EvaluatePath(
                start,
                end,
                normalized,
                wave,
                Vector3.up);
        }

        public static Vector3 EvaluatePath(
            Vector3 start,
            Vector3 end,
            float normalized,
            float wave,
            Vector3 surfaceNormal)
        {
            float t = Mathf.Clamp01(normalized);
            Vector3 position =
                Vector3.LerpUnclamped(start, end, t);
            Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f
                ? surfaceNormal.normalized
                : Vector3.up;
            position += normal *
                        (Mathf.Sin(t * Mathf.PI * 2f) *
                         wave *
                         (1f - t));
            return position;
        }

        public static int VisibleTrailSegments(
            int sampleCount,
            float normalizedProgress)
        {
            int count = Mathf.Max(1, sampleCount);
            if (normalizedProgress <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(
                Mathf.CeilToInt(
                    Mathf.Clamp01(normalizedProgress) * count),
                0,
                count);
        }

        private void ResolveReferences()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (bindings == null)
            {
                bindings = GetComponent<CraftLivePad2Bindings>();
            }
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null)
            {
                return;
            }

            if (observedGroupGeneration != state.groupGeneration)
            {
                ResetFlowLifecycle(state.groupGeneration);
            }

            bool shouldStartFlow = ShouldStartAutomaticFlow(
                externallySequenced,
                state.placement.status,
                state.groupGeneration,
                state.placement.transferSerial,
                handledTransferGeneration,
                handledTransferSerial,
                flowRoutine != null || externalFlowActive);

            RefreshPersistentFills(
                state,
                shouldStartFlow,
                shouldStartFlow
                    ? state.placement.confirmedSlot
                    : default);

            if (!shouldStartFlow)
            {
                return;
            }

            handledTransferGeneration = state.groupGeneration;
            handledTransferSerial =
                state.placement.transferSerial;
            activeTransferGeneration = handledTransferGeneration;
            activeTransferSerial = handledTransferSerial;
            flowRoutine = StartCoroutine(
                FlowGuarded(
                    state.Clone(),
                    activeTransferGeneration,
                    activeTransferSerial));
        }

        private IEnumerator FlowGuarded(
            CraftLiveRoomState snapshot,
            int groupGeneration,
            int transferSerial)
        {
            yield return RunFlowLifecycle(
                snapshot,
                groupGeneration,
                transferSerial,
                true);
        }

        private IEnumerator RunFlowLifecycle(
            CraftLiveRoomState snapshot,
            int groupGeneration,
            int transferSerial,
            bool publishStatsOnComplete)
        {
            // Allow flowRoutine to receive the Coroutine handle before any
            // checked publish synchronously invokes Refresh.
            yield return null;
            bool completed = false;
            try
            {
                if (!IsCurrentTransfer(
                        groupGeneration,
                        transferSerial,
                        CraftLivePlacementStatus.PlacementComplete))
                {
                    yield break;
                }

                yield return Flow(
                    snapshot,
                    groupGeneration,
                    transferSerial);
                completed = IsCurrentTransfer(
                    groupGeneration,
                    transferSerial,
                    CraftLivePlacementStatus.PlacementComplete);
                if (completed)
                {
                    completedTransferGeneration = groupGeneration;
                    completedTransferSerial = transferSerial;
                    if (publishStatsOnComplete && !externallySequenced)
                    {
                        session.PublishCurrentStatsToPad3(
                            groupGeneration,
                            transferSerial);
                    }
                }
            }
            finally
            {
                ClearDrops();
                if (!completed)
                {
                    // Never leave a half-revealed groove behind when a newer
                    // state overtakes this transfer. Refresh rebuilds it from
                    // authoritative slot state when the material was placed.
                    RemovePersistentFill(
                        snapshot.placement.confirmedSlot);
                }

                if (activeTransferGeneration == groupGeneration &&
                    activeTransferSerial == transferSerial)
                {
                    activeTransferGeneration = -1;
                    activeTransferSerial = -1;
                    flowRoutine = null;
                    if (!completed && IsCurrentTransfer(
                            groupGeneration,
                            transferSerial,
                            CraftLivePlacementStatus.PlacementComplete))
                    {
                        handledTransferGeneration = -1;
                        handledTransferSerial = -1;
                    }
                }

                if (!isResettingFlowLifecycle &&
                    isActiveAndEnabled && session != null &&
                    session.State != null &&
                    session.State.groupGeneration ==
                        observedGroupGeneration)
                {
                    Refresh(session.State);
                }
            }
        }

        private IEnumerator Flow(
            CraftLiveRoomState snapshot,
            int groupGeneration,
            int transferSerial)
        {
            CraftLiveMaterialDefinition material =
                session.Catalog != null
                    ? session.Catalog.FindMaterial(
                        snapshot.placement.materialId)
                    : null;
            CraftLiveSlotId slotId =
                snapshot.placement.confirmedSlot;
            Transform slot = GetSlotAnchor(slotId);
            Transform center = bindings != null
                ? bindings.LiquidFlowRoot
                : null;
            if (material == null ||
                slot == null ||
                center == null)
            {
                yield break;
            }

            Color color = material.EffectColor;
            ResolveFlowPath(
                slotId,
                out Vector3 flowStart,
                out Vector3 flowEnd);
            Vector3 surfaceNormal = ResolveSurfaceNormal();
            ResolveTrailDimensions(
                slotId,
                out float trailWidth,
                out _);

            RemovePersistentFill(slotId);
            PersistentFill fill = CreatePersistentFill(
                slotId,
                material.MaterialId,
                color,
                false);

            onFlowStarted?.Invoke(color);
            for (int i = 0; i < dropCount; i++)
            {
                GameObject drop = CreateDrop(
                    flowStart,
                    color);
                activeDrops.Add(drop);
            }

            float totalDuration =
                flowDuration +
                dropSpacingSeconds *
                Mathf.Max(0, dropCount - 1);
            float elapsed = 0f;
            while (elapsed < totalDuration)
            {
                if (!IsCurrentTransfer(
                        groupGeneration,
                        transferSerial,
                        CraftLivePlacementStatus.PlacementComplete))
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < activeDrops.Count; i++)
                {
                    GameObject drop = activeDrops[i];
                    if (drop == null)
                    {
                        continue;
                    }

                    float delayed =
                        elapsed - i * dropSpacingSeconds;
                    float t = Mathf.Clamp01(
                        delayed / flowDuration);
                    drop.SetActive(delayed >= 0f && t < 1f);
                    drop.transform.position = EvaluatePath(
                        flowStart,
                        flowEnd,
                        t,
                        0f,
                        surfaceNormal);
                    float scale =
                        Mathf.Sin(t * Mathf.PI) *
                            ResolveWorldLength(0.08f) +
                        trailWidth;
                    drop.transform.localScale =
                        Vector3.one * scale;
                }

                RevealPersistentFill(
                    fill,
                    Mathf.Clamp01(elapsed / flowDuration));
                onFlowProgress?.Invoke(
                    Mathf.Clamp01(elapsed / totalDuration));
                yield return null;
            }

            RevealPersistentFill(fill, 1f);
            ClearDrops();
            onFlowCompleted?.Invoke();
        }

        private void RefreshPersistentFills(
            CraftLiveRoomState state,
            bool excludeSlot,
            CraftLiveSlotId excludedSlot)
        {
            foreach (CraftLiveSlotId slot in Slots)
            {
                string materialId = state.slots.Get(slot) ??
                                    string.Empty;
                persistentMaterialIds.TryGetValue(
                    slot,
                    out string displayedMaterialId);

                if (string.IsNullOrWhiteSpace(materialId))
                {
                    RemovePersistentFill(slot);
                    continue;
                }

                if (persistentFills.ContainsKey(slot) &&
                    displayedMaterialId == materialId)
                {
                    continue;
                }

                RemovePersistentFill(slot);
                if (excludeSlot && slot == excludedSlot)
                {
                    continue;
                }

                CraftLiveMaterialDefinition material =
                    session != null && session.Catalog != null
                        ? session.Catalog.FindMaterial(materialId)
                        : null;
                if (material != null)
                {
                    CreatePersistentFill(
                        slot,
                        materialId,
                        material.EffectColor,
                        true);
                }
            }
        }

        private PersistentFill CreatePersistentFill(
            CraftLiveSlotId slot,
            string materialId,
            Color color,
            bool revealAll)
        {
            Transform center = bindings != null
                ? bindings.LiquidFlowRoot
                : null;
            if (center == null)
            {
                return null;
            }

            ResolveFlowPath(
                slot,
                out Vector3 flowStart,
                out Vector3 flowEnd);
            ResolveTrailDimensions(
                slot,
                out float resolvedTrailWidth,
                out float resolvedTrailDepth);

            PersistentFill fill = new PersistentFill
            {
                Root = new GameObject(
                    $"PersistentLiquid_{slot}_{materialId}")
            };
            // Flow points are calculated in world space. Keeping the runtime
            // fill under a scaled scene anchor applied that scale a second
            // time, which made later trails sink into the workbench or vanish.
            fill.Root.transform.SetParent(null, false);
            fill.Root.AddComponent<CraftLiveGeneratedRuntimeVisual>();

            if (TryResolveGuidePose(
                    CraftLivePad2AlignmentGuideKind.FlowWidth,
                    slot,
                    out CraftLivePad2GuidePose channelPose))
            {
                fill.GuideChannel = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                fill.GuideChannel.name = "AuthoredGrooveFill";
                fill.GuideChannel.transform.SetParent(
                    fill.Root.transform,
                    false);
                ResolveGuideWorldTransform(
                    channelPose,
                    out fill.GuideChannelPosition,
                    out fill.GuideChannelRotation,
                    out fill.GuideChannelScale);
                fill.GuideChannel.transform.SetPositionAndRotation(
                    fill.GuideChannelPosition,
                    fill.GuideChannelRotation);
                fill.GuideChannel.transform.localScale =
                    fill.GuideChannelScale;
                DestroySafely(
                    fill.GuideChannel.GetComponent<Collider>());
                ApplyColor(fill.GuideChannel, color);
            }

            fill.Rim = CreateRimGlow(
                fill.Root.transform,
                slot,
                color,
                out fill.RimMaterial,
                out fill.RimMesh);

            // Older scenes without alignment guides retain the sampled trail
            // fallback. Pad2 uses the authored FlowWidth transform verbatim.
            if (fill.GuideChannel == null)
            {
                int samples = Mathf.Max(4, trailSampleCount);
                for (int i = 0; i < samples; i++)
                {
                    float t = samples > 1
                        ? i / (float)(samples - 1)
                        : 1f;
                    GameObject segment = GameObject.CreatePrimitive(
                        PrimitiveType.Sphere);
                    segment.name = $"GrooveFill_{i:00}";
                    segment.transform.SetParent(
                        fill.Root.transform,
                        false);
                    segment.transform.position = Vector3.Lerp(
                        flowStart,
                        flowEnd,
                        t);
                    segment.transform.localScale =
                        new Vector3(
                            resolvedTrailWidth,
                            resolvedTrailWidth,
                            resolvedTrailDepth);
                    DestroySafely(segment.GetComponent<Collider>());
                    ApplyColor(segment, color);
                    fill.Trail.Add(segment);
                }
            }

            persistentFills[slot] = fill;
            persistentMaterialIds[slot] = materialId;
            RevealPersistentFill(fill, revealAll ? 1f : 0f);
            return fill;
        }

        private void RevealPersistentFill(
            PersistentFill fill,
            float normalizedProgress)
        {
            if (fill == null)
            {
                return;
            }

            float progress = Mathf.Clamp01(normalizedProgress);
            if (fill.GuideChannel != null)
            {
                Vector2 reveal = ResolveGuideChannelReveal(progress);
                Vector3 scale = fill.GuideChannelScale;
                scale.x *= reveal.y;
                fill.GuideChannel.transform.localScale = scale;
                fill.GuideChannel.transform.SetPositionAndRotation(
                    fill.GuideChannelPosition +
                    fill.GuideChannelRotation *
                    (Vector3.right *
                     (fill.GuideChannelScale.x * reveal.x)),
                    fill.GuideChannelRotation);
                fill.GuideChannel.SetActive(progress > 0f);
            }

            if (fill.Rim != null)
            {
                fill.Rim.SetActive(progress >= 0.98f);
            }

            int visible = VisibleTrailSegments(
                fill.Trail.Count,
                progress);

            for (int i = 0; i < fill.Trail.Count; i++)
            {
                if (fill.Trail[i] != null)
                {
                    fill.Trail[i].SetActive(i < visible);
                }
            }
        }

        public static Vector2 ResolveGuideChannelReveal(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            // x is the center offset measured in full channel lengths;
            // y is the revealed length. The guide's local -X end is the
            // authored FlowStart side, so the light grows toward +X.
            return new Vector2((clamped - 1f) * 0.5f, clamped);
        }

        private GameObject CreateRimGlow(
            Transform parent,
            CraftLiveSlotId slot,
            Color color,
            out Material rimMaterial,
            out Mesh rimMesh)
        {
            rimMaterial = null;
            rimMesh = null;
            if (bindings == null || parent == null ||
                !TryResolveGuidePose(
                    CraftLivePad2AlignmentGuideKind.Pool,
                    slot,
                    out CraftLivePad2GuidePose poolPose))
            {
                return null;
            }

            ResolveGuideWorldTransform(
                poolPose,
                out Vector3 worldPosition,
                out Quaternion worldRotation,
                out Vector3 worldScale);
            GameObject rimRoot = new GameObject("SlotRimGlow");
            rimRoot.transform.SetParent(parent, false);
            rimRoot.transform.SetPositionAndRotation(
                worldPosition,
                worldRotation);

            float radiusX = Mathf.Max(0.005f, worldScale.x * 0.5f);
            float radiusY = Mathf.Max(0.005f, worldScale.y * 0.5f);
            float rimWidth = Mathf.Max(
                0.0035f,
                Mathf.Min(worldScale.x, worldScale.y) * 0.07f);
            rimMesh = CreateEllipseRingMesh(
                radiusX,
                radiusY,
                rimWidth,
                96);
            MeshFilter filter = rimRoot.AddComponent<MeshFilter>();
            filter.sharedMesh = rimMesh;
            MeshRenderer renderer = rimRoot.AddComponent<MeshRenderer>();
            rimMaterial = CreateGlowMaterial(color);
            renderer.sharedMaterial = rimMaterial;
            return rimRoot;
        }

        private void ResolveGuideWorldTransform(
            CraftLivePad2GuidePose pose,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            position = bindings.transform.TransformPoint(
                pose.LocalPosition);
            rotation = bindings.transform.rotation * pose.LocalRotation;
            Vector3 rootScale = bindings.transform.lossyScale;
            scale = new Vector3(
                Mathf.Abs(pose.LocalScale.x * rootScale.x),
                Mathf.Abs(pose.LocalScale.y * rootScale.y),
                Mathf.Abs(pose.LocalScale.z * rootScale.z));
        }

        private static Mesh CreateEllipseRingMesh(
            float radiusX,
            float radiusY,
            float width,
            int segmentCount)
        {
            int segments = Mathf.Max(16, segmentCount);
            float innerRadiusX = Mathf.Max(0.001f, radiusX - width);
            float innerRadiusY = Mathf.Max(0.001f, radiusY - width);
            Vector3[] vertices = new Vector3[segments * 4];
            int[] triangles = new int[segments * 12];
            for (int i = 0; i < segments; i++)
            {
                float angle0 = i / (float)segments * Mathf.PI * 2f;
                float angle1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                int vertex = i * 4;
                vertices[vertex] = new Vector3(
                    Mathf.Cos(angle0) * radiusX,
                    Mathf.Sin(angle0) * radiusY,
                    0f);
                vertices[vertex + 1] = new Vector3(
                    Mathf.Cos(angle1) * radiusX,
                    Mathf.Sin(angle1) * radiusY,
                    0f);
                vertices[vertex + 2] = new Vector3(
                    Mathf.Cos(angle0) * innerRadiusX,
                    Mathf.Sin(angle0) * innerRadiusY,
                    0f);
                vertices[vertex + 3] = new Vector3(
                    Mathf.Cos(angle1) * innerRadiusX,
                    Mathf.Sin(angle1) * innerRadiusY,
                    0f);

                int triangle = i * 12;
                int[] indices =
                {
                    vertex, vertex + 1, vertex + 2,
                    vertex + 1, vertex + 3, vertex + 2,
                    vertex + 2, vertex + 1, vertex,
                    vertex + 2, vertex + 3, vertex + 1
                };
                for (int j = 0; j < indices.Length; j++)
                {
                    triangles[triangle + j] = indices[j];
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Pad2ContinuousRimMesh",
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateGlowMaterial(Color color)
        {
            // Shader.Find is not reliable in WebGL: shaders referenced only
            // by name can be stripped from the player. The Resources-backed
            // material is explicitly included in the build and already uses
            // the compatible URP surface.
            Material material =
                CraftLiveForgeUITheme.CreateCompatibleUnlitMaterial(
                    "Pad2RimGlowMaterial");
            if (material == null)
            {
                return null;
            }

            Color glow = color * 2.5f;
            glow.a = color.a;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", glow);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", glow);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", glow);
            }

            return material;
        }

        private void ResolveFlowPath(
            CraftLiveSlotId slot,
            out Vector3 start,
            out Vector3 end)
        {
            float padScale = ResolvePadScale();
            Vector3 surfaceLift =
                ResolveSurfaceNormal() * surfaceOffset * padScale;
            if (TryResolveGuidePose(
                    CraftLivePad2AlignmentGuideKind.FlowStart,
                    slot,
                    out CraftLivePad2GuidePose startPose) &&
                TryResolveGuidePose(
                    CraftLivePad2AlignmentGuideKind.FlowEnd,
                    slot,
                    out CraftLivePad2GuidePose endPose))
            {
                start = bindings.transform.TransformPoint(
                    startPose.LocalPosition);
                end = bindings.transform.TransformPoint(
                    endPose.LocalPosition);
                return;
            }

            Transform slotAnchor = GetSlotAnchor(slot);
            Transform center = bindings != null
                ? bindings.LiquidFlowRoot
                : null;
            start = slotAnchor != null
                ? slotAnchor.position + surfaceLift
                : transform.position;
            end = center != null
                ? center.TransformPoint(
                    CraftLivePad2SlotLayout.Get(slot)
                        .FlowEndPosition) + surfaceLift
                : start;
        }

        private void ResolveTrailDimensions(
            CraftLiveSlotId slot,
            out float width,
            out float depth)
        {
            float localWidth;
            float localDepth;
            if (TryResolveGuidePose(
                    CraftLivePad2AlignmentGuideKind.FlowWidth,
                    slot,
                    out CraftLivePad2GuidePose pose))
            {
                localWidth = Mathf.Max(0.02f, pose.LocalScale.y);
                localDepth = Mathf.Max(0.005f, pose.LocalScale.z);
            }
            else
            {
                localWidth = trailRadius;
                localDepth = trailRadius * 0.35f;
            }

            Vector2 world = ScaleTrailDimensionsToWorld(
                localWidth,
                localDepth,
                ResolvePadScale());
            width = world.x;
            depth = world.y;
        }

        public static Vector2 ScaleTrailDimensionsToWorld(
            float localWidth,
            float localDepth,
            float padScale)
        {
            float scale = Mathf.Max(0.0001f, Mathf.Abs(padScale));
            return new Vector2(
                Mathf.Max(0f, localWidth) * scale,
                Mathf.Max(0f, localDepth) * scale);
        }

        private bool TryResolveGuidePose(
            CraftLivePad2AlignmentGuideKind kind,
            CraftLiveSlotId slot,
            out CraftLivePad2GuidePose pose)
        {
            pose = default;
            return bindings != null &&
                   CraftLivePad2AlignmentGuide.TryResolveLocalPose(
                       bindings.transform,
                       kind,
                       slot,
                       out pose);
        }

        private GameObject CreateDrop(
            Vector3 position,
            Color color)
        {
            GameObject drop;
            if (liquidDropPrefab != null)
            {
                drop = Instantiate(
                    liquidDropPrefab,
                    position,
                    Quaternion.identity);
            }
            else
            {
                drop = GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);
                drop.transform.position = position;
                DestroySafely(drop.GetComponent<Collider>());
            }

            ApplyColor(drop, color);
            return drop;
        }

        private Transform GetSlotAnchor(CraftLiveSlotId slot)
        {
            if (bindings == null)
            {
                return null;
            }

            switch (slot)
            {
                case CraftLiveSlotId.Top:
                    return bindings.UpperLeftSlot;
                case CraftLiveSlotId.Left:
                    return bindings.MiddleLeftSlot;
                case CraftLiveSlotId.Right:
                    return bindings.UpperRightSlot;
                case CraftLiveSlotId.Bottom:
                    return bindings.MiddleRightSlot;
                case CraftLiveSlotId.Skill:
                    return bindings.LowerLeftSkillSlot;
                default:
                    return bindings.LowerRightAttributeSlot;
            }
        }

        private float ResolvePadScale()
        {
            if (bindings == null)
            {
                return 1f;
            }

            Vector3 scale = bindings.transform.lossyScale;
            return Mathf.Max(
                0.0001f,
                Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Max(
                        Mathf.Abs(scale.y),
                        Mathf.Abs(scale.z))));
        }

        private float ResolveWorldLength(float localLength)
        {
            return Mathf.Max(0f, localLength) * ResolvePadScale();
        }

        private bool IsCurrentTransfer(
            int groupGeneration,
            int transferSerial,
            CraftLivePlacementStatus expectedStatus)
        {
            CraftLiveRoomState current =
                session != null ? session.State : null;
            return current != null &&
                   current.groupGeneration == groupGeneration &&
                   current.placement != null &&
                   current.placement.transferSerial == transferSerial &&
                   current.placement.status == expectedStatus;
        }

        private void ResetFlowLifecycle(int groupGeneration)
        {
            // Set the generation first so a stopped coroutine can never
            // re-admit visuals or completion state from the previous group.
            isResettingFlowLifecycle = true;
            try
            {
                observedGroupGeneration = groupGeneration;
                if (flowRoutine != null)
                {
                    Coroutine staleRoutine = flowRoutine;
                    flowRoutine = null;
                    StopCoroutine(staleRoutine);
                }

                ClearDrops();
                ClearPersistentFills();
                handledTransferGeneration = -1;
                handledTransferSerial = -1;
                activeTransferGeneration = -1;
                activeTransferSerial = -1;
                completedTransferGeneration = -1;
                completedTransferSerial = -1;
                externalFlowActive = false;
            }
            finally
            {
                isResettingFlowLifecycle = false;
            }
        }

        private Vector3 ResolveSurfaceNormal()
        {
            if (bindings == null)
            {
                return Vector3.up;
            }

            Vector3 towardCamera = -bindings.transform.forward;
            return towardCamera.sqrMagnitude > 0.0001f
                ? towardCamera.normalized
                : Vector3.up;
        }

        private void RemovePersistentFill(CraftLiveSlotId slot)
        {
            if (persistentFills.TryGetValue(
                    slot,
                    out PersistentFill fill))
            {
                DestroySafely(fill.RimMaterial);
                DestroySafely(fill.RimMesh);
                DestroySafely(fill.Root);
                persistentFills.Remove(slot);
            }

            persistentMaterialIds.Remove(slot);
        }

        private void ClearPersistentFills()
        {
            foreach (PersistentFill fill in persistentFills.Values)
            {
                if (fill != null)
                {
                    DestroySafely(fill.RimMaterial);
                    DestroySafely(fill.RimMesh);
                    DestroySafely(fill.Root);
                }
            }

            persistentFills.Clear();
            persistentMaterialIds.Clear();
        }

        private void ClearDrops()
        {
            foreach (GameObject drop in activeDrops)
            {
                DestroySafely(drop);
            }

            activeDrops.Clear();
        }

        private static void ApplyColor(
            GameObject target,
            Color color)
        {
            MaterialPropertyBlock block =
                new MaterialPropertyBlock();
            foreach (Renderer renderer in
                     target.GetComponentsInChildren<Renderer>())
            {
                // WebGL displays an unsupported/stripped shader as magenta.
                // Repair the renderer before applying the per-material tint so
                // every drop and persistent groove segment uses a URP shader.
                CraftLiveForgeUITheme.EnsureCompatibleSurface(renderer);
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null &&
                        material.HasProperty("_EmissionColor"))
                    {
                        material.EnableKeyword("_EMISSION");
                    }
                }

                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                block.SetColor(
                    "_EmissionColor",
                    color * 2.5f);
                renderer.SetPropertyBlock(block);
            }
        }

        private static void DestroySafely(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
