using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveStateEvents : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private UnityEvent<string> onMessage;
        [SerializeField] private UnityEvent<string> onInstruction;
        [SerializeField] private UnityEvent<string> onSelectedMaterialName;
        [SerializeField] private UnityEvent<int> onSelectedMaterialCount;
        [SerializeField] private UnityEvent<CraftLivePlacementStatus> onPlacementStatus;
        [SerializeField] private UnityEvent<bool> onMaterialSelectionEnabled;
        [SerializeField] private UnityEvent<bool> onSlotSelectionEnabled;
        [SerializeField] private UnityEvent<bool> onPlacementConfirmationVisible;
        [SerializeField] private UnityEvent<string> onSelectedWeaponName;
        [SerializeField] private UnityEvent<bool> onWeaponConfirmed;
        [SerializeField] private UnityEvent<float> onMixPower;
        [SerializeField] private UnityEvent<string> onMixRank;
        [SerializeField] private UnityEvent<float> onAttackRate;
        [SerializeField] private UnityEvent<float> onDefenseRate;
        [SerializeField] private UnityEvent<float> onEvasionRate;
        // Keep this serialized field so existing scenes retain their field layout.
#pragma warning disable CS0169
        [SerializeField, HideInInspector] private UnityEvent<float> onElementBoost;
#pragma warning restore CS0169
        [SerializeField] private UnityEvent<string> onResultWeaponName;
        [SerializeField] private UnityEvent<string> onResultSkillName;
        [SerializeField] private UnityEvent<string> onResultBuildType;
        [SerializeField] private UnityEvent<bool> onCraftComplete;

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.StateChanged += Publish;
                Publish(session.State);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= Publish;
            }
        }

        public void Refresh()
        {
            if (session != null)
            {
                Publish(session.State);
            }
        }

        private void Publish(CraftLiveRoomState state)
        {
            if (state == null)
            {
                return;
            }

            CraftLiveMaterialDefinition selectedMaterial =
                session.Catalog != null
                    ? session.Catalog.FindMaterial(state.selectedMaterialId)
                    : null;
            CraftLiveWeaponDefinition selectedWeapon =
                session.Catalog != null
                    ? session.Catalog.FindWeapon(state.selectedWeaponId)
                    : null;
            CraftLiveStats stats = state.craft.status == CraftLiveCraftStatus.Complete
                ? state.result.stats
                : session.CalculateCurrentStats();

            onMessage?.Invoke(state.message);
            onInstruction?.Invoke(session.GetInstruction(session.Role));
            onSelectedMaterialName?.Invoke(
                selectedMaterial != null ? selectedMaterial.DisplayName : string.Empty);
            onSelectedMaterialCount?.Invoke(
                selectedMaterial != null
                    ? state.GetInventoryCount(selectedMaterial.MaterialId)
                    : 0);
            onPlacementStatus?.Invoke(state.placement.status);
            onMaterialSelectionEnabled?.Invoke(
                state.placement.status == CraftLivePlacementStatus.Idle);
            onSlotSelectionEnabled?.Invoke(
                state.placement.status == CraftLivePlacementStatus.SelectingSlot ||
                state.placement.status == CraftLivePlacementStatus.ConfirmingSlot);
            onPlacementConfirmationVisible?.Invoke(
                state.placement.status == CraftLivePlacementStatus.ConfirmingSlot);
            onSelectedWeaponName?.Invoke(
                selectedWeapon != null ? selectedWeapon.DisplayName : string.Empty);
            onWeaponConfirmed?.Invoke(state.weaponSelectionConfirmed);
            onMixPower?.Invoke(state.craft.mixPower);
            onMixRank?.Invoke(state.craft.resultRank);
            onAttackRate?.Invoke(stats.attackRate);
            onDefenseRate?.Invoke(stats.defenseRate);
            onEvasionRate?.Invoke(stats.evasionRate);
            onResultWeaponName?.Invoke(state.result.weaponName ?? string.Empty);
            onResultSkillName?.Invoke(state.result.skillName ?? string.Empty);
            onResultBuildType?.Invoke(CraftLiveCalculator.DetermineBuildType(stats));
            onCraftComplete?.Invoke(state.craft.status == CraftLiveCraftStatus.Complete);
        }
    }
}
