using UnityEngine;

namespace CraftOrigin.CraftLive
{
    [CreateAssetMenu(
        menuName = "CraftOrigin/Craft-live/Rules",
        fileName = "CraftLiveRules")]
    public sealed class CraftLiveRules : ScriptableObject
    {
        public const float DefaultSessionDurationSeconds = 330f;

        [Header("Session")]
        [SerializeField, Min(1f)] private float sessionDurationSeconds =
            DefaultSessionDurationSeconds;

        [Header("Required Materials")]
        [SerializeField] private bool requireAttributeSlot = true;
        [SerializeField] private bool requireSkillSlot = true;
        [SerializeField] private bool requireAllFourBaseSlots;

        [Header("Mixing")]
        [SerializeField, Min(0.5f)] private float mixingDurationSeconds = 5f;
        [SerializeField, Min(0.1f)] private float powerPerRadian = 7.5f;
        [SerializeField, Min(1)] private int requiredHammerPasses = 6;
        [SerializeField, Min(20f)] private float hammerStrokePixels = 120f;

        [Header("Results")]
        [SerializeField, Min(1)] private int maximumCompletedWeapons = 12;
        [SerializeField] private string weaponCodePrefix = "CL";

        [Header("Rank Thresholds")]
        [SerializeField, Min(0f)] private float successThreshold = 31f;
        [SerializeField, Min(0f)] private float greatSuccessThreshold = 61f;
        [SerializeField, Min(0f)] private float superSuccessThreshold = 91f;

        [Header("Rank Stat Bonuses")]
        [SerializeField, Min(0f)] private float successBonus = 5f;
        [SerializeField, Min(0f)] private float greatSuccessBonus = 10f;
        [SerializeField, Min(0f)] private float superSuccessBonus = 15f;
        [SerializeField, Min(1f)] private float maximumStat = 1000f;

        public float SessionDurationSeconds =>
            Mathf.Max(1f, sessionDurationSeconds);
        public bool RequireAttributeSlot => requireAttributeSlot;
        public bool RequireSkillSlot => requireSkillSlot;
        public bool RequireAllFourBaseSlots => requireAllFourBaseSlots;
        public float MixingDurationSeconds =>
            Mathf.Max(0.5f, mixingDurationSeconds);
        public float PowerPerRadian => Mathf.Max(0.1f, powerPerRadian);
        public int RequiredHammerPasses =>
            Mathf.Max(1, requiredHammerPasses);
        public float HammerStrokePixels =>
            Mathf.Max(20f, hammerStrokePixels);
        public int MaximumCompletedWeapons =>
            Mathf.Max(1, maximumCompletedWeapons);
        public string WeaponCodePrefix =>
            string.IsNullOrWhiteSpace(weaponCodePrefix)
                ? "CL"
                : weaponCodePrefix.Trim().ToUpperInvariant();
        public float MaximumStat => Mathf.Max(1f, maximumStat);

        public CraftLiveRank EvaluateRank(float power)
        {
            power = Mathf.Clamp(power, 0f, 100f);
            if (power >= superSuccessThreshold)
            {
                return new CraftLiveRank("超成功", superSuccessBonus);
            }

            if (power >= greatSuccessThreshold)
            {
                return new CraftLiveRank("大成功", greatSuccessBonus);
            }

            if (power >= successThreshold)
            {
                return new CraftLiveRank("成功", successBonus);
            }

            return new CraftLiveRank("通常成功", 0f);
        }

        private void OnValidate()
        {
            sessionDurationSeconds = Mathf.Max(1f, sessionDurationSeconds);
            mixingDurationSeconds = Mathf.Max(0.5f, mixingDurationSeconds);
            powerPerRadian = Mathf.Max(0.1f, powerPerRadian);
            requiredHammerPasses =
                Mathf.Max(1, requiredHammerPasses);
            hammerStrokePixels =
                Mathf.Max(20f, hammerStrokePixels);
            maximumCompletedWeapons =
                Mathf.Max(1, maximumCompletedWeapons);
            weaponCodePrefix =
                string.IsNullOrWhiteSpace(weaponCodePrefix)
                    ? "CL"
                    : weaponCodePrefix.Trim().ToUpperInvariant();
            successThreshold = Mathf.Clamp(successThreshold, 0f, 100f);
            greatSuccessThreshold = Mathf.Clamp(
                greatSuccessThreshold,
                successThreshold,
                100f);
            superSuccessThreshold = Mathf.Clamp(
                superSuccessThreshold,
                greatSuccessThreshold,
                100f);
            successBonus = Mathf.Max(0f, successBonus);
            greatSuccessBonus = Mathf.Max(successBonus, greatSuccessBonus);
            superSuccessBonus = Mathf.Max(
                greatSuccessBonus,
                superSuccessBonus);
            maximumStat = Mathf.Max(1f, maximumStat);
        }
    }

    public readonly struct CraftLiveRank
    {
        public string Name { get; }
        public float Bonus { get; }

        public CraftLiveRank(string name, float bonus)
        {
            Name = name;
            Bonus = Mathf.Max(0f, bonus);
        }
    }
}
