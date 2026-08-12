using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad2Bindings : MonoBehaviour
    {
        [Header("Weapon Selection")]
        [SerializeField] private Transform weaponCarouselRoot;

        [Header("Workbench Center")]
        [SerializeField] private Transform centerWeaponRoot;
        [SerializeField] private Transform hammerRoot;

        [Header("Base Material Slots")]
        [SerializeField] private Transform upperLeftSlot;
        [SerializeField] private Transform middleLeftSlot;
        [SerializeField] private Transform upperRightSlot;
        [SerializeField] private Transform middleRightSlot;

        [Header("Special Material Slots")]
        [SerializeField] private Transform lowerLeftSkillSlot;
        [SerializeField] private Transform lowerRightAttributeSlot;

        [Header("Arrival And Effects")]
        [SerializeField] private Transform transferArrivalRoot;
        [SerializeField] private Transform liquidFlowRoot;
        [SerializeField] private Transform resultHologramRoot;

        [Header("User Interface")]
        [SerializeField] private Transform uiRoot;

        public Transform WeaponCarouselRoot => weaponCarouselRoot;
        public Transform CenterWeaponRoot => centerWeaponRoot;
        public Transform HammerRoot => hammerRoot;
        public Transform UpperLeftSlot => upperLeftSlot;
        public Transform MiddleLeftSlot => middleLeftSlot;
        public Transform UpperRightSlot => upperRightSlot;
        public Transform MiddleRightSlot => middleRightSlot;
        public Transform LowerLeftSkillSlot => lowerLeftSkillSlot;
        public Transform LowerRightAttributeSlot =>
            lowerRightAttributeSlot;
        public Transform TransferArrivalRoot => transferArrivalRoot;
        public Transform LiquidFlowRoot => liquidFlowRoot;
        public Transform ResultHologramRoot => resultHologramRoot;
        public Transform UiRoot => uiRoot;
    }
}
