using UnityEngine;

namespace CraftOrigin.CraftLive
{
    /// <summary>
    /// Shared presentation rules for the world-space Craft-live UI.
    /// The theme intentionally decorates existing transforms instead of
    /// changing layout, so every pad keeps its calibrated position.
    /// </summary>
    public static class CraftLiveForgeUITheme
    {
        public static readonly Color Iron =
            new Color(0.16f, 0.13f, 0.105f, 1f);
        public static readonly Color DeepIron =
            new Color(0.055f, 0.043f, 0.034f, 1f);
        public static readonly Color Brass =
            new Color(0.72f, 0.48f, 0.18f, 1f);
        public static readonly Color Ember =
            new Color(0.9f, 0.27f, 0.075f, 1f);
        public static readonly Color ParchmentText =
            new Color(1f, 0.9f, 0.68f, 1f);
        public static readonly Color MutedText =
            new Color(0.72f, 0.67f, 0.57f, 1f);

        private const string FrameRootName = "ForgeFrame";
        private const int ReferenceFontSize = 64;
        private const int CrispFontSize = 128;
        private static Material runtimeForgeMaterial;
        private static Material runtimeUnlitMaterial;
        private static Material runtimeParticleMaterial;

        public static Color RefineSurfaceColor(Color source)
        {
            float alpha = source.a;
            Color refined = Color.Lerp(source, Iron, 0.28f);
            Color.RGBToHSV(refined, out float hue, out float saturation,
                out float value);
            saturation = Mathf.Clamp(saturation * 0.82f, 0.14f, 0.72f);
            value = Mathf.Clamp(value * 0.82f, 0.18f, 0.68f);
            refined = Color.HSVToRGB(hue, saturation, value);
            refined.a = alpha;
            return refined;
        }

        public static void GetButtonPalette(
            Color source,
            out Color normal,
            out Color hover,
            out Color pressed,
            out Color selected,
            out Color disabled)
        {
            normal = RefineSurfaceColor(source);
            hover = Color.Lerp(normal, Brass, 0.38f);
            pressed = Color.Lerp(normal, Ember, 0.52f);
            selected = Color.Lerp(Brass, ParchmentText, 0.18f);
            disabled = Color.Lerp(DeepIron, Iron, 0.42f);
        }

        public static void ApplyForgeSurface(
            Renderer target,
            Color source,
            float emissionStrength = 0.035f,
            float metallic = 0.72f,
            float smoothness = 0.3f)
        {
            if (target == null)
            {
                return;
            }

            Material forgeMaterial = GetRuntimeForgeMaterial();
            if (forgeMaterial != null &&
                target.sharedMaterial != forgeMaterial)
            {
                target.sharedMaterial = forgeMaterial;
            }

            Color color = RefineSurfaceColor(source);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * emissionStrength);
            block.SetFloat("_Metallic", metallic);
            block.SetFloat("_Smoothness", smoothness);
            block.SetFloat("_Glossiness", smoothness);
            target.SetPropertyBlock(block);
        }

        private static Material GetRuntimeForgeMaterial()
        {
            if (runtimeForgeMaterial != null)
            {
                return runtimeForgeMaterial;
            }

            Material includedMaterial =
                Resources.Load<Material>("CraftLiveRuntimeLit");
            if (includedMaterial != null)
            {
                runtimeForgeMaterial = includedMaterial;
                return runtimeForgeMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            runtimeForgeMaterial = new Material(shader)
            {
                name = "Generated_CraftLiveForgeSurface",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (runtimeForgeMaterial.HasProperty("_Surface"))
            {
                runtimeForgeMaterial.SetFloat("_Surface", 0f);
            }

            return runtimeForgeMaterial;
        }

        public static void EnsureCompatibleSurfaces(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            foreach (Renderer renderer in
                     target.GetComponentsInChildren<Renderer>(true))
            {
                EnsureCompatibleSurface(renderer);
            }
        }

        public static void EnsureCompatibleSurface(Renderer renderer)
        {
            if (renderer == null || !NeedsUrpReplacement(renderer.sharedMaterial))
            {
                return;
            }

            Material source = renderer.sharedMaterial;
            Texture texture = ResolveMainTexture(source);
            Color color = ResolveMainColor(source);
            Material replacement = renderer is ParticleSystemRenderer
                ? GetRuntimeParticleMaterial()
                : GetRuntimeForgeMaterial();
            if (replacement == null)
            {
                return;
            }

            renderer.sharedMaterial = replacement;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            if (texture != null)
            {
                block.SetTexture("_BaseMap", texture);
                block.SetTexture("_MainTex", texture);
            }

            renderer.SetPropertyBlock(block);
        }

        public static Material CreateCompatibleUnlitMaterial(string name)
        {
            Material source = GetRuntimeUnlitMaterial();
            return source != null
                ? new Material(source) { name = name }
                : null;
        }

        public static Material CreateCompatibleParticleMaterial(string name)
        {
            Material source = GetRuntimeParticleMaterial();
            return source != null
                ? new Material(source) { name = name }
                : null;
        }

        private static Material GetRuntimeUnlitMaterial()
        {
            if (runtimeUnlitMaterial == null)
            {
                runtimeUnlitMaterial =
                    Resources.Load<Material>("CraftLiveRuntimeUnlit");
            }

            return runtimeUnlitMaterial ?? GetRuntimeForgeMaterial();
        }

        private static Material GetRuntimeParticleMaterial()
        {
            if (runtimeParticleMaterial == null)
            {
                runtimeParticleMaterial =
                    Resources.Load<Material>("CraftLiveRuntimeParticle");
            }

            return runtimeParticleMaterial ?? GetRuntimeUnlitMaterial();
        }

        private static bool NeedsUrpReplacement(Material material)
        {
            if (material == null || material.shader == null)
            {
                return true;
            }

            string shaderName = material.shader.name ?? string.Empty;
            return shaderName == "Standard" ||
                   shaderName.StartsWith("Legacy Shaders/") ||
                   shaderName.Contains("InternalErrorShader");
        }

        private static Texture ResolveMainTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_BaseMap"))
            {
                return material.GetTexture("_BaseMap");
            }

            return material.HasProperty("_MainTex")
                ? material.GetTexture("_MainTex")
                : null;
        }

        private static Color ResolveMainColor(Material material)
        {
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

        public static void ApplyInteractiveSurface(
            Renderer target,
            Color color,
            bool selected,
            bool highlighted)
        {
            ApplyForgeSurface(
                target,
                color,
                selected ? 0.24f : highlighted ? 0.09f : 0.035f,
                selected ? 0.82f : 0.72f,
                highlighted ? 0.44f : 0.3f);
        }

        public static void StyleText(
            TextMesh text,
            float characterSize,
            Color requestedColor,
            bool createUnderlay = true)
        {
            if (text == null)
            {
                return;
            }

            CraftLivePadSceneRoot.TryApplyConfiguredFont(text);
            text.fontSize = CrispFontSize;
            text.characterSize = ScaleCharacterSize(characterSize);
            text.fontStyle = FontStyle.Bold;
            text.richText = true;
            text.color = ResolveTextColor(requestedColor);
            CompensateButtonLabelScale(text);

            Renderer renderer = text.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            if (createUnderlay)
            {
                CraftLiveTextUnderlay underlay =
                    text.GetComponent<CraftLiveTextUnderlay>();
                if (underlay == null)
                {
                    underlay = text.gameObject.AddComponent<
                        CraftLiveTextUnderlay>();
                }

                underlay.Configure(text);
            }
        }

        public static float ScaleCharacterSize(float intendedCharacterSize)
        {
            return intendedCharacterSize * ReferenceFontSize /
                   CrispFontSize;
        }

        public static void BuildButtonFrame(
            Transform button,
            Color accent)
        {
            if (button == null || button.Find(FrameRootName) != null)
            {
                return;
            }

            GameObject frameRoot = new GameObject(FrameRootName);
            frameRoot.transform.SetParent(button, false);

            Color trim = Color.Lerp(Brass, RefineSurfaceColor(accent), 0.24f);
            CreateFramePart(frameRoot.transform, "TopRail",
                new Vector3(0f, 0.47f, -0.475f),
                new Vector3(1.07f, 0.105f, 0.12f), trim);
            CreateFramePart(frameRoot.transform, "BottomRail",
                new Vector3(0f, -0.47f, -0.475f),
                new Vector3(1.07f, 0.105f, 0.12f), DeepIron);
            CreateFramePart(frameRoot.transform, "LeftRail",
                new Vector3(-0.49f, 0f, -0.475f),
                new Vector3(0.085f, 0.86f, 0.12f), trim);
            CreateFramePart(frameRoot.transform, "RightRail",
                new Vector3(0.49f, 0f, -0.475f),
                new Vector3(0.085f, 0.86f, 0.12f), DeepIron);
            CreateFramePart(frameRoot.transform, "EdgeHighlight",
                new Vector3(0f, 0.34f, -0.535f),
                new Vector3(0.72f, 0.026f, 0.025f), ParchmentText,
                0.12f, 0.55f, 0.62f);

            CreateRivet(frameRoot.transform, new Vector3(-0.43f, 0.39f, -0.55f));
            CreateRivet(frameRoot.transform, new Vector3(0.43f, 0.39f, -0.55f));
            CreateRivet(frameRoot.transform, new Vector3(-0.43f, -0.39f, -0.55f));
            CreateRivet(frameRoot.transform, new Vector3(0.43f, -0.39f, -0.55f));
        }

        private static Color ResolveTextColor(Color source)
        {
            Color.RGBToHSV(source, out _, out float saturation,
                out float value);
            Color resolved = saturation < 0.16f || value > 0.88f
                ? ParchmentText
                : Color.Lerp(source, ParchmentText, 0.28f);
            resolved.a = source.a;
            return resolved;
        }

        private static void CompensateButtonLabelScale(TextMesh text)
        {
            Transform parent = text.transform.parent;
            if (parent == null ||
                parent.GetComponent<CraftLiveWorldButton>() == null)
            {
                return;
            }

            float parentWidth = Mathf.Abs(parent.localScale.x);
            float parentHeight = Mathf.Abs(parent.localScale.y);
            if (parentWidth < 0.0001f || parentHeight < 0.0001f)
            {
                return;
            }

            Vector3 scale = text.transform.localScale;
            scale.x = Mathf.Sign(scale.x) * Mathf.Abs(scale.y) *
                      parentHeight / parentWidth;
            text.transform.localScale = scale;
        }

        private static void CreateFramePart(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            float emission = 0.025f,
            float metallic = 0.82f,
            float smoothness = 0.38f)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            DestroySafely(part.GetComponent<Collider>());
            ApplyForgeSurface(part.GetComponent<Renderer>(), color,
                emission, metallic, smoothness);
        }

        private static void CreateRivet(
            Transform parent,
            Vector3 localPosition)
        {
            GameObject rivet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rivet.name = "Rivet";
            rivet.transform.SetParent(parent, false);
            rivet.transform.localPosition = localPosition;
            rivet.transform.localScale = new Vector3(0.065f, 0.065f, 0.035f);
            DestroySafely(rivet.GetComponent<Collider>());
            ApplyForgeSurface(rivet.GetComponent<Renderer>(), Brass,
                0.06f, 0.9f, 0.5f);
        }

        private static void DestroySafely(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }

    /// <summary>
    /// Keeps the dark text underlay synchronized when labels change at runtime.
    /// </summary>
    public sealed class CraftLiveTextUnderlay : MonoBehaviour
    {
        private const string UnderlayName = "ForgeUnderlay";
        private TextMesh source;
        private TextMesh underlay;

        public void Configure(TextMesh target)
        {
            source = target;
            EnsureUnderlay();
            Synchronize();
        }

        private void LateUpdate()
        {
            Synchronize();
        }

        private void EnsureUnderlay()
        {
            if (source == null || underlay != null)
            {
                return;
            }

            Transform existing = transform.Find(UnderlayName);
            GameObject underlayObject;
            if (existing != null)
            {
                underlayObject = existing.gameObject;
            }
            else
            {
                underlayObject = new GameObject(UnderlayName);
                underlayObject.transform.SetParent(transform, false);
                underlayObject.transform.localPosition =
                    new Vector3(0.014f, -0.014f, 0.012f);
            }

            underlay = underlayObject.GetComponent<TextMesh>();
            if (underlay == null)
            {
                underlay = underlayObject.AddComponent<TextMesh>();
            }
        }

        private void Synchronize()
        {
            if (source == null)
            {
                source = GetComponent<TextMesh>();
            }

            EnsureUnderlay();
            if (source == null || underlay == null)
            {
                return;
            }

            underlay.text = source.text;
            underlay.font = source.font;
            underlay.fontSize = source.fontSize;
            underlay.fontStyle = source.fontStyle;
            underlay.characterSize = source.characterSize;
            underlay.lineSpacing = source.lineSpacing;
            underlay.tabSize = source.tabSize;
            underlay.anchor = source.anchor;
            underlay.alignment = source.alignment;
            underlay.richText = source.richText;
            underlay.color = new Color(0.025f, 0.014f, 0.009f,
                Mathf.Clamp01(source.color.a * 0.94f));

            Renderer sourceRenderer = source.GetComponent<Renderer>();
            Renderer underlayRenderer = underlay.GetComponent<Renderer>();
            if (sourceRenderer != null && underlayRenderer != null)
            {
                underlayRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                underlayRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
                underlayRenderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                underlayRenderer.receiveShadows = false;
            }
        }
    }
}
