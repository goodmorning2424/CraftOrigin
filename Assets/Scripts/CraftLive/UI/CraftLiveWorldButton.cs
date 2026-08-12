using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    [RequireComponent(typeof(Collider))]
    public sealed class CraftLiveWorldButton :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        [SerializeField] private Transform pressTarget;
        [SerializeField] private Renderer[] renderers =
            new Renderer[0];
        [SerializeField] private Color normalColor = new Color(0.45f, 0.32f, 0.2f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.65f, 0.48f, 0.28f, 1f);
        [SerializeField] private Color pressedColor = new Color(1f, 0.78f, 0.3f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.25f, 0.8f, 1f, 1f);
        [SerializeField] private Color disabledColor = new Color(0.16f, 0.16f, 0.16f, 1f);
        [SerializeField, Min(0f)] private float pressDepth = 0.025f;
        [SerializeField, Min(0.01f)] private float animationDuration = 0.08f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.15f;
        [SerializeField] private bool interactable = true;
        [SerializeField] private bool selected;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pressClip;
        [SerializeField] private UnityEvent onPressed =
            new UnityEvent();

        private Vector3 restLocalPosition;
        private Coroutine animationCoroutine;
        private bool hovering;
        private bool pointerDown;
        private int activePointerId = int.MinValue;
        private float nextPressTime;

        private void Awake()
        {
            if (onPressed == null)
            {
                onPressed = new UnityEvent();
            }

            if (pressTarget == null)
            {
                pressTarget = transform;
            }

            if (renderers == null || renderers.Length == 0)
            {
                Renderer targetRenderer = pressTarget.GetComponent<Renderer>();
                renderers = targetRenderer != null
                    ? new[] { targetRenderer }
                    : new Renderer[0];
            }

            CraftLiveForgeUITheme.GetButtonPalette(
                normalColor,
                out normalColor,
                out hoverColor,
                out pressedColor,
                out selectedColor,
                out disabledColor);
            CraftLiveForgeUITheme.BuildButtonFrame(
                pressTarget,
                normalColor);

            restLocalPosition = pressTarget.localPosition;
            RefreshVisual();
        }

        private void OnDisable()
        {
            hovering = false;
            pointerDown = false;
            activePointerId = int.MinValue;
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            if (pressTarget != null)
            {
                pressTarget.localPosition = restLocalPosition;
            }
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!interactable)
            {
                pointerDown = false;
                activePointerId = int.MinValue;
            }

            RefreshVisual();
        }

        public void SetDisabledColor(Color value)
        {
            disabledColor = value;
            RefreshVisual();
        }

        public void Configure(
            Transform target,
            Renderer[] targetRenderers,
            Color targetNormalColor,
            Color targetHoverColor,
            Color targetPressedColor)
        {
            pressTarget = target != null ? target : transform;
            renderers = targetRenderers ?? new Renderer[0];
            CraftLiveForgeUITheme.GetButtonPalette(
                targetNormalColor,
                out normalColor,
                out hoverColor,
                out pressedColor,
                out selectedColor,
                out disabledColor);
            CraftLiveForgeUITheme.BuildButtonFrame(
                pressTarget,
                targetNormalColor);
            restLocalPosition = pressTarget.localPosition;
            RefreshVisual();
        }

        public void AddListener(UnityEngine.Events.UnityAction listener)
        {
            if (listener == null)
            {
                return;
            }

            if (onPressed == null)
            {
                onPressed = new UnityEvent();
            }

            onPressed.AddListener(listener);
        }

        public void SetSelected(bool value)
        {
            selected = value;
            RefreshVisual();
        }

        public void Press()
        {
            if (!interactable || Time.unscaledTime < nextPressTime)
            {
                return;
            }

            nextPressTime = Time.unscaledTime + cooldownSeconds;
            if (audioSource != null && pressClip != null)
            {
                audioSource.PlayOneShot(pressClip);
            }

            onPressed?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            RefreshVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (activePointerId != int.MinValue &&
                eventData.pointerId != activePointerId)
            {
                return;
            }

            hovering = false;
            pointerDown = false;
            activePointerId = int.MinValue;
            RefreshVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable)
            {
                return;
            }

            if (activePointerId != int.MinValue &&
                eventData.pointerId != activePointerId)
            {
                return;
            }

            activePointerId = eventData.pointerId;
            pointerDown = true;
            RefreshVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (activePointerId != eventData.pointerId)
            {
                return;
            }

            pointerDown = false;
            activePointerId = int.MinValue;
            RefreshVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (activePointerId != int.MinValue &&
                activePointerId != eventData.pointerId)
            {
                return;
            }

            Press();
        }

        private void RefreshVisual()
        {
            Color color = !interactable
                ? disabledColor
                : pointerDown
                    ? pressedColor
                    : selected
                        ? selectedColor
                        : hovering
                            ? hoverColor
                            : normalColor;
            ApplyColor(color);

            if (pressTarget == null)
            {
                return;
            }

            Vector3 target = restLocalPosition +
                             (pointerDown && interactable
                                 ? Vector3.down * pressDepth
                                 : Vector3.zero);
            if (isActiveAndEnabled)
            {
                if (animationCoroutine != null)
                {
                    StopCoroutine(animationCoroutine);
                }

                animationCoroutine = StartCoroutine(AnimatePosition(target));
            }
            else
            {
                pressTarget.localPosition = target;
            }
        }

        private IEnumerator AnimatePosition(Vector3 target)
        {
            Vector3 start = pressTarget.localPosition;
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                t = t * t * (3f - 2f * t);
                pressTarget.localPosition = Vector3.LerpUnclamped(start, target, t);
                yield return null;
            }

            pressTarget.localPosition = target;
            animationCoroutine = null;
        }

        private void ApplyColor(Color color)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                CraftLiveForgeUITheme.ApplyInteractiveSurface(
                    targetRenderer,
                    color,
                    selected,
                    hovering || pointerDown);
            }
        }
    }
}
