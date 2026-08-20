using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveWorkbenchView : MonoBehaviour
    {
        [Serializable]
        private sealed class SlotAnchor
        {
            public CraftLiveSlotId slot = default;
            public Transform anchor = null;
            public Transform arrivalEntry = null;
        }

        [SerializeField] private CraftLiveSession session;
        [SerializeField] private Transform weaponAnchor;
        [SerializeField] private Transform transferSpawn;
        [SerializeField] private List<SlotAnchor> slotAnchors = new List<SlotAnchor>();
        [SerializeField, Min(0.05f)] private float transferDuration = 0.8f;
        [SerializeField, Min(0f)] private float transferArcHeight = 1.5f;
        [SerializeField, Min(0f)] private float completionHoldSeconds = 0.8f;
        [SerializeField] private GameObject fallbackMaterialPrefab;
        [SerializeField] private GameObject fallbackTicketPrefab;
        [SerializeField] private GameObject fallbackWeaponPrefab;
        [SerializeField] private AudioSource audioSource;

        private readonly Dictionary<CraftLiveSlotId, GameObject> slotObjects =
            new Dictionary<CraftLiveSlotId, GameObject>();
        private readonly Dictionary<CraftLiveSlotId, string> displayedMaterials =
            new Dictionary<CraftLiveSlotId, string>();
        private GameObject weaponObject;
        private string displayedWeaponId;
        private int handledTransferSerial = -1;
        private Coroutine transferCoroutine;
        private GameObject activeTransferObject;

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
                session.StateChanged += HandleStateChanged;
                HandleStateChanged(session.State);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
            }

            if (transferCoroutine != null)
            {
                StopCoroutine(transferCoroutine);
                transferCoroutine = null;
            }

            if (activeTransferObject != null)
            {
                Destroy(activeTransferObject);
                activeTransferObject = null;
            }

            handledTransferSerial = -1;
        }

        private void HandleStateChanged(CraftLiveRoomState state)
        {
            if (state == null)
            {
                return;
            }

            RefreshWeapon(state.selectedWeaponId);
            RefreshSlot(state, CraftLiveSlotId.Attribute);
            RefreshSlot(state, CraftLiveSlotId.Skill);
            RefreshSlot(state, CraftLiveSlotId.Top);
            RefreshSlot(state, CraftLiveSlotId.Right);
            RefreshSlot(state, CraftLiveSlotId.Left);
            RefreshSlot(state, CraftLiveSlotId.Bottom);

            if (state.placement.status == CraftLivePlacementStatus.Pad2Arriving &&
                state.placement.transferSerial != handledTransferSerial &&
                transferCoroutine == null)
            {
                handledTransferSerial = state.placement.transferSerial;
                transferCoroutine = StartCoroutine(ReceiveTransfer(state.Clone()));
            }
        }

        private IEnumerator ReceiveTransfer(CraftLiveRoomState snapshot)
        {
            CraftLiveMaterialDefinition material =
                session.Catalog.FindMaterial(snapshot.placement.materialId);
            Transform target = FindAnchor(snapshot.placement.confirmedSlot);
            Transform arrivalEntry = FindArrivalEntry(snapshot.placement.confirmedSlot);
            if (material == null || target == null)
            {
                session.CompleteCurrentPlacement();
                transferCoroutine = null;
                yield break;
            }

            Vector3 start = arrivalEntry != null
                ? arrivalEntry.position
                : transferSpawn != null
                    ? transferSpawn.position
                    : transform.position;
            Vector3 transformPoint = Vector3.Lerp(start, target.position, 0.58f);
            transformPoint.y += transferArcHeight * 0.35f;

            GameObject ticket = CreateVisual(
                material.TransferTicketPrefab,
                fallbackTicketPrefab,
                PrimitiveType.Cube,
                start,
                Quaternion.identity,
                null);
            activeTransferObject = ticket;
            yield return AnimateTicketArrival(ticket.transform, start, transformPoint);
            Destroy(ticket);
            activeTransferObject = null;

            GameObject materialVisual = CreateVisual(
                material.WorldPrefab,
                fallbackMaterialPrefab,
                PrimitiveType.Sphere,
                transformPoint,
                target.rotation,
                null);
            activeTransferObject = materialVisual;
            ApplyColor(materialVisual, material.EffectColor);
            yield return AnimateMaterialLanding(
                materialVisual.transform,
                transformPoint,
                target,
                material.MaterialForm);

            if (material.PlacementEffectPrefab != null)
            {
                GameObject effect = Instantiate(
                    material.PlacementEffectPrefab,
                    target.position,
                    target.rotation);
                Destroy(effect, 5f);
            }

            CraftLiveAudio.PlayMaterialLanding(material, audioSource);

            Destroy(materialVisual);
            activeTransferObject = null;
            session.CompleteCurrentPlacement();
            if (completionHoldSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(completionHoldSeconds);
            }

            session.ContinueAfterPlacement();
            transferCoroutine = null;
        }

        private IEnumerator AnimateTicketArrival(
            Transform ticket,
            Vector3 start,
            Vector3 end)
        {
            float duration = transferDuration * 0.55f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.LerpUnclamped(start, end, t);
                position.y += Mathf.Sin(t * Mathf.PI) * transferArcHeight;
                ticket.position = position;
                ticket.rotation = Quaternion.Euler(0f, 0f, t * 180f);
                yield return null;
            }
        }

        private IEnumerator AnimateMaterialLanding(
            Transform materialVisual,
            Vector3 start,
            Transform target,
            CraftLiveMaterialForm form)
        {
            float duration = transferDuration * 0.45f;
            float elapsed = 0f;
            Vector3 originalScale = materialVisual.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                Vector3 position = Vector3.LerpUnclamped(start, target.position, eased);
                switch (form)
                {
                    case CraftLiveMaterialForm.Gem:
                        position.y += Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) *
                                      transferArcHeight * (1f - t) * 0.3f;
                        materialVisual.localScale =
                            originalScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.12f);
                        break;
                    case CraftLiveMaterialForm.Charm:
                        materialVisual.rotation =
                            target.rotation * Quaternion.Euler(
                                0f,
                                0f,
                                Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * 18f);
                        break;
                    case CraftLiveMaterialForm.Spirit:
                        position += target.right *
                                    (Mathf.Sin(t * Mathf.PI * 2f) *
                                     transferArcHeight * (1f - t) * 0.2f);
                        position.y += Mathf.Sin(t * Mathf.PI) *
                                      transferArcHeight * 0.25f;
                        break;
                    default:
                        position.y += Mathf.Sin(t * Mathf.PI) *
                                      transferArcHeight * 0.12f;
                        materialVisual.rotation =
                            Quaternion.Slerp(
                                materialVisual.rotation,
                                target.rotation,
                                eased);
                        break;
                }

                materialVisual.position = position;
                yield return null;
            }

            materialVisual.SetPositionAndRotation(target.position, target.rotation);
            materialVisual.localScale = originalScale;
        }

        private void RefreshWeapon(string weaponId)
        {
            if (displayedWeaponId == weaponId)
            {
                return;
            }

            if (weaponObject != null)
            {
                Destroy(weaponObject);
            }

            displayedWeaponId = weaponId;
            CraftLiveWeaponDefinition weapon =
                session.Catalog != null ? session.Catalog.FindWeapon(weaponId) : null;
            if (weaponAnchor == null)
            {
                return;
            }

            weaponObject = CreateVisual(
                weapon != null ? weapon.WorkbenchPrefab : null,
                fallbackWeaponPrefab,
                PrimitiveType.Cube,
                weaponAnchor.position,
                weaponAnchor.rotation,
                weaponAnchor);
            if (weapon != null)
            {
                weaponObject.transform.localScale = weapon.PreviewScale;
            }
        }

        private void RefreshSlot(CraftLiveRoomState state, CraftLiveSlotId slot)
        {
            string materialId = state.slots.Get(slot) ?? string.Empty;
            displayedMaterials.TryGetValue(slot, out string previous);
            if (previous == materialId)
            {
                return;
            }

            displayedMaterials[slot] = materialId;
            if (slotObjects.TryGetValue(slot, out GameObject oldObject) && oldObject != null)
            {
                Destroy(oldObject);
            }

            slotObjects.Remove(slot);
            if (string.IsNullOrWhiteSpace(materialId))
            {
                return;
            }

            Transform anchor = FindAnchor(slot);
            CraftLiveMaterialDefinition material =
                session.Catalog != null ? session.Catalog.FindMaterial(materialId) : null;
            if (anchor == null || material == null)
            {
                return;
            }

            GameObject visual = CreateVisual(
                material.WorldPrefab,
                fallbackMaterialPrefab,
                PrimitiveType.Sphere,
                anchor.position,
                anchor.rotation,
                anchor);
            ApplyColor(visual, material.EffectColor);
            slotObjects[slot] = visual;
        }

        private Transform FindAnchor(CraftLiveSlotId slot)
        {
            foreach (SlotAnchor entry in slotAnchors)
            {
                if (entry != null && entry.slot == slot)
                {
                    return entry.anchor;
                }
            }

            return null;
        }

        private Transform FindArrivalEntry(CraftLiveSlotId slot)
        {
            foreach (SlotAnchor entry in slotAnchors)
            {
                if (entry != null && entry.slot == slot)
                {
                    return entry.arrivalEntry;
                }
            }

            return null;
        }

        private static GameObject CreateVisual(
            GameObject preferred,
            GameObject fallback,
            PrimitiveType primitive,
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            GameObject result;
            if (preferred != null || fallback != null)
            {
                result = Instantiate(preferred != null ? preferred : fallback, position, rotation, parent);
            }
            else
            {
                result = GameObject.CreatePrimitive(primitive);
                result.transform.SetPositionAndRotation(position, rotation);
                if (parent != null)
                {
                    result.transform.SetParent(parent, true);
                }

                if (result.TryGetComponent(out Collider collider))
                {
                    Destroy(collider);
                }
            }

            return result;
        }

        private static void ApplyColor(GameObject target, Color color)
        {
            if (target == null)
            {
                return;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                block.SetColor("_EmissionColor", color * 1.5f);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
