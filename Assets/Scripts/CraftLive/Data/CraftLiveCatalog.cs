using System.Collections.Generic;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    [CreateAssetMenu(
        menuName = "CraftOrigin/Craft-live/Catalog",
        fileName = "CraftLiveCatalog")]
    public sealed class CraftLiveCatalog : ScriptableObject
    {
        [SerializeField] private List<CraftLiveMaterialDefinition> materials =
            new List<CraftLiveMaterialDefinition>();
        [SerializeField] private List<CraftLiveWeaponDefinition> weapons =
            new List<CraftLiveWeaponDefinition>();

        public IReadOnlyList<CraftLiveMaterialDefinition> Materials => materials;
        public IReadOnlyList<CraftLiveWeaponDefinition> Weapons => weapons;

        public CraftLiveMaterialDefinition FindMaterial(string materialId)
        {
            if (string.IsNullOrWhiteSpace(materialId))
            {
                return null;
            }

            foreach (CraftLiveMaterialDefinition material in materials)
            {
                if (material != null && material.MaterialId == materialId)
                {
                    return material;
                }
            }

            return null;
        }

        public CraftLiveWeaponDefinition FindWeapon(string weaponId)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                return null;
            }

            foreach (CraftLiveWeaponDefinition weapon in weapons)
            {
                if (weapon != null && weapon.WeaponId == weaponId)
                {
                    return weapon;
                }
            }

            return null;
        }

        public CraftLiveWeaponDefinition FirstWeapon()
        {
            foreach (CraftLiveWeaponDefinition weapon in weapons)
            {
                if (weapon != null && weapon.VisibleInSelection)
                {
                    return weapon;
                }
            }

            return null;
        }
    }
}
