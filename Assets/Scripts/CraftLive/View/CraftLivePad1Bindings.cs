using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad1Bindings : MonoBehaviour
    {
        [Header("Three Gallery Columns")]
        [SerializeField] private Transform powerUpWall;
        [SerializeField] private Transform skillWall;
        [SerializeField] private Transform typeWall;

        [Header("Selection Preview")]
        [SerializeField] private Transform materialPreviewRoot;
        [SerializeField] private Transform hologramInfoRoot;

        [Header("Transfer")]
        [SerializeField] private Transform transferQueueRoot;
        [SerializeField] private Transform springLauncherRoot;
        [SerializeField] private Transform railCameraAnchor;

        [Header("User Interface")]
        [SerializeField] private Transform uiRoot;

        public Transform PowerUpWall => powerUpWall;
        public Transform SkillWall => skillWall;
        public Transform TypeWall => typeWall;
        public Transform MaterialPreviewRoot => materialPreviewRoot;
        public Transform HologramInfoRoot => hologramInfoRoot;
        public Transform TransferQueueRoot => transferQueueRoot;
        public Transform SpringLauncherRoot => springLauncherRoot;
        public Transform RailCameraAnchor => railCameraAnchor;
        public Transform UiRoot => uiRoot;
    }
}
