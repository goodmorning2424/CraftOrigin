using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveSlotAction : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLiveSlotId slot;

        public void SelectPlacement()
        {
            session?.ChoosePlacementSlot(slot);
        }

        public void ClearPlacementChoice()
        {
            session?.ClearPlacementChoice();
        }

        public void RemoveMaterial()
        {
            session?.RemoveSlot(slot);
        }
    }
}
