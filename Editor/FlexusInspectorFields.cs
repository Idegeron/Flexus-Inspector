using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    public static class FlexusInspectorFields
    {
        public static VisualElement CreateManagedReference(
            SerializedProperty property,
            Type declaredType,
            string label)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            var root = CreateRoot();

            root.Add(new ManagedReferenceElement(property, declaredType, label));
            root.Bind(property.serializedObject);
            FieldColumnLayoutController.Attach(root);

            return root;
        }

        public static VisualElement CreateManagedReferenceList(
            SerializedProperty property,
            Type collectionType,
            Type declaredElementType,
            string label,
            ListDrawerSettingsAttribute settings = null)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            var root = CreateRoot();
            var list = new SerializedListElement(
                property,
                collectionType,
                label,
                settings ?? new ListDrawerSettingsAttribute(),
                declaredElementType: declaredElementType);

            root.Add(list);
            root.Bind(property.serializedObject);
            FieldColumnLayoutController.Attach(root);

            return root;
        }

        private static VisualElement CreateRoot()
        {
            var root = new VisualElement();

            root.AddToClassList("flexus-ui-inspector");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "flexus-theme--dark" : "flexus-theme--light");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.flexus.inspector/Editor/USS/FlexusUIInspector.uss");

            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            return root;
        }
    }
}
