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
        [Tooltip("通常の武器選択一覧に表示するかどうかです。")]
        [SerializeField] private bool visibleInSelection = true;
        [Tooltip("完成表示でモデルを表示しない武器です。")]
        [SerializeField] private bool hidePresentationModel;
        [Tooltip("インポート材質が不安定なモデルへ実行時に明示適用する材質です。")]
        [SerializeField] private Material presentationMaterialOverride;
        [Tooltip("作業台・完成表示で使用するモデルのXYZ倍率です。")]
        [SerializeField] private Vector3 previewScale = Vector3.one;
        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("武器選択カードと中央プレビューだけに使用する、武器ごとの大きさ倍率です。")]
        private float selectionPreviewScale = 1f;

        public string WeaponId => weaponId;
        public string DisplayName => displayName;
        public CraftLiveWeaponType WeaponType => weaponType;
        public CraftLiveStats BaseStats => baseStats.Sanitize();
        public Sprite Icon => icon;
        public GameObject WorkbenchPrefab => workbenchPrefab;
        public GameObject HologramPrefab => hologramPrefab != null ? hologramPrefab : workbenchPrefab;
        public bool VisibleInSelection => visibleInSelection;
        public bool HidePresentationModel => hidePresentationModel;
        public Material PresentationMaterialOverride =>
            presentationMaterialOverride;
        public Vector3 PreviewScale => previewScale;
        public float SelectionPreviewScale =>
            selectionPreviewScale > 0f ? selectionPreviewScale : 1f;

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
            selectionPreviewScale = Mathf.Clamp(
                selectionPreviewScale,
                0.1f,
                3f);
        }
    }
}
