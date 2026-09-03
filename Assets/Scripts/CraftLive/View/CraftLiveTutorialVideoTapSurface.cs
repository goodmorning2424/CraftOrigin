using UnityEngine;
using UnityEngine.EventSystems;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveTutorialVideoTapSurface :
        MonoBehaviour,
        IPointerClickHandler
    {
        private CraftLivePad2ResultController controller;
        private bool interactable;

        public bool Interactable => interactable;

        public void Configure(
            CraftLivePad2ResultController targetController,
            bool canInteract)
        {
            controller = targetController;
            interactable = canInteract;
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (interactable)
            {
                controller?.StartTutorialFromTap();
            }
        }
    }
}
