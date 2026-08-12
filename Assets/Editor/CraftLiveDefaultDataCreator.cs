using System.Collections.Generic;
using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEngine;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLiveDefaultDataCreator
    {
        private const string Root = "Assets/CraftLiveData";
        private const string MaterialsFolder = Root + "/Materials";
        private const string WeaponsFolder = Root + "/Weapons";

        [MenuItem("Tools/Craft-live/Create Default Data Assets")]
        public static void CreateDefaultAssets()
        {
            EnsureFolder("Assets", "CraftLiveData");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Weapons");

            List<CraftLiveMaterialDefinition> materials = new List<CraftLiveMaterialDefinition>
            {
                CreateAttribute("FireCrystal", "fireCrystal", "炎の結晶", "fire", "炎", new Color(1f, 0.18f, 0.05f), true),
                CreateAttribute("WaterStone", "waterStone", "水の雫石", "water", "水", new Color(0.05f, 0.5f, 1f), true),
                CreateAttribute("ThunderCore", "thunderCore", "雷鳴のコア", "thunder", "雷", new Color(1f, 0.9f, 0.1f), true),
                CreateAttribute("DarkStone", "darkStone", "闇の魔石", "dark", "闇", new Color(0.45f, 0.08f, 0.7f), true),
                CreateSkill("ReviveFeather", "reviveFeather", "復活の羽", "revive", "復活付与", "HPが0になった時、1回だけ復活する。", true),
                CreateSkill("LifeHerb", "lifeHerb", "生命の草", "regeneration", "自然回復", "毎ターンHPを少し回復する。", true),
                CreateSkill("CriticalOrb", "criticalOrb", "会心の宝玉", "critical", "一閃付与", "攻撃時、30%の確率でダメージが2倍になる。", true),
                CreateUpgrade("SharpFang", "sharpFang", "鋭い牙", CraftLiveStatType.AttackRate, 30f, 15f, 10f, 5f),
                CreateUpgrade("HardMetal", "hardMetal", "硬い金属", CraftLiveStatType.DefenseRate, 5f, 30f, 15f, 10f),
                CreateUpgrade("WindFeather", "windFeather", "風の羽", CraftLiveStatType.EvasionRate, 10f, 5f, 30f, 15f),
                CreateUpgrade("MagicPowder", "magicPowder", "魔力の粉", CraftLiveStatType.ElementBoost, 15f, 10f, 5f, 30f)
            };

            List<CraftLiveWeaponDefinition> weapons = new List<CraftLiveWeaponDefinition>
            {
                CreateWeapon("IronSword", "weapon_iron_sword", "鍛鉄の剣", CraftLiveWeaponType.Sword),
                CreateWeapon("Rapier", "weapon_rapier", "細身の突剣", CraftLiveWeaponType.Thrust),
                CreateWeapon("ArcaneStaff", "weapon_arcane_staff", "導きの杖", CraftLiveWeaponType.Staff)
            };

            CraftLiveRules rules = LoadOrCreate<CraftLiveRules>($"{Root}/DefaultCraftLiveRules.asset");
            CraftLiveCatalog catalog = LoadOrCreate<CraftLiveCatalog>($"{Root}/DefaultCraftLiveCatalog.asset");
            SerializedObject catalogObject = new SerializedObject(catalog);
            SetObjectList(catalogObject.FindProperty("materials"), materials);
            SetObjectList(catalogObject.FindProperty("weapons"), weapons);
            catalogObject.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(rules);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;
            Debug.Log($"Craft-live標準データを作成しました: {Root}");
        }

        private static CraftLiveMaterialDefinition CreateAttribute(
            string fileName,
            string id,
            string displayName,
            string attributeId,
            string attributeName,
            Color color,
            bool qr)
        {
            CraftLiveMaterialDefinition asset =
                LoadOrCreate<CraftLiveMaterialDefinition>($"{MaterialsFolder}/{fileName}.asset");
            SerializedObject serialized = new SerializedObject(asset);
            SetCommon(
                serialized,
                id,
                displayName,
                CraftLiveMaterialCategory.Attribute,
                qr,
                CraftLiveMaterialForm.Gem,
                $"{attributeName}属性を武器へ付与する素材。",
                "属性スロットに配置");
            serialized.FindProperty("attributeId").stringValue = attributeId;
            serialized.FindProperty("attributeDisplayName").stringValue = attributeName;
            serialized.FindProperty("effectColor").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static CraftLiveMaterialDefinition CreateSkill(
            string fileName,
            string id,
            string displayName,
            string skillId,
            string skillName,
            string skillDescription,
            bool qr)
        {
            CraftLiveMaterialDefinition asset =
                LoadOrCreate<CraftLiveMaterialDefinition>($"{MaterialsFolder}/{fileName}.asset");
            SerializedObject serialized = new SerializedObject(asset);
            SetCommon(
                serialized,
                id,
                displayName,
                CraftLiveMaterialCategory.Skill,
                qr,
                CraftLiveMaterialForm.Charm,
                skillDescription,
                "能力スロットに配置");
            serialized.FindProperty("skillId").stringValue = skillId;
            serialized.FindProperty("skillDisplayName").stringValue = skillName;
            serialized.FindProperty("skillDescription").stringValue = skillDescription;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static CraftLiveMaterialDefinition CreateUpgrade(
            string fileName,
            string id,
            string displayName,
            CraftLiveStatType stat,
            float top,
            float right,
            float left,
            float bottom)
        {
            CraftLiveMaterialDefinition asset =
                LoadOrCreate<CraftLiveMaterialDefinition>($"{MaterialsFolder}/{fileName}.asset");
            SerializedObject serialized = new SerializedObject(asset);
            CraftLiveMaterialForm form = stat == CraftLiveStatType.ElementBoost
                ? CraftLiveMaterialForm.Spirit
                : stat == CraftLiveStatType.EvasionRate
                    ? CraftLiveMaterialForm.Charm
                    : CraftLiveMaterialForm.Ore;
            SetCommon(
                serialized,
                id,
                displayName,
                CraftLiveMaterialCategory.Upgrade,
                true,
                form,
                $"{GetStatLabel(stat)}を強化する素材。",
                "上下左右の強化枠に配置");
            serialized.FindProperty("affectedStat").enumValueIndex = (int)stat;
            serialized.FindProperty("topBonus").floatValue = top;
            serialized.FindProperty("rightBonus").floatValue = right;
            serialized.FindProperty("leftBonus").floatValue = left;
            serialized.FindProperty("bottomBonus").floatValue = bottom;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static CraftLiveWeaponDefinition CreateWeapon(
            string fileName,
            string id,
            string displayName,
            CraftLiveWeaponType type)
        {
            CraftLiveWeaponDefinition asset =
                LoadOrCreate<CraftLiveWeaponDefinition>($"{WeaponsFolder}/{fileName}.asset");
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("weaponId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("weaponType").enumValueIndex = (int)type;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void SetCommon(
            SerializedObject serialized,
            string id,
            string displayName,
            CraftLiveMaterialCategory category,
            bool qr,
            CraftLiveMaterialForm form,
            string ability,
            string usage)
        {
            serialized.FindProperty("materialId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("category").enumValueIndex = (int)category;
            serialized.FindProperty("requiresQrUnlock").boolValue = qr;
            serialized.FindProperty("materialForm").enumValueIndex = (int)form;
            serialized.FindProperty("description").stringValue =
                $"{displayName}。QRコードで作業台へ登録できます。";
            serialized.FindProperty("abilitySummary").stringValue = ability;
            serialized.FindProperty("usageSummary").stringValue = usage;
        }

        private static string GetStatLabel(CraftLiveStatType stat)
        {
            switch (stat)
            {
                case CraftLiveStatType.AttackRate:
                    return "攻撃率";
                case CraftLiveStatType.DefenseRate:
                    return "防御率";
                case CraftLiveStatType.EvasionRate:
                    return "回避率";
                case CraftLiveStatType.ElementBoost:
                    return "属性強化";
                default:
                    return "能力";
            }
        }

        private static void SetObjectList<T>(SerializedProperty property, List<T> values)
            where T : Object
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
