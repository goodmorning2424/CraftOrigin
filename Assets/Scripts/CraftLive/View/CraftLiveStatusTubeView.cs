using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveStatusTubeView :
        MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLiveStatType statType =
            CraftLiveStatType.AttackRate;
        [SerializeField] private GameObject glassTubePrefab;
        [SerializeField] private Transform liquidFill;
        [SerializeField] private Renderer[] liquidRenderers =
            new Renderer[0];

        [Header("Fill Object Coordinates")]
        [Tooltip("始点・終点の数値座標を解釈する基準Transformです。未設定時はガラス管の親を使用します。")]
        [SerializeField] private Transform fillCoordinateSpace;
        [Tooltip("指定時は数値の始点座標より、このTransformのワールド位置を優先します。")]
        [SerializeField] private Transform fillStartPoint;
        [Tooltip("指定時は数値の終点座標より、このTransformのワールド位置を優先します。")]
        [SerializeField] private Transform fillEndPoint;
        [Tooltip("Fill Coordinate Spaceを基準にした液体の始点座標です。")]
        [SerializeField] private Vector3 fillStartLocalPosition =
            new Vector3(-2.1f, 0f, 0f);
        [Tooltip("Fill Coordinate Spaceを基準にした液体の終点座標です。")]
        [SerializeField] private Vector3 fillEndLocalPosition =
            new Vector3(2.1f, 0f, 0f);
        [SerializeField, Min(0.01f)] private float fillWidth = 0.28f;
        [SerializeField, Min(0.05f)] private float animationSeconds = 0.55f;
        [SerializeField] private Color liquidColor =
            new Color(0.85f, 0.18f, 0.13f, 1f);
        [SerializeField] private bool createFallbackVisual = true;

        [Header("Liquid Glow")]
        [Tooltip("チューブ全体の発光量です。0で発光なし、1で下記設定をそのまま使用します。")]
        [SerializeField, Range(0f, 1f)] private float glowAmount = 0.38f;
        [SerializeField, Min(0f)] private float emissionStrength = 5f;
        [SerializeField, Range(0f, 1f)] private float coreWhiteBlend = 0.28f;
        [SerializeField, Min(1f)] private float glowShellRadiusMultiplier = 1.5f;
        [SerializeField, Range(0f, 1f)] private float glowShellAlpha = 0.2f;
        [SerializeField, Min(0f)] private float glowPulseSpeed = 2.4f;
        [SerializeField] private bool createGlowLight = true;
        [SerializeField, Min(0f)] private float glowLightIntensity = 1.35f;
        [SerializeField, Min(0.01f)] private float glowLightRange = 1.25f;

        [Header("Events")]
        [SerializeField] private UnityEvent<float> onValueChanged;
        [SerializeField] private UnityEvent<float> onNormalizedChanged;

        private float displayedNormalized;
        private float targetNormalized;
        private float currentValue;
        private GameObject generatedVisual;
        private Material generatedLiquidMaterial;
        private Transform liquidGlowShell;
        private Renderer liquidGlowRenderer;
        private Material generatedGlowMaterial;
        private Light liquidGlowLight;

        public CraftLiveStatType StatType => statType;
        public float CurrentValue => currentValue;
        public float DisplayedNormalized => displayedNormalized;
        public Color LiquidColor => liquidColor;
        public float GlowAmount => glowAmount;
        public Transform FillCoordinateSpace => fillCoordinateSpace;
        public Transform FillStartPoint => fillStartPoint;
        public Transform FillEndPoint => fillEndPoint;
        public Vector3 FillStartObjectPosition => fillStartLocalPosition;
        public Vector3 FillEndObjectPosition => fillEndLocalPosition;

        private void OnValidate()
        {
            fillWidth = Mathf.Max(0.01f, fillWidth);
            animationSeconds = Mathf.Max(0.05f, animationSeconds);
            glowAmount = Mathf.Clamp01(glowAmount);
            emissionStrength = Mathf.Max(0f, emissionStrength);
            coreWhiteBlend = Mathf.Clamp01(coreWhiteBlend);
            glowShellRadiusMultiplier = Mathf.Max(
                1f,
                glowShellRadiusMultiplier);
            glowShellAlpha = Mathf.Clamp01(glowShellAlpha);
            glowPulseSpeed = Mathf.Max(0f, glowPulseSpeed);
            glowLightIntensity = Mathf.Max(0f, glowLightIntensity);
            glowLightRange = Mathf.Max(0.01f, glowLightRange);
            if (liquidFill != null)
            {
                ApplyFill(displayedNormalized);
                ApplyColor();
            }
        }

        private void OnDrawGizmosSelected()
        {
            ResolveFillEndpointsWorld(
                out Vector3 start,
                out Vector3 end);
            Gizmos.color = liquidColor;
            Gizmos.DrawLine(start, end);
            float markerSize = Mathf.Max(0.04f, fillWidth * 1.4f);
            Gizmos.DrawWireSphere(start, markerSize);
            Gizmos.DrawWireSphere(end, markerSize);
        }

        private void Awake()
        {
            ResolveSession();
            EnsureVisual();
            ApplyFill(0f);
        }

        private void OnEnable()
        {
            ResolveSession();
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
        }

        private void OnDestroy()
        {
            if (generatedLiquidMaterial != null)
            {
                DestroySafely(generatedLiquidMaterial);
                generatedLiquidMaterial = null;
            }

            if (generatedGlowMaterial != null)
            {
                DestroySafely(generatedGlowMaterial);
                generatedGlowMaterial = null;
            }
        }

        private void Update()
        {
            float speed = animationSeconds <= 0f
                ? 1000f
                : 1f / animationSeconds;
            float next = Mathf.MoveTowards(
                displayedNormalized,
                targetNormalized,
                speed * Time.unscaledDeltaTime);
            bool valueChanged = !Mathf.Approximately(
                next,
                displayedNormalized);
            if (valueChanged)
            {
                displayedNormalized = next;
                onNormalizedChanged?.Invoke(displayedNormalized);
            }

            // Transform anchors may be moved at runtime, so the fill pose is
            // refreshed even while the normalized value is unchanged.
            ApplyFill(displayedNormalized);
            UpdateGlowPulse();
        }

        public void Configure(
            CraftLiveSession targetSession,
            CraftLiveStatType targetStatType,
            Color targetColor)
        {
            if (session != null)
            {
                session.StateChanged -= Refresh;
            }

            session = targetSession;
            statType = targetStatType;
            liquidColor = targetColor;
            EnsureVisual();
            ApplyColor();
            if (isActiveAndEnabled && session != null)
            {
                session.StateChanged -= Refresh;
                session.StateChanged += Refresh;
                Refresh(session.State);
            }
        }

        public void Configure(
            CraftLiveSession targetSession,
            CraftLiveStatType targetStatType)
        {
            Configure(targetSession, targetStatType, liquidColor);
        }

        public static float NormalizeValue(
            float value,
            float maximum)
        {
            return maximum <= 0f
                ? 0f
                : Mathf.Clamp01(value / maximum);
        }

        public void PreviewValue(float value)
        {
            float maximum = session != null &&
                            session.Rules != null
                ? session.Rules.MaximumStat
                : 100f;
            currentValue = Mathf.Max(0f, value);
            targetNormalized =
                NormalizeValue(currentValue, maximum);
            onValueChanged?.Invoke(currentValue);
        }

        private void ResolveSession()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null || session == null)
            {
                return;
            }

            CraftLiveStats stats = state.displayedStats;
            currentValue = stats.Get(statType);
            float maximum = session.Rules != null
                ? session.Rules.MaximumStat
                : 100f;
            targetNormalized =
                NormalizeValue(currentValue, maximum);
            onValueChanged?.Invoke(currentValue);
        }

        private void EnsureVisual()
        {
            if (liquidFill != null)
            {
                EnsureGlowLight();
                ApplyColor();
                return;
            }

            DestroySafely(generatedVisual);
            generatedVisual = new GameObject(
                "Generated_StatusTube");
            generatedVisual.transform.SetParent(
                transform,
                false);
            generatedVisual.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();

            bool prefabReferencesThisObject =
                glassTubePrefab == gameObject ||
                (glassTubePrefab != null &&
                 glassTubePrefab.transform.IsChildOf(transform));
            if (prefabReferencesThisObject)
            {
                Debug.LogWarning(
                    "CraftLiveStatusTubeView: glassTubePrefab cannot " +
                    "reference the tube object itself. The existing " +
                    "scene renderer will be used as the glass tube.",
                    this);
                glassTubePrefab = null;
            }

            bool hasExistingGlass =
                GetComponent<Renderer>() != null;
            if (glassTubePrefab != null)
            {
                GameObject glass = Instantiate(
                    glassTubePrefab,
                    generatedVisual.transform,
                    false);
                glass.name = "GlassTubePrefab";
                SetCylinderBetween(
                    glass.transform,
                    fillStartLocalPosition,
                    fillEndLocalPosition,
                    fillWidth * 1.45f);
                DisableColliders(glass);
            }
            else if (createFallbackVisual && !hasExistingGlass)
            {
                GameObject glass = GameObject.CreatePrimitive(
                    PrimitiveType.Cylinder);
                glass.name = "FallbackGlass";
                glass.transform.SetParent(
                    generatedVisual.transform,
                    false);
                SetCylinderBetween(
                    glass.transform,
                    fillStartLocalPosition,
                    fillEndLocalPosition,
                    fillWidth * 1.45f);
                DestroySafely(glass.GetComponent<Collider>());
                ApplyRendererColor(
                    glass.GetComponent<Renderer>(),
                    new Color(0.12f, 0.28f, 0.33f, 1f),
                    0.15f);
            }

            if (glassTubePrefab == null &&
                !createFallbackVisual &&
                !hasExistingGlass)
            {
                return;
            }

            GameObject fill = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            fill.name = "Generated_LiquidFill";
            fill.transform.SetParent(
                ResolveFillRenderParent(),
                false);
            DestroySafely(fill.GetComponent<Collider>());
            fill.AddComponent<CraftLiveGeneratedRuntimeVisual>();
            liquidFill = fill.transform;
            liquidRenderers =
                new[] { fill.GetComponent<Renderer>() };
            ConfigureLiquidMaterial(liquidRenderers[0]);
            CreateGlowShell();
            EnsureGlowLight();
            ApplyColor();
        }

        private void ApplyFill(float normalized)
        {
            if (liquidFill == null)
            {
                return;
            }

            normalized = Mathf.Clamp01(normalized);
            ResolveFillEndpointsWorld(
                out Vector3 start,
                out Vector3 end);
            Vector3 fullDirection = end - start;
            if (fullDirection.sqrMagnitude < 0.0001f)
            {
                fullDirection = Vector3.right * 0.02f;
            }

            float visibleAmount = Mathf.Max(0.004f, normalized);
            Vector3 visibleEnd = start +
                                 fullDirection * visibleAmount;
            SetCylinderBetweenWorld(
                liquidFill,
                start,
                visibleEnd,
                fillWidth);
            if (liquidGlowShell != null)
            {
                SetCylinderBetweenWorld(
                    liquidGlowShell,
                    start,
                    visibleEnd,
                    fillWidth * glowShellRadiusMultiplier);
                liquidGlowShell.gameObject.SetActive(
                    normalized > 0.001f);
            }
            UpdateGlowLight(start, visibleEnd, normalized);
        }

        private void ResolveFillEndpointsWorld(
            out Vector3 start,
            out Vector3 end)
        {
            Transform coordinateSpace = fillCoordinateSpace != null
                ? fillCoordinateSpace
                : transform.parent != null
                    ? transform.parent
                    : transform;
            start = fillStartPoint != null
                ? fillStartPoint.position
                : coordinateSpace.TransformPoint(fillStartLocalPosition);
            end = fillEndPoint != null
                ? fillEndPoint.position
                : coordinateSpace.TransformPoint(fillEndLocalPosition);
        }

        private Transform ResolveFillRenderParent()
        {
            if (fillCoordinateSpace != null)
            {
                return fillCoordinateSpace;
            }

            return transform.parent != null
                ? transform.parent
                : transform;
        }

        private void ApplyColor()
        {
            if (liquidRenderers == null)
            {
                return;
            }

            foreach (Renderer renderer in liquidRenderers)
            {
                ApplyRendererColor(
                    renderer,
                    ResolveCoreColor(),
                    emissionStrength * glowAmount);
            }

            if (liquidGlowLight != null)
            {
                liquidGlowLight.color = liquidColor;
            }
        }

        private static void ApplyRendererColor(
            Renderer renderer,
            Color color,
            float targetEmissionStrength)
        {
            if (renderer == null)
            {
                return;
            }

            CraftLiveForgeUITheme.EnsureCompatibleSurface(renderer);

            MaterialPropertyBlock block =
                new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor(
                "_EmissionColor",
                color * targetEmissionStrength);
            renderer.SetPropertyBlock(block);
        }

        private void ConfigureLiquidMaterial(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            generatedLiquidMaterial =
                CraftLiveForgeUITheme.CreateCompatibleUnlitMaterial(
                    "Generated_StatusLiquidGlow");
            if (generatedLiquidMaterial == null)
            {
                return;
            }
            generatedLiquidMaterial.EnableKeyword("_EMISSION");
            Color coreColor = ResolveCoreColor();
            generatedLiquidMaterial.SetColor("_BaseColor", coreColor);
            generatedLiquidMaterial.SetColor("_Color", coreColor);
            generatedLiquidMaterial.SetColor(
                "_EmissionColor",
                coreColor * emissionStrength * glowAmount);
            generatedLiquidMaterial.SetFloat("_Metallic", 0.05f);
            generatedLiquidMaterial.SetFloat("_Smoothness", 0.72f);
            generatedLiquidMaterial.SetFloat("_Glossiness", 0.72f);
            renderer.sharedMaterial = generatedLiquidMaterial;
        }

        private Color ResolveCoreColor()
        {
            Color core = Color.Lerp(
                liquidColor,
                Color.white,
                coreWhiteBlend);
            core.a = 1f;
            return core;
        }

        private void CreateGlowShell()
        {
            if (liquidGlowShell != null)
            {
                return;
            }

            GameObject shell = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            shell.name = "Generated_LiquidOuterGlow";
            shell.transform.SetParent(
                ResolveFillRenderParent(),
                false);
            DestroySafely(shell.GetComponent<Collider>());
            shell.AddComponent<CraftLiveGeneratedRuntimeVisual>();
            liquidGlowShell = shell.transform;
            liquidGlowRenderer = shell.GetComponent<Renderer>();
            liquidGlowRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            liquidGlowRenderer.receiveShadows = false;
            ConfigureGlowMaterial();
        }

        private void ConfigureGlowMaterial()
        {
            if (liquidGlowRenderer == null)
            {
                return;
            }

            generatedGlowMaterial =
                CraftLiveForgeUITheme.CreateCompatibleUnlitMaterial(
                    "Generated_StatusLiquidOuterGlow");
            if (generatedGlowMaterial == null)
            {
                return;
            }
            generatedGlowMaterial.renderQueue = 3000;
            generatedGlowMaterial.SetFloat("_Surface", 1f);
            generatedGlowMaterial.SetFloat("_ZWrite", 0f);
            generatedGlowMaterial.SetFloat(
                "_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            generatedGlowMaterial.SetFloat(
                "_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.One);
            generatedGlowMaterial.EnableKeyword(
                "_SURFACE_TYPE_TRANSPARENT");
            generatedGlowMaterial.EnableKeyword("_EMISSION");
            liquidGlowRenderer.sharedMaterial = generatedGlowMaterial;
            UpdateGlowPulse();
        }

        private void UpdateGlowPulse()
        {
            float pulse = 0.78f +
                          Mathf.Sin(Time.unscaledTime * glowPulseSpeed) *
                          0.22f;
            Color shellColor = liquidColor;
            shellColor.a = glowShellAlpha * glowAmount * pulse;
            if (generatedGlowMaterial != null)
            {
                generatedGlowMaterial.SetColor(
                    "_BaseColor",
                    shellColor);
                generatedGlowMaterial.SetColor("_Color", shellColor);
                generatedGlowMaterial.SetColor(
                    "_EmissionColor",
                    liquidColor * emissionStrength * glowAmount * pulse);
            }

            if (liquidGlowLight != null && liquidGlowLight.enabled)
            {
                liquidGlowLight.intensity =
                    glowLightIntensity * glowAmount * pulse *
                    Mathf.Clamp01(displayedNormalized * 2f);
            }
        }

        private void EnsureGlowLight()
        {
            if (!createGlowLight || liquidGlowLight != null)
            {
                return;
            }

            GameObject lightObject = new GameObject(
                "Generated_LiquidGlowLight");
            lightObject.transform.SetParent(
                ResolveFillRenderParent(),
                false);
            lightObject.AddComponent<CraftLiveGeneratedRuntimeVisual>();
            liquidGlowLight = lightObject.AddComponent<Light>();
            liquidGlowLight.type = LightType.Point;
            liquidGlowLight.shadows = LightShadows.None;
            liquidGlowLight.color = liquidColor;
        }

        private void UpdateGlowLight(
            Vector3 start,
            Vector3 visibleEnd,
            float normalized)
        {
            if (!createGlowLight)
            {
                if (liquidGlowLight != null)
                {
                    liquidGlowLight.enabled = false;
                }

                return;
            }

            EnsureGlowLight();
            if (liquidGlowLight == null)
            {
                return;
            }

            liquidGlowLight.enabled = normalized > 0.001f;
            liquidGlowLight.transform.position =
                (start + visibleEnd) * 0.5f;
            liquidGlowLight.intensity =
                glowLightIntensity * glowAmount *
                Mathf.Clamp01(normalized * 2f);
            liquidGlowLight.range = glowLightRange;
        }

        private static void SetCylinderBetween(
            Transform cylinder,
            Vector3 start,
            Vector3 end,
            float radius)
        {
            if (cylinder == null)
            {
                return;
            }

            Vector3 direction = end - start;
            float length = Mathf.Max(0.02f, direction.magnitude);
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.right;
            cylinder.localPosition = (start + end) * 0.5f;
            cylinder.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                normalizedDirection);
            cylinder.localScale = new Vector3(
                radius,
                length * 0.5f,
                radius);
        }

        private static void SetCylinderBetweenWorld(
            Transform cylinder,
            Vector3 start,
            Vector3 end,
            float radius)
        {
            if (cylinder == null)
            {
                return;
            }

            Vector3 direction = end - start;
            float length = Mathf.Max(0.02f, direction.magnitude);
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.right;
            cylinder.SetPositionAndRotation(
                (start + end) * 0.5f,
                Quaternion.FromToRotation(
                    Vector3.up,
                    normalizedDirection));

            Transform parent = cylinder.parent;
            Vector3 parentScale = parent != null
                ? parent.lossyScale
                : Vector3.one;
            cylinder.localScale = new Vector3(
                SafeDivide(radius, parentScale.x),
                SafeDivide(length * 0.5f, parentScale.y),
                SafeDivide(radius, parentScale.z));
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.0001f
                ? value / Mathf.Abs(divisor)
                : value;
        }

        private static void DisableColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Collider collider in
                     root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
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
