using System.IO;
using UnityEditor;
using UnityEngine;

namespace UtsavNights.EditorTools
{
    internal static class EditorAssetUtil
    {
        /// <summary>Creates every missing folder in an "Assets/..." path.</summary>
        public static void EnsureFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(normalized));
        }

        public static void SetBool(SerializedObject so, string property, bool value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogWarning($"[Utsav setup] property '{property}' not found on {so.targetObject.GetType().Name}; skipped.");
                return;
            }

            p.boolValue = value;
        }

        public static void SetInt(SerializedObject so, string property, int value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogWarning($"[Utsav setup] property '{property}' not found on {so.targetObject.GetType().Name}; skipped.");
                return;
            }

            p.intValue = value;
        }

        public static void SetRelativeInt(SerializedProperty parent, string relative, int value)
        {
            SerializedProperty p = parent.FindPropertyRelative(relative);
            if (p != null)
            {
                p.intValue = value;
            }
        }

        public static void SetRelativeFloat(SerializedProperty parent, string relative, float value)
        {
            SerializedProperty p = parent.FindPropertyRelative(relative);
            if (p != null)
            {
                p.floatValue = value;
            }
        }
    }
}
