using System;

namespace CraftOrigin.CraftLive
{
    public static class CraftLiveWeaponCode
    {
        private const string Alphabet =
            "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        public static string Generate(
            string prefix,
            string roomId,
            CraftLiveResultState result)
        {
            if (result == null ||
                string.IsNullOrWhiteSpace(result.weaponId))
            {
                return string.Empty;
            }

            string source =
                $"{roomId}|{result.weaponId}|" +
                $"{result.attributeId}|{result.skillId}|" +
                $"{result.resultSerial}|" +
                $"{result.completedAtUnixMs}";
            uint hash = Fnv1a(source);
            string encoded = Encode(hash, 8);
            string safePrefix =
                string.IsNullOrWhiteSpace(prefix)
                    ? "CL"
                    : prefix.Trim().ToUpperInvariant();
            return $"{safePrefix}-{encoded.Substring(0, 4)}-" +
                   $"{encoded.Substring(4, 4)}";
        }

        private static uint Fnv1a(string value)
        {
            uint hash = 2166136261;
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return hash;
        }

        private static string Encode(uint value, int length)
        {
            char[] result = new char[length];
            for (int i = length - 1; i >= 0; i--)
            {
                result[i] =
                    Alphabet[(int)(value %
                                   (uint)Alphabet.Length)];
                value /= (uint)Alphabet.Length;
            }

            return new string(result);
        }
    }
}
