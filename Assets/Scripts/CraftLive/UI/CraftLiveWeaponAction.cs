using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveWeaponAction : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLiveWeaponDefinition weapon;

        public CraftLiveWeaponDefinition Weapon => weapon;

        public void Select()
        {
            session?.SelectWeapon(weapon);
        }

        public void Confirm()
        {
            session?.ConfirmWeapon(weapon);
        }
    }
}
