using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    [Serializable]
    public sealed class CraftLiveInventoryEntry
    {
        public string materialId;
        [Min(0)] public int count;

        public CraftLiveInventoryEntry()
        {
        }

        public CraftLiveInventoryEntry(string materialId, int count)
        {
            this.materialId = materialId;
            this.count = Mathf.Max(0, count);
        }
    }

    [Serializable]
    public sealed class CraftLivePlacementFlow
    {
        public CraftLivePlacementStatus status;
        public string materialId;
        public bool hasCandidateSlot;
        public CraftLiveSlotId candidateSlot;
        public bool hasConfirmedSlot;
        public CraftLiveSlotId confirmedSlot;
        public int transferSerial;
        public long statusChangedAtUnixMs;

        public void Clear()
        {
            status = CraftLivePlacementStatus.Idle;
            materialId = string.Empty;
            hasCandidateSlot = false;
            hasConfirmedSlot = false;
            statusChangedAtUnixMs = 0;
        }
    }

    [Serializable]
    public sealed class CraftLiveTransferQueueEntry
    {
        public int serial;
        public string materialId;
        public CraftLiveSlotId slot;

        public CraftLiveTransferQueueEntry()
        {
        }

        public CraftLiveTransferQueueEntry(
            int serial,
            string materialId,
            CraftLiveSlotId slot)
        {
            this.serial = Mathf.Max(1, serial);
            this.materialId = materialId ?? string.Empty;
            this.slot = slot;
        }

        public void Normalize()
        {
            serial = Mathf.Max(1, serial);
            materialId = materialId ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class CraftLiveSlots
    {
        public string attribute;
        public string skill;
        public string top;
        public string right;
        public string left;
        public string bottom;

        public string Get(CraftLiveSlotId slot)
        {
            switch (slot)
            {
                case CraftLiveSlotId.Attribute:
                    return attribute;
                case CraftLiveSlotId.Skill:
                    return skill;
                case CraftLiveSlotId.Top:
                    return top;
                case CraftLiveSlotId.Right:
                    return right;
                case CraftLiveSlotId.Left:
                    return left;
                case CraftLiveSlotId.Bottom:
                    return bottom;
                default:
                    return string.Empty;
            }
        }

        public void Set(CraftLiveSlotId slot, string materialId)
        {
            materialId = materialId ?? string.Empty;
            switch (slot)
            {
                case CraftLiveSlotId.Attribute:
                    attribute = materialId;
                    break;
                case CraftLiveSlotId.Skill:
                    skill = materialId;
                    break;
                case CraftLiveSlotId.Top:
                    top = materialId;
                    break;
                case CraftLiveSlotId.Right:
                    right = materialId;
                    break;
                case CraftLiveSlotId.Left:
                    left = materialId;
                    break;
                case CraftLiveSlotId.Bottom:
                    bottom = materialId;
                    break;
            }
        }

        public int CountFilledBaseSlots()
        {
            int count = 0;
            count += string.IsNullOrWhiteSpace(top) ? 0 : 1;
            count += string.IsNullOrWhiteSpace(right) ? 0 : 1;
            count += string.IsNullOrWhiteSpace(left) ? 0 : 1;
            count += string.IsNullOrWhiteSpace(bottom) ? 0 : 1;
            return count;
        }

        public void Normalize()
        {
            attribute = attribute ?? string.Empty;
            skill = skill ?? string.Empty;
            top = top ?? string.Empty;
            right = right ?? string.Empty;
            left = left ?? string.Empty;
            bottom = bottom ?? string.Empty;
        }

        public void Clear()
        {
            attribute = string.Empty;
            skill = string.Empty;
            top = string.Empty;
            right = string.Empty;
            left = string.Empty;
            bottom = string.Empty;
        }
    }

    [Serializable]
    public sealed class CraftLiveCraftState
    {
        public CraftLiveCraftStatus status = CraftLiveCraftStatus.Editing;
        [Range(0f, 100f)] public float mixPower;
        [Min(0f)] public float mixBonus;
        [Min(0)] public int hammerPassCount;
        public string resultRank = "未合成";
        public long startedAtUnixMs;
    }

    [Serializable]
    public sealed class CraftLiveResultState
    {
        public string weaponName;
        public string weaponId;
        public CraftLiveWeaponType weaponType;
        public string attributeId;
        public string attributeName;
        public CraftLiveElementEffect elementEffect;
        public string skillId;
        public string skillName;
        public string skillDescription;
        public CraftLiveSkillEffect skillEffect;
        public CraftLiveStats stats;
        public string rank;
        public long completedAtUnixMs;
        public int resultSerial;

        public void Normalize()
        {
            weaponName = weaponName ?? string.Empty;
            weaponId = weaponId ?? string.Empty;
            attributeId = attributeId ?? string.Empty;
            attributeName = attributeName ?? string.Empty;
            skillId = skillId ?? string.Empty;
            skillName = skillName ?? string.Empty;
            skillDescription = skillDescription ?? string.Empty;
            rank = rank ?? string.Empty;
            elementEffect = elementEffect.Sanitize();
            skillEffect = skillEffect.Sanitize();
            stats = stats.Sanitize();
        }

        public CraftLiveResultState Clone()
        {
            return JsonUtility.FromJson<CraftLiveResultState>(
                JsonUtility.ToJson(this));
        }
    }

    [Serializable]
    public sealed class CraftLiveRoomState
    {
        public const int CurrentSchemaVersion = 5;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public long updatedAtUnixMs;
        public string selectedMaterialId;
        public List<string> registeredMaterialIds = new List<string>();

        // V2 migration sources. They remain serialized until all deployed rooms use V3.
        [HideInInspector]
        public List<CraftLiveInventoryEntry> inventory =
            new List<CraftLiveInventoryEntry>();
        [HideInInspector]
        public List<string> qrUnlockedMaterialIds = new List<string>();

        public CraftLivePlacementFlow placement = new CraftLivePlacementFlow();
        public List<CraftLiveTransferQueueEntry> transferQueue =
            new List<CraftLiveTransferQueueEntry>();
        public int transferQueueSerial;
        public int transferBatchSerial;
        [Min(0)] public int transferBatchRemaining;
        public string lastRegisteredMaterialId;
        public int lastRegistrationDelta;
        public int registrationSerial;
        public CraftLiveSlots slots = new CraftLiveSlots();
        public CraftLiveStats displayedStats;
        public int statusDisplaySerial;
        public CraftLiveCraftState craft = new CraftLiveCraftState();
        public string selectedWeaponId;
        public bool weaponSelectionConfirmed;
        public CraftLiveResultState result = new CraftLiveResultState();
        public List<CraftLiveResultState> completedWeapons =
            new List<CraftLiveResultState>();
        public CraftLiveSessionPhase sessionPhase =
            CraftLiveSessionPhase.Playing;
        public long sessionStartedAtUnixMs;
        public long sessionEndsAtUnixMs;
        public int selectedFinalResultSerial;
        public string finalWeaponCode;
        public string message;

        public static CraftLiveRoomState Create(CraftLiveCatalog catalog)
        {
            CraftLiveRoomState state = new CraftLiveRoomState();
            CraftLiveWeaponDefinition firstWeapon =
                catalog != null ? catalog.FirstWeapon() : null;
            state.selectedWeaponId =
                firstWeapon != null ? firstWeapon.WeaponId : string.Empty;
            state.Normalize(catalog);
            return state;
        }

        public CraftLiveRoomState Clone()
        {
            return FromJson(JsonUtility.ToJson(this));
        }

        public static CraftLiveRoomState FromJson(string json)
        {
            CraftLiveRoomState state = string.IsNullOrWhiteSpace(json)
                ? new CraftLiveRoomState()
                : JsonUtility.FromJson<CraftLiveRoomState>(json);
            state = state ?? new CraftLiveRoomState();
            state.Normalize(null);
            return state;
        }

        public void Normalize(CraftLiveCatalog catalog)
        {
            registeredMaterialIds =
                registeredMaterialIds ?? new List<string>();
            inventory = inventory ?? new List<CraftLiveInventoryEntry>();
            qrUnlockedMaterialIds =
                qrUnlockedMaterialIds ?? new List<string>();
            placement = placement ?? new CraftLivePlacementFlow();
            transferQueue =
                transferQueue ??
                new List<CraftLiveTransferQueueEntry>();
            slots = slots ?? new CraftLiveSlots();
            craft = craft ?? new CraftLiveCraftState();
            result = result ?? new CraftLiveResultState();
            completedWeapons =
                completedWeapons ??
                new List<CraftLiveResultState>();
            selectedMaterialId = selectedMaterialId ?? string.Empty;
            selectedWeaponId = selectedWeaponId ?? string.Empty;
            lastRegisteredMaterialId =
                lastRegisteredMaterialId ?? string.Empty;
            message = message ?? string.Empty;
            finalWeaponCode = finalWeaponCode ?? string.Empty;

            MigrateLegacyRegistrations();
            NormalizeRegisteredMaterialIds();

            placement.materialId = placement.materialId ?? string.Empty;
            NormalizeTransferQueue();
            slots.Normalize();
            result.Normalize();
            NormalizeCompletedWeapons();
            displayedStats = displayedStats.Sanitize();
            statusDisplaySerial = Mathf.Max(0, statusDisplaySerial);
            schemaVersion = CurrentSchemaVersion;

            if (string.IsNullOrWhiteSpace(selectedWeaponId) &&
                catalog != null)
            {
                CraftLiveWeaponDefinition firstWeapon = catalog.FirstWeapon();
                selectedWeaponId =
                    firstWeapon != null ? firstWeapon.WeaponId : string.Empty;
            }

            craft.mixPower = Mathf.Clamp(craft.mixPower, 0f, 100f);
            craft.mixBonus = Mathf.Max(0f, craft.mixBonus);
            craft.hammerPassCount =
                Mathf.Max(0, craft.hammerPassCount);
            craft.resultRank = craft.resultRank ?? "未合成";
            sessionStartedAtUnixMs =
                Math.Max(0L, sessionStartedAtUnixMs);
            sessionEndsAtUnixMs =
                Math.Max(0L, sessionEndsAtUnixMs);
            selectedFinalResultSerial =
                Mathf.Max(0, selectedFinalResultSerial);
            lastRegistrationDelta =
                Mathf.Clamp(lastRegistrationDelta, 0, 1);
            transferBatchRemaining = Mathf.Clamp(
                transferBatchRemaining,
                0,
                transferQueue.Count);
        }

        public bool IsSlotReserved(CraftLiveSlotId slot)
        {
            if (placement != null &&
                placement.hasConfirmedSlot &&
                placement.confirmedSlot == slot &&
                placement.status != CraftLivePlacementStatus.Idle &&
                placement.status !=
                    CraftLivePlacementStatus.PlacementComplete)
            {
                return true;
            }

            if (transferQueue == null)
            {
                return false;
            }

            foreach (CraftLiveTransferQueueEntry entry in transferQueue)
            {
                if (entry != null && entry.slot == slot)
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanReserveSlot(CraftLiveSlotId slot)
        {
            return slots != null &&
                   string.IsNullOrWhiteSpace(slots.Get(slot)) &&
                   !IsSlotReserved(slot);
        }

        public bool HasAnyPlacedMaterial()
        {
            if (slots == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(slots.attribute) ||
                   !string.IsNullOrWhiteSpace(slots.skill) ||
                   !string.IsNullOrWhiteSpace(slots.top) ||
                   !string.IsNullOrWhiteSpace(slots.right) ||
                   !string.IsNullOrWhiteSpace(slots.left) ||
                   !string.IsNullOrWhiteSpace(slots.bottom);
        }

        public bool RegisterMaterial(string materialId)
        {
            if (string.IsNullOrWhiteSpace(materialId))
            {
                return false;
            }

            registeredMaterialIds =
                registeredMaterialIds ?? new List<string>();
            materialId = materialId.Trim();
            if (registeredMaterialIds.Contains(materialId))
            {
                return false;
            }

            registeredMaterialIds.Add(materialId);
            return true;
        }

        public bool HasMaterialRegistered(string materialId)
        {
            return !string.IsNullOrWhiteSpace(materialId) &&
                   registeredMaterialIds != null &&
                   registeredMaterialIds.Contains(materialId);
        }

        // Compatibility API for existing UI. A registered material is always reusable.
        public int GetInventoryCount(string materialId)
        {
            return HasMaterialRegistered(materialId) ? 1 : 0;
        }

        // Compatibility API for V2 callers. Negative amounts never unregister in V3.
        public void AddInventory(string materialId, int amount)
        {
            if (amount > 0)
            {
                RegisterMaterial(materialId);
            }
        }

        private void MigrateLegacyRegistrations()
        {
            foreach (CraftLiveInventoryEntry entry in inventory)
            {
                if (entry != null)
                {
                    RegisterMaterial(entry.materialId);
                }
            }

            foreach (string legacyId in qrUnlockedMaterialIds)
            {
                RegisterMaterial(legacyId);
            }

            inventory.Clear();
            qrUnlockedMaterialIds.Clear();
        }

        private void NormalizeRegisteredMaterialIds()
        {
            HashSet<string> unique = new HashSet<string>();
            List<string> normalized = new List<string>();
            foreach (string value in registeredMaterialIds)
            {
                string materialId = value;
                if (string.IsNullOrWhiteSpace(materialId))
                {
                    continue;
                }

                materialId = materialId.Trim();
                if (unique.Add(materialId))
                {
                    normalized.Add(materialId);
                }
            }

            registeredMaterialIds = normalized;
        }

        private void NormalizeTransferQueue()
        {
            HashSet<int> serials = new HashSet<int>();
            HashSet<CraftLiveSlotId> slotsInQueue =
                new HashSet<CraftLiveSlotId>();
            List<CraftLiveTransferQueueEntry> normalized =
                new List<CraftLiveTransferQueueEntry>();
            int highestSerial = Mathf.Max(
                transferQueueSerial,
                placement != null ? placement.transferSerial : 0);
            foreach (CraftLiveTransferQueueEntry entry in transferQueue)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.materialId))
                {
                    continue;
                }

                entry.Normalize();
                while (!serials.Add(entry.serial))
                {
                    entry.serial++;
                }

                if (!slotsInQueue.Add(entry.slot))
                {
                    continue;
                }

                highestSerial = Mathf.Max(
                    highestSerial,
                    entry.serial);
                normalized.Add(entry);
            }

            transferQueue = normalized;
            transferQueueSerial = highestSerial;
            transferBatchSerial = Mathf.Max(0, transferBatchSerial);
        }

        private void NormalizeCompletedWeapons()
        {
            List<CraftLiveResultState> normalized =
                new List<CraftLiveResultState>();
            HashSet<int> serials = new HashSet<int>();
            foreach (CraftLiveResultState completed in
                     completedWeapons)
            {
                if (completed == null)
                {
                    continue;
                }

                completed.Normalize();
                if (completed.resultSerial <= 0 ||
                    !serials.Add(completed.resultSerial))
                {
                    continue;
                }

                normalized.Add(completed);
            }

            completedWeapons = normalized;
        }
    }
}
