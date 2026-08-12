using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad4Bindings : MonoBehaviour
    {
        [SerializeField] private Transform weaponDisplayRoot;
        [SerializeField] private Transform effectRoot;
        [SerializeField] private Transform uiRoot;
        [SerializeField] private CraftLivePad4Calibration calibration;

        public Transform WeaponDisplayRoot => weaponDisplayRoot;
        public Transform EffectRoot => effectRoot;
        public Transform UiRoot => uiRoot;
        public CraftLivePad4Calibration Calibration => calibration;
    }
}
