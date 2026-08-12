using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad3Bindings : MonoBehaviour
    {
        [Header("Status Tubes")]
        [SerializeField] private Transform attackTubeRoot;
        [SerializeField] private Transform defenseTubeRoot;
        [SerializeField] private Transform evasionTubeRoot;

        [Header("QR Area")]
        [SerializeField] private Transform qrReadButtonRoot;
        [SerializeField] private Transform qrFeedbackRoot;

        [Header("User Interface")]
        [SerializeField] private Transform uiRoot;

        [Header("Physical Layout References")]
        [SerializeField] private Camera referenceCamera;
        [SerializeField] private Renderer woodPanel;
        [Tooltip("二重板を含む、カメラ側表面の検出対象です。")]
        [SerializeField] private Renderer[] woodPanelLayers =
            new Renderer[0];
        [SerializeField] private CraftLiveWoodCommentBoard noticeBoard;

        public Transform AttackTubeRoot => attackTubeRoot;
        public Transform DefenseTubeRoot => defenseTubeRoot;
        public Transform EvasionTubeRoot => evasionTubeRoot;
        public Transform QrReadButtonRoot => qrReadButtonRoot;
        public Transform QrFeedbackRoot => qrFeedbackRoot;
        public Transform UiRoot => uiRoot;
        public Camera ReferenceCamera => referenceCamera;
        public Renderer WoodPanel => woodPanel;
        public Renderer[] WoodPanelLayers => woodPanelLayers;
        public CraftLiveWoodCommentBoard NoticeBoard => noticeBoard;
    }
}
