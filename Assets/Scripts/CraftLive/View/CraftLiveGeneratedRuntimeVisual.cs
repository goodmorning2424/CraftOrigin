using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveGeneratedRuntimeVisual : MonoBehaviour
    {
        private void Start()
        {
            RepairMaterials();
        }

        private void OnTransformChildrenChanged()
        {
            RepairMaterials();
        }

        private void RepairMaterials()
        {
            CraftLiveForgeUITheme.EnsureCompatibleSurfaces(gameObject);
        }
    }
}
