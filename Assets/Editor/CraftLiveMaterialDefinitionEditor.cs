using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEngine;

namespace CraftOrigin.CraftLiveEditor
{
    [CustomEditor(typeof(CraftLiveMaterialDefinition))]
    public sealed class CraftLiveMaterialDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSection(
                "Identity",
                "materialId",
                "displayName",
                "description",
                "category",
                "requiresQrUnlock");
            DrawSection(
                "Presentation",
                "icon",
                "worldPrefab",
                "pad1PreviewOffset",
                "pad1PreviewScale",
                "pad1HologramColor",
                "transferTicketPrefab",
                "effectColor",
                "placementEffectPrefab",
                "materialForm",
                "landingAudioClip",
                "abilitySummary",
                "usageSummary");

            CraftLiveMaterialCategory category =
                (CraftLiveMaterialCategory)serializedObject
                    .FindProperty("category")
                    .enumValueIndex;
            EditorGUILayout.Space();
            switch (category)
            {
                case CraftLiveMaterialCategory.Upgrade:
                    EditorGUILayout.LabelField(
                        "Base Stat Material",
                        EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("statModifiers"),
                        true);
                    break;

                case CraftLiveMaterialCategory.Attribute:
                    DrawSection(
                        "Attribute Material",
                        "attributeId",
                        "attributeDisplayName",
                        "elementEffect");
                    DrawSection(
                        "Pad4 Attribute Particle",
                        "pad4ParticlePrefab",
                        "pad4ParticleLocalPosition",
                        "pad4ParticleLocalEulerAngles",
                        "pad4ParticleLocalScale",
                        "tintPad4Particles");
                    break;

                case CraftLiveMaterialCategory.Skill:
                    DrawSection(
                        "Unique Skill Material",
                        "skillId",
                        "skillDisplayName",
                        "skillDescription",
                        "skillEffect");
                    DrawSkillValueHelp();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSection(string label, params string[] propertyNames)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property =
                    serializedObject.FindProperty(propertyName);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
        }

        private void DrawSkillValueHelp()
        {
            SerializedProperty effect =
                serializedObject.FindProperty("skillEffect");
            CraftLiveSkillType type =
                (CraftLiveSkillType)effect
                    .FindPropertyRelative("type")
                    .enumValueIndex;
            string message;
            switch (type)
            {
                case CraftLiveSkillType.Luck:
                    message =
                        "Primary Value: luck/critical bonus. " +
                        "Secondary Value: item acquisition bonus.";
                    break;
                case CraftLiveSkillType.DoubleStrike:
                    message =
                        "Primary Value: second-hit damage percent. " +
                        "Secondary Value is reserved.";
                    break;
                case CraftLiveSkillType.AutoHeal:
                    message =
                        "Primary Value: healing amount. " +
                        "Interval Seconds: time between heals.";
                    break;
                case CraftLiveSkillType.LifeOrb:
                    message =
                        "Primary Value: attack increase. " +
                        "Secondary Value: self-damage cost.";
                    break;
                default:
                    message =
                        "Select one of the four supported unique skills.";
                    break;
            }

            EditorGUILayout.HelpBox(message, MessageType.Info);
        }
    }
}
