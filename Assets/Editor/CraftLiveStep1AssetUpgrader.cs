using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEngine;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLiveStep1AssetUpgrader
    {
        private const string MenuPath =
            "Tools/Craft-live/Step 1/Upgrade Data Assets To V3";
        private const string CalibrationPath =
            "Assets/CraftLiveData/DefaultPad4Calibration.asset";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            int changedAssets = 0;
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:CraftLiveMaterialDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CraftLiveMaterialDefinition material =
                    AssetDatabase.LoadAssetAtPath<
                        CraftLiveMaterialDefinition>(path);
                if (material != null && UpgradeMaterial(material))
                {
                    changedAssets++;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets(
                         "t:CraftLiveRules"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CraftLiveRules rules =
                    AssetDatabase.LoadAssetAtPath<CraftLiveRules>(path);
                if (rules != null && UpgradeRules(rules))
                {
                    changedAssets++;
                }
            }

            EnsureCalibrationAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Craft-live Step 1: V3 data upgrade complete. " +
                $"Changed assets={changedAssets}");
        }

        public static void RunBatch()
        {
            Run();
        }

        private static bool UpgradeMaterial(
            CraftLiveMaterialDefinition material)
        {
            SerializedObject serialized = new SerializedObject(material);
            SerializedProperty category =
                serialized.FindProperty("category");
            bool changed = false;

            if (category.enumValueIndex ==
                (int)CraftLiveMaterialCategory.Upgrade)
            {
                changed |= UpgradeBaseStats(serialized);
            }
            else if (category.enumValueIndex ==
                     (int)CraftLiveMaterialCategory.Attribute)
            {
                changed |= UpgradeElementType(serialized);
            }
            else if (category.enumValueIndex ==
                     (int)CraftLiveMaterialCategory.Skill)
            {
                changed |= UpgradeSkillType(serialized);
            }

            if (!changed)
            {
                return false;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(material);
            return true;
        }

        private static bool UpgradeBaseStats(SerializedObject serialized)
        {
            SerializedProperty stats =
                serialized.FindProperty("statModifiers");
            SerializedProperty attack =
                stats.FindPropertyRelative("attackRate");
            SerializedProperty defense =
                stats.FindPropertyRelative("defenseRate");
            SerializedProperty evasion =
                stats.FindPropertyRelative("evasionRate");
            if (attack.floatValue > 0f ||
                defense.floatValue > 0f ||
                evasion.floatValue > 0f)
            {
                return false;
            }

            float legacyMaximum = Mathf.Max(
                Mathf.Max(
                    serialized.FindProperty("topBonus").floatValue,
                    serialized.FindProperty("rightBonus").floatValue),
                Mathf.Max(
                    serialized.FindProperty("leftBonus").floatValue,
                    serialized.FindProperty("bottomBonus").floatValue));
            CraftLiveStatType legacyType =
                (CraftLiveStatType)serialized
                    .FindProperty("affectedStat")
                    .enumValueIndex;

            switch (legacyType)
            {
                case CraftLiveStatType.AttackRate:
                    attack.floatValue = legacyMaximum;
                    return legacyMaximum > 0f;
                case CraftLiveStatType.DefenseRate:
                    defense.floatValue = legacyMaximum;
                    return legacyMaximum > 0f;
                case CraftLiveStatType.EvasionRate:
                    evasion.floatValue = legacyMaximum;
                    return legacyMaximum > 0f;
                default:
                    return false;
            }
        }

        private static bool UpgradeElementType(SerializedObject serialized)
        {
            SerializedProperty effect =
                serialized.FindProperty("elementEffect");
            SerializedProperty type =
                effect.FindPropertyRelative("type");
            if (type.enumValueIndex != (int)CraftLiveElementType.None)
            {
                return false;
            }

            string id = serialized
                .FindProperty("attributeId")
                .stringValue
                .ToLowerInvariant();
            CraftLiveElementType resolved = CraftLiveElementType.None;
            if (id.Contains("fire"))
            {
                resolved = CraftLiveElementType.Fire;
            }
            else if (id.Contains("freeze") ||
                     id.Contains("ice") ||
                     id.Contains("water"))
            {
                resolved = CraftLiveElementType.Freeze;
            }
            else if (id.Contains("thunder") ||
                     id.Contains("lightning"))
            {
                resolved = CraftLiveElementType.Lightning;
            }

            type.enumValueIndex = (int)resolved;
            return resolved != CraftLiveElementType.None;
        }

        private static bool UpgradeSkillType(SerializedObject serialized)
        {
            SerializedProperty effect =
                serialized.FindProperty("skillEffect");
            SerializedProperty type =
                effect.FindPropertyRelative("type");
            if (type.enumValueIndex != (int)CraftLiveSkillType.None)
            {
                return false;
            }

            string id = serialized
                .FindProperty("skillId")
                .stringValue
                .ToLowerInvariant();
            CraftLiveSkillType resolved = CraftLiveSkillType.None;
            if (id.Contains("luck") || id.Contains("critical"))
            {
                resolved = CraftLiveSkillType.Luck;
            }
            else if (id.Contains("double") || id.Contains("multi"))
            {
                resolved = CraftLiveSkillType.DoubleStrike;
            }
            else if (id.Contains("heal") ||
                     id.Contains("regeneration"))
            {
                resolved = CraftLiveSkillType.AutoHeal;
            }
            else if (id.Contains("lifeorb") ||
                     id.Contains("life_orb"))
            {
                resolved = CraftLiveSkillType.LifeOrb;
            }

            type.enumValueIndex = (int)resolved;
            return resolved != CraftLiveSkillType.None;
        }

        private static bool UpgradeRules(CraftLiveRules rules)
        {
            SerializedObject serialized = new SerializedObject(rules);
            bool changed = false;
            SerializedProperty duration =
                serialized.FindProperty("sessionDurationSeconds");
            if (duration.floatValue <= 0f)
            {
                duration.floatValue = 300f;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rules);
            return true;
        }

        private static void EnsureCalibrationAsset()
        {
            CraftLivePad4Calibration calibration =
                AssetDatabase.LoadAssetAtPath<CraftLivePad4Calibration>(
                    CalibrationPath);
            if (calibration != null)
            {
                return;
            }

            calibration =
                ScriptableObject.CreateInstance<CraftLivePad4Calibration>();
            AssetDatabase.CreateAsset(calibration, CalibrationPath);
            EditorUtility.SetDirty(calibration);
        }
    }
}
