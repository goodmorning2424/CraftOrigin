using System.Text;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public static class CraftLivePad1Presentation
    {
        public static string GetCategoryLabel(
            CraftLiveMaterialCategory category)
        {
            switch (category)
            {
                case CraftLiveMaterialCategory.Upgrade:
                    return "パワーアップ";
                case CraftLiveMaterialCategory.Skill:
                    return "スキル";
                default:
                    return "タイプ";
            }
        }

        public static string BuildDetailText(
            CraftLiveMaterialDefinition material)
        {
            if (material == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(material.DisplayName);
            builder.AppendLine(GetCategoryLabel(material.Category));
            if (!string.IsNullOrWhiteSpace(material.Description))
            {
                builder.AppendLine(material.Description);
            }

            switch (material.Category)
            {
                case CraftLiveMaterialCategory.Upgrade:
                    CraftLiveStats stats = material.StatModifiers;
                    builder.AppendLine(
                        $"攻撃 +{stats.attackRate:0.#}  " +
                        $"防御 +{stats.defenseRate:0.#}  " +
                        $"回避 +{stats.evasionRate:0.#}");
                    break;
                case CraftLiveMaterialCategory.Attribute:
                    CraftLiveElementEffect element = material.ElementEffect;
                    builder.AppendLine(
                        $"{GetElementLabel(element.type)}  " +
                        $"発動 {element.activationChancePercent:0.#}%  " +
                        $"効果 {element.effectAmount:0.#}");
                    break;
                case CraftLiveMaterialCategory.Skill:
                    CraftLiveSkillEffect skill = material.SkillEffect;
                    builder.AppendLine(
                        $"{GetSkillLabel(skill.type)}  " +
                        $"発動 {skill.activationChancePercent:0.#}%  " +
                        $"効果 {skill.primaryValue:0.#}");
                    break;
            }

            if (!string.IsNullOrWhiteSpace(material.AbilitySummary))
            {
                builder.AppendLine(material.AbilitySummary);
            }

            if (!string.IsNullOrWhiteSpace(material.UsageSummary))
            {
                builder.AppendLine(material.UsageSummary);
            }

            return builder.ToString().TrimEnd();
        }

        public static PrimitiveType GetPlaceholderPrimitive(
            CraftLiveMaterialDefinition material)
        {
            if (material == null)
            {
                return PrimitiveType.Sphere;
            }

            switch (material.MaterialForm)
            {
                case CraftLiveMaterialForm.Ore:
                    return PrimitiveType.Capsule;
                case CraftLiveMaterialForm.Gem:
                    return PrimitiveType.Sphere;
                case CraftLiveMaterialForm.Charm:
                    return PrimitiveType.Cylinder;
                case CraftLiveMaterialForm.Spirit:
                    return PrimitiveType.Sphere;
                default:
                    return material.Category ==
                           CraftLiveMaterialCategory.Skill
                        ? PrimitiveType.Cylinder
                        : PrimitiveType.Sphere;
            }
        }

        private static string GetElementLabel(CraftLiveElementType type)
        {
            switch (type)
            {
                case CraftLiveElementType.Fire:
                    return "炎";
                case CraftLiveElementType.Freeze:
                    return "凍結";
                case CraftLiveElementType.Lightning:
                    return "雷";
                default:
                    return "属性未設定";
            }
        }

        private static string GetSkillLabel(CraftLiveSkillType type)
        {
            switch (type)
            {
                case CraftLiveSkillType.Luck:
                    return "幸運";
                case CraftLiveSkillType.DoubleStrike:
                    return "2連撃";
                case CraftLiveSkillType.AutoHeal:
                    return "自動回復";
                case CraftLiveSkillType.LifeOrb:
                    return "命の珠";
                default:
                    return "スキル未設定";
            }
        }
    }
}
