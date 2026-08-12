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

        private int handledTransferSerial = -1;
        private Coroutine flowRoutine;

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

            if (flowRoutine != null)
            {
                StopCoroutine(flowRoutine);
                flowRoutine = null;
            }

            ClearDrops();
            ClearPersistentFills();
        }

        public void Configure(CraftLivePad2Bindings targetBindings)
        {
            bindings = targetBindings;
            ResolveReferences();
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

            bool shouldStartFlow =
                state.placement.status ==
                    CraftLivePlacementStatus.PlacementComplete &&
                state.placement.transferSerial !=
                    handledTransferSerial &&
                flowRoutine == null;

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

            handledTransferSerial =
                state.placement.transferSerial;
            flowRoutine = StartCoroutine(
                Flow(state.Clone()));
        }

        private IEnumerator Flow(CraftLiveRoomState snapshot)
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
                session.PublishCurrentStatsToPad3();
                flowRoutine = null;
                yield break;
            }

            Color color = material.EffectColor;
            ResolveFlowPath(
                slotId,
                out Vector3 flowStart,
                out Vector3 flowEnd);
            float padScale = ResolvePadScale();
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
                elapsed += Time.deltaTime;
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
                        Mathf.Sin(t * Mathf.PI) * 0.08f +
                        trailWidth;
                    drop.transform.localScale =
                        Vector3.one * scale * padScale;
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
            session.PublishCurrentStatsToPad3();
            onFlowCompleted?.Invoke();
            flowRoutine = null;
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
            fill.Root.transform.SetParent(center, false);
            fill.Root.AddComponent<CraftLiveGeneratedRuntimeVisual>();
            fill.Rim = CreateRimGlow(
                fill.Root.transform,
                slot,
                color,
                out fill.RimMaterial,
                out fill.RimMesh);
            if (fill.Rim != null)
            {
                fill.Rim.SetActive(revealAll);
            }

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
                segment.SetActive(revealAll);
                fill.Trail.Add(segment);
            }

            persistentFills[slot] = fill;
            persistentMaterialIds[slot] = materialId;
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

            int visible = VisibleTrailSegments(
                fill.Trail.Count,
                normalizedProgress);
            if (fill.Rim != null)
            {
                fill.Rim.SetActive(normalizedProgress >= 0.98f);
            }

            for (int i = 0; i < fill.Trail.Count; i++)
            {
                if (fill.Trail[i] != null)
                {
                    fill.Trail[i].SetActive(i < visible);
                }
            }
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
            if (bindings == null || parent == null)
            {
                return null;
            }

            Vector3 localCenter;
            Quaternion localRotation;
            Vector3 localSize;
            if (TryResolveGuidePose(
                    CraftLivePad2AlignmentGuideKind.Pool,
                    slot,
                    out CraftLivePad2GuidePose poolPose))
            {
                localCenter = poolPose.LocalPosition;
                localRotation = poolPose.LocalRotation;
                localSize = poolPose.LocalScale;
            }
            else
            {
                Transform anchor = GetSlotAnchor(slot);
                if (anchor == null)
                {
                    return null;
                }

                localCenter = bindings.transform.InverseTransformPoint(
                    anchor.position);
                localRotation =
                    Quaternion.Inverse(bindings.transform.rotation) *
                    anchor.rotation;
                localSize = new Vector3(1.05f, 1.05f, 0.04f);
            }

            GameObject rimRoot = new GameObject("SlotRimGlow");
            rimRoot.transform.SetParent(parent, false);
            rimRoot.transform.SetPositionAndRotation(
                bindings.transform.TransformPoint(localCenter),
                bindings.transform.rotation * localRotation);
            const int rimSegments = 96;
            float radiusX = Mathf.Max(0.05f, localSize.x * 0.5f);
            float radiusY = Mathf.Max(0.05f, localSize.y * 0.5f);
            float rimWidth = Mathf.Max(
                0.035f,
                Mathf.Min(localSize.x, localSize.y) * 0.07f);
            rimMesh = CreateEllipseRingMesh(
                radiusX,
                radiusY,
                rimWidth,
                rimSegments);
            MeshFilter filter = rimRoot.AddComponent<MeshFilter>();
            filter.sharedMesh = rimMesh;
            MeshRenderer renderer =
                rimRoot.AddComponent<MeshRenderer>();
            rimMaterial = CreateGlowMaterial(color);
            renderer.sharedMaterial = rimMaterial;

            return rimRoot;
        }

        private static Mesh CreateEllipseRingMesh(
            float radiusX,
            float radiusY,
            float width,
            int segmentCount)
        {
            int segments = Mathf.Max(16, segmentCount);
            float innerRadiusX = Mathf.Max(0.01f, radiusX - width);
            float innerRadiusY = Mathf.Max(0.01f, radiusY - width);
            Vector3[] vertices = new Vector3[segments * 4];
            int[] triangles = new int[segments * 12];

            for (int i = 0; i < segments; i++)
            {
                float angle0 = i / (float)segments *
                               Mathf.PI * 2f;
                float angle1 = (i + 1) / (float)segments *
                               Mathf.PI * 2f;
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
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
                triangles[triangle + 6] = vertex + 2;
                triangles[triangle + 7] = vertex + 1;
                triangles[triangle + 8] = vertex;
                triangles[triangle + 9] = vertex + 2;
                triangles[triangle + 10] = vertex + 3;
                triangles[triangle + 11] = vertex + 1;
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
            Shader shader = Shader.Find(
                                "Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Color") ??
                            Shader.Find("Standard");
            Material material = new Material(shader)
            {
                name = "Pad2RimGlowMaterial"
            };
            Color hdrColor = color * 2.5f;
            hdrColor.a = color.a;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", hdrColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", hdrColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", hdrColor);
            }

            return material;
        }

        private void ResolveFlowPath(
            CraftLiveSlotId slot,
            out Vector3 start,
            out Vector3 end)
        {
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
            float padScale = ResolvePadScale();
            Vector3 surfaceLift =
                ResolveSurfaceNormal() * surfaceOffset * padScale;
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
            if (TryResolveGuidePose(
                    CraftLivePad2AlignmentGuideKind.FlowWidth,
                    slot,
                    out CraftLivePad2GuidePose pose))
            {
                width = Mathf.Max(0.02f, pose.LocalScale.y);
                depth = Mathf.Max(0.005f, pose.LocalScale.z);
                return;
            }

            width = trailRadius;
            depth = trailRadius * 0.35f;
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
                foreach (Material material in renderer.materials)
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
