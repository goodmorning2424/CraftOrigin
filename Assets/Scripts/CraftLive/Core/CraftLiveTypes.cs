using System;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public enum CraftLiveRole
    {
        Auto,
        MaterialPad,
        WorkbenchPad,
        QrPad,
        HologramPad
    }

    // Numeric values are part of the V2/V3 JSON and asset compatibility contract.
    public enum CraftLiveMaterialCategory
    {
        Attribute = 0,
        Skill = 1,
        Upgrade = 2
    }

    public enum CraftLiveMaterialForm
    {
        Generic,
        Ore,
        Gem,
        Charm,
        Spirit
    }

    // Keep these values stable. They match the six physical slots on Pad2.
    public enum CraftLiveSlotId
    {
        Attribute = 0,
        Skill = 1,
        Top = 2,
        Right = 3,
        Left = 4,
        Bottom = 5
    }

    // ElementBoost remains reserved so old assets deserialize without remapping.
    public enum CraftLiveStatType
    {
        None = 0,
        AttackRate = 1,
        DefenseRate = 2,
        EvasionRate = 3,
        ElementBoost = 4
    }

    public enum CraftLiveElementType
    {
        None,
        Fire,
        Freeze,
        Lightning
    }

    public enum CraftLiveSkillType
    {
        None,
        Luck,
        DoubleStrike,
        AutoHeal,
        LifeOrb
    }

    public enum CraftLiveWeaponType
    {
        Sword,
        Thrust,
        Staff
    }

    public enum CraftLivePlacementStatus
    {
        Idle,
        SelectingSlot,
        ConfirmingSlot,
        Pad1Loading,
        Pad1Launching,
        Pad2Arriving,
        PlacementComplete
    }

    public enum CraftLiveCraftStatus
    {
        Editing,
        Mixing,
        Complete
    }

    public enum CraftLiveSessionPhase
    {
        Playing,
        FinalSelection,
        Finished
    }

    [Serializable]
    public struct CraftLiveStats
    {
        [Min(0f)] public float attackRate;
        [Min(0f)] public float defenseRate;
        [Min(0f)] public float evasionRate;

        // V2 compatibility only. V3 never displays or calculates this value.
        [HideInInspector] public float elementBoost;

        public bool HasAnyValue =>
            attackRate > 0f ||
            defenseRate > 0f ||
            evasionRate > 0f;

        public float Get(CraftLiveStatType type)
        {
            switch (type)
            {
                case CraftLiveStatType.AttackRate:
                    return attackRate;
                case CraftLiveStatType.DefenseRate:
                    return defenseRate;
                case CraftLiveStatType.EvasionRate:
                    return evasionRate;
                default:
                    return 0f;
            }
        }

        public void Add(CraftLiveStatType type, float value)
        {
            value = Mathf.Max(0f, value);
            switch (type)
            {
                case CraftLiveStatType.AttackRate:
                    attackRate += value;
                    break;
                case CraftLiveStatType.DefenseRate:
                    defenseRate += value;
                    break;
                case CraftLiveStatType.EvasionRate:
                    evasionRate += value;
                    break;
            }
        }

        public void Add(CraftLiveStats value)
        {
            attackRate += Mathf.Max(0f, value.attackRate);
            defenseRate += Mathf.Max(0f, value.defenseRate);
            evasionRate += Mathf.Max(0f, value.evasionRate);
        }

        public void AddAll(float value)
        {
            value = Mathf.Max(0f, value);
            attackRate += value;
            defenseRate += value;
            evasionRate += value;
        }

        public CraftLiveStats Clamp(float maximum)
        {
            maximum = Mathf.Max(0f, maximum);
            attackRate = Mathf.Clamp(attackRate, 0f, maximum);
            defenseRate = Mathf.Clamp(defenseRate, 0f, maximum);
            evasionRate = Mathf.Clamp(evasionRate, 0f, maximum);
            elementBoost = 0f;
            return this;
        }

        public CraftLiveStats Sanitize()
        {
            attackRate = Mathf.Max(0f, attackRate);
            defenseRate = Mathf.Max(0f, defenseRate);
            evasionRate = Mathf.Max(0f, evasionRate);
            elementBoost = 0f;
            return this;
        }
    }

    [Serializable]
    public struct CraftLiveElementEffect
    {
        public CraftLiveElementType type;
        [Range(0f, 100f)] public float activationChancePercent;
        [Min(0f)] public float effectAmount;
        [Min(0f)] public float durationSeconds;

        public CraftLiveElementEffect Sanitize()
        {
            activationChancePercent =
                Mathf.Clamp(activationChancePercent, 0f, 100f);
            effectAmount = Mathf.Max(0f, effectAmount);
            durationSeconds = Mathf.Max(0f, durationSeconds);
            return this;
        }
    }

    [Serializable]
    public struct CraftLiveSkillEffect
    {
        public CraftLiveSkillType type;
        [Range(0f, 100f)] public float activationChancePercent;
        [Min(0f)] public float primaryValue;
        [Min(0f)] public float secondaryValue;
        [Min(0f)] public float intervalSeconds;

        public CraftLiveSkillEffect Sanitize()
        {
            activationChancePercent =
                Mathf.Clamp(activationChancePercent, 0f, 100f);
            primaryValue = Mathf.Max(0f, primaryValue);
            secondaryValue = Mathf.Max(0f, secondaryValue);
            intervalSeconds = Mathf.Max(0f, intervalSeconds);
            return this;
        }
    }

    public static class CraftLiveSlot
    {
        public static string ToKey(CraftLiveSlotId slot)
        {
            return slot.ToString().ToLowerInvariant();
        }

        public static CraftLiveMaterialCategory RequiredCategory(
            CraftLiveSlotId slot)
        {
            switch (slot)
            {
                case CraftLiveSlotId.Attribute:
                    return CraftLiveMaterialCategory.Attribute;
                case CraftLiveSlotId.Skill:
                    return CraftLiveMaterialCategory.Skill;
                default:
                    return CraftLiveMaterialCategory.Upgrade;
            }
        }

        public static bool IsBaseStatSlot(CraftLiveSlotId slot)
        {
            return slot == CraftLiveSlotId.Top ||
                   slot == CraftLiveSlotId.Right ||
                   slot == CraftLiveSlotId.Left ||
                   slot == CraftLiveSlotId.Bottom;
        }
    }
}
