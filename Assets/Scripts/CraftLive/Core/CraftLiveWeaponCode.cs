using System;

namespace CraftOrigin.CraftLive
{
    [Serializable]
    public sealed class CraftLiveWeaponCodeData
    {
        public string weaponId = string.Empty;
        public CraftLiveElementType attribute;
        public CraftLiveSkillType skill;
        public int attackMaterialCount;
        public int defenseMaterialCount;
        public int evasionMaterialCount;
    }

    public static class CraftLiveWeaponCode
    {
        private const int CodeLength = 6;

        public static string Generate(CraftLiveResultState result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.weaponId))
            {
                return string.Empty;
            }

            int attack = ClampMaterialCount(result.attackMaterialCount);
            int defense = ClampMaterialCount(result.defenseMaterialCount);
            int evasion = ClampMaterialCount(result.evasionMaterialCount);
            if (CraftLiveCalculator.IsSecretWeaponId(result.weaponId))
            {
                attack = 0;
                defense = 0;
                evasion = 0;
            }

            return $"{GetWeaponSymbol(result.weaponId)}" +
                   $"{GetAttributeSymbol(result.elementEffect.type)}" +
                   $"{GetSkillSymbol(result.skillEffect.type)}" +
                   $"{attack}{defense}{evasion}";
        }

        public static bool TryDecode(
            string code,
            out CraftLiveWeaponCodeData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            string normalized = code.Trim().ToUpperInvariant();
            if (normalized.Length != CodeLength ||
                !TryGetWeaponId(normalized[0], out string weaponId) ||
                !TryGetAttribute(normalized[1], out CraftLiveElementType attribute) ||
                !TryGetSkill(normalized[2], out CraftLiveSkillType skill) ||
                !TryGetCount(normalized[3], out int attack) ||
                !TryGetCount(normalized[4], out int defense) ||
                !TryGetCount(normalized[5], out int evasion) ||
                attack + defense + evasion > 4)
            {
                return false;
            }

            if (CraftLiveCalculator.IsSecretWeaponId(weaponId) &&
                (attack != 0 || defense != 0 || evasion != 0))
            {
                return false;
            }

            data = new CraftLiveWeaponCodeData
            {
                weaponId = weaponId,
                attribute = attribute,
                skill = skill,
                attackMaterialCount = attack,
                defenseMaterialCount = defense,
                evasionMaterialCount = evasion
            };
            return true;
        }

        public static bool TryGetWeaponId(string code, out string weaponId)
        {
            weaponId = string.Empty;
            if (!TryDecode(code, out CraftLiveWeaponCodeData data))
            {
                return false;
            }

            weaponId = data.weaponId;
            return true;
        }

        private static int ClampMaterialCount(int value)
        {
            return Math.Max(0, Math.Min(4, value));
        }

        private static bool TryGetCount(char symbol, out int count)
        {
            count = symbol - '0';
            return count >= 0 && count <= 4;
        }

        private static char GetWeaponSymbol(string weaponId)
        {
            switch (weaponId)
            {
                case "weapon_bigsword_sword": return '2';
                case "weapon_fude_staff": return '3';
                case "weapon_katate_sword": return '4';
                case "weapon_kaziki": return '5';
                case "weapon_kobushi": return '6';
                case "weapon_pikopiko_sword": return '7';
                case "weapon_staff": return '8';
                case "weapon_rapier": return '9';
                default: return 'X';
            }
        }

        private static bool TryGetWeaponId(char symbol, out string weaponId)
        {
            switch (symbol)
            {
                case '2': weaponId = "weapon_bigsword_sword"; return true;
                case '3': weaponId = "weapon_fude_staff"; return true;
                case '4': weaponId = "weapon_katate_sword"; return true;
                case '5': weaponId = "weapon_kaziki"; return true;
                case '6': weaponId = "weapon_kobushi"; return true;
                case '7': weaponId = "weapon_pikopiko_sword"; return true;
                case '8': weaponId = "weapon_staff"; return true;
                case '9': weaponId = "weapon_rapier"; return true;
                default: weaponId = string.Empty; return false;
            }
        }

        private static char GetAttributeSymbol(CraftLiveElementType type)
        {
            switch (type)
            {
                case CraftLiveElementType.Fire: return 'F';
                case CraftLiveElementType.Freeze: return 'C';
                case CraftLiveElementType.Lightning: return 'T';
                default: return 'N';
            }
        }

        private static bool TryGetAttribute(
            char symbol,
            out CraftLiveElementType type)
        {
            switch (symbol)
            {
                case 'F': type = CraftLiveElementType.Fire; return true;
                case 'C': type = CraftLiveElementType.Freeze; return true;
                case 'T': type = CraftLiveElementType.Lightning; return true;
                case 'N': type = CraftLiveElementType.None; return true;
                default: type = CraftLiveElementType.None; return false;
            }
        }

        private static char GetSkillSymbol(CraftLiveSkillType type)
        {
            switch (type)
            {
                case CraftLiveSkillType.Luck: return 'L';
                case CraftLiveSkillType.DoubleStrike: return 'D';
                case CraftLiveSkillType.AutoHeal: return 'H';
                case CraftLiveSkillType.LifeOrb: return 'B';
                default: return 'N';
            }
        }

        private static bool TryGetSkill(
            char symbol,
            out CraftLiveSkillType type)
        {
            switch (symbol)
            {
                case 'L': type = CraftLiveSkillType.Luck; return true;
                case 'D': type = CraftLiveSkillType.DoubleStrike; return true;
                case 'H': type = CraftLiveSkillType.AutoHeal; return true;
                case 'B': type = CraftLiveSkillType.LifeOrb; return true;
                case 'N': type = CraftLiveSkillType.None; return true;
                default: type = CraftLiveSkillType.None; return false;
            }
        }
    }
}
