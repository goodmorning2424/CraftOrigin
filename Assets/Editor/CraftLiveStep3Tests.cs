using System.Collections.Generic;
using System.Reflection;
using CraftOrigin.CraftLive;
using CraftOrigin.CraftLiveEditor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CraftOrigin.CraftLiveTests
{
    public sealed class CraftLiveStep3Tests
    {
        private readonly List<Object> createdObjects =
            new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
            CraftLiveGalleryWallView.ResetNameplateReference();
        }

        [TestCase(
            CraftLiveMaterialCategory.Upgrade,
            "パワーアップ")]
        [TestCase(
            CraftLiveMaterialCategory.Skill,
            "スキル")]
        [TestCase(
            CraftLiveMaterialCategory.Attribute,
            "タイプ")]
        public void Presentation_UsesLatestCategoryLabels(
            CraftLiveMaterialCategory category,
            string expected)
        {
            Assert.That(
                CraftLivePad1Presentation.GetCategoryLabel(category),
                Is.EqualTo(expected));
        }

        [Test]
        public void SynchronizedStartScreen_IsPlacedInFrontOfSceneCamera()
        {
            GameObject cameraObject = new GameObject("StartScreenCamera");
            createdObjects.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;

            GameObject displayRoot = new GameObject("DisplayRoot");
            createdObjects.Add(displayRoot);
            GameObject underlying = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            underlying.name = "UnderlyingPadVisual";
            createdObjects.Add(underlying);
            Renderer underlyingRenderer = underlying.GetComponent<Renderer>();
            GameObject host = new GameObject("StartScreenHost");
            createdObjects.Add(host);
            CraftLiveSynchronizedStartScreen screen =
                host.AddComponent<CraftLiveSynchronizedStartScreen>();
            SetField(screen, "displayRoot", displayRoot.transform);

            MethodInfo build = typeof(CraftLiveSynchronizedStartScreen)
                .GetMethod(
                    "BuildScreen",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null);
            build.Invoke(screen, null);

            Transform generated = displayRoot.transform.Find(
                "Generated_SynchronizedStartScreen");
            Assert.That(generated, Is.Not.Null);
            Vector3 viewport = camera.WorldToViewportPoint(
                generated.position);
            Assert.That(viewport.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(viewport.y, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane));
            Assert.That(viewport.z, Is.EqualTo(1f).Within(0.05f));
            Assert.That(
                generated.Find("StartButtonFace"),
                Is.Null);
            Assert.That(
                generated.Find("StartButtonLabel"),
                Is.Null);
            Transform backing = generated.Find("CastIronShadow");
            Assert.That(backing, Is.Not.Null);
            Transform occluder = generated.Find("FullViewportOccluder");
            Assert.That(occluder, Is.Not.Null);
            float visibleHeight = 2f * viewport.z * Mathf.Tan(
                camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visibleWidth = visibleHeight * camera.aspect;
            Assert.That(
                backing.lossyScale.x,
                Is.GreaterThanOrEqualTo(visibleWidth));
            Assert.That(
                backing.lossyScale.y,
                Is.GreaterThanOrEqualTo(visibleHeight));
            Assert.That(underlyingRenderer.enabled, Is.False);
            Assert.That(
                underlying.GetComponent<Collider>().enabled,
                Is.False);

            underlyingRenderer.enabled = true;
            GameObject lateVisual = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            lateVisual.name = "LatePadVisual";
            createdObjects.Add(lateVisual);
            Renderer lateRenderer = lateVisual.GetComponent<Renderer>();
            MethodInfo suppress = typeof(CraftLiveSynchronizedStartScreen)
                .GetMethod(
                    "SuppressSceneRenderers",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(suppress, Is.Not.Null);
            suppress.Invoke(screen, null);
            Assert.That(underlyingRenderer.enabled, Is.False);
            Assert.That(lateRenderer.enabled, Is.False);

            MethodInfo restore = typeof(CraftLiveSynchronizedStartScreen)
                .GetMethod(
                    "RestoreSceneRenderers",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(restore, Is.Not.Null);
            restore.Invoke(screen, null);
            MethodInfo restoreColliders =
                typeof(CraftLiveSynchronizedStartScreen).GetMethod(
                    "RestoreSceneColliders",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(restoreColliders, Is.Not.Null);
            restoreColliders.Invoke(screen, null);
            Assert.That(underlyingRenderer.enabled, Is.True);
            Assert.That(lateRenderer.enabled, Is.True);
            Assert.That(
                underlying.GetComponent<Collider>().enabled,
                Is.True);
        }

        [Test]
        public void RuntimeVisualFit_IgnoresParticleBoundsWhenMeshExists()
        {
            GameObject parent = new GameObject("FitParent");
            createdObjects.Add(parent);
            GameObject content = new GameObject("FitContent");
            createdObjects.Add(content);
            content.transform.SetParent(parent.transform, false);
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            createdObjects.Add(mesh);
            mesh.transform.SetParent(content.transform, false);
            GameObject particleObject = new GameObject("HugeParticles");
            createdObjects.Add(particleObject);
            particleObject.transform.SetParent(content.transform, false);
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystemRenderer particleRenderer =
                particleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.localBounds =
                new Bounds(Vector3.zero, Vector3.one * 1000f);

            System.Type utility = System.Type.GetType(
                "CraftOrigin.CraftLive.CraftLiveRuntimeVisualUtility, Assembly-CSharp");
            Assert.That(utility, Is.Not.Null);
            MethodInfo fit = utility.GetMethod(
                "FitAndCenter",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(fit, Is.Not.Null);
            bool fitted = (bool)fit.Invoke(
                null,
                new object[]
                {
                    content.transform,
                    2f,
                    false,
                    0f,
                    false,
                    false
                });

            Assert.That(fitted, Is.True);
            Assert.That(
                mesh.GetComponent<Renderer>().bounds.size.x,
                Is.EqualTo(2f).Within(0.05f));
            particles.Stop();
        }

        [TestCase(CraftLiveMaterialCategory.Upgrade, "パワーアップ")]
        [TestCase(CraftLiveMaterialCategory.Skill, "スキル")]
        [TestCase(CraftLiveMaterialCategory.Attribute, "タイプ")]
        public void GalleryWall_BuildsIndependentNameplateForEveryTier(
            CraftLiveMaterialCategory category,
            string label)
        {
            GameObject parent = new GameObject("HeaderLayer");
            createdObjects.Add(parent);
            GameObject wallObject = new GameObject("Wall");
            createdObjects.Add(wallObject);
            CraftLiveGalleryWallView wall =
                wallObject.AddComponent<CraftLiveGalleryWallView>();
            GameObject headerObject = new GameObject("Header");
            headerObject.transform.SetParent(parent.transform, false);
            TextMesh header = headerObject.AddComponent<TextMesh>();
            header.text = label;
            header.anchor = TextAnchor.MiddleCenter;
            SetField(wall, "category", category);
            SetField(wall, "headerText", header);

            MethodInfo ensure = typeof(CraftLiveGalleryWallView).GetMethod(
                "EnsureHeaderNameplate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(ensure, Is.Not.Null);
            ensure.Invoke(wall, new object[] { null });

            FieldInfo generatedField =
                typeof(CraftLiveGalleryWallView).GetField(
                    "generatedHeaderNameplate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(generatedField, Is.Not.Null);
            GameObject nameplate =
                generatedField.GetValue(wall) as GameObject;
            Assert.That(nameplate, Is.Not.Null);
            Assert.That(nameplate.activeSelf, Is.True);
            Assert.That(
                nameplate.transform.parent,
                Is.EqualTo(parent.transform));
            Assert.That(
                nameplate.transform.IsChildOf(header.transform),
                Is.False);
            Assert.That(nameplate.transform.childCount, Is.EqualTo(7));
        }

        [Test]
        public void GalleryWall_NameplatesUseEqualSidePaddingAndReducedHeight()
        {
            CraftLiveGalleryWallView.ResetNameplateReference();
            MethodInfo ensure = typeof(CraftLiveGalleryWallView).GetMethod(
                "EnsureHeaderNameplate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo generatedField =
                typeof(CraftLiveGalleryWallView).GetField(
                    "generatedHeaderNameplate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(ensure, Is.Not.Null);
            Assert.That(generatedField, Is.Not.Null);

            GameObject parent = new GameObject("HeaderLayer");
            createdObjects.Add(parent);
            CraftLiveGalleryWallView top = CreateNameplateWall(
                parent.transform,
                CraftLiveMaterialCategory.Upgrade,
                "パワーアップ",
                40);
            ensure.Invoke(top, new object[] { null });
            GameObject topPlate = generatedField.GetValue(top) as GameObject;

            CraftLiveGalleryWallView lower = CreateNameplateWall(
                parent.transform,
                CraftLiveMaterialCategory.Skill,
                "スキル",
                96);
            ensure.Invoke(lower, new object[] { null });
            GameObject lowerPlate =
                generatedField.GetValue(lower) as GameObject;

            Assert.That(topPlate, Is.Not.Null);
            Assert.That(lowerPlate, Is.Not.Null);
            Vector3 topSize = topPlate.transform.Find("WoodShadow").localScale;
            Vector3 lowerSize =
                lowerPlate.transform.Find("WoodShadow").localScale;
            Renderer topTextRenderer =
                GetField<TextMesh>(top, "headerText")
                    .GetComponent<Renderer>();
            Renderer lowerTextRenderer =
                GetField<TextMesh>(lower, "headerText")
                    .GetComponent<Renderer>();
            float topPadding =
                (topSize.x - topTextRenderer.bounds.size.x) * 0.5f;
            float lowerPadding =
                (lowerSize.x - lowerTextRenderer.bounds.size.x) * 0.5f;
            Assert.That(
                lowerPadding,
                Is.EqualTo(topPadding).Within(0.0001f));
            Assert.That(
                lowerSize.y,
                Is.EqualTo(topSize.y * 0.82f * 0.78f)
                    .Within(0.0001f));
            Assert.That(
                lowerPlate.transform.position.x,
                Is.LessThan(topPlate.transform.position.x));
        }

        [Test]
        public void SynchronizedStartScreen_OnlyShowsDuringStartPhase()
        {
            CraftLiveRoomState state = new CraftLiveRoomState
            {
                sessionPhase = CraftLiveSessionPhase.StartScreen
            };
            Assert.That(
                CraftLiveSynchronizedStartScreen.ShouldShow(state),
                Is.True);

            state.sessionPhase = CraftLiveSessionPhase.Playing;
            Assert.That(
                CraftLiveSynchronizedStartScreen.ShouldShow(state),
                Is.False);
        }

        private CraftLiveGalleryWallView CreateNameplateWall(
            Transform parent,
            CraftLiveMaterialCategory category,
            string label,
            int fontSize)
        {
            GameObject wallObject = new GameObject($"Wall_{category}");
            createdObjects.Add(wallObject);
            CraftLiveGalleryWallView wall =
                wallObject.AddComponent<CraftLiveGalleryWallView>();
            GameObject headerObject = new GameObject($"Header_{category}");
            headerObject.transform.SetParent(parent, false);
            TextMesh header = headerObject.AddComponent<TextMesh>();
            header.text = label;
            header.fontSize = fontSize;
            header.anchor = TextAnchor.MiddleCenter;
            SetField(wall, "category", category);
            SetField(wall, "headerText", header);
            return wall;
        }

        [Test]
        public void Presentation_UpgradeDetailsContainThreeStats()
        {
            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);
            SetField(
                material,
                "displayName",
                "Test Ore");
            SetField(
                material,
                "category",
                CraftLiveMaterialCategory.Upgrade);
            SetField(
                material,
                "statModifiers",
                new CraftLiveStats
                {
                    attackRate = 12f,
                    defenseRate = 7f,
                    evasionRate = 3f
                });

            string details =
                CraftLivePad1Presentation.BuildDetailText(material);

            StringAssert.Contains("攻撃 +12", details);
            StringAssert.Contains("防御 +7", details);
            StringAssert.Contains("回避 +3", details);
        }

        [Test]
        public void DisplayRule_HidesLockedMaterialUntilRegistered()
        {
            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);
            SetField(material, "materialId", "locked");
            SetField(material, "requiresQrUnlock", true);
            CraftLiveRoomState state = new CraftLiveRoomState();
            state.Normalize(null);

            Assert.That(
                CraftLivePad1GalleryController.ShouldDisplayMaterial(
                    material,
                    state,
                    false),
                Is.False);

            state.RegisterMaterial("locked");

            Assert.That(
                CraftLivePad1GalleryController.ShouldDisplayMaterial(
                    material,
                    state,
                    false),
                Is.True);
            Assert.That(
                CraftLivePad1GalleryController.ShouldDisplayMaterial(
                    material,
                    new CraftLiveRoomState(),
                    true),
                Is.True);
        }

        [Test]
        public void GalleryColumn_ClampsScrollableRange()
        {
            GameObject root = new GameObject("Column");
            createdObjects.Add(root);
            GameObject content = new GameObject("Content");
            content.transform.SetParent(root.transform, false);
            CraftLiveGalleryColumn column =
                root.AddComponent<CraftLiveGalleryColumn>();
            List<CraftLiveMaterialPaintingView> items =
                new List<CraftLiveMaterialPaintingView>();
            for (int i = 0; i < 4; i++)
            {
                GameObject item = new GameObject($"Item_{i}");
                item.transform.SetParent(content.transform, false);
                item.transform.localPosition =
                    new Vector3(i * 2f, 2f - i * 2f, 0f);
                items.Add(
                    item.AddComponent<
                        CraftLiveMaterialPaintingView>());
            }

            column.Configure(
                content.transform,
                items,
                2f,
                3,
                0.01f,
                1f);
            column.SetScrollOffset(-100f);

            Assert.That(column.MinimumOffset, Is.EqualTo(-2f));
            Assert.That(column.MaximumOffset, Is.Zero);
            Assert.That(column.ScrollOffset, Is.EqualTo(-2f));
            Assert.That(content.transform.localPosition.x, Is.EqualTo(-2f));
            Assert.That(content.transform.localPosition.y, Is.Zero);
            Assert.That(items.TrueForAll(item => item.ViewportVisible), Is.True);

            column.SetScrollOffset(100f);

            Assert.That(column.ScrollOffset, Is.Zero);
        }

        [Test]
        public void GalleryColumn_MovesConfiguredWallInsteadOfContent()
        {
            GameObject wall = new GameObject("Wall");
            createdObjects.Add(wall);
            wall.transform.position = new Vector3(5f, 2f, 1f);
            GameObject content = new GameObject("ScrollContent");
            content.transform.SetParent(wall.transform, false);
            CraftLiveGalleryColumn column =
                wall.AddComponent<CraftLiveGalleryColumn>();
            List<CraftLiveMaterialPaintingView> items =
                new List<CraftLiveMaterialPaintingView>();
            for (int i = 0; i < 4; i++)
            {
                GameObject item = new GameObject($"Item_{i}");
                item.transform.SetParent(content.transform, false);
                item.transform.localPosition =
                    new Vector3(i * 2f, 0f, 0f);
                items.Add(
                    item.AddComponent<
                        CraftLiveMaterialPaintingView>());
            }

            column.SetMovementRoot(wall.transform, null);
            column.Configure(
                content.transform,
                items,
                2f,
                3,
                1f,
                1f);
            column.SetScrollOffset(-2f);

            Assert.That(
                wall.transform.position,
                Is.EqualTo(new Vector3(3f, 2f, 1f)));
            Assert.That(content.transform.localPosition, Is.EqualTo(Vector3.zero));

            column.SetScrollOffset(0f);

            Assert.That(
                wall.transform.position,
                Is.EqualTo(new Vector3(5f, 2f, 1f)));
        }

        [Test]
        public void PreplacedWall_BindsExistingSlotsWithoutInstantiation()
        {
            GameObject sessionObject = new GameObject("Session");
            createdObjects.Add(sessionObject);
            CraftLiveSession session =
                sessionObject.AddComponent<CraftLiveSession>();

            GameObject wallObject = new GameObject("Wall");
            createdObjects.Add(wallObject);
            CraftLiveGalleryWallView wall =
                wallObject.AddComponent<CraftLiveGalleryWallView>();
            CraftLiveGalleryColumn column =
                wallObject.AddComponent<CraftLiveGalleryColumn>();
            GameObject contentObject = new GameObject("ScrollContent");
            contentObject.transform.SetParent(wallObject.transform, false);

            CraftLiveMaterialPaintingView[] slots =
                new CraftLiveMaterialPaintingView[2];
            for (int i = 0; i < slots.Length; i++)
            {
                GameObject slotObject = new GameObject($"FrameSlot_{i}");
                slotObject.transform.SetParent(
                    contentObject.transform,
                    false);
                slotObject.transform.localPosition =
                    new Vector3(0f, 1f - i, 0f);
                slots[i] = slotObject.AddComponent<
                    CraftLiveMaterialPaintingView>();
                slots[i].CaptureRestingTransform();
            }

            SetField(
                wall,
                "category",
                CraftLiveMaterialCategory.Upgrade);
            SetField(wall, "contentRoot", contentObject.transform);
            SetField(wall, "column", column);
            SetField(wall, "frameSlots", slots);

            List<CraftLiveMaterialDefinition> materials =
                new List<CraftLiveMaterialDefinition>();
            for (int i = 0; i < 2; i++)
            {
                CraftLiveMaterialDefinition material =
                    ScriptableObject.CreateInstance<
                        CraftLiveMaterialDefinition>();
                createdObjects.Add(material);
                SetField(material, "materialId", $"material_{i}");
                SetField(material, "requiresQrUnlock", false);
                SetField(
                    material,
                    "category",
                    CraftLiveMaterialCategory.Upgrade);
                materials.Add(material);
            }

            List<CraftLiveMaterialPaintingView> bound =
                new List<CraftLiveMaterialPaintingView>();
            bool result = wall.TryBind(
                null,
                CraftLiveMaterialCategory.Upgrade,
                materials,
                session.State,
                session,
                "Upgrade",
                1,
                0.01f,
                1f,
                bound);

            Assert.That(result, Is.True);
            Assert.That(bound, Has.Count.EqualTo(2));
            Assert.That(slots[0].Material, Is.SameAs(materials[0]));
            Assert.That(slots[1].Material, Is.SameAs(materials[1]));
            Assert.That(column.ItemCount, Is.EqualTo(2));
            Assert.That(column.MaximumOffset, Is.Zero);

            wall.ClearBindings();

            Assert.That(slots[0].gameObject.activeSelf, Is.False);
            Assert.That(slots[1].gameObject.activeSelf, Is.False);
        }

        [Test]
        public void PreplacedWall_RejectsInsufficientSlotCapacity()
        {
            GameObject wallObject = new GameObject("Wall");
            createdObjects.Add(wallObject);
            CraftLiveGalleryWallView wall =
                wallObject.AddComponent<CraftLiveGalleryWallView>();
            CraftLiveGalleryColumn column =
                wallObject.AddComponent<CraftLiveGalleryColumn>();
            GameObject contentObject = new GameObject("ScrollContent");
            contentObject.transform.SetParent(wallObject.transform, false);
            GameObject slotObject = new GameObject("FrameSlot_0");
            slotObject.transform.SetParent(contentObject.transform, false);
            CraftLiveMaterialPaintingView slot =
                slotObject.AddComponent<CraftLiveMaterialPaintingView>();
            SetField(
                wall,
                "category",
                CraftLiveMaterialCategory.Skill);
            SetField(wall, "contentRoot", contentObject.transform);
            SetField(wall, "column", column);
            SetField(
                wall,
                "frameSlots",
                new[] { slot });

            List<CraftLiveMaterialDefinition> materials =
                new List<CraftLiveMaterialDefinition>
                {
                    ScriptableObject.CreateInstance<
                        CraftLiveMaterialDefinition>(),
                    ScriptableObject.CreateInstance<
                        CraftLiveMaterialDefinition>()
                };
            createdObjects.AddRange(materials);

            bool result = wall.TryBind(
                null,
                CraftLiveMaterialCategory.Skill,
                materials,
                null,
                null,
                "Skill",
                1,
                0.01f,
                1f,
                new List<CraftLiveMaterialPaintingView>());

            Assert.That(result, Is.False);
        }

        [Test]
        public void WallSlider_MovesWallAndItsFrameAsOneUnit()
        {
            GameObject sliderObject = new GameObject("WallSlider");
            createdObjects.Add(sliderObject);
            CraftLiveGalleryWallSlider slider =
                sliderObject.AddComponent<CraftLiveGalleryWallSlider>();
            SetField(slider, "fitSpacingToCamera", false);
            SetField(slider, "wallSpacing", 2f);
            SetField(slider, "dragSensitivity", 1f);

            CraftLiveGalleryWallView[] walls =
                new CraftLiveGalleryWallView[3];
            Transform firstFrame = null;
            for (int i = 0; i < walls.Length; i++)
            {
                GameObject wallObject = new GameObject($"Wall_{i}");
                createdObjects.Add(wallObject);
                walls[i] = wallObject.AddComponent<
                    CraftLiveGalleryWallView>();
                GameObject frameObject = new GameObject($"Frame_{i}");
                frameObject.transform.SetParent(
                    wallObject.transform,
                    false);
                if (i == 0)
                {
                    firstFrame = frameObject.transform;
                }
            }

            slider.Configure(walls, null);

            Assert.That(walls[0].transform.position.x, Is.Zero);
            Assert.That(walls[1].transform.position.x, Is.EqualTo(2f));
            Assert.That(walls[2].transform.position.x, Is.EqualTo(4f));

            slider.BeginDrag();
            slider.Drag(-0.6f);
            slider.EndDrag();
            slider.CompleteTransitionImmediately();

            Assert.That(slider.SelectedIndex, Is.EqualTo(1));
            Assert.That(walls[0].transform.position.x, Is.EqualTo(-2f));
            Assert.That(walls[1].transform.position.x, Is.Zero);
            Assert.That(walls[2].transform.position.x, Is.EqualTo(2f));
            Assert.That(firstFrame.position.x, Is.EqualTo(-2f));
        }

        [Test]
        public void WallSlider_DoesNotMoveBeyondFirstOrLastWall()
        {
            GameObject sliderObject = new GameObject("WallSlider");
            createdObjects.Add(sliderObject);
            CraftLiveGalleryWallSlider slider =
                sliderObject.AddComponent<CraftLiveGalleryWallSlider>();
            SetField(slider, "fitSpacingToCamera", false);
            SetField(slider, "wallSpacing", 2f);
            SetField(slider, "dragSensitivity", 1f);

            CraftLiveGalleryWallView[] walls =
                new CraftLiveGalleryWallView[3];
            for (int i = 0; i < walls.Length; i++)
            {
                GameObject wallObject = new GameObject($"Wall_{i}");
                createdObjects.Add(wallObject);
                walls[i] = wallObject.AddComponent<
                    CraftLiveGalleryWallView>();
            }

            slider.Configure(walls, null);
            slider.BeginDrag();
            slider.Drag(100f);
            Assert.That(walls[0].transform.position.x, Is.Zero);
            slider.EndDrag();

            slider.SetSelectedIndex(2, true);
            slider.BeginDrag();
            slider.Drag(-100f);
            Assert.That(walls[2].transform.position.x, Is.Zero);
            slider.EndDrag();
        }

        [Test]
        public void WallSlider_AlignsAuthoredWallsToOneCarouselPlane()
        {
            GameObject sliderObject = new GameObject("WallSlider");
            createdObjects.Add(sliderObject);
            CraftLiveGalleryWallSlider slider =
                sliderObject.AddComponent<CraftLiveGalleryWallSlider>();
            SetField(slider, "fitSpacingToCamera", false);
            SetField(slider, "wallSpacing", 2f);

            CraftLiveGalleryWallView[] walls =
                new CraftLiveGalleryWallView[3];
            for (int i = 0; i < walls.Length; i++)
            {
                GameObject wallObject = new GameObject($"Wall_{i}");
                createdObjects.Add(wallObject);
                wallObject.transform.position =
                    new Vector3(i * 8f, i * 3f, i * 20f);
                walls[i] = wallObject.AddComponent<
                    CraftLiveGalleryWallView>();
            }

            slider.Configure(walls, null);

            Assert.That(walls[0].transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(
                walls[1].transform.position,
                Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.That(
                walls[2].transform.position,
                Is.EqualTo(new Vector3(4f, 0f, 0f)));
        }

        [Test]
        public void Painting_IconPreservesAspectInsideFrameArea()
        {
            GameObject paintingObject = new GameObject("Painting");
            createdObjects.Add(paintingObject);
            CraftLiveMaterialPaintingView painting =
                paintingObject.AddComponent<
                    CraftLiveMaterialPaintingView>();
            GameObject artworkObject = new GameObject("Artwork");
            artworkObject.transform.SetParent(
                paintingObject.transform,
                false);
            SpriteRenderer artwork =
                artworkObject.AddComponent<SpriteRenderer>();
            artwork.drawMode = SpriteDrawMode.Sliced;
            artwork.size = new Vector2(4f, 2f);
            SetField(painting, "iconRenderer", artwork);
            InvokeMethod(painting, "Awake");

            Texture2D texture = new Texture2D(100, 100);
            createdObjects.Add(texture);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 100f, 100f),
                new Vector2(0.5f, 0.5f),
                100f);
            createdObjects.Add(sprite);
            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);
            SetField(material, "icon", sprite);

            painting.Bind(null, material);

            Assert.That(artwork.drawMode, Is.EqualTo(SpriteDrawMode.Simple));
            Assert.That(artwork.transform.localScale.x, Is.EqualTo(2f));
            Assert.That(artwork.transform.localScale.y, Is.EqualTo(2f));
        }

        [Test]
        public void Painting_VisibleMaterialKeepsInputColliderEnabled()
        {
            GameObject paintingObject = new GameObject("Painting");
            createdObjects.Add(paintingObject);
            BoxCollider collider =
                paintingObject.AddComponent<BoxCollider>();
            CraftLiveMaterialPaintingView painting =
                paintingObject.AddComponent<
                    CraftLiveMaterialPaintingView>();
            painting.ConfigureFallbackVisuals(
                paintingObject.transform,
                new Renderer[0],
                new Collider[] { collider },
                null,
                null);
            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);

            painting.Bind(null, material);
            painting.SetViewportVisible(true);

            Assert.That(painting.Interactable, Is.False);
            Assert.That(collider.enabled, Is.True);
        }

        [Test]
        public void Painting_PreviewSuppressionHidesVisualsButKeepsInput()
        {
            GameObject paintingObject = new GameObject("Painting");
            createdObjects.Add(paintingObject);
            MeshRenderer renderer =
                paintingObject.AddComponent<MeshRenderer>();
            BoxCollider collider =
                paintingObject.AddComponent<BoxCollider>();
            CraftLiveMaterialPaintingView painting =
                paintingObject.AddComponent<
                    CraftLiveMaterialPaintingView>();
            painting.ConfigureFallbackVisuals(
                paintingObject.transform,
                new Renderer[] { renderer },
                new Collider[] { collider },
                null,
                null);

            painting.SetViewportVisible(true);
            painting.SetPreviewSuppressed(true);

            Assert.That(painting.PreviewSuppressed, Is.True);
            Assert.That(renderer.enabled, Is.False);
            Assert.That(collider.enabled, Is.True);

            painting.SetPreviewSuppressed(false);

            Assert.That(renderer.enabled, Is.True);
            Assert.That(collider.enabled, Is.True);
        }

        [Test]
        public void GalleryInput_ConfiguresWallSurfaceAndKeepsFrameCollider()
        {
            GameObject systemObject = new GameObject("Pad1System");
            createdObjects.Add(systemObject);
            CraftLivePad1Bindings bindings =
                systemObject.AddComponent<CraftLivePad1Bindings>();
            CraftLivePad1GalleryController controller =
                systemObject.AddComponent<
                    CraftLivePad1GalleryController>();
            GameObject wallObject = new GameObject("Wall");
            wallObject.transform.SetParent(systemObject.transform, false);
            BoxCollider wallCollider =
                wallObject.AddComponent<BoxCollider>();
            GameObject frameObject = new GameObject("Frame");
            frameObject.transform.SetParent(wallObject.transform, false);
            BoxCollider frameCollider =
                frameObject.AddComponent<BoxCollider>();
            frameObject.AddComponent<CraftLiveMaterialPaintingView>();
            SetField(bindings, "powerUpWall", wallObject.transform);
            SetField(controller, "bindings", bindings);

            wallObject.AddComponent<CraftLiveGalleryColumn>();
            InvokeMethod(controller, "ConfigureWallInputSurfaces");

            Assert.That(wallCollider.enabled, Is.True);
            Assert.That(frameCollider.enabled, Is.True);
            Assert.That(
                wallObject.GetComponent<CraftLiveGalleryInputSurface>(),
                Is.Not.Null);
        }

        [Test]
        public void GalleryInputSurface_DragMovesOnlyItsColumn()
        {
            GameObject wallObject = new GameObject("Wall");
            createdObjects.Add(wallObject);
            GameObject contentObject = new GameObject("ScrollContent");
            contentObject.transform.SetParent(wallObject.transform, false);
            CraftLiveGalleryColumn column =
                wallObject.AddComponent<CraftLiveGalleryColumn>();
            CraftLiveGalleryInputSurface surface =
                wallObject.AddComponent<CraftLiveGalleryInputSurface>();
            List<CraftLiveMaterialPaintingView> items =
                new List<CraftLiveMaterialPaintingView>();
            for (int i = 0; i < 4; i++)
            {
                GameObject itemObject = new GameObject($"Frame_{i}");
                itemObject.transform.SetParent(
                    contentObject.transform,
                    false);
                itemObject.transform.localPosition =
                    new Vector3(i * 2f, 0f, 0f);
                items.Add(itemObject.AddComponent<
                    CraftLiveMaterialPaintingView>());
            }

            column.Configure(
                contentObject.transform,
                items,
                2f,
                3,
                1f,
                1f);
            surface.Configure(column, wallObject.transform);
            GameObject eventSystemObject = new GameObject("EventSystem");
            createdObjects.Add(eventSystemObject);
            EventSystem eventSystem =
                eventSystemObject.AddComponent<EventSystem>();
            PointerEventData eventData =
                new PointerEventData(eventSystem)
                {
                    delta = new Vector2(2f, 0f)
                };

            surface.OnBeginDrag(eventData);
            surface.OnDrag(eventData);
            surface.OnEndDrag(eventData);

            Assert.That(column.ScrollOffset, Is.EqualTo(-2f));
            Assert.That(contentObject.transform.localPosition.x, Is.EqualTo(-2f));
        }

        [Test]
        public void Painting_SelectionPreservesCapturedListPosition()
        {
            GameObject sessionObject = new GameObject("Session");
            createdObjects.Add(sessionObject);
            CraftLiveSession session =
                sessionObject.AddComponent<CraftLiveSession>();

            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);
            SetField(material, "materialId", "test_material");
            SetField(material, "requiresQrUnlock", false);

            GameObject paintingObject = new GameObject("Painting");
            createdObjects.Add(paintingObject);
            CraftLiveMaterialPaintingView painting =
                paintingObject.AddComponent<
                    CraftLiveMaterialPaintingView>();
            paintingObject.transform.localPosition =
                new Vector3(0f, -4f, 0f);
            paintingObject.transform.localScale =
                new Vector3(0.8f, 0.9f, 1f);
            painting.CaptureRestingTransform();
            SetField(painting, "movePaintingOnSelection", true);
            painting.Bind(null, material);

            CraftLiveRoomState state = new CraftLiveRoomState
            {
                selectedMaterialId = "test_material"
            };
            state.placement.status =
                CraftLivePlacementStatus.SelectingSlot;
            state.Normalize(null);
            painting.Refresh(state, session);

            Assert.That(
                paintingObject.transform.localPosition,
                Is.EqualTo(new Vector3(0f, -4f, 0f)));
            Assert.That(
                paintingObject.transform.localScale,
                Is.EqualTo(new Vector3(0.8f, 0.9f, 1f)));
        }

        [Test]
        public void MaterialHologram_RequiresCompatibleOpenSlot()
        {
            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);
            SetField(material, "materialId", "slot_guard_material");
            SetField(
                material,
                "category",
                CraftLiveMaterialCategory.Upgrade);

            CraftLiveRoomState state = new CraftLiveRoomState
            {
                sessionPhase = CraftLiveSessionPhase.Playing,
                weaponSelectionConfirmed = true
            };
            state.Normalize(null);

            Assert.That(
                CraftLivePad1GalleryController.
                    CanBeginMaterialPlacement(state, material),
                Is.True);

            state.slots.top = "used";
            state.slots.left = "used";
            state.slots.right = "used";
            state.slots.bottom = "used";

            Assert.That(
                CraftLivePad1GalleryController.
                    CanBeginMaterialPlacement(state, material),
                Is.False);
        }

        [Test]
        public void PaintingSelection_SecondSelectionReturnsToIdle()
        {
            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);
            SetField(material, "materialId", "preview_material");
            SetField(material, "displayName", "Preview Material");
            SetField(material, "requiresQrUnlock", false);

            CraftLiveCatalog catalog =
                ScriptableObject.CreateInstance<CraftLiveCatalog>();
            createdObjects.Add(catalog);
            SetField(
                catalog,
                "materials",
                new List<CraftLiveMaterialDefinition> { material });

            GameObject sessionObject = new GameObject("Session");
            createdObjects.Add(sessionObject);
            CraftLiveSession session =
                sessionObject.AddComponent<CraftLiveSession>();
            SetField(session, "catalog", catalog);
            SetField(
                session,
                "state",
                CraftLiveRoomState.Create(catalog));
            session.State.weaponSelectionConfirmed = true;

            GameObject systemObject = new GameObject("Pad1System");
            createdObjects.Add(systemObject);
            systemObject.SetActive(false);
            CraftLivePad1Bindings bindings =
                systemObject.AddComponent<CraftLivePad1Bindings>();
            CraftLivePad1GalleryController controller =
                systemObject.AddComponent<
                    CraftLivePad1GalleryController>();
            CraftLivePad1MaterialPreview preview =
                systemObject.AddComponent<CraftLivePad1MaterialPreview>();
            GameObject previewRoot = new GameObject("PreviewRoot");
            previewRoot.transform.SetParent(systemObject.transform, false);
            GameObject hologramRoot = new GameObject("HologramRoot");
            hologramRoot.transform.SetParent(systemObject.transform, false);
            SetField(
                bindings,
                "materialPreviewRoot",
                previewRoot.transform);
            SetField(
                bindings,
                "hologramInfoRoot",
                hologramRoot.transform);
            SetField(controller, "session", session);
            SetField(controller, "bindings", bindings);
            SetField(preview, "session", session);
            SetField(preview, "bindings", bindings);
            SetField(preview, "createPlaceholderWhenMissing", false);
            SetField(preview, "createFallbackHologram", false);
            systemObject.SetActive(true);
            InvokeMethod(preview, "OnDisable");
            InvokeMethod(preview, "OnEnable");

            GameObject paintingObject = new GameObject("Painting");
            createdObjects.Add(paintingObject);
            CraftLiveMaterialPaintingView painting =
                paintingObject.AddComponent<
                    CraftLiveMaterialPaintingView>();
            painting.Bind(controller, material);
            painting.Refresh(session.State, session);
            painting.Select();

            Assert.That(
                session.State.selectedMaterialId,
                Is.EqualTo(material.MaterialId));
            Assert.That(
                preview.DisplayedMaterialId,
                Is.EqualTo(material.MaterialId));

            controller.SelectMaterial(material);

            Assert.That(session.State.selectedMaterialId, Is.Empty);
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
            Assert.That(preview.DisplayedMaterialId, Is.Empty);
        }

        [Test]
        public void PaintingSelection_WithoutConfirmedWeaponKeepsHologramClosed()
        {
            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);
            SetField(material, "materialId", "browse_material");
            SetField(material, "displayName", "Browse Material");
            SetField(material, "requiresQrUnlock", false);

            CraftLiveCatalog catalog =
                ScriptableObject.CreateInstance<CraftLiveCatalog>();
            createdObjects.Add(catalog);
            SetField(
                catalog,
                "materials",
                new List<CraftLiveMaterialDefinition> { material });

            GameObject sessionObject = new GameObject("Session");
            createdObjects.Add(sessionObject);
            CraftLiveSession session =
                sessionObject.AddComponent<CraftLiveSession>();
            SetField(session, "catalog", catalog);
            SetField(
                session,
                "state",
                CraftLiveRoomState.Create(catalog));

            GameObject systemObject = new GameObject("Pad1System");
            createdObjects.Add(systemObject);
            systemObject.SetActive(false);
            CraftLivePad1Bindings bindings =
                systemObject.AddComponent<CraftLivePad1Bindings>();
            CraftLivePad1GalleryController controller =
                systemObject.AddComponent<
                    CraftLivePad1GalleryController>();
            CraftLivePad1MaterialPreview preview =
                systemObject.AddComponent<CraftLivePad1MaterialPreview>();
            GameObject previewRoot = new GameObject("PreviewRoot");
            previewRoot.transform.SetParent(systemObject.transform, false);
            GameObject hologramRoot = new GameObject("HologramRoot");
            hologramRoot.transform.SetParent(systemObject.transform, false);
            SetField(
                bindings,
                "materialPreviewRoot",
                previewRoot.transform);
            SetField(
                bindings,
                "hologramInfoRoot",
                hologramRoot.transform);
            SetField(controller, "session", session);
            SetField(controller, "bindings", bindings);
            SetField(preview, "session", session);
            SetField(preview, "bindings", bindings);
            SetField(preview, "createPlaceholderWhenMissing", false);
            SetField(preview, "createFallbackHologram", false);
            systemObject.SetActive(true);
            InvokeMethod(preview, "OnDisable");
            InvokeMethod(preview, "OnEnable");

            GameObject paintingObject = new GameObject("Painting");
            createdObjects.Add(paintingObject);
            CraftLiveMaterialPaintingView painting =
                paintingObject.AddComponent<
                    CraftLiveMaterialPaintingView>();
            painting.Bind(controller, material);
            painting.Refresh(session.State, session);

            Assert.That(painting.Interactable, Is.False);
            painting.Select();
            preview.Refresh(session.State);

            Assert.That(session.State.weaponSelectionConfirmed, Is.False);
            Assert.That(session.State.selectedMaterialId, Is.Empty);
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
            Assert.That(
                preview.DisplayedMaterialId,
                Is.Empty);
        }

        [Test]
        public void Pad1Scene_HasOneGalleryAndPreview()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad1ScenePath,
                scene =>
                {
                    AssertSingle<
                        CraftLivePad1GalleryController>(scene);
                    AssertSingle<
                        CraftLivePad1MaterialPreview>(scene);
                });
        }

        [Test]
        public void Pad1Scene_HasCompletePreplacedWallLayout()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad1ScenePath,
                scene =>
                {
                    CraftLivePad1GalleryController controller =
                        FindSingle<CraftLivePad1GalleryController>(scene);
                    CraftLiveGalleryWallSlider slider =
                        FindSingle<CraftLiveGalleryWallSlider>(scene);
                    Assert.That(controller.WallSlider, Is.SameAs(slider));

                    List<CraftLiveGalleryWallView> walls =
                        FindAll<CraftLiveGalleryWallView>(scene);
                    Assert.That(walls, Has.Count.EqualTo(3));
                    HashSet<CraftLiveMaterialCategory> categories =
                        new HashSet<CraftLiveMaterialCategory>();
                    int totalSlots = 0;
                    foreach (CraftLiveGalleryWallView wall in walls)
                    {
                        Assert.That(wall.HasUsableLayout, Is.True);
                        Assert.That(
                            wall.SlotCapacity,
                            Is.GreaterThanOrEqualTo(3));
                        Assert.That(
                            categories.Add(wall.Category),
                            Is.True,
                            $"Duplicate wall category: {wall.Category}");
                        totalSlots += wall.SlotCapacity;
                    }

                    Assert.That(totalSlots, Is.GreaterThanOrEqualTo(9));
                    Assert.That(
                        categories,
                        Is.EquivalentTo(new[]
                        {
                            CraftLiveMaterialCategory.Upgrade,
                            CraftLiveMaterialCategory.Skill,
                            CraftLiveMaterialCategory.Attribute
                        }));
                });
        }

        [Test]
        public void BootstrapCamera_HasOnePhysicsRaycaster()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.BootstrapScenePath,
                scene =>
                {
                    Camera camera = FindSingle<Camera>(scene);
                    Assert.That(
                        camera.GetComponents<PhysicsRaycaster>(),
                        Has.Length.EqualTo(1));
                });
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static T GetField<T>(
            object target,
            string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void InvokeMethod(
            object target,
            string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static void AssertSingle<T>(Scene scene)
            where T : Component
        {
            Assert.That(
                FindAll<T>(scene),
                Has.Count.EqualTo(1),
                $"{scene.path}: {typeof(T).Name}");
        }

        private static T FindSingle<T>(Scene scene)
            where T : Component
        {
            List<T> results = FindAll<T>(scene);
            Assert.That(results, Has.Count.EqualTo(1));
            return results[0];
        }

        private static List<T> FindAll<T>(Scene scene)
            where T : Component
        {
            List<T> results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(
                    root.GetComponentsInChildren<T>(true));
            }

            return results;
        }

        private static void WithScene(
            string scenePath,
            System.Action<Scene> action)
        {
            SceneSetup[] setup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Single);
                action(scene);
            }
            finally
            {
                bool canRestore = false;
                foreach (SceneSetup sceneSetup in setup)
                {
                    if (sceneSetup.isLoaded && sceneSetup.isActive)
                    {
                        canRestore = true;
                        break;
                    }
                }

                if (canRestore)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }
        }
    }
}
