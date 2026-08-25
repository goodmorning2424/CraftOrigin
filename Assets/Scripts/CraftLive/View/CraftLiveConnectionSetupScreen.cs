using System.Collections.Generic;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    /// <summary>
    /// Universal four-pad room check. It is enabled with setup=1 while the
    /// existing screen/pad and room query parameters remain unchanged.
    /// </summary>
    public sealed class CraftLiveConnectionSetupScreen : MonoBehaviour
    {
        private static readonly CraftLiveRole[] Roles =
        {
            CraftLiveRole.MaterialPad,
            CraftLiveRole.WorkbenchPad,
            CraftLiveRole.QrPad,
            CraftLiveRole.HologramPad
        };

        private CraftLiveSession session;
        private CraftLiveRoomTransport transport;
        private CraftLiveRole role;
        private Camera presentationCamera;
        private Transform displayRoot;
        private GameObject generatedScreen;
        private readonly Renderer[] statusDots = new Renderer[4];
        private readonly TextMesh[] statusTexts = new TextMesh[4];
        private TextMesh connectionText;
        private TextMesh startText;
        private Renderer startRenderer;
        private Collider startCollider;
        private int setupGeneration;
        private bool generationCaptured;
        private bool started;
        private float nextRefreshTime;
        private readonly Dictionary<Renderer, bool>
            hiddenPresentationRenderers =
                new Dictionary<Renderer, bool>();

        private const float SetupCameraDistance = 2f;
        private const float SetupDesignWidth = 7.7f;
        private const float SetupDesignHeight = 10.6f;

        public void Configure(
            CraftLiveSession targetSession,
            CraftLiveRoomTransport targetTransport,
            CraftLiveRole targetRole,
            Camera targetCamera = null)
        {
            Unsubscribe();
            session = targetSession;
            transport = targetTransport;
            role = targetRole;
            presentationCamera = targetCamera;
            ResolveDisplayRoot();
            Subscribe();
            Build();
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            RestoreUnderlyingPresentation();
        }

        private void OnDestroy()
        {
            RestoreUnderlyingPresentation();
            DestroySafely(generatedScreen);
        }

        private void LateUpdate()
        {
            if (!started && generatedScreen != null &&
                generatedScreen.activeSelf)
            {
                HideUnderlyingPresentation();
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private void Subscribe()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
                session.StateChanged += HandleStateChanged;
            }

            if (transport != null)
            {
                transport.PresenceChanged -= Refresh;
                transport.PresenceChanged += Refresh;
            }
        }

        private void Unsubscribe()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
            }

            if (transport != null)
            {
                transport.PresenceChanged -= Refresh;
            }
        }

        private void HandleStateChanged(CraftLiveRoomState unused)
        {
            Refresh();
        }

        private void ResolveDisplayRoot()
        {
            CraftLivePad1Bindings pad1 =
                GetComponentInChildren<CraftLivePad1Bindings>(true);
            CraftLivePad2Bindings pad2 =
                GetComponentInChildren<CraftLivePad2Bindings>(true);
            CraftLivePad3Bindings pad3 =
                GetComponentInChildren<CraftLivePad3Bindings>(true);
            CraftLivePad4Bindings pad4 =
                GetComponentInChildren<CraftLivePad4Bindings>(true);
            displayRoot = pad1 != null ? pad1.UiRoot :
                pad2 != null ? pad2.UiRoot :
                pad3 != null ? pad3.UiRoot :
                pad4 != null ? pad4.UiRoot : transform;
        }

        private void Build()
        {
            DestroySafely(generatedScreen);
            if (displayRoot == null && presentationCamera == null)
            {
                return;
            }

            generatedScreen = new GameObject(
                "Generated_ConnectionSetupScreen");
            ConfigureScreenTransform(generatedScreen.transform);
            generatedScreen.AddComponent<CraftLiveGeneratedRuntimeVisual>();

            CreatePart(
                "Backdrop",
                new Vector3(0f, 0f, 0.18f),
                new Vector3(7.7f, 10.6f, 0.2f),
                CraftLiveForgeUITheme.DeepIron,
                false);
            CreatePart(
                "SetupBoard",
                new Vector3(0f, 0f, -0.38f),
                new Vector3(5.7f, 8.4f, 0.18f),
                new Color(0.16f, 0.055f, 0.018f),
                false);
            CreateText(
                "Title",
                "ルーム接続確認",
                new Vector3(0f, 3.45f, -0.62f),
                0.075f,
                Color.white);
            CreateText(
                "Room",
                $"ROOM  {session?.RoomId ?? "-"}",
                new Vector3(0f, 2.85f, -0.62f),
                0.046f,
                CraftLiveForgeUITheme.Brass);
            CreateText(
                "Role",
                $"この端末: {RoleLabel(role)}",
                new Vector3(0f, 2.35f, -0.62f),
                0.038f,
                CraftLiveForgeUITheme.ParchmentText);

            for (int i = 0; i < Roles.Length; i++)
            {
                float y = 1.35f - i * 0.9f;
                GameObject dot = CreatePart(
                    $"StatusDot_{i}",
                    new Vector3(-1.85f, y, -0.68f),
                    new Vector3(0.28f, 0.28f, 0.12f),
                    new Color(0.38f, 0.12f, 0.1f),
                    false);
                statusDots[i] = dot.GetComponent<Renderer>();
                statusTexts[i] = CreateText(
                    $"StatusText_{i}",
                    string.Empty,
                    new Vector3(0.2f, y, -0.7f),
                    0.04f,
                    Color.white);
            }

            connectionText = CreateText(
                "ConnectionMessage",
                string.Empty,
                new Vector3(0f, -2.25f, -0.7f),
                0.032f,
                CraftLiveForgeUITheme.ParchmentText);

            if (role == CraftLiveRole.WorkbenchPad)
            {
                GameObject start = CreatePart(
                    "StartConnectedGroup",
                    new Vector3(0f, -3.2f, -0.72f),
                    new Vector3(3.8f, 0.82f, 0.24f),
                    CraftLiveForgeUITheme.Ember,
                    true);
                startRenderer = start.GetComponent<Renderer>();
                startCollider = start.GetComponent<Collider>();
                CraftLiveWorldButton button =
                    start.AddComponent<CraftLiveWorldButton>();
                button.Configure(
                    start.transform,
                    new[] { startRenderer },
                    CraftLiveForgeUITheme.Ember,
                    Color.Lerp(
                        CraftLiveForgeUITheme.Ember,
                        Color.white,
                        0.3f),
                    Color.white);
                button.AddListener(StartGroup);
                startText = CreateText(
                    "StartLabel",
                    "未接続の端末があってもスタートできます",
                    new Vector3(0f, -3.2f, -0.9f),
                    0.031f,
                    Color.white);
            }
            else
            {
                startText = CreateText(
                    "WaitLabel",
                    "確認後、Pad 2でスタート",
                    new Vector3(0f, -3.2f, -0.7f),
                    0.038f,
                    CraftLiveForgeUITheme.Brass);
            }

            HideUnderlyingPresentation();
        }

        private void ConfigureScreenTransform(Transform screen)
        {
            if (presentationCamera == null)
            {
                screen.SetParent(displayRoot, false);
                screen.localPosition = Vector3.zero;
                screen.localRotation = Quaternion.identity;
                screen.localScale = Vector3.one * 0.72f;
                return;
            }

            // The pad UI roots use different authored rotations and scales.
            // Keeping setup on the shared camera makes it the same upright,
            // full-screen gate for every role without moving scene anchors.
            screen.SetParent(presentationCamera.transform, false);
            screen.localPosition = new Vector3(
                0f,
                0f,
                SetupCameraDistance);
            screen.localRotation = Quaternion.identity;
            screen.localScale = Vector3.one *
                ResolveCameraFacingScale(
                    presentationCamera,
                    SetupCameraDistance);
        }

        public static float ResolveCameraFacingScale(
            Camera targetCamera,
            float distance)
        {
            if (targetCamera == null)
            {
                return 0.72f;
            }

            float aspect = targetCamera.aspect > 0.01f
                ? targetCamera.aspect
                : 0.75f;
            float visibleHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 2f * Mathf.Max(0.1f, distance) *
                  Mathf.Tan(targetCamera.fieldOfView *
                            0.5f * Mathf.Deg2Rad);
            float visibleWidth = visibleHeight * aspect;
            const float viewportMargin = 0.9f;
            return Mathf.Max(
                0.01f,
                Mathf.Min(
                    visibleWidth * viewportMargin / SetupDesignWidth,
                    visibleHeight * viewportMargin / SetupDesignHeight));
        }

        private void HideUnderlyingPresentation()
        {
            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);
            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null ||
                    (generatedScreen != null &&
                     targetRenderer.transform.IsChildOf(
                         generatedScreen.transform)))
                {
                    continue;
                }

                if (!hiddenPresentationRenderers.ContainsKey(targetRenderer))
                {
                    hiddenPresentationRenderers[targetRenderer] =
                        targetRenderer.enabled;
                }

                targetRenderer.enabled = false;
            }
        }

        private void RestoreUnderlyingPresentation()
        {
            foreach (KeyValuePair<Renderer, bool> entry in
                     hiddenPresentationRenderers)
            {
                if (entry.Key != null)
                {
                    entry.Key.enabled = entry.Value;
                }
            }

            hiddenPresentationRenderers.Clear();
        }

        private void Refresh()
        {
            if (session == null || generatedScreen == null)
            {
                return;
            }

            if (!generationCaptured &&
                (transport == null ||
                 !transport.IsRemoteMode ||
                 transport.InitialSyncComplete))
            {
                setupGeneration = session.State != null
                    ? session.State.groupGeneration
                    : 0;
                generationCaptured = true;
            }

            if (started ||
                (generationCaptured && session.State != null &&
                 session.State.groupGeneration > setupGeneration))
            {
                generatedScreen.SetActive(false);
                RestoreUnderlyingPresentation();
                return;
            }

            generatedScreen.SetActive(true);
            HideUnderlyingPresentation();
            for (int i = 0; i < Roles.Length; i++)
            {
                bool connected = transport != null &&
                                 transport.IsRoleConnected(Roles[i]);
                Color color = connected
                    ? new Color(0.18f, 0.82f, 0.42f)
                    : new Color(0.55f, 0.12f, 0.09f);
                CraftLiveForgeUITheme.ApplyForgeSurface(
                    statusDots[i],
                    color,
                    0.01f,
                    0.18f,
                    0.3f);
                if (statusTexts[i] != null)
                {
                    statusTexts[i].text =
                        $"{RoleLabel(Roles[i])}    " +
                        (connected ? "接続済み" : "未接続");
                    statusTexts[i].color = connected
                        ? Color.white
                        : new Color(1f, 0.68f, 0.58f);
                }
            }

            bool allConnected = transport != null &&
                                transport.AreAllPadsConnected();
            bool canStart = session != null && !started;
            if (connectionText != null)
            {
                connectionText.text = transport == null
                    ? "接続情報を取得できません"
                    : transport.IsOnline
                        ? "同じROOMの端末を確認しています"
                        : transport.ConnectionMessage;
            }

            if (role == CraftLiveRole.WorkbenchPad)
            {
                if (startCollider != null)
                {
                    startCollider.enabled = canStart;
                }

                if (startRenderer != null)
                {
                    CraftLiveForgeUITheme.ApplyForgeSurface(
                        startRenderer,
                        canStart
                            ? CraftLiveForgeUITheme.Ember
                            : new Color(0.24f, 0.19f, 0.17f));
                }

                if (startText != null)
                {
                    startText.text = allConnected
                        ? "ゲームを最初からスタート"
                        : "未接続ありでもゲームをスタート";
                }
            }
        }

        private void StartGroup()
        {
            if (role != CraftLiveRole.WorkbenchPad ||
                session == null)
            {
                return;
            }

            started = true;
            RestoreUnderlyingPresentation();
            session.RestartGroupFromConnectionSetup();
            Refresh();
        }

        private GameObject CreatePart(
            string name,
            Vector3 position,
            Vector3 scale,
            Color color,
            bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(generatedScreen.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (!keepCollider)
            {
                DestroySafely(part.GetComponent<Collider>());
            }

            CraftLiveForgeUITheme.ApplyForgeSurface(
                part.GetComponent<Renderer>(),
                color,
                0.02f,
                0.16f,
                0.25f);
            return part;
        }

        private TextMesh CreateText(
            string name,
            string value,
            Vector3 position,
            float size,
            Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(generatedScreen.transform, false);
            textObject.transform.localPosition = position;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(text, size, color);
            return text;
        }

        private static string RoleLabel(CraftLiveRole targetRole)
        {
            switch (targetRole)
            {
                case CraftLiveRole.MaterialPad:
                    return "Pad 1 / 素材";
                case CraftLiveRole.WorkbenchPad:
                    return "Pad 2 / 合成";
                case CraftLiveRole.QrPad:
                    return "Pad 3 / QR・状態";
                case CraftLiveRole.HologramPad:
                    return "Pad 4 / 完成表示";
                default:
                    return "Pad";
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
