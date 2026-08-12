using System.Collections.Generic;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveMaterialBoardView : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private CraftLiveMaterialTicketView ticketPrefab;
        [SerializeField] private bool showUnregisteredForDebug;

        private readonly Dictionary<string, CraftLiveMaterialTicketView> tickets =
            new Dictionary<string, CraftLiveMaterialTicketView>();
        private bool filterEnabled;
        private CraftLiveMaterialCategory categoryFilter;
        private int handledRegistrationSerial = -1;
        private bool initialized;

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
                session.StateChanged += Refresh;
                Refresh(session.State);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= Refresh;
            }
        }

        public void ShowAll()
        {
            filterEnabled = false;
            Refresh(session != null ? session.State : null);
        }

        public void ShowAttributes()
        {
            SetFilter(CraftLiveMaterialCategory.Attribute);
        }

        public void ShowSkills()
        {
            SetFilter(CraftLiveMaterialCategory.Skill);
        }

        public void ShowUpgrades()
        {
            SetFilter(CraftLiveMaterialCategory.Upgrade);
        }

        private void SetFilter(CraftLiveMaterialCategory category)
        {
            filterEnabled = true;
            categoryFilter = category;
            Refresh(session != null ? session.State : null);
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null || session == null || session.Catalog == null ||
                contentRoot == null || ticketPrefab == null)
            {
                return;
            }

            bool newRegistration = initialized &&
                                   state.registrationSerial != handledRegistrationSerial;
            foreach (CraftLiveMaterialDefinition material in session.Catalog.Materials)
            {
                if (material == null)
                {
                    continue;
                }

                int count = state.GetInventoryCount(material.MaterialId);
                bool shouldExist =
                    state.HasMaterialRegistered(material.MaterialId) ||
                    showUnregisteredForDebug;
                bool visible = shouldExist &&
                               (!filterEnabled || material.Category == categoryFilter);
                if (!tickets.TryGetValue(
                        material.MaterialId,
                        out CraftLiveMaterialTicketView ticket))
                {
                    if (!shouldExist)
                    {
                        continue;
                    }

                    ticket = Instantiate(ticketPrefab, contentRoot);
                    ticket.name = $"Ticket_{material.MaterialId}";
                    ticket.Bind(session, material, count);
                    tickets.Add(material.MaterialId, ticket);
                    ticket.gameObject.SetActive(visible);
                    if (visible &&
                        newRegistration &&
                        state.lastRegisteredMaterialId == material.MaterialId)
                    {
                        ticket.PlayDropIn();
                    }
                }
                else
                {
                    ticket.gameObject.SetActive(visible);
                    ticket.SetCount(count);
                    if (visible &&
                        newRegistration &&
                        state.lastRegisteredMaterialId == material.MaterialId)
                    {
                        ticket.PlayIncrement(state.lastRegistrationDelta);
                    }
                }

                if (visible)
                {
                    bool selected = state.selectedMaterialId == material.MaterialId;
                    bool canInteract =
                        state.placement.status == CraftLivePlacementStatus.Idle &&
                        count > 0;
                    ticket.SetState(selected, canInteract);
                }
            }

            handledRegistrationSerial = state.registrationSerial;
            initialized = true;
        }
    }
}
