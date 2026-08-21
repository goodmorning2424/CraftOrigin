using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveCommandActions : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;

        public void ConfirmPlacement()
        {
            session?.ConfirmPlacement();
        }

        public void CancelPlacement()
        {
            session?.CancelPlacement();
        }

        public void ContinueAfterPlacement()
        {
            session?.ContinueAfterPlacement();
        }

        public void StartSynthesis()
        {
            session?.StartSynthesis();
        }

        public void CompleteSynthesis()
        {
            session?.CompleteSynthesis();
        }

        public void ResetRoom()
        {
            session?.ResetRoomForNextGroup();
        }
    }
}
