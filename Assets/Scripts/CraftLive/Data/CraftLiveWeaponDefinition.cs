using UnityEngine;

namespace CraftOrigin.CraftLive
{
    [CreateAssetMenu(
        menuName = "CraftOrigin/Craft-live/Base Weapon",
        fileName = "CraftLiveWeapon")]
    public sealed class CraftLiveWeaponDefinition : ScriptableObject
    {
        [SerializeField] private string weaponId = "weapon_id";
        [SerializeField] private string displayName = "New Weapon";
        [SerializeField] private CraftLiveWeaponType weaponType;

        [Header("Base Stats")]
        [SerializeField] private CraftLiveStats baseStats;

        [Header("Presentation")]
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject workbenchPrefab;
        [SerializeField] private GameObject hologramPrefab;
        [SerializeField] private Vector3 previewScale = Vector3.one;

        public string WeaponId => weaponId;
        public string DisplayName => displayName;
        public CraftLiveWeaponType WeaponType => weaponType;
        public CraftLiveStats BaseStats => baseStats.Sanitize();
        public Sprite Icon => icon;
        public GameObject WorkbenchPrefab => workbenchPrefab;
        public GameObject HologramPrefab => hologramPrefab != null ? hologramPrefab : workbenchPrefab;
        public Vector3 PreviewScale => previewScale;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                weaponId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = weaponId;
            }

            baseStats = baseStats.Sanitize();
        }
    }
}
