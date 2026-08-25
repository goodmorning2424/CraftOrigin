using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraftOrigin.CraftLive
{
    [DefaultExecutionOrder(-250)]
    public sealed class CraftLiveBootstrap : MonoBehaviour
    {
#if UNITY_EDITOR
        public const string IntegratedEditorTestSessionKey =
            "CraftLive.IntegratedEditorTestWorkflow";
#endif
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLiveRoomTransport transport;
        [SerializeField] private CraftLiveLaunchConfig launchConfig;
        [SerializeField] private Camera targetCamera;
        [Header("Editor Test Play")]
        [SerializeField] private bool showEditorPadSwitcher = true;
        [SerializeField]
        [Tooltip("Toolsメニュー起動時にPad1～3の工程を同じ状態で連携させます。直接Bootstrapを再生した場合はオフのままです。")]
        private bool integratedEditorTestWorkflow;

        public CraftLiveRole ResolvedRole { get; private set; }
        public string ResolvedRoomId { get; private set; }
        public string LoadedPadSceneName { get; private set; }
        public bool IsSwitchingPad { get; private set; }
        public bool IsIntegratedEditorTestWorkflow =>
            integratedEditorTestWorkflow;
        private bool showConnectionSetup;

        private void Awake()
        {
#if UNITY_EDITOR
            integratedEditorTestWorkflow =
                UnityEditor.SessionState.GetBool(
                    IntegratedEditorTestSessionKey,
                    false);
            UnityEditor.SessionState.EraseBool(
                IntegratedEditorTestSessionKey);
#endif
            if (session == null)
            {
                session = GetComponent<CraftLiveSession>();
            }

            if (transport == null)
            {
                transport = GetComponent<CraftLiveRoomTransport>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            CraftLiveRole fallbackRole = launchConfig != null
                ? launchConfig.EditorRole
                : CraftLiveRole.MaterialPad;
            string fallbackRoom = launchConfig != null
                ? launchConfig.EditorRoomId
                : "001";

            ResolvedRole = CraftLiveLaunchQuery.ResolveRole(fallbackRole);
            ResolvedRoomId = CraftLiveLaunchQuery.Read(
                "room",
                fallbackRoom);
            // Room lifecycle is no longer owned by Pad 1. A fresh group is
            // created only by the authoritative Pad 2 setup button.
            showConnectionSetup =
                CraftLiveLaunchQuery.ShouldShowConnectionSetup();
            session?.Configure(ResolvedRoomId, ResolvedRole);

            if (transport != null && launchConfig != null)
            {
                transport.Configure(
                    ShouldUseFirebase(),
                    launchConfig.FirebaseDatabaseUrl,
                    launchConfig.PollIntervalSeconds,
                    launchConfig.RequestTimeoutSeconds,
                    launchConfig.InitialRetryDelaySeconds,
                    launchConfig.MaximumRetryDelaySeconds,
                    launchConfig.CachePendingState);
            }
        }

        private IEnumerator Start()
        {
            if (transport != null && !transport.enabled)
            {
                transport.enabled = true;
            }

            if (launchConfig == null)
            {
                Debug.LogError(
                    "CraftLiveBootstrap: Launch Config is missing.",
                    this);
                yield break;
            }

            yield return LoadPadScene(ResolvedRole, false);
        }

        public void SwitchPad(CraftLiveRole role)
        {
            if (launchConfig == null)
            {
                Debug.LogError(
                    "CraftLiveBootstrap: Launch Config is missing.",
                    this);
                return;
            }

            if (!IsTestablePadRole(role) ||
                IsSwitchingPad ||
                (role == ResolvedRole &&
                 !string.IsNullOrEmpty(LoadedPadSceneName)))
            {
                return;
            }

            StartCoroutine(SwitchPadRoutine(role));
        }

        public void SetIntegratedEditorTestWorkflow(bool value)
        {
            integratedEditorTestWorkflow = value;
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(LoadedPadSceneName))
            {
                Scene loadedScene =
                    SceneManager.GetSceneByName(LoadedPadSceneName);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    ConfigureEditorTestWorkflow(loadedScene);
                }
            }
#endif
        }

        public void SwitchToPad1()
        {
            SwitchPad(CraftLiveRole.MaterialPad);
        }

        public void SwitchToPad2()
        {
            SwitchPad(CraftLiveRole.WorkbenchPad);
        }

        public void SwitchToPad3()
        {
            SwitchPad(CraftLiveRole.QrPad);
        }

        public void SwitchToPad4()
        {
            SwitchPad(CraftLiveRole.HologramPad);
        }

        public static bool IsTestablePadRole(CraftLiveRole role)
        {
            return role == CraftLiveRole.MaterialPad ||
                   role == CraftLiveRole.WorkbenchPad ||
                   role == CraftLiveRole.QrPad ||
                   role == CraftLiveRole.HologramPad;
        }

        private IEnumerator SwitchPadRoutine(CraftLiveRole role)
        {
            IsSwitchingPad = true;
            yield return LoadPadScene(role, true);
            IsSwitchingPad = false;
        }

        private IEnumerator LoadPadScene(
            CraftLiveRole role,
            bool unloadPrevious)
        {
            if (launchConfig == null)
            {
                Debug.LogError(
                    "CraftLiveBootstrap: Launch Config is missing.",
                    this);
                yield break;
            }

            string sceneName = launchConfig.GetSceneName(role);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError(
                    $"CraftLiveBootstrap: No Scene for {role}.",
                    this);
                yield break;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"CraftLiveBootstrap: Scene '{sceneName}' is not " +
                    "available in Build Settings.",
                    this);
                yield break;
            }

            Scene padScene = SceneManager.GetSceneByName(sceneName);
            if (!padScene.isLoaded)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    sceneName,
                    LoadSceneMode.Additive);
                if (load == null)
                {
                    Debug.LogError(
                        $"CraftLiveBootstrap: Failed to load {sceneName}.",
                        this);
                    yield break;
                }

                yield return load;
                padScene = SceneManager.GetSceneByName(sceneName);
            }

            if (!padScene.IsValid() || !padScene.isLoaded)
            {
                Debug.LogError(
                    $"CraftLiveBootstrap: Scene '{sceneName}' did not load.",
                    this);
                yield break;
            }

            CraftLivePadSceneRoot padRoot = FindPadRoot(padScene);
            if (padRoot == null)
            {
                Debug.LogError(
                    $"CraftLiveBootstrap: {sceneName} has no Pad Scene Root.",
                    this);
                yield break;
            }

            if (padRoot.Role != role)
            {
                Debug.LogError(
                    $"CraftLiveBootstrap: Scene role {padRoot.Role} does " +
                    $"not match requested role {role}.",
                    padRoot);
                yield break;
            }

            string previousSceneName = LoadedPadSceneName;
            session?.Configure(ResolvedRoomId, role);
            PrepareStandalonePad(role);
            DisablePadSceneAudioListeners(padScene);
            ConfigureEditorTestWorkflow(padScene);
            padRoot.ApplyCamera(targetCamera);
            ConfigureConnectionSetup(padRoot, role);
            SceneManager.SetActiveScene(padScene);
            ResolvedRole = role;
            LoadedPadSceneName = sceneName;

            if (!unloadPrevious ||
                string.IsNullOrWhiteSpace(previousSceneName) ||
                previousSceneName == sceneName)
            {
                yield break;
            }

            Scene previousScene =
                SceneManager.GetSceneByName(previousSceneName);
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                AsyncOperation unload =
                    SceneManager.UnloadSceneAsync(previousScene);
                if (unload != null)
                {
                    yield return unload;
                }
            }
        }

        private void ConfigureConnectionSetup(
            CraftLivePadSceneRoot padRoot,
            CraftLiveRole role)
        {
            if (!showConnectionSetup || padRoot == null)
            {
                return;
            }

            CraftLiveConnectionSetupScreen setup =
                padRoot.GetComponent<CraftLiveConnectionSetupScreen>();
            if (setup == null)
            {
                setup = padRoot.gameObject.AddComponent<
                    CraftLiveConnectionSetupScreen>();
            }

            setup.Configure(session, transport, role, targetCamera);
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showEditorPadSwitcher || !Application.isPlaying)
            {
                return;
            }

            GUILayout.BeginArea(
                new Rect(12f, 12f, 158f, 222f),
                GUI.skin.box);
            GUILayout.Label("PAD TEST", GUI.skin.label);
            GUILayout.Label(
                IsSwitchingPad
                    ? "切替中…"
                    : $"表示中: {GetPadLabel(ResolvedRole)}",
                GUI.skin.label);
            DrawPadSwitchButton("1  素材ギャラリー", CraftLiveRole.MaterialPad);
            DrawPadSwitchButton("2  武器クラフト", CraftLiveRole.WorkbenchPad);
            DrawPadSwitchButton("3  ステータス / QR", CraftLiveRole.QrPad);
            DrawPadSwitchButton("4  完成ホログラム", CraftLiveRole.HologramPad);
            GUILayout.EndArea();
        }

        private void DrawPadSwitchButton(
            string label,
            CraftLiveRole role)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled &&
                          !IsSwitchingPad &&
                          role != ResolvedRole;
            if (GUILayout.Button(label, GUILayout.Height(32f)))
            {
                SwitchPad(role);
            }

            GUI.enabled = previousEnabled;
        }

        private static string GetPadLabel(CraftLiveRole role)
        {
            switch (role)
            {
                case CraftLiveRole.MaterialPad:
                    return "Pad 1";
                case CraftLiveRole.WorkbenchPad:
                    return "Pad 2";
                case CraftLiveRole.QrPad:
                    return "Pad 3";
                case CraftLiveRole.HologramPad:
                    return "Pad 4";
                default:
                    return "-";
            }
        }
#endif

        private bool ShouldUseFirebase()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return launchConfig.UseFirebaseInWebGl;
#else
            return launchConfig.UseFirebaseInEditor;
#endif
        }

        private static CraftLivePadSceneRoot FindPadRoot(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                CraftLivePadSceneRoot result =
                    rootObject.GetComponentInChildren<
                        CraftLivePadSceneRoot>(true);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void DisablePadSceneAudioListeners(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                AudioListener[] listeners =
                    rootObject.GetComponentsInChildren<AudioListener>(true);
                foreach (AudioListener listener in listeners)
                {
                    listener.enabled = false;
                }
            }
        }

        private void ConfigureEditorTestWorkflow(Scene scene)
        {
#if UNITY_EDITOR
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                CraftLivePad1MaterialPreview[] previews =
                    rootObject.GetComponentsInChildren<
                        CraftLivePad1MaterialPreview>(true);
                foreach (CraftLivePad1MaterialPreview preview in previews)
                {
                    preview.SetPlayTestTransferWithoutPlacement(
                        !integratedEditorTestWorkflow);
                }

                CraftLivePad1TransferController[] launchers =
                    rootObject.GetComponentsInChildren<
                        CraftLivePad1TransferController>(true);
                foreach (CraftLivePad1TransferController launcher in launchers)
                {
                    launcher.SetStandaloneResetEnabled(
                        !integratedEditorTestWorkflow);
                }

                CraftLivePad2TransferReceiver[] receivers =
                    rootObject.GetComponentsInChildren<
                        CraftLivePad2TransferReceiver>(true);
                foreach (CraftLivePad2TransferReceiver receiver in receivers)
                {
                    receiver.SetLocalAutoStartEnabled(
                        !integratedEditorTestWorkflow);
                }
            }
#endif
        }

        private void PrepareStandalonePad(CraftLiveRole role)
        {
#if UNITY_EDITOR
            if (integratedEditorTestWorkflow ||
                role != CraftLiveRole.WorkbenchPad ||
                session == null ||
                session.State == null ||
                session.Catalog == null)
            {
                return;
            }

            CraftLiveRoomState state = session.State;
            if (state.placement.status != CraftLivePlacementStatus.Idle ||
                (state.transferQueue != null &&
                 state.transferQueue.Count > 0) ||
                HasAnyPlacedMaterial(state))
            {
                return;
            }

            if (!state.weaponSelectionConfirmed)
            {
                CraftLiveWeaponDefinition weapon =
                    session.Catalog.FirstWeapon();
                if (weapon != null)
                {
                    session.ConfirmWeapon(weapon);
                    state = session.State;
                }
            }

            CraftLiveSlotId[] slots =
            {
                CraftLiveSlotId.Top,
                CraftLiveSlotId.Left,
                CraftLiveSlotId.Right,
                CraftLiveSlotId.Bottom,
                CraftLiveSlotId.Skill,
                CraftLiveSlotId.Attribute
            };
            foreach (CraftLiveMaterialDefinition material in
                     session.Catalog.Materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (!session.IsMaterialUnlocked(material))
                {
                    session.UnlockMaterialId(material.MaterialId);
                }

                foreach (CraftLiveSlotId slot in slots)
                {
                    if (!material.CanUseIn(slot) ||
                        !session.State.CanReserveSlot(slot))
                    {
                        continue;
                    }

                    session.SelectMaterial(material);
                    session.ChoosePlacementSlot(slot);
                    session.ConfirmPlacement();
                    return;
                }
            }
#endif
        }

        private static bool HasAnyPlacedMaterial(
            CraftLiveRoomState state)
        {
            if (state == null || state.slots == null)
            {
                return false;
            }

            foreach (CraftLiveSlotId slot in
                     (CraftLiveSlotId[])Enum.GetValues(
                         typeof(CraftLiveSlotId)))
            {
                if (!string.IsNullOrWhiteSpace(state.slots.Get(slot)))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class CraftLiveLaunchQuery
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string CraftLiveGetQueryParameter(string key);
#endif

        public static CraftLiveRole ResolveRole(CraftLiveRole fallback)
        {
            CraftLiveRole role = ParseRole(Read("screen", string.Empty));
            if (role == CraftLiveRole.Auto)
            {
                role = ParseRole(Read("pad", string.Empty));
            }

            return role == CraftLiveRole.Auto ? fallback : role;
        }

        public static CraftLiveRole ParseRole(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "1":
                case "items":
                case "materials":
                case "pad1":
                    return CraftLiveRole.MaterialPad;
                case "2":
                case "craft":
                case "workbench":
                case "pad2":
                    return CraftLiveRole.WorkbenchPad;
                case "3":
                case "status":
                case "qr":
                case "pad3":
                    return CraftLiveRole.QrPad;
                case "4":
                case "hologram":
                case "pad4":
                    return CraftLiveRole.HologramPad;
                default:
                    return CraftLiveRole.Auto;
            }
        }

        public static bool ShouldResetRoomOnLaunch(
            CraftLiveRole role,
            string value)
        {
            if (role != CraftLiveRole.MaterialPad)
            {
                return false;
            }

            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "0":
                case "false":
                case "off":
                case "no":
                    return false;
                default:
                    return true;
            }
        }

        public static bool ShouldShowConnectionSetup()
        {
            switch (Read("setup", "0").Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "on":
                case "yes":
                    return true;
                default:
                    return false;
            }
        }

        public static string Read(string key, string fallback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                string value = CraftLiveGetQueryParameter(key);
                return string.IsNullOrWhiteSpace(value)
                    ? fallback
                    : value.Trim();
            }
            catch (Exception)
            {
                return fallback;
            }
#else
            return fallback;
#endif
        }
    }
}
