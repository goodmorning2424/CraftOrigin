using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveMaterialAction : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLiveMaterialDefinition material;

        public CraftLiveMaterialDefinition Material => material;

        public void Select()
        {
            session?.SelectMaterial(material);
        }
    }
}
