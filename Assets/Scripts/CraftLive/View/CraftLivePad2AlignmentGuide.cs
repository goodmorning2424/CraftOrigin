using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CraftOrigin.CraftLive
{
    public readonly struct CraftLivePad2GuidePose
    {
        public CraftLivePad2GuidePose(
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }
    }

    public enum CraftLivePad2AlignmentGuideKind
    {
        Material,
        FlowStart,
        FlowEnd,
        FlowWidth,
        Pool
    }

    [ExecuteAlways]
    public sealed class CraftLivePad2AlignmentGuide : MonoBehaviour
    {
        [SerializeField] private CraftLivePad2AlignmentGuideKind kind;
        [SerializeField] private string guideLabel = "Pad2 Guide";
        [SerializeField] private Color guideColor =
            new Color(0.1f, 0.8f, 1f, 0.35f);

        public CraftLivePad2AlignmentGuideKind Kind => kind;
        public string GuideLabel => guideLabel;

        public static bool TryResolveLocalPose(
            Transform padRoot,
            CraftLivePad2AlignmentGuideKind requestedKind,
            CraftLiveSlotId slot,
            out CraftLivePad2GuidePose pose)
        {
            pose = default;
            if (padRoot == null)
            {
                return false;
            }

            CraftLivePad2AlignmentGuide[] guides =
                padRoot.GetComponentsInChildren<
                    CraftLivePad2AlignmentGuide>(true);
            string exactSuffix = "_" + slot;
            foreach (CraftLivePad2AlignmentGuide guide in guides)
            {
                if (guide != null &&
                    guide.kind == requestedKind &&
                    guide.name.EndsWith(
                        exactSuffix,
                        System.StringComparison.Ordinal))
                {
                    pose = ResolvePose(padRoot, guide, false);
                    return true;
                }
            }

            if (!TryGetSourceSuffix(
                    slot,
                    out string suffix,
                    out bool mirrorHorizontally))
            {
                return false;
            }

            foreach (CraftLivePad2AlignmentGuide guide in guides)
            {
                if (guide == null ||
                    guide.kind != requestedKind ||
                    !guide.name.EndsWith(suffix,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                pose = ResolvePose(
                    padRoot,
                    guide,
                    mirrorHorizontally);
                return true;
            }

            return false;
        }

        private static CraftLivePad2GuidePose ResolvePose(
            Transform padRoot,
            CraftLivePad2AlignmentGuide guide,
            bool mirrorHorizontally)
        {
            Vector3 localPosition =
                padRoot.InverseTransformPoint(
                    guide.transform.position);
            Quaternion localRotation =
                Quaternion.Inverse(padRoot.rotation) *
                guide.transform.rotation;
            Vector3 rootScale = padRoot.lossyScale;
            Vector3 guideScale = guide.transform.lossyScale;
            Vector3 localScale = new Vector3(
                SafeScaleRatio(guideScale.x, rootScale.x),
                SafeScaleRatio(guideScale.y, rootScale.y),
                SafeScaleRatio(guideScale.z, rootScale.z));

            if (mirrorHorizontally)
            {
                localPosition.x = -localPosition.x;
                Vector3 euler = localRotation.eulerAngles;
                localRotation = Quaternion.Euler(
                    euler.x,
                    -euler.y,
                    -euler.z);
            }

            return new CraftLivePad2GuidePose(
                localPosition,
                localRotation,
                localScale);
        }

        private static bool TryGetSourceSuffix(
            CraftLiveSlotId slot,
            out string suffix,
            out bool mirrorHorizontally)
        {
            switch (slot)
            {
                case CraftLiveSlotId.Right:
                    suffix = "UpperRight";
                    mirrorHorizontally = false;
                    return true;
                case CraftLiveSlotId.Top:
                    suffix = "UpperRight";
                    mirrorHorizontally = true;
                    return true;
                case CraftLiveSlotId.Attribute:
                    suffix = "LowerRight";
                    mirrorHorizontally = false;
                    return true;
                case CraftLiveSlotId.Skill:
                    suffix = "LowerRight";
                    mirrorHorizontally = true;
                    return true;
                default:
                    suffix = string.Empty;
                    mirrorHorizontally = false;
                    return false;
            }
        }

        private static float SafeScaleRatio(
            float value,
            float divisor)
        {
            return Mathf.Abs(divisor) > 0.0001f
                ? Mathf.Abs(value / divisor)
                : Mathf.Abs(value);
        }

        private void OnDrawGizmos()
        {
            Color solid = guideColor;
            solid.a = Mathf.Clamp(guideColor.a, 0.08f, 0.45f);
            Color wire = guideColor;
            wire.a = 1f;

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Color oldColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;

            switch (kind)
            {
                case CraftLivePad2AlignmentGuideKind.Material:
                    Gizmos.color = solid;
                    Gizmos.DrawCube(Vector3.zero, Vector3.one);
                    Gizmos.color = wire;
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                    DrawArrow(Vector3.zero, Vector3.up * 0.85f);
                    break;
                case CraftLivePad2AlignmentGuideKind.FlowStart:
                case CraftLivePad2AlignmentGuideKind.FlowEnd:
                    Gizmos.color = solid;
                    Gizmos.DrawSphere(Vector3.zero, 0.5f);
                    Gizmos.color = wire;
                    Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
                    break;
                case CraftLivePad2AlignmentGuideKind.FlowWidth:
                    Gizmos.color = solid;
                    Gizmos.DrawCube(Vector3.zero, Vector3.one);
                    Gizmos.color = wire;
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                    break;
                case CraftLivePad2AlignmentGuideKind.Pool:
                    DrawDisc(solid, wire);
                    break;
            }

            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;

#if UNITY_EDITOR
            Handles.Label(
                transform.position,
                string.IsNullOrWhiteSpace(guideLabel)
                    ? name
                    : guideLabel);
#endif
        }

        private static void DrawArrow(Vector3 start, Vector3 end)
        {
            Gizmos.DrawLine(start, end);
            Vector3 direction = (end - start).normalized;
            Vector3 side = Vector3.right * 0.16f;
            Gizmos.DrawLine(end, end - direction * 0.22f + side);
            Gizmos.DrawLine(end, end - direction * 0.22f - side);
        }

        private static void DrawDisc(Color solid, Color wire)
        {
            const int segments = 32;
            Vector3 previous = new Vector3(0.5f, 0f, 0f);
            Gizmos.color = wire;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = new Vector3(
                    Mathf.Cos(angle) * 0.5f,
                    Mathf.Sin(angle) * 0.5f,
                    0f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }

            Gizmos.color = solid;
            Gizmos.DrawCube(
                Vector3.zero,
                new Vector3(0.7f, 0.7f, 0.12f));
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CraftLivePad2AlignmentGuide))]
    internal sealed class CraftLivePad2AlignmentGuideEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            CraftLivePad2AlignmentGuide guide =
                (CraftLivePad2AlignmentGuide)target;
            if (IsPlacementGuide(guide))
            {
                EditorGUILayout.HelpBox(
                    guide.Kind == CraftLivePad2AlignmentGuideKind.Material
                        ? "Sceneビューでハンドルを動かすと、素材の設置位置を直接調整できます。青いZハンドルがカメラから見た奥行です。ガイドの大きさは素材プレビューの大きさになります。"
                        : "この枠に専用Materialガイドがない場合、このPoolガイドが素材の設置位置にもなります。青いZハンドルでカメラから見た奥行を調整できます。",
                    MessageType.Info);
            }
        }

        private void OnSceneGUI()
        {
            CraftLivePad2AlignmentGuide guide =
                (CraftLivePad2AlignmentGuide)target;
            if (guide == null || !IsPlacementGuide(guide))
            {
                return;
            }

            Transform guideTransform = guide.transform;
            EditorGUI.BeginChangeCheck();
            Vector3 position = Handles.PositionHandle(
                guideTransform.position,
                guideTransform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(
                    guideTransform,
                    "Move Pad2 material guide");
                guideTransform.position = position;
                EditorUtility.SetDirty(guideTransform);
            }

            Handles.Label(
                guideTransform.position -
                guideTransform.forward *
                HandleUtility.GetHandleSize(
                    guideTransform.position) * 0.45f,
                "Z = カメラ奥行");
        }

        private static bool IsPlacementGuide(
            CraftLivePad2AlignmentGuide guide)
        {
            return guide != null &&
                   (guide.Kind ==
                        CraftLivePad2AlignmentGuideKind.Material ||
                    guide.Kind ==
                        CraftLivePad2AlignmentGuideKind.Pool);
        }
    }
#endif
}
