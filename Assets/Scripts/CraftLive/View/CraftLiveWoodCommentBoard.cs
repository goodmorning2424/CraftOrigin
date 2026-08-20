using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace CraftOrigin.CraftLive
{
    /// <summary>
    /// Forge-style notice board that can either attach to the Pad 1 wooden
    /// Box or use its own hierarchy transform as a standalone board.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CraftLiveWoodCommentBoard : MonoBehaviour
    {
        private const string GeneratedRootName =
            "Generated_WoodCommentBoard";

        [Header("References")]
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Renderer boxRenderer;

        [Header("Content")]
        [SerializeField] private string heading = "工房掲示板";
        [SerializeField, TextArea(1, 3)] private string fallbackComment =
            "素材を選んで、武器の設計を始めよう";
        [SerializeField, Range(8, 28)] private int charactersPerLine = 17;
        [SerializeField, Range(1, 3)] private int maximumLines = 2;

        [Header("Box Integration")]
        [SerializeField, Range(0.45f, 0.9f)]
        private float widthInsideBox = 0.76f;
        [SerializeField, Range(0.12f, 0.3f)]
        private float heightInsideBox = 0.2f;
        [FormerlySerializedAs("topGap")]
        [SerializeField, Range(0f, 0.08f)]
        [Tooltip("How far the board frame overlaps the Box top edge.")]
        private float frameOverlap = 0.012f;
        [SerializeField, Range(0.03f, 0.12f)]
        [Tooltip("Width of the two wooden mounting posts.")]
        private float mountWidthRatio = 0.055f;
        [SerializeField, Range(0.08f, 0.3f)]
        [Tooltip("Length of the mounting posts below the board.")]
        private float mountLengthRatio = 0.18f;
        [SerializeField, Range(0.01f, 0.08f)]
        private float screenMargin = 0.025f;
        [SerializeField, Range(0.04f, 2.5f)]
        private float cameraApproach = 1.1f;

        [Header("Standalone Layout")]
        [SerializeField] private bool standaloneLayout;
        [SerializeField] private Vector2 standaloneSize =
            new Vector2(6.2f, 1.35f);
        [SerializeField] private Material standaloneWoodMaterial;

        [Header("Display")]
        [SerializeField] private bool digitalStyle;
        [SerializeField] private Color digitalFrameColor =
            new Color(0.025f, 0.055f, 0.075f, 1f);
        [SerializeField] private Color digitalAccentColor =
            new Color(0.08f, 0.88f, 1f, 1f);
        [SerializeField] private Color displayColor =
            new Color(0.004f, 0.012f, 0.008f, 1f);
        [SerializeField] private Color displayTextColor =
            new Color(1f, 0.82f, 0.42f, 1f);
        [SerializeField] private Color indicatorColor =
            new Color(0.25f, 0.9f, 0.43f, 1f);
        [SerializeField, Range(8, 32)] private int ledColumns = 24;
        [SerializeField, Range(3, 12)] private int ledRows = 7;

        private GameObject generatedRoot;
        private Transform backplate;
        private Transform displayPanel;
        private Transform topRail;
        private Transform bottomRail;
        private Transform leftRail;
        private Transform rightRail;
        private Transform leftMount;
        private Transform rightMount;
        private readonly Transform[] trimRails = new Transform[4];
        private readonly Transform[] rivets = new Transform[4];
        private readonly Transform[] scanLines = new Transform[3];
        private readonly List<Transform> ledMatrixDots =
            new List<Transform>();
        private readonly List<Transform> sideStatusLeds =
            new List<Transform>();
        private Transform indicator;
        private Renderer[] surfaceReferenceRenderers =
            new Renderer[0];
        private TextMesh headingText;
        private TextMesh commentText;
        private string currentComment;
        private string explicitComment;
        private bool subscribed;
        private float appliedWidth = -1f;
        private float appliedHeight = -1f;
        private Material appliedWoodMaterial;
        private readonly List<Renderer> sceneTextRenderers =
            new List<Renderer>();
        private readonly HashSet<Renderer> suppressedTextRenderers =
            new HashSet<Renderer>();

        public string CurrentComment => currentComment;
        public Renderer BoxRenderer => boxRenderer;
        public TextMesh CommentText => commentText;

        private void OnValidate()
        {
            charactersPerLine = Mathf.Clamp(charactersPerLine, 8, 28);
            maximumLines = Mathf.Clamp(maximumLines, 1, 3);
            ledColumns = Mathf.Clamp(ledColumns, 8, 32);
            ledRows = Mathf.Clamp(ledRows, 3, 12);
            standaloneSize.x = Mathf.Max(0.5f, standaloneSize.x);
            standaloneSize.y = Mathf.Max(0.25f, standaloneSize.y);
            appliedWidth = -1f;
            appliedHeight = -1f;
            appliedWoodMaterial = null;

            if (Application.isPlaying)
            {
                ResolveReferences();
                EnsureVisuals();
                RefreshContent(session != null ? session.State : null);
                ApplyWoodMaterial();
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            RefreshContent(session != null ? session.State : null);
        }

        private void Start()
        {
            ResolveReferences();
            EnsureVisuals();
            RefreshContent(session != null ? session.State : null);
        }

        private void OnDisable()
        {
            Unsubscribe();
            RestoreSuppressedSceneText();
        }

        private void LateUpdate()
        {
            if (targetCamera == null ||
                (!standaloneLayout && boxRenderer == null))
            {
                ResolveReferences();
            }

            EnsureVisuals();
            if (standaloneLayout)
            {
                PlaceStandalone();
            }
            else
            {
                PlaceIntegratedWithBox();
            }
        }

        public void Configure(
            CraftLiveSession value,
            Camera camera)
        {
            if (subscribed && session != value)
            {
                Unsubscribe();
            }

            session = value;
            targetCamera = camera;
            ResolveReferences();
            Subscribe();
            RefreshContent(session != null ? session.State : null);
        }

        public void Configure(
            CraftLiveSession value,
            Camera camera,
            Renderer targetBox)
        {
            boxRenderer = targetBox;
            standaloneLayout = targetBox == null;
            Configure(value, camera);
            appliedWidth = -1f;
            appliedHeight = -1f;
        }

        public void Configure(
            CraftLiveSession value,
            Camera camera,
            Renderer targetBox,
            Renderer[] surfaceReferences)
        {
            surfaceReferenceRenderers =
                surfaceReferences ?? new Renderer[0];
            Configure(value, camera, targetBox);
        }

        /// <summary>
        /// Overrides the live room message. Pass null or an empty string to
        /// return to automatic session comments.
        /// </summary>
        public void SetComment(string value)
        {
            explicitComment = value ?? string.Empty;
            RefreshContent(session != null ? session.State : null);
        }

        [ContextMenu("Refresh Board Preview")]
        public void RefreshNow()
        {
            ResolveReferences();
            EnsureVisuals();
            RefreshContent(session != null ? session.State : null);
            ApplyWoodMaterial();
            if (standaloneLayout)
            {
                PlaceStandalone();
            }
            else
            {
                PlaceIntegratedWithBox();
            }
        }

        public static string FormatComment(
            string value,
            int charactersPerLine,
            int maximumLines)
        {
            int lineLength = Mathf.Max(1, charactersPerLine);
            int lineCount = Mathf.Max(1, maximumLines);
            string normalized = NormalizeComment(value);
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            int capacity = lineLength * lineCount;
            bool truncated = normalized.Length > capacity;
            int usableCapacity = truncated
                ? Mathf.Max(1, capacity - 1)
                : capacity;
            string visible = normalized.Substring(
                0,
                Mathf.Min(normalized.Length, usableCapacity));
            if (truncated)
            {
                visible += "…";
            }

            StringBuilder result = new StringBuilder(visible.Length + 2);
            for (int i = 0; i < visible.Length; i++)
            {
                if (i > 0 && i % lineLength == 0)
                {
                    result.Append('\n');
                }

                result.Append(visible[i]);
            }

            return result.ToString();
        }

        public static Rect CalculateBoardViewportRect(
            Rect boxBounds,
            float widthRatio,
            float heightRatio,
            float frameOverlap,
            float margin)
        {
            float safeMargin = Mathf.Clamp(margin, 0f, 0.2f);
            float width = Mathf.Clamp(
                boxBounds.width * Mathf.Clamp01(widthRatio),
                0.16f,
                1f - safeMargin * 2f);
            float height = Mathf.Clamp(
                boxBounds.height * Mathf.Clamp01(heightRatio),
                0.085f,
                0.22f);
            float x = Mathf.Clamp(
                boxBounds.center.x,
                safeMargin + width * 0.5f,
                1f - safeMargin - width * 0.5f);
            float overlap = Mathf.Clamp(
                frameOverlap,
                0f,
                height * 0.45f);
            float y = boxBounds.yMax - overlap + height * 0.5f;
            y = Mathf.Clamp(
                y,
                safeMargin + height * 0.5f,
                1f - safeMargin - height * 0.5f);
            return new Rect(
                x - width * 0.5f,
                y - height * 0.5f,
                width,
                height);
        }

        private static string NormalizeComment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder normalized = new StringBuilder(value.Length);
            bool previousWasSpace = false;
            foreach (char character in value.Trim())
            {
                char safeCharacter = character == '<'
                    ? '＜'
                    : character == '>'
                        ? '＞'
                        : character;
                bool isSpace = char.IsWhiteSpace(safeCharacter);
                if (isSpace)
                {
                    if (!previousWasSpace)
                    {
                        normalized.Append(' ');
                    }
                }
                else
                {
                    normalized.Append(safeCharacter);
                }

                previousWasSpace = isSpace;
            }

            return normalized.ToString();
        }

        private void ResolveReferences()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (targetCamera == null)
            {
                CraftLivePad1GalleryController gallery =
                    GetComponent<CraftLivePad1GalleryController>();
                targetCamera = gallery != null
                    ? gallery.TargetCamera
                    : Camera.main;
            }

            if (targetCamera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(
                    FindObjectsInactive.Include);
                foreach (Camera candidate in cameras)
                {
                    if (candidate != null &&
                        candidate.gameObject.scene == gameObject.scene)
                    {
                        targetCamera = candidate;
                        break;
                    }
                }
            }

            if (!standaloneLayout && boxRenderer == null)
            {
                Renderer[] renderers = FindObjectsByType<Renderer>(
                    FindObjectsInactive.Include);
                foreach (Renderer candidate in renderers)
                {
                    if (candidate != null &&
                        candidate.gameObject.name == "Box" &&
                        candidate.gameObject.scene == gameObject.scene)
                    {
                        boxRenderer = candidate;
                        break;
                    }
                }
            }

            ApplyWoodMaterial();
        }

        private void Subscribe()
        {
            if (subscribed || session == null || !isActiveAndEnabled)
            {
                return;
            }

            session.StateChanged += HandleStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
            }

            subscribed = false;
        }

        private void HandleStateChanged(CraftLiveRoomState state)
        {
            RefreshContent(state);
        }

        private void RefreshContent(CraftLiveRoomState state)
        {
            string source = !string.IsNullOrWhiteSpace(explicitComment)
                ? explicitComment
                : state != null && !string.IsNullOrWhiteSpace(state.message)
                    ? state.message
                    : fallbackComment;
            currentComment = source ?? string.Empty;
            if (commentText != null)
            {
                commentText.text = FormatComment(
                    currentComment,
                    charactersPerLine,
                    maximumLines);
            }
        }

        private void EnsureVisuals()
        {
            if (generatedRoot != null)
            {
                return;
            }

            Transform existing = transform.Find(GeneratedRootName);
            if (existing == null && boxRenderer != null)
            {
                existing = boxRenderer.transform.root.Find(
                    GeneratedRootName);
            }

            generatedRoot = existing != null
                ? existing.gameObject
                : new GameObject(GeneratedRootName);
            generatedRoot.hideFlags = Application.isPlaying
                ? HideFlags.None
                : HideFlags.DontSaveInEditor;
            generatedRoot.transform.SetParent(transform, false);

            Material woodMaterial = digitalStyle
                ? null
                : standaloneLayout
                ? standaloneWoodMaterial
                : boxRenderer != null
                    ? boxRenderer.sharedMaterial
                    : null;
            backplate = CreateCube("WoodBackplate", woodMaterial);
            displayPanel = CreateCube("ElectronicDisplay");
            topRail = CreateCube("WoodTopRail", woodMaterial);
            bottomRail = CreateCube("WoodBottomRail", woodMaterial);
            leftRail = CreateCube("WoodLeftRail", woodMaterial);
            rightRail = CreateCube("WoodRightRail", woodMaterial);
            leftMount = CreateCube("WoodLeftMount", woodMaterial);
            rightMount = CreateCube("WoodRightMount", woodMaterial);

            for (int i = 0; i < trimRails.Length; i++)
            {
                trimRails[i] = CreateCube($"BrassTrim_{i + 1}");
            }

            for (int i = 0; i < rivets.Length; i++)
            {
                rivets[i] = CreateSphere($"Rivet_{i + 1}");
            }

            for (int i = 0; i < scanLines.Length; i++)
            {
                scanLines[i] = CreateCube($"DisplayLine_{i + 1}");
            }

            if (digitalStyle)
            {
                CreateLedMatrix();
            }

            indicator = CreateSphere("LiveIndicator");

            headingText = CreateText("Heading", heading);
            headingText.anchor = TextAnchor.MiddleCenter;
            headingText.alignment = TextAlignment.Center;
            commentText = CreateText("Comment", string.Empty);
            commentText.anchor = TextAnchor.MiddleCenter;
            commentText.alignment = TextAlignment.Center;
            commentText.lineSpacing = 0.88f;
            RefreshContent(session != null ? session.State : null);
            ApplyWoodMaterial();
        }

        private void CreateLedMatrix()
        {
            for (int row = 0; row < ledRows; row++)
            {
                for (int column = 0; column < ledColumns; column++)
                {
                    ledMatrixDots.Add(CreateCube(
                        $"LedDot_{row:00}_{column:00}"));
                }
            }

            for (int side = 0; side < 2; side++)
            {
                for (int row = 0; row < ledRows; row++)
                {
                    sideStatusLeds.Add(CreateCube(
                        $"StatusLed_{side}_{row:00}"));
                }
            }
        }

        private void ApplyWoodMaterial()
        {
            if (digitalStyle)
            {
                ApplyDigitalFrameStyle();
                return;
            }

            Material woodMaterial = standaloneLayout
                ? standaloneWoodMaterial
                : boxRenderer != null
                    ? boxRenderer.sharedMaterial
                    : null;
            if (woodMaterial == null || woodMaterial == appliedWoodMaterial)
            {
                return;
            }

            appliedWoodMaterial = woodMaterial;
            Transform[] woodParts =
            {
                backplate,
                topRail,
                bottomRail,
                leftRail,
                rightRail,
                leftMount,
                rightMount
            };
            foreach (Transform woodPart in woodParts)
            {
                Renderer renderer = woodPart != null
                    ? woodPart.GetComponent<Renderer>()
                    : null;
                if (renderer != null)
                {
                    renderer.sharedMaterial = woodMaterial;
                }
            }
        }

        private void ApplyDigitalFrameStyle()
        {
            Transform[] frameParts =
            {
                backplate,
                topRail,
                bottomRail,
                leftRail,
                rightRail,
                leftMount,
                rightMount
            };
            foreach (Transform framePart in frameParts)
            {
                Renderer renderer = framePart != null
                    ? framePart.GetComponent<Renderer>()
                    : null;
                CraftLiveForgeUITheme.ApplyForgeSurface(
                    renderer,
                    digitalFrameColor,
                    0.12f,
                    0.88f,
                    0.58f);
            }

            CraftLiveForgeUITheme.ApplyForgeSurface(
                topRail != null ? topRail.GetComponent<Renderer>() : null,
                digitalAccentColor,
                0.6f,
                0.7f,
                0.72f);
        }

        private Transform CreateCube(string name, Material material = null)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(generatedRoot.transform, false);
            RemoveGeneratedCollider(part.GetComponent<Collider>());
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
            else
            {
                CraftLiveForgeUITheme.EnsureCompatibleSurface(renderer);
            }

            return part.transform;
        }

        private Transform CreateSphere(string name)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            part.transform.SetParent(generatedRoot.transform, false);
            RemoveGeneratedCollider(part.GetComponent<Collider>());
            CraftLiveForgeUITheme.EnsureCompatibleSurface(
                part.GetComponent<Renderer>());
            return part.transform;
        }

        private static void RemoveGeneratedCollider(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        private TextMesh CreateText(string name, string value)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(generatedRoot.transform, false);
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = value;
            return text;
        }

        private void PlaceStandalone()
        {
            if (generatedRoot == null)
            {
                return;
            }

            if (generatedRoot.transform.parent != transform)
            {
                generatedRoot.transform.SetParent(transform, false);
            }

            generatedRoot.SetActive(true);
            generatedRoot.transform.localPosition = Vector3.zero;
            generatedRoot.transform.localRotation = Quaternion.identity;
            generatedRoot.transform.localScale = Vector3.one;
            LayoutVisuals(standaloneSize.x, standaloneSize.y);
        }

        private void PlaceIntegratedWithBox()
        {
            if (generatedRoot == null || targetCamera == null ||
                boxRenderer == null || !boxRenderer.enabled)
            {
                if (generatedRoot != null)
                {
                    generatedRoot.SetActive(false);
                }

                return;
            }

            if (!TryGetViewportBounds(
                    targetCamera,
                    boxRenderer.bounds,
                    out Rect boxBounds,
                    out float frontDepth))
            {
                generatedRoot.SetActive(false);
                return;
            }

            Rect boardRect = CalculateBoardViewportRect(
                boxBounds,
                widthInsideBox,
                heightInsideBox,
                frameOverlap,
                screenMargin);
            UpdateSuppressedSceneText(boardRect);
            float depth = Mathf.Max(
                targetCamera.nearClipPlane + 0.05f,
                FindNearestSurfaceDepth(frontDepth) - cameraApproach);
            Vector3 center = targetCamera.ViewportToWorldPoint(
                new Vector3(boardRect.center.x, boardRect.center.y, depth));
            Vector3 left = targetCamera.ViewportToWorldPoint(
                new Vector3(boardRect.xMin, boardRect.center.y, depth));
            Vector3 right = targetCamera.ViewportToWorldPoint(
                new Vector3(boardRect.xMax, boardRect.center.y, depth));
            Vector3 bottom = targetCamera.ViewportToWorldPoint(
                new Vector3(boardRect.center.x, boardRect.yMin, depth));
            Vector3 top = targetCamera.ViewportToWorldPoint(
                new Vector3(boardRect.center.x, boardRect.yMax, depth));

            generatedRoot.SetActive(true);
            AttachGeneratedRootToBox();
            generatedRoot.transform.SetPositionAndRotation(
                center,
                targetCamera.transform.rotation);
            CompensateBoxScale();
            LayoutVisuals(
                Vector3.Distance(left, right),
                Vector3.Distance(bottom, top));
        }

        private float FindNearestSurfaceDepth(float fallbackDepth)
        {
            float nearest = fallbackDepth;
            if (surfaceReferenceRenderers == null || targetCamera == null)
            {
                return nearest;
            }

            foreach (Renderer surface in surfaceReferenceRenderers)
            {
                if (surface != null && surface.enabled &&
                    TryGetViewportBounds(
                        targetCamera,
                        surface.bounds,
                        out _,
                        out float surfaceDepth))
                {
                    nearest = Mathf.Min(nearest, surfaceDepth);
                }
            }

            return nearest;
        }

        private void AttachGeneratedRootToBox()
        {
            if (generatedRoot == null || boxRenderer == null)
            {
                return;
            }

            Transform boxRoot = boxRenderer.transform.root;
            if (boxRoot != null && generatedRoot.transform.parent != boxRoot)
            {
                generatedRoot.transform.SetParent(boxRoot, true);
            }
        }

        private void CompensateBoxScale()
        {
            Transform parent = generatedRoot != null
                ? generatedRoot.transform.parent
                : null;
            if (parent == null)
            {
                return;
            }

            Vector3 scale = parent.lossyScale;
            generatedRoot.transform.localScale = new Vector3(
                Mathf.Abs(scale.x) > 0.0001f ? 1f / scale.x : 1f,
                Mathf.Abs(scale.y) > 0.0001f ? 1f / scale.y : 1f,
                Mathf.Abs(scale.z) > 0.0001f ? 1f / scale.z : 1f);
        }

        private void UpdateSuppressedSceneText(Rect boardRect)
        {
            if (sceneTextRenderers.Count == 0)
            {
                TextMesh[] sceneTexts = FindObjectsByType<TextMesh>(
                    FindObjectsInactive.Exclude);
                foreach (TextMesh sceneText in sceneTexts)
                {
                    if (sceneText == null ||
                        sceneText.transform.IsChildOf(
                            generatedRoot.transform) ||
                        sceneText.gameObject.scene != gameObject.scene)
                    {
                        continue;
                    }

                    Renderer renderer = sceneText.GetComponent<Renderer>();
                    if (renderer != null && renderer.enabled)
                    {
                        sceneTextRenderers.Add(renderer);
                    }
                }
            }

            foreach (Renderer renderer in sceneTextRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                bool overlaps = TryGetViewportBounds(
                    targetCamera,
                    renderer.bounds,
                    out Rect textBounds,
                    out _) &&
                    boardRect.Overlaps(textBounds);
                if (overlaps && renderer.enabled)
                {
                    renderer.enabled = false;
                    suppressedTextRenderers.Add(renderer);
                }
                else if (!overlaps &&
                         suppressedTextRenderers.Remove(renderer))
                {
                    renderer.enabled = true;
                }
            }
        }

        private void RestoreSuppressedSceneText()
        {
            foreach (Renderer renderer in suppressedTextRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            suppressedTextRenderers.Clear();
        }

        private void LayoutVisuals(float width, float height)
        {
            if (Mathf.Abs(width - appliedWidth) < 0.0001f &&
                Mathf.Abs(height - appliedHeight) < 0.0001f)
            {
                return;
            }

            appliedWidth = width;
            appliedHeight = height;
            float depth = Mathf.Clamp(height * 0.13f, 0.018f, 0.08f);
            float rail = height * 0.105f;
            float innerWidth = Mathf.Max(0.05f, width - rail * 2f);
            float innerHeight = Mathf.Max(0.05f, height - rail * 2f);
            float frontZ = -depth * 0.62f;

            SetPart(backplate, Vector3.zero,
                new Vector3(width, height, depth));
            SetPart(displayPanel, new Vector3(0f, 0f, frontZ),
                new Vector3(innerWidth, innerHeight, depth * 0.24f));
            ApplyDisplaySurface(
                displayPanel.GetComponent<Renderer>(),
                displayColor,
                0.23f,
                0.16f,
                0.52f);

            SetPart(topRail,
                new Vector3(0f, height * 0.5f - rail * 0.5f, frontZ),
                new Vector3(width, rail, depth * 0.58f));
            SetPart(bottomRail,
                new Vector3(0f, -height * 0.5f + rail * 0.5f, frontZ),
                new Vector3(width, rail, depth * 0.58f));
            SetPart(leftRail,
                new Vector3(-width * 0.5f + rail * 0.5f, 0f, frontZ),
                new Vector3(rail, innerHeight, depth * 0.58f));
            SetPart(rightRail,
                new Vector3(width * 0.5f - rail * 0.5f, 0f, frontZ),
                new Vector3(rail, innerHeight, depth * 0.58f));

            float mountWidth = Mathf.Max(
                rail * 0.55f,
                width * mountWidthRatio);
            float mountLength = Mathf.Max(
                rail,
                height * mountLengthRatio);
            float mountHeight = mountLength + rail * 0.58f;
            float mountY = -height * 0.5f + rail * 0.18f -
                           mountHeight * 0.5f;
            float mountX = innerWidth * 0.36f;
            Vector3 mountScale = new Vector3(
                mountWidth,
                mountHeight,
                depth * 0.72f);
            SetPart(leftMount,
                new Vector3(-mountX, mountY, frontZ + depth * 0.08f),
                mountScale);
            SetPart(rightMount,
                new Vector3(mountX, mountY, frontZ + depth * 0.08f),
                mountScale);

            float trim = Mathf.Max(0.004f, rail * 0.14f);
            float trimZ = frontZ - depth * 0.22f;
            SetPart(trimRails[0],
                new Vector3(0f, innerHeight * 0.5f, trimZ),
                new Vector3(innerWidth, trim, depth * 0.13f));
            SetPart(trimRails[1],
                new Vector3(0f, -innerHeight * 0.5f, trimZ),
                new Vector3(innerWidth, trim, depth * 0.13f));
            SetPart(trimRails[2],
                new Vector3(-innerWidth * 0.5f, 0f, trimZ),
                new Vector3(trim, innerHeight, depth * 0.13f));
            SetPart(trimRails[3],
                new Vector3(innerWidth * 0.5f, 0f, trimZ),
                new Vector3(trim, innerHeight, depth * 0.13f));
            foreach (Transform trimRail in trimRails)
            {
                CraftLiveForgeUITheme.ApplyForgeSurface(
                    trimRail.GetComponent<Renderer>(),
                    digitalStyle
                        ? digitalAccentColor
                        : CraftLiveForgeUITheme.Brass,
                    digitalStyle ? 0.75f : 0.08f,
                    0.84f,
                    digitalStyle ? 0.68f : 0.42f);
            }

            Vector2[] corners =
            {
                new Vector2(-1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-1f, -1f),
                new Vector2(1f, -1f)
            };
            float rivetSize = rail * 0.42f;
            for (int i = 0; i < rivets.Length; i++)
            {
                Vector2 corner = corners[i];
                SetPart(rivets[i], new Vector3(
                        corner.x * (width * 0.5f - rail * 0.5f),
                        corner.y * (height * 0.5f - rail * 0.5f),
                        trimZ - depth * 0.08f),
                    new Vector3(rivetSize, rivetSize, depth * 0.22f));
                CraftLiveForgeUITheme.ApplyForgeSurface(
                    rivets[i].GetComponent<Renderer>(),
                    digitalStyle
                        ? digitalAccentColor
                        : CraftLiveForgeUITheme.Brass,
                    digitalStyle ? 0.48f : 0.08f,
                    0.9f,
                    0.5f);
            }

            float lineWidth = innerWidth * 0.88f;
            for (int i = 0; i < scanLines.Length; i++)
            {
                float y = Mathf.Lerp(
                    -innerHeight * 0.32f,
                    innerHeight * 0.32f,
                    i / (float)(scanLines.Length - 1));
                SetPart(scanLines[i],
                    new Vector3(0f, y, trimZ - depth * 0.02f),
                    new Vector3(lineWidth, trim * 0.32f, depth * 0.05f));
                ApplyDisplaySurface(
                    scanLines[i].GetComponent<Renderer>(),
                    digitalStyle
                        ? new Color(0.01f, 0.24f, 0.3f, 1f)
                        : new Color(0.015f, 0.055f, 0.034f, 1f),
                    digitalStyle ? 0.48f : 0.1f,
                    0.1f,
                    0.38f);
            }

            LayoutLedMatrix(
                innerWidth,
                innerHeight,
                trimZ,
                depth);

            float indicatorSize = rail * 0.32f;
            SetPart(indicator,
                new Vector3(
                    -innerWidth * 0.44f,
                    -innerHeight * 0.37f,
                    trimZ - depth * 0.1f),
                new Vector3(
                    indicatorSize,
                    indicatorSize,
                    depth * 0.16f));
            CraftLiveForgeUITheme.ApplyForgeSurface(
                indicator.GetComponent<Renderer>(),
                indicatorColor,
                0.75f,
                0.16f,
                0.45f);

            float textZ = trimZ - depth * 0.16f;
            headingText.transform.localPosition =
                new Vector3(0f, innerHeight * 0.27f, textZ);
            commentText.transform.localPosition =
                new Vector3(0f, -innerHeight * 0.045f, textZ);
            headingText.transform.localScale = Vector3.one;
            commentText.transform.localScale = Vector3.one;
            CraftLiveForgeUITheme.StyleText(
                headingText,
                height * 0.016f,
                digitalStyle
                    ? digitalAccentColor
                    : CraftLiveForgeUITheme.MutedText,
                true);
            CraftLiveForgeUITheme.StyleText(
                commentText,
                height * 0.023f,
                displayTextColor,
                true);
            // Keep the board on the original high-legibility gothic face;
            // weapon names and forge headings continue using the newer fonts.
            CraftLiveForgeUITheme.ApplyBoardFont(headingText);
            CraftLiveForgeUITheme.ApplyBoardFont(commentText);
            SetTextUnderlayOffset(headingText, height * 0.004f);
            SetTextUnderlayOffset(commentText, height * 0.004f);
        }

        private void LayoutLedMatrix(
            float innerWidth,
            float innerHeight,
            float frontZ,
            float depth)
        {
            if (!digitalStyle || ledMatrixDots.Count == 0)
            {
                return;
            }

            float matrixWidth = innerWidth * 0.86f;
            float matrixHeight = innerHeight * 0.72f;
            float dotSize = Mathf.Min(
                matrixWidth / Mathf.Max(1f, ledColumns * 3.2f),
                matrixHeight / Mathf.Max(1f, ledRows * 3.2f));
            int index = 0;
            for (int row = 0; row < ledRows; row++)
            {
                for (int column = 0; column < ledColumns; column++)
                {
                    Transform dot = ledMatrixDots[index++];
                    float x = Mathf.Lerp(
                        -matrixWidth * 0.5f,
                        matrixWidth * 0.5f,
                        ledColumns <= 1
                            ? 0.5f
                            : column / (float)(ledColumns - 1));
                    float y = Mathf.Lerp(
                        -matrixHeight * 0.5f,
                        matrixHeight * 0.5f,
                        ledRows <= 1
                            ? 0.5f
                            : row / (float)(ledRows - 1));
                    SetPart(
                        dot,
                        new Vector3(x, y, frontZ - depth * 0.18f),
                        new Vector3(dotSize, dotSize, depth * 0.055f));
                    ApplyDisplaySurface(
                        dot.GetComponent<Renderer>(),
                        new Color(0.2f, 0.24f, 0.17f, 1f),
                        0.06f,
                        0f,
                        0.18f);
                }
            }

            float ledX = innerWidth * 0.47f;
            float ledSize = dotSize * 1.6f;
            index = 0;
            for (int side = 0; side < 2; side++)
            {
                for (int row = 0; row < ledRows; row++)
                {
                    Transform led = sideStatusLeds[index++];
                    float y = Mathf.Lerp(
                        -matrixHeight * 0.45f,
                        matrixHeight * 0.45f,
                        ledRows <= 1
                            ? 0.5f
                            : row / (float)(ledRows - 1));
                    Color ledColor = row < ledRows * 0.62f
                        ? new Color(0.12f, 1f, 0.28f, 1f)
                        : new Color(1f, 0.34f, 0.08f, 1f);
                    SetPart(
                        led,
                        new Vector3(
                            side == 0 ? -ledX : ledX,
                            y,
                            frontZ - depth * 0.22f),
                        new Vector3(ledSize, ledSize, depth * 0.07f));
                    ApplyDisplaySurface(
                        led.GetComponent<Renderer>(),
                        ledColor,
                        1.4f,
                        0.04f,
                        0.58f);
                }
            }
        }

        private static void SetTextUnderlayOffset(
            TextMesh text,
            float offset)
        {
            Transform underlay = text != null
                ? text.transform.Find("ForgeUnderlay")
                : null;
            if (underlay != null)
            {
                underlay.localPosition = new Vector3(
                    offset,
                    -offset,
                    Mathf.Max(0.0005f, offset * 0.6f));
            }
        }

        private static void ApplyDisplaySurface(
            Renderer target,
            Color color,
            float emissionStrength,
            float metallic,
            float smoothness)
        {
            if (target == null)
            {
                return;
            }

            // Runtime primitives use Unity's built-in material by default.
            // That shader is not available in URP WebGL builds and renders
            // magenta, so replace it before applying display colours.
            CraftLiveForgeUITheme.EnsureCompatibleSurface(target);

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

        private static void SetPart(
            Transform part,
            Vector3 localPosition,
            Vector3 localScale)
        {
            if (part == null)
            {
                return;
            }

            part.localPosition = localPosition;
            part.localRotation = Quaternion.identity;
            part.localScale = localScale;
        }

        private static bool TryGetViewportBounds(
            Camera camera,
            Bounds bounds,
            out Rect viewportBounds,
            out float frontDepth)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            frontDepth = float.PositiveInfinity;
            bool found = false;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 point = bounds.center + Vector3.Scale(
                            bounds.extents,
                            new Vector3(x, y, z));
                        Vector3 viewport = camera.WorldToViewportPoint(point);
                        if (viewport.z <= camera.nearClipPlane)
                        {
                            continue;
                        }

                        found = true;
                        minX = Mathf.Min(minX, viewport.x);
                        minY = Mathf.Min(minY, viewport.y);
                        maxX = Mathf.Max(maxX, viewport.x);
                        maxY = Mathf.Max(maxY, viewport.y);
                        frontDepth = Mathf.Min(frontDepth, viewport.z);
                    }
                }
            }

            viewportBounds = found
                ? Rect.MinMaxRect(minX, minY, maxX, maxY)
                : new Rect();
            return found;
        }
    }
}
