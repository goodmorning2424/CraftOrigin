using System;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveHologramView : MonoBehaviour
    {
        [Serializable]
        private sealed class AttributeParticleSetting
        {
            [Tooltip("この設定を使用する属性タイプです。")]
            public CraftLiveElementType elementType =
                CraftLiveElementType.None;
            [Tooltip("属性と一緒に表示するParticleSystem Prefabです。")]
            public GameObject particlePrefab = null;
            public Vector3 localPosition = Vector3.zero;
            public Vector3 localEulerAngles = Vector3.zero;
            public Vector3 localScale = Vector3.one;
            [Tooltip("Prefab内のParticleSystemを素材の属性色で着色します。")]
            public bool tintWithAttributeColor = true;

            public Quaternion LocalRotation =>
                Quaternion.Euler(localEulerAngles);
        }

        [SerializeField] private CraftLiveSession session;
        [SerializeField] private Transform spawnRoot;
        [SerializeField] private Transform effectRoot;
        [SerializeField] private GameObject fallbackPrefab;
        [SerializeField] private CraftLivePad4Calibration calibration;
        [SerializeField] private bool rotate = true;
        [SerializeField] private float rotationSpeed = 30f;
        [Header("Weapon Display")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Pad4に表示する完成武器全体の大きさです。1が元の大きさです。")]
        private float weaponSizeMultiplier = 1f;
        [SerializeField] private Camera targetCamera;
        [SerializeField, Range(0.02f, 0.2f)]
        [Tooltip("回転中も武器と属性演出を画面内に収めるための余白です。")]
        private float viewportMargin = 0.07f;
        [SerializeField] private bool applyAttributeColor = true;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("0 keeps the original weapon colors; 1 fully applies the attribute color.")]
        private float attributeColorStrength = 0.7f;
        [SerializeField, Min(0f)] private float emissionStrength = 2f;
        [Header("Attribute Particles")]
        [SerializeField]
        [Tooltip("Overall particle scale applied after each attribute's local scale.")]
        private Vector3 particleScaleMultiplier = Vector3.one;
        [Tooltip("素材AssetにParticle Prefabがない場合に使う属性タイプ別設定です。")]
        [SerializeField] private AttributeParticleSetting[]
            attributeParticleSettings = new AttributeParticleSetting[0];
        [SerializeField] private bool createFallbackAttributeParticles = true;

        private GameObject currentWeapon;
        private GameObject currentParticleEffect;
        private Material generatedParticleMaterial;
        private int displayedResultSerial = -1;
        private float currentScreenFitMultiplier = 1f;

        public float WeaponSizeMultiplier => weaponSizeMultiplier;

        private void OnValidate()
        {
            weaponSizeMultiplier = Mathf.Max(0.01f, weaponSizeMultiplier);
            viewportMargin = Mathf.Clamp(viewportMargin, 0.02f, 0.2f);
            particleScaleMultiplier.x = Mathf.Max(
                0.001f,
                particleScaleMultiplier.x);
            particleScaleMultiplier.y = Mathf.Max(
                0.001f,
                particleScaleMultiplier.y);
            particleScaleMultiplier.z = Mathf.Max(
                0.001f,
                particleScaleMultiplier.z);
            if (attributeParticleSettings == null)
            {
                attributeParticleSettings =
                    new AttributeParticleSetting[0];
                return;
            }

            foreach (AttributeParticleSetting setting in
                     attributeParticleSettings)
            {
                if (setting == null)
                {
                    continue;
                }

                setting.localScale.x = Mathf.Max(
                    0.001f,
                    setting.localScale.x);
                setting.localScale.y = Mathf.Max(
                    0.001f,
                    setting.localScale.y);
                setting.localScale.z = Mathf.Max(
                    0.001f,
                    setting.localScale.z);
            }
        }

        public void Configure(
            CraftLiveSession targetSession,
            Transform targetSpawnRoot,
            CraftLivePad4Calibration targetCalibration)
        {
            session = targetSession;
            spawnRoot = targetSpawnRoot != null
                ? targetSpawnRoot
                : transform;
            calibration = targetCalibration;
            ResolveEffectRoot();
        }

        public void Configure(
            CraftLiveSession targetSession,
            Transform targetSpawnRoot,
            Transform targetEffectRoot,
            CraftLivePad4Calibration targetCalibration)
        {
            session = targetSession;
            spawnRoot = targetSpawnRoot != null
                ? targetSpawnRoot
                : transform;
            effectRoot = targetEffectRoot;
            calibration = targetCalibration;
        }

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (spawnRoot == null)
            {
                spawnRoot = transform;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            ResolveEffectRoot();
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.StateChanged += HandleStateChanged;
                HandleStateChanged(session.State);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
            }
        }

        private void Update()
        {
            if (rotate && currentWeapon != null)
            {
                currentWeapon.transform.Rotate(
                    Vector3.up,
                    GetRotationSpeed() * Time.deltaTime,
                    Space.World);
            }
        }

        private void HandleStateChanged(CraftLiveRoomState state)
        {
            if (state == null)
            {
                ClearPresentation();
                return;
            }

            if (!state.craft.completionPresentationReady ||
                (state.craft.status !=
                     CraftLiveCraftStatus.Complete &&
                 state.sessionPhase !=
                     CraftLiveSessionPhase.Finished) ||
                state.result == null)
            {
                ClearPresentation();
                return;
            }

            if (state.result.resultSerial == displayedResultSerial)
            {
                return;
            }

            displayedResultSerial = state.result.resultSerial;
            ShowResult(state.result);
        }

        private void ShowResult(CraftLiveResultState result)
        {
            if (currentWeapon != null)
            {
                Destroy(currentWeapon);
            }

            DestroyParticleEffect();

            CraftLiveWeaponDefinition weapon =
                session.Catalog.FindWeapon(result.weaponId);
            GameObject prefab = weapon != null ? weapon.HologramPrefab : null;
            if (weapon != null && weapon.HidePresentationModel)
            {
                currentWeapon = new GameObject("HiddenWeaponModel");
                currentWeapon.transform.SetParent(spawnRoot, false);
            }
            else if (prefab != null || fallbackPrefab != null)
            {
                currentWeapon = Instantiate(
                    prefab != null ? prefab : fallbackPrefab,
                    spawnRoot);
            }
            else
            {
                currentWeapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                currentWeapon.transform.SetParent(spawnRoot, false);
                if (currentWeapon.TryGetComponent(out Collider collider))
                {
                    Destroy(collider);
                }
            }

            Vector3 weaponScale =
                weapon != null ? weapon.PreviewScale : Vector3.one;
            currentWeapon.transform.localPosition = calibration != null
                ? calibration.ModelLocalPosition
                : Vector3.zero;
            currentWeapon.transform.localRotation = calibration != null
                ? calibration.ModelLocalRotation
                : Quaternion.identity;
            currentWeapon.transform.localScale = calibration != null
                ? Vector3.Scale(
                    weaponScale,
                    calibration.ModelScaleMultiplier) *
                  weaponSizeMultiplier
                : weaponScale * weaponSizeMultiplier;

            // Imported Standard/legacy materials can lose their texture or
            // tint in a WebGL URP build. Replace only incompatible surfaces
            // while preserving their original texture and base color.
            CraftLiveForgeUITheme.ApplyMaterialOverride(
                currentWeapon,
                weapon != null
                    ? weapon.PresentationMaterialOverride
                    : null);
            CraftLiveForgeUITheme.EnsureCompatibleSurfaces(currentWeapon);

            currentScreenFitMultiplier = FitWeaponInsideCamera();

            CraftLiveMaterialDefinition attribute =
                FindAttribute(result.attributeId);
            if (applyAttributeColor && attribute != null)
            {
                ApplyColor(currentWeapon, attribute.EffectColor);
            }


            ShowAttributeParticles(attribute);
        }

        private void ResolveEffectRoot()
        {
            if (effectRoot != null)
            {
                return;
            }

            CraftLivePad4Bindings bindings =
                GetComponentInParent<CraftLivePad4Bindings>();
            effectRoot = bindings != null && bindings.EffectRoot != null
                ? bindings.EffectRoot
                : spawnRoot;
        }

        private void ShowAttributeParticles(
            CraftLiveMaterialDefinition attribute)
        {
            if (attribute == null)
            {
                return;
            }

            ResolveEffectRoot();
            Transform parent = effectRoot != null ? effectRoot : spawnRoot;
            if (parent == null)
            {
                return;
            }

            AttributeParticleSetting sharedSetting =
                FindParticleSetting(attribute.ElementEffect.type);
            bool usesMaterialSetting =
                attribute.Pad4ParticlePrefab != null;
            if (usesMaterialSetting)
            {
                currentParticleEffect = Instantiate(
                    attribute.Pad4ParticlePrefab,
                    parent);
            }
            else if (sharedSetting != null &&
                     sharedSetting.particlePrefab != null)
            {
                currentParticleEffect = Instantiate(
                    sharedSetting.particlePrefab,
                    parent);
            }
            else if (createFallbackAttributeParticles)
            {
                currentParticleEffect =
                    CreateFallbackParticleEffect(attribute, parent);
            }

            if (currentParticleEffect == null)
            {
                return;
            }

            Vector3 calibrationPosition = calibration != null
                ? calibration.ModelLocalPosition
                : Vector3.zero;
            Quaternion calibrationRotation = calibration != null
                ? calibration.ModelLocalRotation
                : Quaternion.identity;
            Vector3 calibrationScale = calibration != null
                ? calibration.ModelScaleMultiplier
                : Vector3.one;
            Vector3 particlePosition = usesMaterialSetting ||
                                       sharedSetting == null
                ? attribute.Pad4ParticleLocalPosition
                : sharedSetting.localPosition;
            Quaternion particleRotation = usesMaterialSetting ||
                                          sharedSetting == null
                ? attribute.Pad4ParticleLocalRotation
                : sharedSetting.LocalRotation;
            Vector3 particleScale = usesMaterialSetting ||
                                    sharedSetting == null
                ? attribute.Pad4ParticleLocalScale
                : sharedSetting.localScale;
            currentParticleEffect.transform.localRotation =
                calibrationRotation *
                particleRotation;
            currentParticleEffect.transform.localScale = Vector3.Scale(
                calibrationScale,
                Vector3.Scale(
                    particleScale,
                    particleScaleMultiplier)) *
                weaponSizeMultiplier;

            currentParticleEffect.transform.localPosition =
                calibrationPosition +
                particlePosition * weaponSizeMultiplier *
                currentScreenFitMultiplier;
            currentParticleEffect.transform.localScale *=
                currentScreenFitMultiplier;

            bool tintParticles = usesMaterialSetting ||
                                 sharedSetting == null
                ? attribute.TintPad4Particles
                : sharedSetting.tintWithAttributeColor;
            if (tintParticles)
            {
                TintParticles(
                    currentParticleEffect,
                    attribute.EffectColor);
            }

            // Particle prefabs imported from older Unity versions do not
            // always resume automatically in WebGL. Normalize their material
            // and explicitly warm/play every child system.
            CraftLiveForgeUITheme.EnsureCompatibleSurfaces(
                currentParticleEffect);
            foreach (ParticleSystem particles in
                     currentParticleEffect.GetComponentsInChildren<
                         ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.cullingMode =
                    ParticleSystemCullingMode.AlwaysSimulate;
                particles.Simulate(0.12f, true, true, true);
                particles.Play(true);
            }
        }

        private float FitWeaponInsideCamera()
        {
            if (currentWeapon == null)
            {
                return 1f;
            }

            Camera camera = targetCamera != null
                ? targetCamera
                : Camera.main;
            if (camera == null)
            {
                return 1f;
            }

            Renderer[] renderers = currentWeapon.GetComponentsInChildren<Renderer>();
            Vector3 pivot = currentWeapon.transform.position;
            float radius = 0f;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3 extents = bounds.extents;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = bounds.center +
                                Vector3.Scale(extents, new Vector3(x, y, z));
                            radius = Mathf.Max(
                                radius,
                                Vector3.Distance(pivot, corner));
                        }
                    }
                }
            }

            if (radius <= 0.0001f)
            {
                return 1f;
            }

            Vector3 viewportCenter = camera.WorldToViewportPoint(pivot);
            if (viewportCenter.z <= camera.nearClipPlane)
            {
                return 1f;
            }

            float worldHeight;
            float worldWidth;
            if (camera.orthographic)
            {
                worldHeight = camera.orthographicSize * 2f;
                worldWidth = worldHeight * camera.aspect;
            }
            else
            {
                worldHeight = 2f * viewportCenter.z * Mathf.Tan(
                    camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                worldWidth = worldHeight * camera.aspect;
            }

            float fit = ResolveScreenFitScale(
                radius,
                viewportCenter,
                viewportMargin,
                worldWidth,
                worldHeight);
            currentWeapon.transform.localScale *= fit;
            return fit;
        }

        public static float ResolveScreenFitScale(
            float radius,
            Vector3 viewportCenter,
            float margin,
            float worldWidth,
            float worldHeight)
        {
            if (radius <= 0f || worldWidth <= 0f || worldHeight <= 0f)
            {
                return 1f;
            }

            float safeMargin = Mathf.Clamp(margin, 0f, 0.45f);
            float availableX = Mathf.Max(
                0f,
                Mathf.Min(
                    viewportCenter.x - safeMargin,
                    1f - safeMargin - viewportCenter.x));
            float availableY = Mathf.Max(
                0f,
                Mathf.Min(
                    viewportCenter.y - safeMargin,
                    1f - safeMargin - viewportCenter.y));
            float allowedRadius = Mathf.Min(
                worldWidth * availableX,
                worldHeight * availableY);
            return Mathf.Clamp01(allowedRadius / radius * 0.96f);
        }

        private void ClearPresentation()
        {
            displayedResultSerial = -1;
            currentScreenFitMultiplier = 1f;
            if (currentWeapon != null)
            {
                Destroy(currentWeapon);
                currentWeapon = null;
            }

            DestroyParticleEffect();
        }

        private AttributeParticleSetting FindParticleSetting(
            CraftLiveElementType elementType)
        {
            if (attributeParticleSettings == null ||
                elementType == CraftLiveElementType.None)
            {
                return null;
            }

            foreach (AttributeParticleSetting setting in
                     attributeParticleSettings)
            {
                if (setting != null &&
                    setting.elementType == elementType)
                {
                    return setting;
                }
            }

            return null;
        }

        private GameObject CreateFallbackParticleEffect(
            CraftLiveMaterialDefinition attribute,
            Transform parent)
        {
            GameObject effect = new GameObject(
                "Generated_Pad4AttributeParticles");
            effect.transform.SetParent(parent, false);

            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 2f;
            main.startLifetime = 1.4f;
            main.startSpeed = 0.35f;
            main.startSize = 0.09f;
            main.maxParticles = 96;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 24f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.85f;
            shape.radiusThickness = 0.15f;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.6f;

            ConfigureFallbackForElement(
                particles,
                attribute.ElementEffect.type);

            ParticleSystemRenderer particleRenderer =
                effect.GetComponent<ParticleSystemRenderer>();
            generatedParticleMaterial =
                CraftLiveForgeUITheme.CreateCompatibleParticleMaterial(
                    "Generated_Pad4ParticleMaterial");
            if (generatedParticleMaterial != null)
            {
                particleRenderer.sharedMaterial = generatedParticleMaterial;
            }

            particles.Play(true);
            return effect;
        }

        private static void ConfigureFallbackForElement(
            ParticleSystem particles,
            CraftLiveElementType elementType)
        {
            ParticleSystem.MainModule main = particles.main;
            ParticleSystem.ShapeModule shape = particles.shape;
            ParticleSystem.EmissionModule emission = particles.emission;

            switch (elementType)
            {
                case CraftLiveElementType.Fire:
                    main.startLifetime = 0.85f;
                    main.startSpeed = 0.55f;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 18f;
                    shape.radius = 0.5f;
                    emission.rateOverTime = 34f;
                    break;
                case CraftLiveElementType.Freeze:
                    main.startLifetime = 1.8f;
                    main.startSpeed = 0.16f;
                    main.startSize = 0.075f;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.9f;
                    emission.rateOverTime = 18f;
                    break;
                case CraftLiveElementType.Lightning:
                    main.startLifetime = 0.28f;
                    main.startSpeed = 0.08f;
                    main.startSize = 0.13f;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.75f;
                    emission.rateOverTime = 42f;
                    break;
            }
        }

        private static void TintParticles(GameObject target, Color color)
        {
            foreach (ParticleSystem particles in
                     target.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.startColor = color;
            }
        }

        private void DestroyParticleEffect()
        {
            if (currentParticleEffect != null)
            {
                Destroy(currentParticleEffect);
                currentParticleEffect = null;
            }

            if (generatedParticleMaterial != null)
            {
                Destroy(generatedParticleMaterial);
                generatedParticleMaterial = null;
            }
        }

        private CraftLiveMaterialDefinition FindAttribute(
            string attributeId)
        {
            if (session == null || session.Catalog == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(attributeId))
            {
                foreach (CraftLiveMaterialDefinition material in
                         session.Catalog.Materials)
                {
                    if (material != null &&
                        material.Category ==
                            CraftLiveMaterialCategory.Attribute &&
                        material.AttributeId == attributeId)
                    {
                        return material;
                    }
                }
            }

            return session.State != null
                ? session.Catalog.FindMaterial(
                    session.State.slots.attribute)
                : null;
        }

        private float GetRotationSpeed()
        {
            return calibration != null
                ? calibration.RotationSpeedDegreesPerSecond
                : rotationSpeed;
        }

        private void ApplyColor(GameObject target, Color color)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                Color appliedColor = Color.Lerp(
                    ResolveRendererColor(renderer),
                    color,
                    attributeColorStrength);
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", appliedColor);
                block.SetColor("_Color", appliedColor);
                block.SetColor(
                    "_EmissionColor",
                    color * emissionStrength * attributeColorStrength);
                renderer.SetPropertyBlock(block);
            }
        }

        private static Color ResolveRendererColor(Renderer renderer)
        {
            Material material = renderer != null
                ? renderer.sharedMaterial
                : null;
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            return material.HasProperty("_Color")
                ? material.GetColor("_Color")
                : Color.white;
        }
    }
}
