using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveTransferLauncherView : MonoBehaviour
    {
        [Serializable]
        private sealed class SlotExit
        {
            public CraftLiveSlotId slot = default;
            public Transform exit = null;
        }

        [SerializeField] private CraftLiveSession session;
        [SerializeField] private Transform ticketStart;
        [SerializeField] private Transform launcherSeat;
        [SerializeField] private Transform launcherArm;
        [SerializeField] private Transform springVisual;
        [SerializeField] private List<SlotExit> slotExits = new List<SlotExit>();
        [SerializeField] private GameObject fallbackTicketPrefab;
        [SerializeField, Min(0.05f)] private float loadingDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float launchDuration = 0.55f;
        [SerializeField, Min(0f)] private float launchArcHeight = 0.7f;
        [SerializeField] private Vector3 loadedArmEuler = new Vector3(-28f, 0f, 0f);
        [SerializeField] private Vector3 compressedSpringScale = new Vector3(1f, 0.55f, 1f);
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip loadingClip;
        [SerializeField] private AudioClip launchClip;
        [SerializeField] private UnityEvent onLoadingStarted;
        [SerializeField] private UnityEvent onLaunched;

        private Quaternion armRestRotation;
        private Vector3 springRestScale;
        private int handledTransferSerial = -1;
        private Coroutine launchCoroutine;
        private GameObject activeTicket;

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            armRestRotation = launcherArm != null
                ? launcherArm.localRotation
                : Quaternion.identity;
            springRestScale = springVisual != null
                ? springVisual.localScale
                : Vector3.one;
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

            if (launchCoroutine != null)
            {
                StopCoroutine(launchCoroutine);
                launchCoroutine = null;
            }

            if (activeTicket != null)
            {
                Destroy(activeTicket);
                activeTicket = null;
            }

            handledTransferSerial = -1;
            RestoreMechanism();
        }

        private void HandleStateChanged(CraftLiveRoomState state)
        {
            if (state == null ||
                state.placement.status != CraftLivePlacementStatus.Pad1Loading ||
                state.placement.transferSerial == handledTransferSerial ||
                launchCoroutine != null)
            {
                return;
            }

            handledTransferSerial = state.placement.transferSerial;
            launchCoroutine = StartCoroutine(Launch(state.Clone()));
        }

        private IEnumerator Launch(CraftLiveRoomState snapshot)
        {
            CraftLiveMaterialDefinition material = session.Catalog != null
                ? session.Catalog.FindMaterial(snapshot.placement.materialId)
                : null;
            Transform exit = FindExit(snapshot.placement.confirmedSlot);
            Vector3 start = ticketStart != null ? ticketStart.position : transform.position;
            Vector3 seat = launcherSeat != null ? launcherSeat.position : start;
            Vector3 end = exit != null ? exit.position : seat + transform.right * 1.5f;

            GameObject ticket = CreateTicket(material, start);
            activeTicket = ticket;
            onLoadingStarted?.Invoke();
            PlayOneShot(loadingClip);
            yield return AnimateLoading(ticket.transform, start, seat);

            session.MarkTransferLaunching();
            onLaunched?.Invoke();
            PlayOneShot(launchClip);
            yield return AnimateLaunch(ticket.transform, seat, end);

            Destroy(ticket);
            activeTicket = null;
            RestoreMechanism();
            session.MarkTransferArriving();
            launchCoroutine = null;
        }

        private IEnumerator AnimateLoading(Transform ticket, Vector3 start, Vector3 end)
        {
            Quaternion armStart = launcherArm != null
                ? launcherArm.localRotation
                : Quaternion.identity;
            Vector3 springStart = springVisual != null
                ? springVisual.localScale
                : Vector3.one;
            Quaternion armEnd = Quaternion.Euler(loadedArmEuler) * armRestRotation;
            float elapsed = 0f;
            while (elapsed < loadingDuration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(Mathf.Clamp01(elapsed / loadingDuration));
                ticket.position = Vector3.LerpUnclamped(start, end, t);
                if (launcherArm != null)
                {
                    launcherArm.localRotation = Quaternion.Slerp(armStart, armEnd, t);
                }

                if (springVisual != null)
                {
                    springVisual.localScale =
                        Vector3.LerpUnclamped(springStart, compressedSpringScale, t);
                }

                yield return null;
            }
        }

        private IEnumerator AnimateLaunch(Transform ticket, Vector3 start, Vector3 end)
        {
            Quaternion startRotation = ticket.rotation;
            float elapsed = 0f;
            while (elapsed < launchDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / launchDuration);
                Vector3 position = Vector3.LerpUnclamped(start, end, t);
                position.y += Mathf.Sin(t * Mathf.PI) * launchArcHeight;
                ticket.position = position;
                ticket.rotation = startRotation * Quaternion.Euler(0f, 0f, t * 220f);
                if (launcherArm != null)
                {
                    launcherArm.localRotation =
                        Quaternion.Slerp(
                            Quaternion.Euler(loadedArmEuler) * armRestRotation,
                            armRestRotation,
                            EaseOut(t));
                }

                if (springVisual != null)
                {
                    springVisual.localScale =
                        Vector3.LerpUnclamped(
                            compressedSpringScale,
                            springRestScale,
                            EaseOut(t));
                }

                yield return null;
            }
        }

        private GameObject CreateTicket(
            CraftLiveMaterialDefinition material,
            Vector3 position)
        {
            GameObject prefab = material != null && material.TransferTicketPrefab != null
                ? material.TransferTicketPrefab
                : fallbackTicketPrefab;
            if (prefab != null)
            {
                return Instantiate(prefab, position, Quaternion.identity);
            }

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "FallbackTransferTicket";
            fallback.transform.SetPositionAndRotation(position, Quaternion.identity);
            fallback.transform.localScale = new Vector3(0.35f, 0.5f, 0.04f);
            if (fallback.TryGetComponent(out Collider ticketCollider))
            {
                Destroy(ticketCollider);
            }

            return fallback;
        }

        private Transform FindExit(CraftLiveSlotId slot)
        {
            foreach (SlotExit slotExit in slotExits)
            {
                if (slotExit != null && slotExit.slot == slot)
                {
                    return slotExit.exit;
                }
            }

            return null;
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void RestoreMechanism()
        {
            if (launcherArm != null)
            {
                launcherArm.localRotation = armRestRotation;
            }

            if (springVisual != null)
            {
                springVisual.localScale = springRestScale;
            }
        }

        private static float EaseOut(float value)
        {
            return 1f - Mathf.Pow(1f - value, 3f);
        }
    }
}
