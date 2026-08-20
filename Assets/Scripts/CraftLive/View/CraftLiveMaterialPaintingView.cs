using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveMaterialPaintingView :
        MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        [Header("Optional Custom Prefab Bindings")]
        [SerializeField] private Transform movingRoot;
        [SerializeField] private Renderer[] tintRenderers =
            new Renderer[0];
        [SerializeField] private Collider[] interactionColliders =
            new Collider[0];
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private bool preserveIconAspect = true;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private TextMesh fallbackNameText;
        [SerializeField] private TextMesh fallbackStateText;

        [Header("Selection")]
        [SerializeField] private bool movePaintingOnSelection;
        [SerializeField] private Vector3 selectedOffset =
            new Vector3(0f, 0f, -0.55f);
        [SerializeField, Min(0f)] private float selectedCameraApproach = 0.18f;
        [SerializeField, Min(1f)] private float selectedScale = 1.06f;
        [SerializeField, Range(0f, 1f)] private float lockedBrightness = 0.2f;

        [Header("Events")]
        [SerializeField] private UnityEvent<Sprite> onIconChanged;
        [SerializeField] private UnityEvent<string> onNameChanged;
        [SerializeField] private UnityEvent<string> onCategoryChanged;
        [SerializeField] private UnityEvent<bool> onSelectedChanged;
        [SerializeField] private UnityEvent<bool> onLockedChanged;

        private CraftLivePad1GalleryController controller;
        private CraftLiveMaterialDefinition material;
        private Vector3 restingPosition;
        private Vector3 restingScale;
        private bool interactable;
        private bool viewportVisible = true;
        private Vector2 iconTargetSize;
        private Vector3 iconBaseScale;
        private CraftLiveGalleryColumn owningColumn;
        private float nextAllowedSelectTime;

        public CraftLiveMaterialDefinition Material => material;
        public bool Interactable => interactable;
        public bool ViewportVisible => viewportVisible;
        public Transform PresentationAnchor =>
            movingRoot != null ? movingRoot : transform;

        private void Awake()
        {
            if (movingRoot == null)
            {
                movingRoot = transform;
            }

            restingPosition = movingRoot.localPosition;
            restingScale = movingRoot.localScale;
            CaptureIconLayout();
            owningColumn = GetComponentInParent<CraftLiveGalleryColumn>();
        }

        public void ConfigureFallbackVisuals(
            Transform targetMovingRoot,
            Renderer[] renderers,
            Collider[] colliders,
            TextMesh nameText,
            TextMesh stateText)
        {
            movingRoot = targetMovingRoot != null
                ? targetMovingRoot
                : transform;
            tintRenderers = renderers ?? new Renderer[0];
            interactionColliders = colliders ?? new Collider[0];
            fallbackNameText = nameText;
            fallbackStateText = stateText;
            restingPosition = movingRoot.localPosition;
            restingScale = movingRoot.localScale;
        }

        public void CaptureRestingTransform()
        {
            if (movingRoot == null)
            {
                movingRoot = transform;
            }

            restingPosition = movingRoot.localPosition;
            restingScale = movingRoot.localScale;
        }

        public void UseFixedGalleryPosition()
        {
            movePaintingOnSelection = false;
            RestoreRestingTransform();
        }

        public void Bind(
            CraftLivePad1GalleryController owner,
            CraftLiveMaterialDefinition definition)
        {
            // A painting is the gallery's selection anchor. Moving that same
            // object toward the camera puts the frame in front of the detail
            // model/hologram, especially for pre-placed scene paintings that
            // do not pass through the runtime creation path.
            UseFixedGalleryPosition();
            gameObject.SetActive(true);
            controller = owner;
            material = definition;
            viewportVisible = true;
            if (material == null)
            {
                return;
            }

            string category =
                CraftLivePad1Presentation.GetCategoryLabel(
                    material.Category);
            onIconChanged?.Invoke(material.Icon);
            onNameChanged?.Invoke(material.DisplayName);
            onCategoryChanged?.Invoke(category);

            if (iconRenderer != null)
            {
                iconRenderer.sprite = material.Icon;
                iconRenderer.color = Color.white;
                FitIconToFrame();
            }

            if (fallbackNameText != null)
            {
                fallbackNameText.text = material.DisplayName;
            }
        }

        public void Unbind()
        {
            RestoreRestingTransform();
            controller = null;
            material = null;
            interactable = false;
            viewportVisible = false;
            onIconChanged?.Invoke(null);
            onNameChanged?.Invoke(string.Empty);
            onCategoryChanged?.Invoke(string.Empty);
            onSelectedChanged?.Invoke(false);
            onLockedChanged?.Invoke(false);
            if (iconRenderer != null)
            {
                iconRenderer.sprite = null;
            }

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(false);
            }

            RefreshColliders();
            gameObject.SetActive(false);
        }

        public void Refresh(
            CraftLiveRoomState state,
            CraftLiveSession session)
        {
            if (material == null || state == null || session == null)
            {
                return;
            }

            bool unlocked = session.IsMaterialUnlocked(material);
            bool selected = state.selectedMaterialId == material.MaterialId;
            interactable =
                unlocked &&
                (state.placement.status == CraftLivePlacementStatus.Idle ||
                 selected);

            onSelectedChanged?.Invoke(selected);
            onLockedChanged?.Invoke(!unlocked);
            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(!unlocked);
            }
            if (fallbackStateText != null)
            {
                fallbackStateText.text = unlocked
                    ? CraftLivePad1Presentation.GetCategoryLabel(
                        material.Category)
                    : "未登録";
            }

            ApplySelectionTransform(selected && movePaintingOnSelection);

            Color color = material.EffectColor;
            if (!unlocked)
            {
                color *= lockedBrightness;
                color.a = 1f;
            }
            else if (selected)
            {
                color = Color.Lerp(color, Color.white, 0.35f);
            }

            ApplyColor(color, selected);
            RefreshColliders();
        }

        public void SetViewportVisible(bool visible)
        {
            viewportVisible = visible;
            Renderer[] visibleRenderers =
                GetComponentsInChildren<Renderer>(true);
            foreach (Renderer targetRenderer in visibleRenderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.enabled = visible;
                }
            }

            if (fallbackNameText != null)
            {
                fallbackNameText.gameObject.SetActive(visible);
            }

            if (fallbackStateText != null)
            {
                fallbackStateText.gameObject.SetActive(visible);
            }

            RefreshColliders();
        }

        public void Select()
        {
            if (Time.unscaledTime < nextAllowedSelectTime ||
                !interactable ||
                !viewportVisible ||
                material == null)
            {
                return;
            }

            nextAllowedSelectTime = Time.unscaledTime + 0.1f;
            controller?.SelectMaterial(material, PresentationAnchor);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Select();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            ResolveOwningColumn();
            owningColumn?.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            ResolveOwningColumn();
            owningColumn?.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ResolveOwningColumn();
            owningColumn?.OnEndDrag(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            ResolveOwningColumn();
            owningColumn?.OnScroll(eventData);
        }

        private void OnMouseDown()
        {
            Select();
        }

        private void ApplyColor(Color color, bool selected)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            if (tintRenderers == null)
            {
                return;
            }

            foreach (Renderer targetRenderer in tintRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                // Prefab fields can accidentally be assigned renderers from
                // a wall or another asset. Never recolor objects outside this
                // painting hierarchy.
                Transform rendererTransform = targetRenderer.transform;
                if (rendererTransform != transform &&
                    !rendererTransform.IsChildOf(transform))
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                block.SetColor(
                    "_EmissionColor",
                    selected ? color * 0.45f : Color.black);
                targetRenderer.SetPropertyBlock(block);
            }
        }

        private void RefreshColliders()
        {
            if (interactionColliders == null)
            {
                return;
            }

            foreach (Collider targetCollider in interactionColliders)
            {
                if (targetCollider != null)
                {
                    targetCollider.enabled =
                        viewportVisible;
                }
            }
        }

        private void ResolveOwningColumn()
        {
            if (owningColumn == null)
            {
                owningColumn =
                    GetComponentInParent<CraftLiveGalleryColumn>();
            }
        }

        private void RestoreRestingTransform()
        {
            if (movingRoot == null)
            {
                movingRoot = transform;
            }

            movingRoot.localPosition = restingPosition;
            movingRoot.localScale = restingScale;
        }

        private void ApplySelectionTransform(bool selected)
        {
            if (movingRoot == null)
            {
                return;
            }

            movingRoot.localPosition = restingPosition;
            movingRoot.localScale =
                restingScale * (selected ? selectedScale : 1f);
            if (!selected)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                movingRoot.localPosition = restingPosition + selectedOffset;
                return;
            }

            Vector3 restingWorldPosition = movingRoot.parent != null
                ? movingRoot.parent.TransformPoint(restingPosition)
                : restingPosition;
            Vector3 towardCamera =
                camera.transform.position - restingWorldPosition;
            if (towardCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            movingRoot.position =
                restingWorldPosition +
                towardCamera.normalized * selectedCameraApproach;
        }

        private void CaptureIconLayout()
        {
            if (iconRenderer == null)
            {
                return;
            }

            iconTargetSize = iconRenderer.size;
            iconBaseScale = iconRenderer.transform.localScale;
        }

        private void FitIconToFrame()
        {
            if (iconRenderer == null || iconRenderer.sprite == null)
            {
                return;
            }

            if (iconTargetSize.x <= 0.0001f ||
                iconTargetSize.y <= 0.0001f)
            {
                CaptureIconLayout();
            }

            if (!preserveIconAspect)
            {
                iconRenderer.drawMode = SpriteDrawMode.Sliced;
                iconRenderer.size = iconTargetSize;
                iconRenderer.transform.localScale = iconBaseScale;
                return;
            }

            Vector2 spriteSize = iconRenderer.sprite.bounds.size;
            if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f)
            {
                return;
            }

            float fitScale = Mathf.Min(
                iconTargetSize.x / spriteSize.x,
                iconTargetSize.y / spriteSize.y);
            iconRenderer.drawMode = SpriteDrawMode.Simple;
            iconRenderer.transform.localScale = new Vector3(
                Mathf.Sign(iconBaseScale.x) * fitScale,
                Mathf.Sign(iconBaseScale.y) * fitScale,
                iconBaseScale.z);
        }
    }
}
