using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CraftOrigin.CraftLive.EditorTests
{
    public sealed class CraftLiveForgeUIThemeTests
    {
        [Test]
        public void RefineSurfaceColor_PreservesAlphaAndControlsBrightness()
        {
            Color refined = CraftLiveForgeUITheme.RefineSurfaceColor(
                new Color(0.1f, 0.85f, 1f, 0.37f));

            Color.RGBToHSV(refined, out _, out _, out float value);
            Assert.That(refined.a, Is.EqualTo(0.37f).Within(0.0001f));
            Assert.That(value, Is.InRange(0.18f, 0.68f));
        }

        [Test]
        public void StyleText_UsesBoldTextWithoutOffsetUnderlay()
        {
            GameObject target = new GameObject("ThemeTextTest");
            try
            {
                TextMesh text = target.AddComponent<TextMesh>();
                text.text = "鍛造";

                CraftLiveForgeUITheme.StyleText(
                    text,
                    0.08f,
                    Color.white);
                CraftLiveForgeUITheme.StyleText(
                    text,
                    0.08f,
                    Color.white);

                Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(128));
                Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Bold));
                Assert.That(
                    text.characterSize,
                    Is.EqualTo(
                        CraftLiveForgeUITheme.ScaleCharacterSize(
                            0.08f)));
                Assert.That(target.transform.Find("ForgeUnderlay"), Is.Null);
                Assert.That(
                    target.GetComponentsInChildren<TextMesh>(true).Length,
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void BuildButtonFrame_IsIdempotentAndKeepsLayout()
        {
            GameObject button = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            button.transform.localPosition = new Vector3(2f, -1f, 0.5f);
            button.transform.localScale = new Vector3(3f, 0.8f, 0.2f);
            Vector3 position = button.transform.localPosition;
            Vector3 scale = button.transform.localScale;

            try
            {
                CraftLiveForgeUITheme.BuildButtonFrame(
                    button.transform,
                    Color.red);
                CraftLiveForgeUITheme.BuildButtonFrame(
                    button.transform,
                    Color.blue);

                Assert.That(button.transform.localPosition, Is.EqualTo(position));
                Assert.That(button.transform.localScale, Is.EqualTo(scale));
                Assert.That(button.transform.Find("ForgeFrame"), Is.Not.Null);
                Assert.That(
                    CountNamedChildren(button.transform, "ForgeFrame"),
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(button);
            }
        }

        [Test]
        public void StyleText_CompensatesNonUniformButtonScale()
        {
            GameObject button = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            button.transform.localScale = new Vector3(2.4f, 0.7f, 0.22f);
            button.AddComponent<CraftLiveWorldButton>();
            GameObject label = new GameObject("Label");
            label.transform.SetParent(button.transform, false);

            try
            {
                TextMesh text = label.AddComponent<TextMesh>();
                CraftLiveForgeUITheme.StyleText(
                    text,
                    0.032f,
                    Color.white);

                Assert.That(
                    label.transform.localScale.x,
                    Is.EqualTo(0.7f / 2.4f).Within(0.0001f));
                Assert.That(label.transform.localScale.y, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(button);
            }
        }

        [Test]
        public void CommentBoard_FormatsAndTruncatesLongComments()
        {
            string result = CraftLiveWoodCommentBoard.FormatComment(
                "ABCDEFGHIJKLM",
                5,
                2);

            Assert.That(result, Is.EqualTo("ABCDE\nFGHI…"));
        }

        [Test]
        public void CommentBoard_OverlapsBoxTopToFormOneUnit()
        {
            Rect box = CraftLivePad1PortraitFraming
                .GetTargetBoxViewportRect(3f / 4f);
            Rect board =
                CraftLiveWoodCommentBoard.CalculateBoardViewportRect(
                    box,
                    0.75f,
                    0.2f,
                    0.012f,
                    0.02f);

            Assert.That(board.center.x,
                Is.EqualTo(box.center.x).Within(0.0001f));
            Assert.That(board.yMin, Is.LessThan(box.yMax));
            Assert.That(box.yMax - board.yMin,
                Is.EqualTo(0.012f).Within(0.0001f));
            Assert.That(board.yMax, Is.LessThanOrEqualTo(0.98f));
            Assert.That(board.width, Is.LessThan(box.width));
        }

        [Test]
        public void Pad1Scene_SerializesBoardAndFramingForInspector()
        {
            string scenePath = Path.Combine(
                Application.dataPath,
                "Scenes",
                "CraftLive",
                "Pad1_MaterialGallery.unity");
            string sceneYaml = File.ReadAllText(scenePath);

            StringAssert.Contains(
                "CraftLive.CraftLiveWoodCommentBoard",
                sceneYaml);
            StringAssert.Contains(
                "CraftLive.CraftLivePad1PortraitFraming",
                sceneYaml);
            StringAssert.Contains("frameOverlap: 0.012", sceneYaml);
            StringAssert.Contains("mountLengthRatio: 0.18", sceneYaml);
        }

        [Test]
        public void Pad1PortraitFraming_ReservesTopBandForBoard()
        {
            Rect box = CraftLivePad1PortraitFraming
                .GetTargetBoxViewportRect(3f / 4f);

            Assert.That(box.width, Is.EqualTo(0.84f).Within(0.0001f));
            Assert.That(box.yMin, Is.GreaterThanOrEqualTo(0.05f));
            Assert.That(box.yMax, Is.LessThan(0.76f));
            Assert.That(1f - box.yMax, Is.GreaterThan(0.24f));
        }

        private static int CountNamedChildren(
            Transform parent,
            string childName)
        {
            int count = 0;
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
