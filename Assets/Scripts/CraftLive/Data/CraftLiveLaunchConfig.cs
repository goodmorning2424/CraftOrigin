using UnityEngine;

namespace CraftOrigin.CraftLive
{
    [CreateAssetMenu(
        menuName = "CraftOrigin/Craft-live/Launch Config",
        fileName = "CraftLiveLaunchConfig")]
    public sealed class CraftLiveLaunchConfig : ScriptableObject
    {
        [Header("Editor Preview")]
        [SerializeField] private CraftLiveRole editorRole =
            CraftLiveRole.MaterialPad;
        [SerializeField] private string editorRoomId = "001";

        [Header("Pad Scenes")]
        [SerializeField] private string pad1SceneName =
            "Pad1_MaterialGallery";
        [SerializeField] private string pad2SceneName =
            "Pad2_Workbench";
        [SerializeField] private string pad3SceneName =
            "Pad3_StatusQr";
        [SerializeField] private string pad4SceneName =
            "Pad4_Hologram";

        [Header("Firebase")]
        [SerializeField] private bool useFirebaseInEditor;
        [SerializeField] private bool useFirebaseInWebGl = true;
        [SerializeField] private string firebaseDatabaseUrl =
            "https://craft-live-default-rtdb.firebaseio.com";
        [SerializeField, Min(0.2f)] private float pollIntervalSeconds = 0.5f;
        [SerializeField, Min(2f)] private float requestTimeoutSeconds = 10f;
        [SerializeField, Min(0.25f)] private float initialRetryDelaySeconds =
            0.75f;
        [SerializeField, Min(1f)] private float maximumRetryDelaySeconds =
            8f;
        [SerializeField] private bool cachePendingState = true;

        public CraftLiveRole EditorRole => editorRole;
        public string EditorRoomId => string.IsNullOrWhiteSpace(editorRoomId)
            ? "001"
            : editorRoomId.Trim();
        public bool UseFirebaseInEditor => useFirebaseInEditor;
        public bool UseFirebaseInWebGl => useFirebaseInWebGl;
        public string FirebaseDatabaseUrl => firebaseDatabaseUrl;
        public float PollIntervalSeconds =>
            Mathf.Max(0.2f, pollIntervalSeconds);
        public float RequestTimeoutSeconds =>
            Mathf.Max(2f, requestTimeoutSeconds);
        public float InitialRetryDelaySeconds =>
            Mathf.Max(0.25f, initialRetryDelaySeconds);
        public float MaximumRetryDelaySeconds =>
            Mathf.Max(
                InitialRetryDelaySeconds,
                maximumRetryDelaySeconds);
        public bool CachePendingState => cachePendingState;

        public string GetSceneName(CraftLiveRole role)
        {
            switch (role)
            {
                case CraftLiveRole.MaterialPad:
                    return pad1SceneName;
                case CraftLiveRole.WorkbenchPad:
                    return pad2SceneName;
                case CraftLiveRole.QrPad:
                    return pad3SceneName;
                case CraftLiveRole.HologramPad:
                    return pad4SceneName;
                default:
                    return string.Empty;
            }
        }

        private void OnValidate()
        {
            editorRoomId = string.IsNullOrWhiteSpace(editorRoomId)
                ? "001"
                : editorRoomId.Trim();
            pollIntervalSeconds = Mathf.Max(0.2f, pollIntervalSeconds);
            requestTimeoutSeconds = Mathf.Max(2f, requestTimeoutSeconds);
            initialRetryDelaySeconds = Mathf.Max(
                0.25f,
                initialRetryDelaySeconds);
            maximumRetryDelaySeconds = Mathf.Max(
                initialRetryDelaySeconds,
                maximumRetryDelaySeconds);
        }
    }
}
