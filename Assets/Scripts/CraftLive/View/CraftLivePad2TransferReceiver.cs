using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad2TransferReceiver :
        MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad2Bindings bindings;
        [SerializeField] private GameObject fallbackTicketPrefab;
        [SerializeField] private GameObject fallbackMaterialPrefab;

        [Header("Animation")]
        [SerializeField, Min(0.1f)] private float arrivalDelay = 0.35f;
        [SerializeField, Min(0.1f)] private float arrivalDuration = 0.75f;
        [SerializeField, Min(0f)] private float arrivalArcHeight = 1.2f;
        [SerializeField, Min(0f)]
        [Tooltip("素材ガイドがない旧配置アンカーだけに適用する、盤面からカメラ側への距離です。ガイド使用時はガイドTransformのZがそのまま奥行になります。")]
        private float materialSurfaceOffset = 0.45f;
        [SerializeField, Min(0f), Tooltip(
            "WebGLで素材が盤面へ埋まることを防ぐ、カメラ方向への追加距離です。")]
        private float webGlSurfaceSafetyOffset = 0.12f;
        [SerializeField, Min(0f)] private float completionHoldSeconds = 0.55f;
        [SerializeField] private bool publishStatsAfterArrival = true;
        [SerializeField, Min(0f)] private float statusPublishDelay = 0.2f;

        [Header("Local Play Test")]
        [SerializeField] private bool autoStartQueuedArrivalInEditor = true;
        [SerializeField, Min(0f)] private float localAutoStartDelay = 0.2f;
        [SerializeField, Min(0f)] private float localStageDelay = 0.12f;

        [Header("Events")]
        [SerializeField] private UnityEvent<CraftLiveSlotId>
            onArrivalStarted;
        [SerializeField] private UnityEvent<Color>
            onThemeColorChanged;
        [SerializeField] private UnityEvent<CraftLiveSlotId>
            onPlacementCompleted;

        private readonly Dictionary<CraftLiveSlotId, GameObject>
            placedVisuals =
                new Dictionary<CraftLiveSlotId, GameObject>();
        private readonly Dictionary<CraftLiveSlotId, string>
            displayedMaterialIds =
                new Dictionary<CraftLiveSlotId, string>();
        private int handledTransferSerial = -1;
        private Coroutine receiveRoutine;
        private Coroutine localAutoStartRoutine;
        private GameObject activeTransferVisual;

        private static readonly CraftLiveSlotId[] Slots =
        {
            CraftLiveSlotId.Top,
            CraftLiveSlotId.Left,
            CraftLiveSlotId.Right,
            CraftLiveSlotId.Bottom,
            CraftLiveSlotId.Skill,
            CraftLiveSlotId.Attribute
        };

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

            if (receiveRoutine != null)
            {
                StopCoroutine(receiveRoutine);
                receiveRoutine = null;
            }

            if (localAutoStartRoutine != null)
            {
                StopCoroutine(localAutoStartRoutine);
                localAutoStartRoutine = null;
            }

            DestroySafely(activeTransferVisual);
            activeTransferVisual = null;
        }

        public void Configure(CraftLivePad2Bindings targetBindings)
        {
            bindings = targetBindings;
            ResolveReferences();
        }

        public void SetLocalAutoStartEnabled(bool value)
        {
            autoStartQueuedArrivalInEditor = value;
            if (!value && localAutoStartRoutine != null)
            {
                StopCoroutine(localAutoStartRoutine);
                localAutoStartRoutine = null;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Start First Queued Arrival")]
        private void DebugStartFirstQueuedArrival()
        {
            ResolveReferences();
            if (!Application.isPlaying ||
                session == null ||
                session.State == null ||
                session.State.placement.status !=
                    CraftLivePlacementStatus.Idle ||
                session.State.transferQueue == null ||
                session.State.transferQueue.Count == 0)
            {
                Debug.LogWarning(
                    "Craft-live: Play Modeで素材を転送待ちへ " +
                    "追加してから実行してください。",
                    this);
                return;
            }

            session.BeginSingleTransfer();
            session.MarkTransferLaunching();
            session.MarkTransferArriving();
        }
#endif

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
            if (state == null || bindings == null)
            {
                return;
            }

            foreach (CraftLiveSlotId slot in Slots)
            {
                RefreshPlacedVisual(state, slot);
            }

#if UNITY_EDITOR
            if (autoStartQueuedArrivalInEditor &&
                localAutoStartRoutine == null &&
                IsLocalPlayTest() &&
                CanAutoStartLocalArrival(state, session.Role))
            {
                localAutoStartRoutine = StartCoroutine(
                    AutoStartLocalArrival());
            }
#endif

            if (state.placement.status ==
                    CraftLivePlacementStatus.Pad2Arriving &&
                state.placement.transferSerial !=
                    handledTransferSerial &&
                receiveRoutine == null)
            {
                handledTransferSerial =
                    state.placement.transferSerial;
                receiveRoutine = StartCoroutine(
                    Receive(state.Clone()));
            }
        }

        private static bool IsLocalPlayTest()
        {
            CraftLiveRoomTransport transport =
                FindAnyObjectByType<CraftLiveRoomTransport>();
            return transport == null || !transport.IsRemoteMode;
        }

        public static bool CanAutoStartLocalArrival(
            CraftLiveRoomState state,
            CraftLiveRole role)
        {
            return state != null &&
                   role == CraftLiveRole.WorkbenchPad &&
                   state.sessionPhase == CraftLiveSessionPhase.Playing &&
                   state.placement.status ==
                       CraftLivePlacementStatus.Idle &&
                   state.transferQueue != null &&
                   state.transferQueue.Count > 0;
        }

#if UNITY_EDITOR
        private IEnumerator AutoStartLocalArrival()
        {
            if (localAutoStartDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    localAutoStartDelay);
            }

            if (!CanAutoStartLocalArrival(
                    session != null ? session.State : null,
                    session != null
                        ? session.Role
                        : CraftLiveRole.Auto) ||
                !session.BeginSingleTransfer())
            {
                localAutoStartRoutine = null;
                yield break;
            }

            if (localStageDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    localStageDelay);
            }

            session.MarkTransferLaunching();
            if (localStageDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    localStageDelay);
            }

            session.MarkTransferArriving();
            localAutoStartRoutine = null;
        }
#endif

        private IEnumerator Receive(CraftLiveRoomState snapshot)
        {
            if (arrivalDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    arrivalDelay);
            }

            CraftLiveMaterialDefinition material =
                session.Catalog != null
                    ? session.Catalog.FindMaterial(
                        snapshot.placement.materialId)
                    : null;
            CraftLiveSlotId slot =
                snapshot.placement.confirmedSlot;
            Transform target = GetSlotAnchor(slot);
            if (material == null || target == null)
            {
                session.CompleteCurrentPlacement();
                receiveRoutine = null;
                session.ContinueAfterPlacement();
                yield break;
            }

            onArrivalStarted?.Invoke(slot);
            onThemeColorChanged?.Invoke(material.EffectColor);
            Vector3 start = ResolveArrivalPosition(slot);
            Vector3 displayTarget = ResolveDisplayPosition(
                slot,
                target);
            Quaternion displayRotation = ResolveDisplayRotation(
                slot,
                target);
            float materialSize = ResolveMaterialSize(slot, 0.58f);
            Vector3 transformPoint =
                Vector3.Lerp(start, displayTarget, 0.5f);
            transformPoint += ResolveSurfaceNormal() *
                              arrivalArcHeight * 0.5f;

            GameObject ticket = CreateVisual(
                material.TransferTicketPrefab,
                fallbackTicketPrefab,
                PrimitiveType.Cube,
                start,
                Quaternion.identity,
                null,
                ResolveWorldVisualSize(0.42f),
                0f,
                false);
            activeTransferVisual = ticket;
            ApplyGlowColor(ticket, material.EffectColor);
            yield return AnimateTicket(
                ticket.transform,
                start,
                transformPoint);
            DestroySafely(ticket);

            GameObject materialVisual = CreateVisual(
                material.WorldPrefab,
                fallbackMaterialPrefab,
                ResolvePrimitive(material.MaterialForm),
                transformPoint,
                displayRotation,
                null,
                ResolveWorldVisualSize(materialSize),
                material.Pad2PreviewRollDegrees,
                true);
            activeTransferVisual = materialVisual;
            ApplyMaterialColor(
                materialVisual,
                material.EffectColor);
            yield return AnimateLanding(
                materialVisual.transform,
                transformPoint,
                slot,
                target,
                material.MaterialForm);

            if (material.PlacementEffectPrefab != null)
            {
                GameObject effect = Instantiate(
                    material.PlacementEffectPrefab,
                    displayTarget,
                    displayRotation);
                Destroy(effect, 5f);
            }

            CraftLiveAudio.PlayMaterialLanding(
                material,
                GetComponent<AudioSource>());

            DestroySafely(materialVisual);
            activeTransferVisual = null;
            session.CompleteCurrentPlacement();
            onPlacementCompleted?.Invoke(slot);
            if (publishStatsAfterArrival)
            {
                if (statusPublishDelay > 0f)
                {
                    yield return new WaitForSecondsRealtime(
                        statusPublishDelay);
                }

                session.PublishCurrentStatsToPad3();
            }

            CraftLiveLiquidFlowController flowController =
                FindAnyObjectByType<CraftLiveLiquidFlowController>();
            int transferSerial = snapshot.placement.transferSerial;
            if (flowController != null)
            {
                // CompleteCurrentPlacement starts the groove light through
                // StateChanged. Do not advance the batch until that material's
                // full light pass has finished, so every queued item repeats
                // the same place-then-flow sequence as the first one.
                yield return null;
                float waited = 0f;
                float timeout = Mathf.Max(8f, completionHoldSeconds);
                while (!flowController.HasCompletedFlow(transferSerial) &&
                       waited < timeout)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else if (completionHoldSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(completionHoldSeconds);
            }

            receiveRoutine = null;
            session.ContinueAfterPlacement();
        }

        private void RefreshPlacedVisual(
            CraftLiveRoomState state,
            CraftLiveSlotId slot)
        {
            string materialId = state.slots.Get(slot) ??
                                string.Empty;
            displayedMaterialIds.TryGetValue(
                slot,
                out string displayed);
            if (displayed == materialId)
            {
                return;
            }

            displayedMaterialIds[slot] = materialId;
            if (placedVisuals.TryGetValue(
                    slot,
                    out GameObject oldVisual))
            {
                DestroySafely(oldVisual);
                placedVisuals.Remove(slot);
            }

            if (string.IsNullOrWhiteSpace(materialId))
            {
                return;
            }

            CraftLiveMaterialDefinition material =
                session.Catalog != null
                    ? session.Catalog.FindMaterial(materialId)
                    : null;
            Transform anchor = GetSlotAnchor(slot);
            if (material == null || anchor == null)
            {
                return;
            }

            GameObject visual = CreateVisual(
                material.WorldPrefab,
                fallbackMaterialPrefab,
                ResolvePrimitive(material.MaterialForm),
                ResolveDisplayPosition(slot, anchor),
                ResolveDisplayRotation(slot, anchor),
                anchor,
                ResolveMaterialSize(slot, 0.58f),
                material.Pad2PreviewRollDegrees,
                true);
            visual.name = $"Placed_{slot}_{materialId}";
            ApplyMaterialColor(visual, material.EffectColor);
            DisableColliders(visual);
            placedVisuals[slot] = visual;
        }

        private IEnumerator AnimateTicket(
            Transform ticket,
            Vector3 start,
            Vector3 end)
        {
            float duration = arrivalDuration * 0.45f;
            float elapsed = 0f;
            Vector3 surfaceNormal = ResolveSurfaceNormal();
            Quaternion startRotation = ticket.rotation;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 position =
                    Vector3.LerpUnclamped(start, end, t);
                position += surfaceNormal *
                            Mathf.Sin(t * Mathf.PI) *
                            arrivalArcHeight;
                ticket.position = position;
                ticket.rotation = Quaternion.AngleAxis(
                    Mathf.Sin(t * Mathf.PI) * 8f,
                    surfaceNormal) * startRotation;
                yield return null;
            }
        }

        private IEnumerator AnimateLanding(
            Transform visual,
            Vector3 start,
            CraftLiveSlotId slot,
            Transform target,
            CraftLiveMaterialForm form)
        {
            float duration = arrivalDuration * 0.55f;
            float elapsed = 0f;
            Vector3 originalScale = visual.localScale;
            Vector3 targetPosition = ResolveDisplayPosition(
                slot,
                target);
            Quaternion targetRotation = ResolveDisplayRotation(
                slot,
                target);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased =
                    1f - Mathf.Pow(1f - t, 3f);
                Vector3 position = Vector3.LerpUnclamped(
                    start,
                    targetPosition,
                    eased);
                ApplyFormMotion(
                    visual,
                    targetRotation,
                    form,
                    t,
                    ref position,
                    originalScale);
                visual.position = position;
                yield return null;
            }

            visual.SetPositionAndRotation(
                targetPosition,
                targetRotation);
            visual.localScale = originalScale;
        }

        private void ApplyFormMotion(
            Transform visual,
            Quaternion targetRotation,
            CraftLiveMaterialForm form,
            float t,
            ref Vector3 position,
            Vector3 originalScale)
        {
            switch (form)
            {
                case CraftLiveMaterialForm.Gem:
                    position.y +=
                        Mathf.Abs(
                            Mathf.Sin(t * Mathf.PI * 2f)) *
                        arrivalArcHeight *
                        (1f - t) *
                        0.28f;
                    visual.localScale =
                        originalScale *
                        (1f +
                         Mathf.Sin(t * Mathf.PI) * 0.12f);
                    break;
                case CraftLiveMaterialForm.Charm:
                    visual.rotation =
                        targetRotation *
                        Quaternion.Euler(
                            0f,
                            0f,
                            Mathf.Sin(t * Mathf.PI * 3f) *
                            (1f - t) *
                            20f);
                    break;
                case CraftLiveMaterialForm.Spirit:
                    position += (targetRotation * Vector3.right) *
                                (Mathf.Sin(
                                     t * Mathf.PI * 2f) *
                                 arrivalArcHeight *
                                 (1f - t) *
                                 0.22f);
                    position.y +=
                        Mathf.Sin(t * Mathf.PI) *
                        arrivalArcHeight *
                        0.24f;
                    break;
                default:
                    position.y +=
                        Mathf.Sin(t * Mathf.PI) *
                        arrivalArcHeight *
                        0.1f;
                    visual.rotation = Quaternion.Slerp(
                        visual.rotation,
                        targetRotation,
                        t);
                    break;
            }
        }

        private Vector3 ResolveArrivalPosition(
            CraftLiveSlotId slot)
        {
            Vector3 center = bindings.TransferArrivalRoot != null
                ? bindings.TransferArrivalRoot.position
                : transform.position + Vector3.up * 5f;
            Transform target = GetSlotAnchor(slot);
            if (target == null)
            {
                return center;
            }

            Transform padRoot = bindings.transform;
            Vector3 arrivalLocal =
                padRoot.InverseTransformPoint(center);
            Vector3 targetLocal =
                padRoot.InverseTransformPoint(
                    ResolveDisplayPosition(slot, target));
            arrivalLocal = ResolveArrivalLocalPosition(
                arrivalLocal,
                targetLocal);
            return padRoot.TransformPoint(arrivalLocal);
        }

        public static Vector3 ResolveArrivalLocalPosition(
            Vector3 arrivalLocal,
            Vector3 targetLocal)
        {
            arrivalLocal.x += Mathf.Clamp(
                targetLocal.x * 0.28f,
                -0.55f,
                0.55f);
            return arrivalLocal;
        }

        public static Vector3 ResolveDisplayLocalPosition(
            Vector3 targetLocal,
            float surfaceOffset,
            bool guideDefinesFinalPosition = false)
        {
            if (!guideDefinesFinalPosition)
            {
                targetLocal.z -= Mathf.Max(0f, surfaceOffset);
            }

            return targetLocal;
        }

        private Vector3 ResolveDisplayPosition(
            CraftLiveSlotId slot,
            Transform target)
        {
            Vector3 resolved;
            if (TryResolvePlacementPose(
                    slot,
                    out CraftLivePad2GuidePose pose))
            {
                resolved = bindings.transform.TransformPoint(
                    ResolveDisplayLocalPosition(
                        pose.LocalPosition,
                        materialSurfaceOffset,
                        true));
            }
            else
            {
                resolved = target != null
                    ? target.TransformPoint(
                    ResolveDisplayLocalPosition(
                        Vector3.zero,
                        materialSurfaceOffset))
                    : transform.position;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            resolved += ResolveSurfaceNormal() *
                        ResolveWorldVisualSize(webGlSurfaceSafetyOffset);
#endif
            return resolved;
        }

        private Quaternion ResolveDisplayRotation(
            CraftLiveSlotId slot,
            Transform target)
        {
            if (TryResolvePlacementPose(
                    slot,
                    out CraftLivePad2GuidePose pose))
            {
                return bindings.transform.rotation *
                       pose.LocalRotation;
            }

            return target != null
                ? target.rotation
                : transform.rotation;
        }

        private bool TryResolvePlacementPose(
            CraftLiveSlotId slot,
            out CraftLivePad2GuidePose pose)
        {
            pose = default;
            if (bindings == null)
            {
                return false;
            }

            // Dedicated material guides take priority. Slots from the older
            // scene that do not have one use their visible pool guide, keeping
            // the Scene-view guide and final placement at exactly one pose.
            return CraftLivePad2AlignmentGuide.TryResolveLocalPose(
                       bindings.transform,
                       CraftLivePad2AlignmentGuideKind.Material,
                       slot,
                       out pose) ||
                   CraftLivePad2AlignmentGuide.TryResolveLocalPose(
                       bindings.transform,
                       CraftLivePad2AlignmentGuideKind.Pool,
                       slot,
                       out pose);
        }

        private float ResolveMaterialSize(
            CraftLiveSlotId slot,
            float fallback)
        {
            if (bindings != null &&
                CraftLivePad2AlignmentGuide.TryResolveLocalPose(
                    bindings.transform,
                    CraftLivePad2AlignmentGuideKind.Material,
                    slot,
                    out CraftLivePad2GuidePose pose))
            {
                return Mathf.Max(
                    0.05f,
                    Mathf.Max(
                        pose.LocalScale.x,
                        pose.LocalScale.y));
            }

            return fallback;
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

        private Transform GetSlotAnchor(CraftLiveSlotId slot)
        {
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

        private static PrimitiveType ResolvePrimitive(
            CraftLiveMaterialForm form)
        {
            switch (form)
            {
                case CraftLiveMaterialForm.Ore:
                    return PrimitiveType.Cube;
                case CraftLiveMaterialForm.Gem:
                    return PrimitiveType.Capsule;
                case CraftLiveMaterialForm.Charm:
                    return PrimitiveType.Quad;
                default:
                    return PrimitiveType.Sphere;
            }
        }

        private static GameObject CreateVisual(
            GameObject preferred,
            GameObject fallback,
            PrimitiveType primitive,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            float targetSize,
            float rollDegrees,
            bool preferUpright)
        {
            GameObject visual = new GameObject("RuntimeVisual");
            if (parent != null)
            {
                visual.transform.SetParent(parent, false);
            }

            visual.transform.SetPositionAndRotation(position, rotation);
            GameObject contentObject = new GameObject("VisualContent");
            contentObject.transform.SetParent(visual.transform, false);
            Transform content = contentObject.transform;
            if (preferred != null || fallback != null)
            {
                Instantiate(
                    preferred != null ? preferred : fallback,
                    content,
                    false);
            }
            else
            {
                GameObject primitiveObject =
                    GameObject.CreatePrimitive(primitive);
                primitiveObject.transform.SetParent(content, false);
            }

            CraftLiveRuntimeVisualUtility.FitAndCenter(
                content,
                targetSize,
                true,
                rollDegrees,
                preferUpright: preferUpright);
            DisableColliders(visual);
            return visual;
        }

        private float ResolveWorldVisualSize(float localSize)
        {
            Vector3 scale = transform.lossyScale;
            float padScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return localSize * Mathf.Max(0.0001f, padScale);
        }

        private static void DisableColliders(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            foreach (Collider collider in
                     target.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
        }

        private static void ApplyGlowColor(
            GameObject target,
            Color color)
        {
            if (target == null)
            {
                return;
            }

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
                    color * 2f);
                renderer.SetPropertyBlock(block);
            }
        }

        private static void ApplyMaterialColor(
            GameObject target,
            Color color)
        {
            if (target == null)
            {
                return;
            }

            MaterialPropertyBlock block =
                new MaterialPropertyBlock();
            foreach (Renderer renderer in
                     target.GetComponentsInChildren<Renderer>())
            {
                foreach (Material material in renderer.materials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    material.DisableKeyword("_EMISSION");
                    if (material.HasProperty("_EmissionColor"))
                    {
                        material.SetColor(
                            "_EmissionColor",
                            Color.black);
                    }
                }

                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                block.SetColor("_EmissionColor", Color.black);
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
