using UnityEngine;

namespace CraftOrigin.CraftLive
{
    [CreateAssetMenu(
        menuName = "CraftOrigin/Craft-live/Pad4 Calibration",
        fileName = "CraftLivePad4Calibration")]
    public sealed class CraftLivePad4Calibration : ScriptableObject
    {
        [Header("Physical Acrylic Plate")]
        [SerializeField, Min(1f)] private float plateWidthMillimeters = 180f;
        [SerializeField, Min(1f)] private float plateHeightMillimeters = 240f;
        [SerializeField, Range(0f, 90f)] private float plateAngleDegrees = 45f;
        [SerializeField, Min(0f)] private float distanceFromIpadMillimeters = 80f;

        [Header("Model Display")]
        [SerializeField] private Vector3 modelLocalPosition;
        [SerializeField] private Vector3 modelLocalEulerAngles;
        [SerializeField] private Vector3 modelScaleMultiplier = Vector3.one;
        [SerializeField] private float rotationSpeedDegreesPerSecond = 30f;

        public float PlateWidthMillimeters => plateWidthMillimeters;
        public float PlateHeightMillimeters => plateHeightMillimeters;
        public float PlateAngleDegrees => plateAngleDegrees;
        public float DistanceFromIpadMillimeters =>
            distanceFromIpadMillimeters;
        public Vector3 ModelLocalPosition => modelLocalPosition;
        public Quaternion ModelLocalRotation =>
            Quaternion.Euler(modelLocalEulerAngles);
        public Vector3 ModelScaleMultiplier => modelScaleMultiplier;
        public float RotationSpeedDegreesPerSecond =>
            rotationSpeedDegreesPerSecond;

        private void OnValidate()
        {
            plateWidthMillimeters = Mathf.Max(1f, plateWidthMillimeters);
            plateHeightMillimeters = Mathf.Max(1f, plateHeightMillimeters);
            plateAngleDegrees = Mathf.Clamp(plateAngleDegrees, 0f, 90f);
            distanceFromIpadMillimeters =
                Mathf.Max(0f, distanceFromIpadMillimeters);
            modelScaleMultiplier.x = Mathf.Max(0.001f, modelScaleMultiplier.x);
            modelScaleMultiplier.y = Mathf.Max(0.001f, modelScaleMultiplier.y);
            modelScaleMultiplier.z = Mathf.Max(0.001f, modelScaleMultiplier.z);
        }
    }
}
