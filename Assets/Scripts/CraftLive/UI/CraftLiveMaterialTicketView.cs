using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveMaterialTicketView :
        MonoBehaviour,
        IPointerClickHandler
    {
        [Header("Visual")]
        [SerializeField] private Transform movingRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Vector3 selectedLocalOffset = new Vector3(0f, 0f, -0.08f);
        [SerializeField] private Vector3 dropLocalOffset = new Vector3(0f, 0.6f, 0f);
        [SerializeField, Min(0.05f)] private float moveDuration = 0.22f;
        [SerializeField, Min(0f)] private float dimmedAlpha = 0.35f;
        [SerializeField, Min(1f)] private float incrementScale = 1.08f;

        [Header("Bindings")]
        [SerializeField] private UnityEvent<Sprite> onIconChanged;
        [SerializeField] private UnityEvent<string> onNameChanged;
        [SerializeField] private UnityEvent<string> onCategoryChanged;
        [SerializeField] private UnityEvent<string> onCountChanged;
        [SerializeField] private UnityEvent<string> onDescriptionChanged;
        [SerializeField] private UnityEvent<string> onAbilityChanged;
        [SerializeField] private UnityEvent<string> onUsageChanged;
        [SerializeField] private UnityEvent<bool> onDetailsVisible;
        [SerializeField] private UnityEvent<string> onIncrementFeedback;

        private CraftLiveSession session;
        private CraftLiveMaterialDefinition material;
        private Vector3 restingLocalPosition;
        private Vector3 restingLocalScale;
        private Coroutine animationCoroutine;
        private bool interactable;
        private bool selected;

        public CraftLiveMaterialDefinition Material => material;

        private void Awake()
        {
            if (movingRoot == null)
            {
                movingRoot = transform;
            }

            restingLocalPosition = movingRoot.localPosition;
            restingLocalScale = movingRoot.localScale;
        }

        private void OnDisable()
        {
            StopAnimation();
            if (movingRoot != null)
            {
                movingRoot.localPosition = restingLocalPosition;
                movingRoot.localScale = restingLocalScale;
            }
        }

        public void Bind(
            CraftLiveSession targetSession,
            CraftLiveMaterialDefinition definition,
            int count)
        {
            session = targetSession;
            material = definition;
            if (material == null)
            {
                return;
            }

            onIconChanged?.Invoke(material.Icon);
            onNameChanged?.Invoke(material.DisplayName);
            onCategoryChanged?.Invoke(GetCategoryLabel(material.Category));
            onDescriptionChanged?.Invoke(material.Description);
            onAbilityChanged?.Invoke(material.AbilitySummary);
            onUsageChanged?.Invoke(material.UsageSummary);
            SetCount(count);
        }

        public void SetCount(int count)
        {
            onCountChanged?.Invoke($"×{Mathf.Max(0, count)}");
        }

        public void SetState(bool isSelected, bool canInteract)
        {
            selected = isSelected;
            interactable = canInteract || selected;
            onDetailsVisible?.Invoke(selected);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = interactable ? 1f : dimmedAlpha;
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = interactable;
            }

            Vector3 target = restingLocalPosition +
                             (selected ? selectedLocalOffset : Vector3.zero);
            StartMove(target, restingLocalScale, moveDuration);
        }

        public void Select()
        {
            if (interactable && material != null)
            {
                session?.SelectMaterial(material);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Select();
        }

        public void PlayDropIn()
        {
            if (movingRoot == null)
            {
                return;
            }

            movingRoot.localPosition = restingLocalPosition + dropLocalOffset;
            movingRoot.localScale = restingLocalScale * 0.94f;
            StartMove(restingLocalPosition, restingLocalScale, moveDuration * 1.8f);
        }

        public void PlayIncrement(int amount)
        {
            onIncrementFeedback?.Invoke($"×{Mathf.Max(1, amount)}追加");
            StartCoroutine(IncrementPulse());
        }

        private IEnumerator IncrementPulse()
        {
            SetEmission(Color.white * 2f);
            StartMove(
                restingLocalPosition + (selected ? selectedLocalOffset : Vector3.zero),
                restingLocalScale * incrementScale,
                moveDuration * 0.5f);
            yield return new WaitForSecondsRealtime(moveDuration * 0.5f);
            SetEmission(Color.black);
            StartMove(
                restingLocalPosition + (selected ? selectedLocalOffset : Vector3.zero),
                restingLocalScale,
                moveDuration * 0.5f);
        }

        private void StartMove(Vector3 position, Vector3 scale, float duration)
        {
            StopAnimation();
            animationCoroutine = StartCoroutine(MoveTo(position, scale, duration));
        }

        private IEnumerator MoveTo(Vector3 position, Vector3 scale, float duration)
        {
            Vector3 startPosition = movingRoot.localPosition;
            Vector3 startScale = movingRoot.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                movingRoot.localPosition = Vector3.LerpUnclamped(startPosition, position, t);
                movingRoot.localScale = Vector3.LerpUnclamped(startScale, scale, t);
                yield return null;
            }

            movingRoot.localPosition = position;
            movingRoot.localScale = scale;
            animationCoroutine = null;
        }

        private void StopAnimation()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
        }

        private void SetEmission(Color color)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", color);
                targetRenderer.SetPropertyBlock(block);
            }
        }

        private static string GetCategoryLabel(CraftLiveMaterialCategory category)
        {
            switch (category)
            {
                case CraftLiveMaterialCategory.Attribute:
                    return "属性";
                case CraftLiveMaterialCategory.Skill:
                    return "能力";
                default:
                    return "強化";
            }
        }
    }
}
