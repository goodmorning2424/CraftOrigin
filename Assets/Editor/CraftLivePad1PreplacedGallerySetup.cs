using System.Collections.Generic;
using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLivePad1PreplacedGallerySetup
    {
        private const string MenuPath =
            "Tools/Craft-live/Pad1/Set Up Preplaced Wall Galleries";
        private const string Pad1ScenePath =
            "Assets/Scenes/CraftLive/Pad1_MaterialGallery.unity";
        private const string DefaultFramePath =
            "Assets/Pad1/Prefab/Gakubuti.prefab";
        private const int SlotCapacity = 4;

        private struct Layout
        {
            public Vector2 paintingSize;
            public float spacing;
            public float firstY;
            public float viewportTop;
            public float viewportBottom;
            public Vector3 galleryPosition;
        }

        [MenuItem(MenuPath)]
        public static void Setup()
        {
            Scene scene = EditorSceneManager.OpenScene(
                Pad1ScenePath,
                OpenSceneMode.Single);
            CraftLivePad1Bindings bindings =
                FindSingle<CraftLivePad1Bindings>(scene);
            CraftLivePad1GalleryController controller =
                FindSingle<CraftLivePad1GalleryController>(scene);
            if (bindings == null || controller == null)
            {
                Debug.LogError(
                    "Craft-live: Pad1 bindings or gallery controller is " +
                    "missing. The scene was not changed.");
                return;
            }

            SerializedObject controllerObject =
                new SerializedObject(controller);
            CraftLiveMaterialPaintingView framePrefab =
                controllerObject.FindProperty("paintingPrefab")
                    ?.objectReferenceValue as
                    CraftLiveMaterialPaintingView;
            if (framePrefab == null)
            {
                GameObject defaultFrame =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        DefaultFramePath);
                framePrefab = defaultFrame != null
                    ? defaultFrame.GetComponent<
                        CraftLiveMaterialPaintingView>()
                    : null;
            }

            if (framePrefab == null)
            {
                Debug.LogError(
                    "Craft-live: a frame prefab with " +
                    "CraftLiveMaterialPaintingView is required. The scene " +
                    "was not changed.",
                    controller);
                return;
            }

            Camera camera = controllerObject
                .FindProperty("targetCamera")
                ?.objectReferenceValue as Camera;
            float horizontalPadding = GetFloat(
                controllerObject,
                "wallHorizontalPadding",
                0.15f);
            float verticalPadding = GetFloat(
                controllerObject,
                "wallVerticalPadding",
                0.3f);
            float gap = GetFloat(
                controllerObject,
                "paintingGap",
                0.18f);
            float frontOffset = GetFloat(
                controllerObject,
                "wallFrontOffset",
                0.03f);
            int visibleCount = Mathf.Max(
                1,
                GetInt(controllerObject, "visiblePaintings", 3));

            bool changed = false;
            changed |= ConfigureWall(
                scene,
                bindings.PowerUpWall,
                CraftLiveMaterialCategory.Upgrade,
                "パワーアップ",
                framePrefab,
                camera,
                horizontalPadding,
                verticalPadding,
                gap,
                frontOffset,
                visibleCount);
            changed |= ConfigureWall(
                scene,
                bindings.SkillWall,
                CraftLiveMaterialCategory.Skill,
                "スキル",
                framePrefab,
                camera,
                horizontalPadding,
                verticalPadding,
                gap,
                frontOffset,
                visibleCount);
            changed |= ConfigureWall(
                scene,
                bindings.TypeWall,
                CraftLiveMaterialCategory.Attribute,
                "タイプ",
                framePrefab,
                camera,
                horizontalPadding,
                verticalPadding,
                gap,
                frontOffset,
                visibleCount);
            changed |= ConfigureWallSlider(
                controllerObject,
                controller,
                bindings,
                camera);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeObject = controller.gameObject;
            Debug.Log(
                "Craft-live: preplaced Pad1 wall galleries are ready. " +
                "Their scene overrides can now be adjusted visually.",
                controller);
        }

        private static bool ConfigureWall(
            Scene scene,
            Transform wallRoot,
            CraftLiveMaterialCategory category,
            string header,
            CraftLiveMaterialPaintingView framePrefab,
            Camera camera,
            float horizontalPadding,
            float verticalPadding,
            float gap,
            float frontOffset,
            int visibleCount)
        {
            if (wallRoot == null)
            {
                Debug.LogWarning(
                    $"Craft-live: the {category} wall is not assigned.");
                return false;
            }

            CraftLiveGalleryWallView wallView =
                wallRoot.GetComponent<CraftLiveGalleryWallView>();
            bool changed = false;
            if (wallView == null)
            {
                wallView = Undo.AddComponent<
                    CraftLiveGalleryWallView>(wallRoot.gameObject);
                changed = true;
            }

            CraftLiveGalleryColumn column =
                wallRoot.GetComponent<CraftLiveGalleryColumn>();
            if (column == null)
            {
                column = Undo.AddComponent<CraftLiveGalleryColumn>(
                    wallRoot.gameObject);
                changed = true;
            }

            Transform galleryRoot = wallRoot.Find("PreplacedGallery");
            bool newLayout = galleryRoot == null;
            if (newLayout)
            {
                GameObject galleryObject = new GameObject(
                    "PreplacedGallery");
                Undo.RegisterCreatedObjectUndo(
                    galleryObject,
                    "Create Pad1 preplaced gallery");
                galleryRoot = galleryObject.transform;
                galleryRoot.SetParent(wallRoot, false);
                changed = true;
            }

            if (!TryGetLocalBounds(
                    wallRoot,
                    galleryRoot,
                    out Bounds wallBounds))
            {
                Debug.LogWarning(
                    $"Craft-live: {wallRoot.name} has no usable wall " +
                    "renderer or collider. Its existing layout was kept.",
                    wallRoot);
                return changed;
            }

            Layout layout = CalculateLayout(
                wallRoot,
                wallBounds,
                camera,
                horizontalPadding,
                verticalPadding,
                gap,
                frontOffset,
                visibleCount);
            if (newLayout)
            {
                galleryRoot.localPosition = layout.galleryPosition;
                galleryRoot.localRotation = Quaternion.identity;
                galleryRoot.localScale = Vector3.one;
            }

            TextMesh headerText = EnsureText(
                galleryRoot,
                "Header",
                header,
                new Vector3(
                    0f,
                    layout.viewportTop + 0.25f,
                    0f),
                0.055f,
                ref changed);
            Transform contentRoot = EnsureChild(
                galleryRoot,
                "ScrollContent",
                ref changed);
            TextMesh emptyText = EnsureText(
                galleryRoot,
                "EmptyState",
                "QRで素材を登録",
                Vector3.zero,
                0.045f,
                ref changed);
            GameObject emptyRoot = emptyText.gameObject;
            emptyRoot.SetActive(false);

            List<CraftLiveMaterialPaintingView> slots =
                CollectSlots(contentRoot);
            while (slots.Count < SlotCapacity)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(
                    framePrefab.gameObject,
                    scene) as GameObject;
                if (instance == null)
                {
                    break;
                }

                Undo.RegisterCreatedObjectUndo(
                    instance,
                    "Create Pad1 frame slot");
                instance.name = $"FrameSlot_{slots.Count}";
                instance.transform.SetParent(contentRoot, false);
                instance.transform.localPosition = new Vector3(
                    0f,
                    layout.firstY - slots.Count * layout.spacing,
                    0f);
                instance.transform.localRotation = Quaternion.identity;
                EnsureNonZeroScale(instance.transform);
                FitToTargetSize(
                    instance.transform,
                    contentRoot,
                    layout.paintingSize);
                CraftLiveMaterialPaintingView slot =
                    instance.GetComponent<
                        CraftLiveMaterialPaintingView>();
                if (slot == null)
                {
                    Object.DestroyImmediate(instance);
                    break;
                }

                RepairSlotBindings(slot, camera);
                slots.Add(slot);
                changed = true;
            }

            SerializedObject wallObject = new SerializedObject(wallView);
            wallObject.FindProperty("category").enumValueIndex =
                (int)category;
            wallObject.FindProperty("slideRoot").objectReferenceValue =
                wallRoot;
            wallObject.FindProperty("contentRoot").objectReferenceValue =
                contentRoot;
            wallObject.FindProperty("column").objectReferenceValue = column;
            wallObject.FindProperty("headerText").objectReferenceValue =
                headerText;
            wallObject.FindProperty("emptyStateRoot").objectReferenceValue =
                emptyRoot;
            wallObject.FindProperty("emptyStateText").objectReferenceValue =
                emptyText;
            wallObject.FindProperty("itemSpacing").floatValue =
                layout.spacing;
            wallObject.FindProperty("viewportTop").floatValue =
                layout.viewportTop;
            wallObject.FindProperty("viewportBottom").floatValue =
                layout.viewportBottom;
            SerializedProperty slotProperty =
                wallObject.FindProperty("frameSlots");
            slotProperty.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                slotProperty.GetArrayElementAtIndex(i)
                    .objectReferenceValue = slots[i];
            }

            changed |= wallObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(wallView);
            EditorUtility.SetDirty(column);
            return changed;
        }

        private static bool ConfigureWallSlider(
            SerializedObject controllerObject,
            CraftLivePad1GalleryController controller,
            CraftLivePad1Bindings bindings,
            Camera camera)
        {
            CraftLiveGalleryWallView[] walls =
            {
                FindWallView(bindings.PowerUpWall),
                FindWallView(bindings.SkillWall),
                FindWallView(bindings.TypeWall)
            };
            foreach (CraftLiveGalleryWallView wall in walls)
            {
                if (wall == null)
                {
                    Debug.LogWarning(
                        "Craft-live: horizontal wall sliding was not " +
                        "configured because a preplaced wall view is " +
                        "missing.",
                        controller);
                    return false;
                }
            }

            bool changed = false;
            CraftLiveGalleryWallSlider slider =
                controller.GetComponent<CraftLiveGalleryWallSlider>();
            if (slider == null)
            {
                slider = Undo.AddComponent<CraftLiveGalleryWallSlider>(
                    controller.gameObject);
                changed = true;
            }

            SerializedObject sliderObject = new SerializedObject(slider);
            sliderObject.FindProperty("targetCamera").objectReferenceValue =
                camera;
            SerializedProperty wallsProperty =
                sliderObject.FindProperty("walls");
            wallsProperty.arraySize = walls.Length;
            for (int i = 0; i < walls.Length; i++)
            {
                wallsProperty.GetArrayElementAtIndex(i)
                    .objectReferenceValue = walls[i];
            }

            changed |= sliderObject.ApplyModifiedPropertiesWithoutUndo();
            controllerObject.Update();
            controllerObject.FindProperty("wallSlider")
                .objectReferenceValue = slider;
            changed |= controllerObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(slider);
            EditorUtility.SetDirty(controller);
            return changed;
        }

        private static CraftLiveGalleryWallView FindWallView(
            Transform root)
        {
            if (root == null)
            {
                return null;
            }

            CraftLiveGalleryWallView wall =
                root.GetComponent<CraftLiveGalleryWallView>();
            return wall != null
                ? wall
                : root.GetComponentInChildren<
                    CraftLiveGalleryWallView>(true);
        }

        private static void RepairSlotBindings(
            CraftLiveMaterialPaintingView slot,
            Camera camera)
        {
            BoxCollider collider = slot.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(slot.gameObject);
                if (TryGetRendererBoundsRelativeTo(
                        slot.transform,
                        slot.transform,
                        out Bounds bounds))
                {
                    collider.center = bounds.center;
                    collider.size = bounds.size;
                }
            }

            SpriteRenderer artwork =
                slot.transform.Find("Artwork")
                    ?.GetComponent<SpriteRenderer>();
            if (artwork == null &&
                TryGetRendererBoundsRelativeTo(
                    slot.transform,
                    slot.transform,
                    out Bounds frameBounds))
            {
                GameObject artworkObject = new GameObject("Artwork");
                Undo.RegisterCreatedObjectUndo(
                    artworkObject,
                    "Create Pad1 artwork surface");
                artworkObject.transform.SetParent(slot.transform, false);
                float cameraZ = camera != null
                    ? slot.transform.InverseTransformPoint(
                        camera.transform.position).z
                    : frameBounds.center.z + frameBounds.extents.z;
                float direction = cameraZ >= frameBounds.center.z
                    ? 1f
                    : -1f;
                artworkObject.transform.localPosition = new Vector3(
                    frameBounds.center.x,
                    frameBounds.center.y,
                    frameBounds.center.z +
                    direction * (frameBounds.extents.z + 0.01f));
                artwork = artworkObject.AddComponent<SpriteRenderer>();
                artwork.drawMode = SpriteDrawMode.Sliced;
                artwork.size = new Vector2(
                    Mathf.Max(0.01f, frameBounds.size.x * 0.72f),
                    Mathf.Max(0.01f, frameBounds.size.y * 0.72f));
                artwork.sortingOrder = 1;
            }

            SerializedObject slotObject = new SerializedObject(slot);
            slotObject.FindProperty("movingRoot").objectReferenceValue =
                slot.transform;
            slotObject.FindProperty("iconRenderer").objectReferenceValue =
                artwork;
            SerializedProperty tintProperty =
                slotObject.FindProperty("tintRenderers");
            tintProperty.arraySize = 0;
            SerializedProperty colliderProperty =
                slotObject.FindProperty("interactionColliders");
            colliderProperty.arraySize = collider != null ? 1 : 0;
            if (collider != null)
            {
                colliderProperty.GetArrayElementAtIndex(0)
                    .objectReferenceValue = collider;
            }

            slotObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(slot);
        }

        private static Layout CalculateLayout(
            Transform wallRoot,
            Bounds wallBounds,
            Camera camera,
            float horizontalPadding,
            float verticalPadding,
            float gap,
            float frontOffset,
            int visibleCount)
        {
            float width = Mathf.Max(
                0.1f,
                wallBounds.size.x - horizontalPadding * 2f);
            float availableHeight = Mathf.Max(
                0.1f,
                wallBounds.size.y - verticalPadding * 2f);
            float resolvedGap = Mathf.Min(
                gap,
                availableHeight / visibleCount);
            float height = Mathf.Max(
                0.1f,
                (availableHeight -
                 resolvedGap * (visibleCount - 1)) /
                visibleCount);
            float top = wallBounds.size.y * 0.5f - verticalPadding;
            float cameraZ = camera != null
                ? wallRoot.InverseTransformPoint(
                    camera.transform.position).z
                : wallBounds.center.z - wallBounds.size.z;
            float direction = cameraZ >= wallBounds.center.z ? 1f : -1f;
            Vector3 center = wallBounds.center;
            center.z += direction *
                (wallBounds.size.z * 0.5f + frontOffset);
            return new Layout
            {
                paintingSize = new Vector2(width, height),
                spacing = height + resolvedGap,
                firstY = top - height * 0.5f,
                viewportTop = top,
                viewportBottom =
                    -wallBounds.size.y * 0.5f + verticalPadding,
                galleryPosition = center
            };
        }

        private static bool TryGetLocalBounds(
            Transform wallRoot,
            Transform ignoredRoot,
            out Bounds bounds)
        {
            bool found = false;
            bounds = default;
            foreach (Renderer renderer in
                     wallRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null ||
                    (ignoredRoot != null &&
                     renderer.transform.IsChildOf(ignoredRoot)))
                {
                    continue;
                }

                EncapsulateWorldBounds(
                    wallRoot,
                    renderer.bounds,
                    ref bounds,
                    ref found);
            }

            foreach (Collider collider in
                     wallRoot.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null ||
                    (ignoredRoot != null &&
                     collider.transform.IsChildOf(ignoredRoot)))
                {
                    continue;
                }

                EncapsulateWorldBounds(
                    wallRoot,
                    collider.bounds,
                    ref bounds,
                    ref found);
            }

            return found &&
                   bounds.size.x > Mathf.Epsilon &&
                   bounds.size.y > Mathf.Epsilon;
        }

        private static bool TryGetRendererBoundsRelativeTo(
            Transform root,
            Transform relativeTo,
            out Bounds bounds)
        {
            bool found = false;
            bounds = default;
            foreach (Renderer renderer in
                     root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null ||
                    renderer is SpriteRenderer)
                {
                    continue;
                }

                EncapsulateWorldBounds(
                    relativeTo,
                    renderer.bounds,
                    ref bounds,
                    ref found);
            }

            return found;
        }

        private static void EncapsulateWorldBounds(
            Transform relativeTo,
            Bounds worldBounds,
            ref Bounds result,
            ref bool found)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };
            foreach (Vector3 corner in corners)
            {
                Vector3 local = relativeTo.InverseTransformPoint(corner);
                if (!found)
                {
                    result = new Bounds(local, Vector3.zero);
                    found = true;
                }
                else
                {
                    result.Encapsulate(local);
                }
            }
        }

        private static void FitToTargetSize(
            Transform target,
            Transform relativeTo,
            Vector2 targetSize)
        {
            if (!TryGetRendererBoundsRelativeTo(
                    target,
                    relativeTo,
                    out Bounds bounds) ||
                bounds.size.x <= 0.0001f ||
                bounds.size.y <= 0.0001f)
            {
                return;
            }

            float factor = Mathf.Min(
                targetSize.x / bounds.size.x,
                targetSize.y / bounds.size.y);
            target.localScale *= Mathf.Max(0.0001f, factor);
        }

        private static void EnsureNonZeroScale(Transform target)
        {
            Vector3 scale = target.localScale;
            if (Mathf.Abs(scale.x) <= 0.0001f ||
                Mathf.Abs(scale.y) <= 0.0001f ||
                Mathf.Abs(scale.z) <= 0.0001f)
            {
                target.localScale = Vector3.one * 0.1f;
            }
        }

        private static Transform EnsureChild(
            Transform parent,
            string name,
            ref bool changed)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(
                childObject,
                $"Create {name}");
            child = childObject.transform;
            child.SetParent(parent, false);
            changed = true;
            return child;
        }

        private static TextMesh EnsureText(
            Transform parent,
            string name,
            string value,
            Vector3 position,
            float characterSize,
            ref bool changed)
        {
            Transform child = parent.Find(name);
            TextMesh text = child != null
                ? child.GetComponent<TextMesh>()
                : null;
            if (text == null)
            {
                GameObject textObject = child != null
                    ? child.gameObject
                    : new GameObject(name);
                if (child == null)
                {
                    Undo.RegisterCreatedObjectUndo(
                        textObject,
                        $"Create {name}");
                    textObject.transform.SetParent(parent, false);
                }

                text = Undo.AddComponent<TextMesh>(textObject);
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 64;
                changed = true;
            }

            text.text = value;
            text.characterSize = characterSize;
            text.transform.localPosition = position;
            return text;
        }

        private static List<CraftLiveMaterialPaintingView> CollectSlots(
            Transform contentRoot)
        {
            List<CraftLiveMaterialPaintingView> result =
                new List<CraftLiveMaterialPaintingView>();
            foreach (CraftLiveMaterialPaintingView slot in
                     contentRoot.GetComponentsInChildren<
                         CraftLiveMaterialPaintingView>(true))
            {
                if (slot != null && !result.Contains(slot))
                {
                    result.Add(slot);
                }
            }

            result.Sort((left, right) =>
                string.CompareOrdinal(left.name, right.name));
            return result;
        }

        private static T FindSingle<T>(Scene scene)
            where T : Component
        {
            T result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T candidate in
                         root.GetComponentsInChildren<T>(true))
                {
                    if (result != null)
                    {
                        Debug.LogError(
                            $"Craft-live: {scene.path} contains multiple " +
                            $"{typeof(T).Name} components.");
                        return null;
                    }

                    result = candidate;
                }
            }

            return result;
        }

        private static float GetFloat(
            SerializedObject target,
            string propertyName,
            float fallback)
        {
            SerializedProperty property =
                target.FindProperty(propertyName);
            return property != null ? property.floatValue : fallback;
        }

        private static int GetInt(
            SerializedObject target,
            string propertyName,
            int fallback)
        {
            SerializedProperty property =
                target.FindProperty(propertyName);
            return property != null ? property.intValue : fallback;
        }
    }
}
