using UnityEngine;

namespace CraftOrigin.CraftLive
{
    [CreateAssetMenu(
        menuName = "CraftOrigin/Craft-live/Material",
        fileName = "CraftLiveMaterial")]
    public sealed class CraftLiveMaterialDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string materialId = "material_id";
        [SerializeField] private string displayName = "New Material";
        [SerializeField, TextArea] private string description;
        [SerializeField] private CraftLiveMaterialCategory category;
        [SerializeField] private bool requiresQrUnlock;

        [Header("Presentation")]
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject worldPrefab;
        [SerializeField, Range(-180f, 180f)]
        [Tooltip("Pad2上で素材の上下方向を補正する回転角度です。")]
        private float pad2PreviewRollDegrees;
        [SerializeField]
        [Tooltip("Pad1で手前に表示するモデルの素材別位置補正です。")]
        private Vector3 pad1PreviewOffset = Vector3.zero;
        [SerializeField, Range(0.05f, 3f)]
        [Tooltip("Pad1で手前に表示するモデルの素材別サイズ倍率です。")]
        private float pad1PreviewScale = 1f;
        [SerializeField]
        [Tooltip("Pad1の説明ホログラムに使用する素材別カラーです。")]
        private Color pad1HologramColor =
            new Color(0.08f, 0.72f, 0.82f, 1f);
        [SerializeField] private GameObject transferTicketPrefab;
        [SerializeField] private Color effectColor = Color.white;
        [SerializeField] private GameObject placementEffectPrefab;
        [SerializeField] private CraftLiveMaterialForm materialForm;
        [SerializeField] private AudioClip landingAudioClip;
        [SerializeField, TextArea] private string abilitySummary;
        [SerializeField, TextArea] private string usageSummary;

        [Header("Pad4 Attribute Particle")]
        [Tooltip("Pad4で完成武器と一緒に表示する属性パーティクルPrefabです。未設定時は属性色の簡易パーティクルを表示します。")]
        [SerializeField] private GameObject pad4ParticlePrefab;
        [SerializeField] private Vector3 pad4ParticleLocalPosition;
        [SerializeField] private Vector3 pad4ParticleLocalEulerAngles;
        [SerializeField] private Vector3 pad4ParticleLocalScale = Vector3.one;
        [Tooltip("Prefab内のParticleSystemをこの属性のeffectColorで着色します。")]
        [SerializeField] private bool tintPad4Particles = true;

        [Header("Base Stat Material")]
        [SerializeField] private CraftLiveStats statModifiers;

        [Header("Attribute Material")]
        [SerializeField] private string attributeId;
        [SerializeField] private string attributeDisplayName;
        [SerializeField] private CraftLiveElementEffect elementEffect;

        [Header("Unique Skill Material")]
        [SerializeField] private string skillId;
        [SerializeField] private string skillDisplayName;
        [SerializeField, TextArea] private string skillDescription;
        [SerializeField] private CraftLiveSkillEffect skillEffect;

        // V2 fields are retained so existing assets can migrate without data loss.
        [SerializeField, HideInInspector] private CraftLiveStatType affectedStat;
        [SerializeField, HideInInspector, Min(0f)] private float topBonus;
        [SerializeField, HideInInspector, Min(0f)] private float rightBonus;
        [SerializeField, HideInInspector, Min(0f)] private float leftBonus;
        [SerializeField, HideInInspector, Min(0f)] private float bottomBonus;

        public string MaterialId => materialId;
        public string DisplayName => displayName;
        public string Description => description;
        public CraftLiveMaterialCategory Category => category;
        public bool RequiresQrUnlock => requiresQrUnlock;
        public Sprite Icon => icon;
        public GameObject WorldPrefab => worldPrefab;
        public float Pad2PreviewRollDegrees =>
            pad2PreviewRollDegrees;
        public Vector3 Pad1PreviewOffset => pad1PreviewOffset;
        public float Pad1PreviewScale =>
            pad1PreviewScale > 0f ? pad1PreviewScale : 1f;
        public Color Pad1HologramColor => pad1HologramColor;
        public GameObject TransferTicketPrefab => transferTicketPrefab;
        public Color EffectColor => effectColor;
        public GameObject PlacementEffectPrefab => placementEffectPrefab;
        public CraftLiveMaterialForm MaterialForm => materialForm;
        public AudioClip LandingAudioClip => landingAudioClip;
        public string AbilitySummary => abilitySummary;
        public string UsageSummary => usageSummary;
        public GameObject Pad4ParticlePrefab => pad4ParticlePrefab;
        public Vector3 Pad4ParticleLocalPosition =>
            pad4ParticleLocalPosition;
        public Quaternion Pad4ParticleLocalRotation =>
            Quaternion.Euler(pad4ParticleLocalEulerAngles);
        public Vector3 Pad4ParticleLocalScale => pad4ParticleLocalScale;
        public bool TintPad4Particles => tintPad4Particles;
        public string AttributeId => attributeId;
        public string AttributeDisplayName => attributeDisplayName;
        public string SkillId => skillId;
        public string SkillDisplayName => skillDisplayName;
        public string SkillDescription => skillDescription;
        public CraftLiveStatType AffectedStat => affectedStat;
        public CraftLiveStats StatModifiers => ResolveStatModifiers();
        public CraftLiveElementEffect ElementEffect =>
            ResolveElementEffect();
        public CraftLiveSkillEffect SkillEffect => ResolveSkillEffect();

        public bool CanUseIn(CraftLiveSlotId slot)
        {
            return category == CraftLiveSlot.RequiredCategory(slot);
        }

        // Kept for V2 callers. V3 bonuses no longer depend on slot position.
        public float GetUpgradeBonus(CraftLiveSlotId slot)
        {
            if (category != CraftLiveMaterialCategory.Upgrade ||
                !CraftLiveSlot.IsBaseStatSlot(slot))
            {
                return 0f;
            }

            return StatModifiers.Get(affectedStat);
        }

        private CraftLiveStats ResolveStatModifiers()
        {
            CraftLiveStats resolved = statModifiers.Sanitize();
            if (category != CraftLiveMaterialCategory.Upgrade ||
                resolved.HasAnyValue)
            {
                return resolved;
            }

            float legacyMaximum = Mathf.Max(
                Mathf.Max(topBonus, rightBonus),
                Mathf.Max(leftBonus, bottomBonus));
            resolved.Add(affectedStat, legacyMaximum);
            return resolved;
        }

        private CraftLiveElementEffect ResolveElementEffect()
        {
            CraftLiveElementEffect resolved = elementEffect.Sanitize();
            if (resolved.type != CraftLiveElementType.None)
            {
                return resolved;
            }

            string key = (attributeId ?? string.Empty).ToLowerInvariant();
            if (key.Contains("fire"))
            {
                resolved.type = CraftLiveElementType.Fire;
            }
            else if (key.Contains("freeze") ||
                     key.Contains("ice") ||
                     key.Contains("water"))
            {
                resolved.type = CraftLiveElementType.Freeze;
            }
            else if (key.Contains("thunder") ||
                     key.Contains("lightning"))
            {
                resolved.type = CraftLiveElementType.Lightning;
            }

            return resolved;
        }

        private CraftLiveSkillEffect ResolveSkillEffect()
        {
            CraftLiveSkillEffect resolved = skillEffect.Sanitize();
            if (resolved.type != CraftLiveSkillType.None)
            {
                return resolved;
            }

            string key = (skillId ?? string.Empty).ToLowerInvariant();
            if (key.Contains("luck") || key.Contains("critical"))
            {
                resolved.type = CraftLiveSkillType.Luck;
            }
            else if (key.Contains("double") || key.Contains("multi"))
            {
                resolved.type = CraftLiveSkillType.DoubleStrike;
            }
            else if (key.Contains("heal") || key.Contains("regeneration"))
            {
                resolved.type = CraftLiveSkillType.AutoHeal;
            }
            else if (key.Contains("lifeorb") || key.Contains("life_orb"))
            {
                resolved.type = CraftLiveSkillType.LifeOrb;
            }

            return resolved;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(materialId))
            {
                materialId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = materialId;
            }

            statModifiers = statModifiers.Sanitize();
            elementEffect = elementEffect.Sanitize();
            skillEffect = skillEffect.Sanitize();
            pad1PreviewScale = Mathf.Clamp(
                pad1PreviewScale,
                0.05f,
                3f);
            pad4ParticleLocalScale.x = Mathf.Max(
                0.001f,
                pad4ParticleLocalScale.x);
            pad4ParticleLocalScale.y = Mathf.Max(
                0.001f,
                pad4ParticleLocalScale.y);
            pad4ParticleLocalScale.z = Mathf.Max(
                0.001f,
                pad4ParticleLocalScale.z);
            topBonus = Mathf.Max(0f, topBonus);
            rightBonus = Mathf.Max(0f, rightBonus);
            leftBonus = Mathf.Max(0f, leftBonus);
            bottomBonus = Mathf.Max(0f, bottomBonus);
        }
    }
}
