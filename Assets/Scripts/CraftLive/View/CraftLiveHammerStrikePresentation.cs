using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    /// <summary>
    /// Presentation layer for Pad 2 forging. A user-authored hammer can be
    /// assigned later; its handle end acts as the rotation pivot. The class
    /// owns camera focus, directional guidance, weapon glow, sparks and
    /// material-merging motes without changing synthesis state rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CraftLiveHammerStrikePresentation : MonoBehaviour
    {
        private const string GeneratedRootName =
            "Generated_HammerStrikePresentation";

        [Header("Scene References")]
        [SerializeField] private CraftLivePad2Bindings bindings;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform weaponFocusTarget;

        [Header("Hammer Model - Assign Your Model Here")]
        [Tooltip("Prefab root should use the end of the handle as its pivot.")]
        [SerializeField] private GameObject hammerPrefab;
        [Tooltip("Use this when the hammer already exists in the Pad 2 scene.")]
        [SerializeField] private Transform hammerHandlePivot;
        [Tooltip("Optional point on the hammer head. Name a prefab child ImpactPoint for automatic lookup.")]
        [SerializeField] private Transform hammerHeadImpactPoint;
        [SerializeField] private string impactPointChildName = "ImpactPoint";
        [SerializeField] private Vector3 hammerPivotLocalPosition =
            new Vector3(1.35f, -0.5f, -0.58f);
        [SerializeField] private Vector3 hammerModelLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 hammerModelLocalEuler = Vector3.zero;
        [SerializeField] private Vector3 hammerModelLocalScale = Vector3.one;
        [SerializeField] private Vector3 raisedHammerEuler =
            new Vector3(0f, 0f, -35f);
        [SerializeField] private Vector3 impactHammerEuler =
            new Vector3(0f, 0f, 65f);
        [Tooltip("At synthesis start, move the handle pivot so ImpactPoint meets the weapon at the impact rotation.")]
        [SerializeField] private bool alignImpactPointToWeapon = true;
        [SerializeField] private Vector3 hammerImpactLocalOffset =
            new Vector3(0f, 0f, -0.035f);
        [SerializeField, Min(0f)]
        [Tooltip("武器より確実にカメラ側へ出す距離です。")]
        private float hammerCameraClearance = 0.12f;

        [Header("Strike Timing")]
        [SerializeField, Min(0.04f)] private float strikeDownDuration = 0.16f;
        [SerializeField, Min(0.02f)] private float impactHoldDuration = 0.055f;
        [SerializeField, Min(0.05f)] private float returnDuration = 0.28f;
        [SerializeField, Range(0.4f, 0.95f)]
        private float dragPreviewFraction = 0.72f;

        [Header("Camera Focus")]
        [SerializeField] private bool focusCameraDuringSynthesis = true;
        [SerializeField, Range(0.35f, 0.9f)]
        private float focusDistanceRatio = 0.82f;
        [SerializeField, Min(0.25f)] private float minimumFocusDistance = 1.12f;
        [SerializeField] private Vector3 weaponFocusLocalOffset =
            new Vector3(0f, 0.22f, -0.08f);
        [SerializeField, Range(25f, 60f)] private float focusFieldOfView = 50f;
        [SerializeField, Min(0.05f)] private float cameraFocusDuration = 0.38f;
        [SerializeField, Min(0f)] private float completionFocusHold = 0.85f;
        [SerializeField, Min(0.05f)] private float cameraRestoreDuration = 0.48f;

        [Header("Weapon Glow")]
        [SerializeField] private Color weaponGlowColor =
            new Color(1f, 0.34f, 0.055f, 1f);
        [SerializeField, Range(0f, 5f)] private float baseGlowStrength = 0.45f;
        [SerializeField, Range(0f, 8f)] private float completedGlowStrength = 3.4f;
        [SerializeField, Range(0f, 8f)] private float strikeGlowBoost = 3.8f;
        [SerializeField, Min(0.05f)] private float strikeGlowDecay = 0.34f;

        [Header("Impact Sparks")]
        [SerializeField] private ParticleSystem sparkPrefab;
        [SerializeField, Range(4, 80)] private int sparkCount = 28;
        [SerializeField, Range(0.02f, 0.2f)] private float sparkSize = 0.055f;
        [SerializeField, Range(0.1f, 2f)] private float sparkSpeed = 0.72f;

        [Header("Material Merge")]
        [SerializeField, Range(1, 4)] private int motesPerMaterialSlot = 2;
        [SerializeField, Range(0.02f, 0.2f)] private float materialMoteSize = 0.075f;
        [SerializeField, Min(0.1f)] private float materialMergeDuration = 0.52f;
        [SerializeField, Range(0f, 0.8f)] private float materialMergeArc = 0.28f;

        [Header("Strike Direction Guide")]
        [SerializeField] private Color guideColor =
            new Color(1f, 0.56f, 0.12f, 1f);
        [SerializeField] private Vector3 guideStartLocal =
            new Vector3(1.8f, 0.95f, -0.92f);
        [SerializeField] private Vector3 guideEndLocal =
            new Vector3(0f, 0.15f, -0.92f);
        [SerializeField, Range(0.03f, 0.18f)]
        private float guideThickness = 0.075f;
        [SerializeField] private string guideLabel =
            "矢印に沿って\n打撃";

        [Header("Events")]
        [SerializeField] private UnityEngine.Events.UnityEvent onImpactVisual;
        [SerializeField] private UnityEngine.Events.UnityEvent<float>
            onBlendProgress;

        private GameObject generatedRoot;
        private GameObject generatedHammer;
        private Transform runtimeHammerPivot;
        private Transform runtimeImpactPoint;
        private GameObject guideRoot;
        private readonly List<Renderer> guideRenderers =
            new List<Renderer>();
        private GameObject progressHud;
        private TextMesh progressText;
        private ParticleSystem sparks;
        private Material generatedSparkMaterial;
        private Light weaponLight;

        private readonly List<Renderer> weaponRenderers =
            new List<Renderer>();
        private readonly List<MaterialPropertyBlock> originalWeaponBlocks =
            new List<MaterialPropertyBlock>();
        private readonly List<Material[]> originalWeaponMaterials =
            new List<Material[]>();
        private readonly List<Material[]> glowWeaponMaterials =
            new List<Material[]>();

        private bool isMixing;
        private bool isStriking;
        private bool built;
        private float blendProgress;
        private float strikeGlow;
        private Quaternion raisedRotation;
        private Quaternion impactRotation;

        private bool cameraPoseCaptured;
        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        private float originalCameraFieldOfView;
        private Coroutine cameraRoutine;
        private Coroutine finishRoutine;
        private Coroutine activeStrikeRoutine;

        public bool IsStriking => isStriking;
        public bool IsGuideVisible =>
            guideRoot != null && guideRoot.activeSelf;

        private void Awake()
        {
            ResolveReferences();
            BuildVisuals();
        }

        private void OnDisable()
        {
            HideImmediately();
        }

        private void OnDestroy()
        {
            if (generatedSparkMaterial != null)
            {
                DestroySafely(generatedSparkMaterial);
            }
        }

        private void LateUpdate()
        {
            if (!built)
            {
                return;
            }

            if (strikeGlow > 0f)
            {
                strikeGlow = Mathf.MoveTowards(
                    strikeGlow,
                    0f,
                    Time.deltaTime / Mathf.Max(0.05f, strikeGlowDecay));
            }

            UpdateWeaponGlow();
            UpdateGuidePulse();
        }

        public void Configure(CraftLivePad2Bindings targetBindings)
        {
            bindings = targetBindings;
            ResolveReferences();
            BuildVisuals();
        }

        public void SetMixing(
            bool value,
            float normalizedProgress,
            int completedPasses,
            int requiredPasses)
        {
            ResolveReferences();
            BuildVisuals();
            blendProgress = Mathf.Clamp01(normalizedProgress);
            onBlendProgress?.Invoke(blendProgress);

            if (progressText != null)
            {
                progressText.text =
                    $"鍛錬 {Mathf.Max(0, completedPasses)} / " +
                    $"{Mathf.Max(1, requiredPasses)}";
            }

            if (value == isMixing)
            {
                SetGuideVisible(value && !isStriking);
                if (!value && finishRoutine == null)
                {
                    SetHammerVisible(false);
                    SetVisualRootsActive(false);
                    RestoreWeaponMaterials();
                    RestoreCameraImmediate();
                }
                return;
            }

            isMixing = value;
            if (value)
            {
                if (finishRoutine != null)
                {
                    StopCoroutine(finishRoutine);
                    finishRoutine = null;
                }

                SetVisualRootsActive(true);
                SetHammerVisible(true);
                CacheWeaponRenderers();
                AlignHammerImpactPoint();
                SetHammerRotation(raisedRotation);
                SetGuideVisible(true);
                BeginCameraFocus();
            }
            else
            {
                SetGuideVisible(false);
                strikeGlow = 1f;
                if (finishRoutine != null)
                {
                    StopCoroutine(finishRoutine);
                }

                finishRoutine = StartCoroutine(FinishPresentation());
            }
        }

        /// <summary>
        /// Immediately removes every forging-only visual and input surface.
        /// This is used when Pad 2 returns to editing/material placement; a
        /// completion hold from the previous state must never cover the slot
        /// colliders or the placement confirmation controls.
        /// </summary>
        public void HideImmediately()
        {
            StopAllCoroutines();
            cameraRoutine = null;
            finishRoutine = null;
            activeStrikeRoutine = null;
            isMixing = false;
            isStriking = false;
            strikeGlow = 0f;
            SetGuideVisible(false);
            SetHammerVisible(false);
            SetVisualRootsActive(false);
            RestoreWeaponMaterials();
            RestoreCameraImmediate();
        }

        public void PreviewStrike(float normalized)
        {
            if (!isMixing || isStriking || runtimeHammerPivot == null)
            {
                return;
            }

            float preview = Mathf.Clamp01(normalized) *
                            Mathf.Clamp01(dragPreviewFraction);
            SetHammerRotation(Quaternion.Slerp(
                raisedRotation,
                impactRotation,
                Smooth01(preview)));
        }

        public IEnumerator PlayStrikeSequence(Action impactCallback)
        {
            if (!isMixing || isStriking || runtimeHammerPivot == null)
            {
                yield break;
            }

            isStriking = true;
            SetGuideVisible(false);
            Quaternion start = runtimeHammerPivot.localRotation;
            float elapsed = 0f;
            while (elapsed < strikeDownDuration)
            {
                elapsed += Time.deltaTime;
                float t = Smooth01(elapsed /
                                   Mathf.Max(0.04f, strikeDownDuration));
                SetHammerRotation(Quaternion.Slerp(
                    start,
                    impactRotation,
                    t));
                yield return null;
            }

            SetHammerRotation(impactRotation);
            TriggerImpactVisuals();
            impactCallback?.Invoke();

            if (impactHoldDuration > 0f)
            {
                yield return new WaitForSeconds(impactHoldDuration);
            }

            Quaternion recoil = Quaternion.SlerpUnclamped(
                impactRotation,
                raisedRotation,
                1.08f);
            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Smooth01(elapsed /
                                   Mathf.Max(0.05f, returnDuration));
                Quaternion target = t < 0.42f
                    ? Quaternion.Slerp(
                        impactRotation,
                        recoil,
                        Smooth01(t / 0.42f))
                    : Quaternion.Slerp(
                        recoil,
                        raisedRotation,
                        Smooth01((t - 0.42f) / 0.58f));
                SetHammerRotation(target);
                yield return null;
            }

            SetHammerRotation(raisedRotation);
            isStriking = false;
            activeStrikeRoutine = null;
            SetGuideVisible(isMixing);
        }

        public Coroutine StartStrike(Action impactCallback)
        {
            if (activeStrikeRoutine != null || !isMixing)
            {
                return activeStrikeRoutine;
            }

            activeStrikeRoutine = StartCoroutine(
                PlayStrikeSequence(impactCallback));
            return activeStrikeRoutine;
        }

        public static float ProjectInputDelta(
            Vector2 pointerDelta,
            Vector2 strikeDirection)
        {
            Vector2 direction = strikeDirection.sqrMagnitude > 0.0001f
                ? strikeDirection.normalized
                : Vector2.down;
            return Vector2.Dot(pointerDelta, direction);
        }

        private void ResolveReferences()
        {
            if (bindings == null)
            {
                bindings = GetComponent<CraftLivePad2Bindings>();
            }

            if (weaponFocusTarget == null && bindings != null)
            {
                weaponFocusTarget = bindings.CenterWeaponRoot;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(
                    FindObjectsInactive.Include);
                foreach (Camera candidate in cameras)
                {
                    if (candidate != null && candidate.isActiveAndEnabled)
                    {
                        targetCamera = candidate;
                        break;
                    }
                }

                if (targetCamera == null && cameras.Length > 0)
                {
                    targetCamera = cameras[0];
                }
            }
        }

        private void BuildVisuals()
        {
            if (built || bindings == null || bindings.HammerRoot == null)
            {
                return;
            }

            Transform existing = bindings.HammerRoot.Find(GeneratedRootName);
            generatedRoot = existing != null
                ? existing.gameObject
                : new GameObject(GeneratedRootName);
            generatedRoot.transform.SetParent(bindings.HammerRoot, false);
            if (generatedRoot.GetComponent<CraftLiveGeneratedRuntimeVisual>() ==
                null)
            {
                generatedRoot.AddComponent<CraftLiveGeneratedRuntimeVisual>();
            }

            raisedRotation = Quaternion.Euler(raisedHammerEuler);
            impactRotation = Quaternion.Euler(impactHammerEuler);
            BuildHammer();
            BuildDirectionGuide();
            BuildProgressHud();
            BuildSparks();
            BuildWeaponLight();
            SetVisualRootsActive(false);
            SetHammerVisible(false);
            built = true;
        }

        private void BuildHammer()
        {
            if (hammerHandlePivot != null)
            {
                runtimeHammerPivot = hammerHandlePivot;
                runtimeImpactPoint = hammerHeadImpactPoint != null
                    ? hammerHeadImpactPoint
                    : FindChildRecursive(
                        hammerHandlePivot,
                        impactPointChildName);
                return;
            }

            GameObject pivotObject = new GameObject("HammerHandlePivot");
            runtimeHammerPivot = pivotObject.transform;
            runtimeHammerPivot.SetParent(generatedRoot.transform, false);
            runtimeHammerPivot.localPosition = hammerPivotLocalPosition;
            runtimeHammerPivot.localRotation = raisedRotation;

            if (hammerPrefab != null)
            {
                generatedHammer = Instantiate(
                    hammerPrefab,
                    runtimeHammerPivot);
                generatedHammer.name = "HammerModel";
                generatedHammer.transform.localPosition =
                    hammerModelLocalPosition;
                generatedHammer.transform.localRotation =
                    Quaternion.Euler(hammerModelLocalEuler);
                generatedHammer.transform.localScale =
                    hammerModelLocalScale;
                CraftLiveForgeUITheme.EnsureCompatibleSurfaces(
                    generatedHammer);
                runtimeImpactPoint = FindChildRecursive(
                    generatedHammer.transform,
                    impactPointChildName);
            }
            else
            {
                generatedHammer = BuildFallbackHammer(runtimeHammerPivot);
                runtimeImpactPoint = generatedHammer.transform.Find(
                    impactPointChildName);
            }
        }

        private GameObject BuildFallbackHammer(Transform parent)
        {
            GameObject root = new GameObject("FallbackForgeHammer");
            root.transform.SetParent(parent, false);

            CreatePart(
                root.transform,
                "WoodHandle",
                new Vector3(0f, 0.63f, 0f),
                new Vector3(0.16f, 1.24f, 0.15f),
                new Color(0.28f, 0.11f, 0.035f),
                0.02f,
                0.12f,
                0.32f);
            CreatePart(
                root.transform,
                "LeatherGrip",
                new Vector3(0f, 0.25f, -0.005f),
                new Vector3(0.205f, 0.42f, 0.19f),
                CraftLiveForgeUITheme.DeepIron,
                0.025f,
                0.35f,
                0.25f);
            CreatePart(
                root.transform,
                "BrassCollar",
                new Vector3(0f, 1.17f, 0f),
                new Vector3(0.3f, 0.16f, 0.25f),
                CraftLiveForgeUITheme.Brass,
                0.08f,
                0.9f,
                0.48f);
            CreatePart(
                root.transform,
                "HammerHead",
                new Vector3(0f, 1.38f, 0f),
                new Vector3(0.86f, 0.3f, 0.3f),
                CraftLiveForgeUITheme.Iron,
                0.045f,
                0.82f,
                0.36f);

            GameObject impact = new GameObject(impactPointChildName);
            impact.transform.SetParent(root.transform, false);
            impact.transform.localPosition = new Vector3(-0.44f, 1.38f, 0f);
            return root;
        }

        private void AlignHammerImpactPoint()
        {
            if (!alignImpactPointToWeapon ||
                runtimeHammerPivot == null ||
                runtimeImpactPoint == null ||
                weaponFocusTarget == null)
            {
                return;
            }

            Quaternion savedRotation = runtimeHammerPivot.localRotation;
            runtimeHammerPivot.localRotation = impactRotation;
            Vector3 desiredImpact = GetWeaponImpactPosition() +
                                    transform.TransformVector(
                                        hammerImpactLocalOffset);
            runtimeHammerPivot.position +=
                desiredImpact - runtimeImpactPoint.position;
            if (targetCamera != null)
            {
                runtimeHammerPivot.position -=
                    targetCamera.transform.forward * hammerCameraClearance;
            }
            runtimeHammerPivot.localRotation = savedRotation;
        }

        private void BuildDirectionGuide()
        {
            guideRoot = new GameObject("StrikeDirectionGuide");
            guideRoot.transform.SetParent(generatedRoot.transform, false);

            Vector3 direction = guideEndLocal - guideStartLocal;
            float length = direction.magnitude;
            if (length < 0.001f)
            {
                direction = Vector3.down;
                length = 1f;
            }

            Vector3 normalized = direction.normalized;
            float headLength = Mathf.Min(length * 0.26f, 0.42f);
            Vector3 shaftEnd = guideEndLocal - normalized * headLength * 0.45f;
            CreateGuideLine(
                guideRoot.transform,
                "ArrowShaft",
                guideStartLocal,
                shaftEnd,
                guideThickness);

            Vector3 perpendicular = new Vector3(
                -normalized.y,
                normalized.x,
                0f);
            Vector3 headBase = guideEndLocal - normalized * headLength;
            CreateGuideLine(
                guideRoot.transform,
                "ArrowHeadLeft",
                guideEndLocal,
                headBase + perpendicular * headLength * 0.48f,
                guideThickness * 1.18f);
            CreateGuideLine(
                guideRoot.transform,
                "ArrowHeadRight",
                guideEndLocal,
                headBase - perpendicular * headLength * 0.48f,
                guideThickness * 1.18f);

            TextMesh label = CreateText(
                guideRoot.transform,
                "GuideLabel",
                guideLabel,
                Vector3.Lerp(guideStartLocal, guideEndLocal, 0.44f) +
                perpendicular * -0.34f,
                0.034f,
                guideColor);
            label.transform.localRotation = Quaternion.identity;
        }

        private void BuildProgressHud()
        {
            if (bindings.UiRoot == null)
            {
                return;
            }

            progressHud = new GameObject("ForgeProgressHud");
            progressHud.transform.SetParent(bindings.UiRoot, false);
            progressHud.AddComponent<CraftLiveGeneratedRuntimeVisual>();

            CreatePart(
                progressHud.transform,
                "ProgressBackplate",
                new Vector3(0f, 2.56f, -0.7f),
                new Vector3(3.15f, 0.66f, 0.2f),
                CraftLiveForgeUITheme.DeepIron,
                0.04f,
                0.76f,
                0.34f);
            CreatePart(
                progressHud.transform,
                "ProgressTopTrim",
                new Vector3(0f, 2.86f, -0.82f),
                new Vector3(3.18f, 0.07f, 0.05f),
                CraftLiveForgeUITheme.Brass,
                0.1f,
                0.9f,
                0.52f);
            CreatePart(
                progressHud.transform,
                "ProgressBottomTrim",
                new Vector3(0f, 2.26f, -0.82f),
                new Vector3(3.18f, 0.07f, 0.05f),
                CraftLiveForgeUITheme.Brass,
                0.08f,
                0.9f,
                0.48f);
            progressText = CreateText(
                progressHud.transform,
                "ProgressText",
                "鍛錬 0 / 1",
                new Vector3(0f, 2.56f, -0.84f),
                0.056f,
                CraftLiveForgeUITheme.ParchmentText);
        }

        private void BuildSparks()
        {
            if (sparkPrefab != null)
            {
                sparks = Instantiate(sparkPrefab, generatedRoot.transform);
                sparks.name = "ImpactSparks";
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            GameObject sparkObject = new GameObject("ImpactSparks");
            sparkObject.transform.SetParent(generatedRoot.transform, false);
            sparks = sparkObject.AddComponent<ParticleSystem>();
            sparks.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = sparks.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                sparkSpeed * 0.55f,
                sparkSpeed * 1.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                sparkSize * 0.45f,
                sparkSize);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.24f, 0.025f, 1f),
                new Color(1f, 0.83f, 0.24f, 1f));
            main.gravityModifier = 0.22f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;

            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 58f;
            shape.radius = 0.018f;

            ParticleSystemRenderer particleRenderer =
                sparkObject.GetComponent<ParticleSystemRenderer>();
            generatedSparkMaterial =
                CraftLiveForgeUITheme.CreateCompatibleParticleMaterial(
                    "Generated_ForgeSparkMaterial");
            if (generatedSparkMaterial != null)
            {
                generatedSparkMaterial.SetColor(
                    "_BaseColor",
                    new Color(1f, 0.42f, 0.045f, 1f));
                generatedSparkMaterial.SetColor(
                    "_Color",
                    new Color(1f, 0.42f, 0.045f, 1f));
                generatedSparkMaterial.SetColor(
                    "_EmissionColor",
                    new Color(1f, 0.16f, 0.015f, 1f) * 2.6f);
                particleRenderer.sharedMaterial = generatedSparkMaterial;
            }
        }

        private void BuildWeaponLight()
        {
            GameObject lightObject = new GameObject("WeaponForgeLight");
            lightObject.transform.SetParent(generatedRoot.transform, false);
            weaponLight = lightObject.AddComponent<Light>();
            weaponLight.type = LightType.Point;
            weaponLight.color = weaponGlowColor;
            weaponLight.range = 1.35f;
            weaponLight.intensity = 0f;
            weaponLight.shadows = LightShadows.None;
        }

        private void TriggerImpactVisuals()
        {
            strikeGlow = 1f;
            Vector3 impactPosition = GetWeaponImpactPosition();
            if (sparks != null)
            {
                sparks.transform.position = impactPosition;
                Vector3 forward = transform.up.sqrMagnitude > 0.001f
                    ? transform.up
                    : Vector3.up;
                sparks.transform.rotation = Quaternion.LookRotation(forward);
                sparks.Emit(Mathf.Max(1, sparkCount));
            }

            SpawnMaterialMotes(impactPosition);
            onImpactVisual?.Invoke();
        }

        private void SpawnMaterialMotes(Vector3 target)
        {
            Transform[] slots = GetMaterialSlots();
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                Transform slot = slots[slotIndex];
                if (slot == null)
                {
                    continue;
                }

                Color color = ResolveMaterialColor(slot, slotIndex);
                for (int moteIndex = 0;
                     moteIndex < Mathf.Max(1, motesPerMaterialSlot);
                     moteIndex++)
                {
                    GameObject mote = GameObject.CreatePrimitive(
                        PrimitiveType.Sphere);
                    mote.name = $"MaterialMote_{slotIndex}_{moteIndex}";
                    mote.transform.SetParent(generatedRoot.transform, true);
                    mote.transform.position = slot.position;
                    mote.transform.localScale =
                        Vector3.one * materialMoteSize;
                    DestroySafely(mote.GetComponent<Collider>());
                    CraftLiveForgeUITheme.ApplyForgeSurface(
                        mote.GetComponent<Renderer>(),
                        color,
                        0.85f,
                        0.36f,
                        0.58f);
                    float phase = (slotIndex * 0.37f + moteIndex * 0.61f) % 1f;
                    StartCoroutine(AnimateMaterialMote(
                        mote,
                        slot.position,
                        target,
                        phase));
                }
            }
        }

        private IEnumerator AnimateMaterialMote(
            GameObject mote,
            Vector3 start,
            Vector3 target,
            float phase)
        {
            if (mote == null)
            {
                yield break;
            }

            Vector3 direction = target - start;
            Vector3 lateral = Vector3.Cross(
                direction.sqrMagnitude > 0.001f
                    ? direction.normalized
                    : Vector3.forward,
                targetCamera != null ? targetCamera.transform.forward :
                    Vector3.forward).normalized;
            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, materialMergeDuration) *
                             Mathf.Lerp(0.88f, 1.12f, phase);
            while (elapsed < duration && mote != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Smooth01(t);
                Vector3 arc = transform.up *
                              (Mathf.Sin(t * Mathf.PI) * materialMergeArc);
                Vector3 swirl = lateral *
                                (Mathf.Sin((t + phase) * Mathf.PI * 2f) *
                                 materialMergeArc * 0.22f * (1f - t));
                mote.transform.position =
                    Vector3.Lerp(start, target, eased) + arc + swirl;
                mote.transform.localScale = Vector3.one *
                    (materialMoteSize * Mathf.Lerp(1f, 0.08f, eased));
                yield return null;
            }

            DestroySafely(mote);
        }

        private void CacheWeaponRenderers()
        {
            RestoreWeaponMaterials();
            if (weaponFocusTarget == null)
            {
                return;
            }

            Renderer[] renderers =
                weaponFocusTarget.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original);
                Material[] sourceMaterials = renderer.sharedMaterials;
                Material[] glowMaterials = new Material[sourceMaterials.Length];
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    if (sourceMaterials[i] == null)
                    {
                        continue;
                    }

                    Material glowMaterial = new Material(sourceMaterials[i])
                    {
                        name = sourceMaterials[i].name + " (Forge Glow)"
                    };
                    glowMaterial.EnableKeyword("_EMISSION");
                    glowMaterials[i] = glowMaterial;
                }

                renderer.sharedMaterials = glowMaterials;
                weaponRenderers.Add(renderer);
                originalWeaponBlocks.Add(original);
                originalWeaponMaterials.Add(sourceMaterials);
                glowWeaponMaterials.Add(glowMaterials);
            }
        }

        private void UpdateWeaponGlow()
        {
            if (weaponRenderers.Count == 0 && isMixing)
            {
                CacheWeaponRenderers();
            }

            float progressStrength = Mathf.Lerp(
                baseGlowStrength,
                completedGlowStrength,
                Smooth01(blendProgress));
            float pulse = isMixing
                ? 0.82f + Mathf.Sin(Time.time * 5.4f) * 0.18f
                : 1f;
            float strength = (progressStrength + strikeGlow * strikeGlowBoost) *
                             pulse;
            Color emission = weaponGlowColor * Mathf.Max(0f, strength);

            foreach (Renderer renderer in weaponRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", emission);
                block.SetColor("_EmissiveColor", emission);
                renderer.SetPropertyBlock(block);
            }

            if (weaponLight != null)
            {
                weaponLight.transform.position = GetWeaponImpactPosition();
                weaponLight.color = weaponGlowColor;
                weaponLight.intensity = Mathf.Clamp(strength * 0.56f, 0f, 4.5f);
            }
        }

        private void RestoreWeaponMaterials()
        {
            int count = Mathf.Min(
                weaponRenderers.Count,
                originalWeaponBlocks.Count);
            for (int i = 0; i < count; i++)
            {
                if (weaponRenderers[i] != null)
                {
                    weaponRenderers[i].SetPropertyBlock(
                        originalWeaponBlocks[i]);
                    if (i < originalWeaponMaterials.Count)
                    {
                        weaponRenderers[i].sharedMaterials =
                            originalWeaponMaterials[i];
                    }
                }
            }

            foreach (Material[] materials in glowWeaponMaterials)
            {
                if (materials == null)
                {
                    continue;
                }

                foreach (Material material in materials)
                {
                    DestroySafely(material);
                }
            }

            weaponRenderers.Clear();
            originalWeaponBlocks.Clear();
            originalWeaponMaterials.Clear();
            glowWeaponMaterials.Clear();
            if (weaponLight != null)
            {
                weaponLight.intensity = 0f;
            }
        }

        private void UpdateGuidePulse()
        {
            if (guideRoot == null || !guideRoot.activeSelf)
            {
                return;
            }

            float pulse = 0.82f + Mathf.Sin(Time.time * 6.2f) * 0.18f;
            Color emission = guideColor * Mathf.Lerp(0.8f, 2.4f, pulse);
            foreach (Renderer renderer in guideRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", emission);
                renderer.SetPropertyBlock(block);
            }
        }

        private void BeginCameraFocus()
        {
            if (!focusCameraDuringSynthesis)
            {
                return;
            }

            ResolveReferences();
            if (targetCamera == null || weaponFocusTarget == null)
            {
                return;
            }

            if (cameraRoutine != null)
            {
                StopCoroutine(cameraRoutine);
            }

            if (!cameraPoseCaptured)
            {
                originalCameraPosition = targetCamera.transform.position;
                originalCameraRotation = targetCamera.transform.rotation;
                originalCameraFieldOfView = targetCamera.fieldOfView;
                cameraPoseCaptured = true;
            }

            cameraRoutine = StartCoroutine(AnimateCameraFocus());
        }

        private IEnumerator AnimateCameraFocus()
        {
            Vector3 focus = GetWeaponFocusPosition();
            Vector3 startPosition = targetCamera.transform.position;
            Quaternion startRotation = targetCamera.transform.rotation;
            float startFov = targetCamera.fieldOfView;
            Vector3 fromFocus = startPosition - focus;
            if (fromFocus.sqrMagnitude < 0.001f)
            {
                fromFocus = -targetCamera.transform.forward;
            }

            float distance = Mathf.Max(
                minimumFocusDistance,
                fromFocus.magnitude * focusDistanceRatio);
            Vector3 targetPosition = focus + fromFocus.normalized * distance;
            Quaternion targetRotation = Quaternion.LookRotation(
                focus - targetPosition,
                targetCamera.transform.up);
            float targetFov = Mathf.Min(startFov, focusFieldOfView);

            float elapsed = 0f;
            while (elapsed < cameraFocusDuration && isMixing)
            {
                elapsed += Time.deltaTime;
                float t = Smooth01(elapsed /
                                   Mathf.Max(0.05f, cameraFocusDuration));
                targetCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, targetPosition, t),
                    Quaternion.Slerp(startRotation, targetRotation, t));
                targetCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
                yield return null;
            }

            if (isMixing)
            {
                targetCamera.transform.SetPositionAndRotation(
                    targetPosition,
                    targetRotation);
                targetCamera.fieldOfView = targetFov;
            }

            cameraRoutine = null;
        }

        private IEnumerator FinishPresentation()
        {
            yield return new WaitForSeconds(completionFocusHold);
            RestoreWeaponMaterials();
            SetHammerVisible(false);
            SetVisualRootsActive(false);

            if (focusCameraDuringSynthesis && cameraPoseCaptured &&
                targetCamera != null)
            {
                Vector3 startPosition = targetCamera.transform.position;
                Quaternion startRotation = targetCamera.transform.rotation;
                float startFov = targetCamera.fieldOfView;
                float elapsed = 0f;
                while (elapsed < cameraRestoreDuration && !isMixing)
                {
                    elapsed += Time.deltaTime;
                    float t = Smooth01(elapsed /
                                       Mathf.Max(0.05f, cameraRestoreDuration));
                    targetCamera.transform.SetPositionAndRotation(
                        Vector3.Lerp(
                            startPosition,
                            originalCameraPosition,
                            t),
                        Quaternion.Slerp(
                            startRotation,
                            originalCameraRotation,
                            t));
                    targetCamera.fieldOfView = Mathf.Lerp(
                        startFov,
                        originalCameraFieldOfView,
                        t);
                    yield return null;
                }

                if (!isMixing)
                {
                    RestoreCameraImmediate();
                }
            }

            finishRoutine = null;
        }

        private void RestoreCameraImmediate()
        {
            if (!cameraPoseCaptured || targetCamera == null)
            {
                return;
            }

            targetCamera.transform.SetPositionAndRotation(
                originalCameraPosition,
                originalCameraRotation);
            targetCamera.fieldOfView = originalCameraFieldOfView;
            cameraPoseCaptured = false;
            cameraRoutine = null;
        }

        private Vector3 GetWeaponFocusPosition()
        {
            if (weaponFocusTarget == null)
            {
                return transform.position;
            }

            return weaponFocusTarget.position +
                   transform.TransformVector(weaponFocusLocalOffset);
        }

        private Vector3 GetWeaponImpactPosition()
        {
            if (weaponFocusTarget == null)
            {
                return runtimeImpactPoint != null
                    ? runtimeImpactPoint.position
                    : transform.position;
            }

            Renderer[] renderers =
                weaponFocusTarget.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return GetWeaponFocusPosition();
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.center;
        }

        private Transform[] GetMaterialSlots()
        {
            return bindings == null
                ? Array.Empty<Transform>()
                : new[]
                {
                    bindings.UpperLeftSlot,
                    bindings.MiddleLeftSlot,
                    bindings.UpperRightSlot,
                    bindings.MiddleRightSlot,
                    bindings.LowerLeftSkillSlot,
                    bindings.LowerRightAttributeSlot
                };
        }

        private static Color ResolveMaterialColor(
            Transform slot,
            int index)
        {
            Renderer renderer = slot != null
                ? slot.GetComponentInChildren<Renderer>(true)
                : null;
            Material material = renderer != null
                ? renderer.sharedMaterial
                : null;
            if (material != null)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    return material.GetColor("_BaseColor");
                }

                if (material.HasProperty("_Color"))
                {
                    return material.color;
                }
            }

            Color[] palette =
            {
                new Color(0.95f, 0.31f, 0.08f),
                new Color(0.26f, 0.58f, 0.96f),
                new Color(0.83f, 0.63f, 0.2f),
                new Color(0.4f, 0.82f, 0.48f),
                new Color(0.66f, 0.34f, 0.88f),
                new Color(1f, 0.48f, 0.12f)
            };
            return palette[Mathf.Abs(index) % palette.Length];
        }

        private void SetVisualRootsActive(bool value)
        {
            if (generatedRoot != null)
            {
                generatedRoot.SetActive(value);
            }

            if (progressHud != null)
            {
                progressHud.SetActive(value);
            }
        }

        private void SetHammerVisible(bool value)
        {
            if (hammerHandlePivot != null)
            {
                hammerHandlePivot.gameObject.SetActive(value);
            }
            else if (runtimeHammerPivot != null)
            {
                runtimeHammerPivot.gameObject.SetActive(value);
            }
        }

        private void SetGuideVisible(bool value)
        {
            if (guideRoot != null)
            {
                guideRoot.SetActive(value);
            }
        }

        private void SetHammerRotation(Quaternion rotation)
        {
            if (runtimeHammerPivot != null)
            {
                runtimeHammerPivot.localRotation = rotation;
            }
        }

        private void CreateGuideLine(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float thickness)
        {
            Vector3 delta = end - start;
            GameObject part = CreatePart(
                parent,
                name,
                Vector3.Lerp(start, end, 0.5f),
                new Vector3(thickness, delta.magnitude, thickness * 0.45f),
                guideColor,
                0.9f,
                0.52f,
                0.62f);
            part.transform.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                delta.sqrMagnitude > 0.0001f
                    ? delta.normalized
                    : Vector3.down);
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                guideRenderers.Add(renderer);
            }
        }

        private static GameObject CreatePart(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            float emission,
            float metallic,
            float smoothness)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            DestroySafely(part.GetComponent<Collider>());
            CraftLiveForgeUITheme.ApplyForgeSurface(
                part.GetComponent<Renderer>(),
                color,
                emission,
                metallic,
                smoothness);
            return part;
        }

        private static TextMesh CreateText(
            Transform parent,
            string name,
            string value,
            Vector3 localPosition,
            float size,
            Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = value ?? string.Empty;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(text, size, color, true);
            return text;
        }

        private static Transform FindChildRecursive(
            Transform parent,
            string childName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static void DestroySafely(UnityEngine.Object target)
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
