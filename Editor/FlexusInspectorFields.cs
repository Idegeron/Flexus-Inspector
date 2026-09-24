using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    public static class FlexusInspectorFields
    {
        public static VisualElement CreateProperty(
            SerializedProperty property,
            string label)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            var root = CreateRoot();

            root.Add(new PropertyField(property.Copy(), label));
            root.Bind(property.serializedObject);
            FieldColumnLayoutController.Attach(root);

            return root;
        }

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

        public static VisualElement CreateManagedObject(
            SerializedProperty property,
            Type declaredType,
            Func<MemberInfo, bool> memberFilter = null)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            var root = CreateRoot();
            Func<MemberDescriptor, bool> descriptorFilter = memberFilter == null
                ? null
                : descriptor => memberFilter(descriptor.Member);

            root.Add(new ManagedReferenceElement(
                property,
                declaredType,
                null,
                false,
                showHeader: false,
                memberFilter: descriptorFilter));
            root.Bind(property.serializedObject);
            FieldColumnLayoutController.Attach(root);

            return root;
        }

        public static VisualElement CreateList(
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
            var root = new VisualElement { name = "flexus-ui-inspector" };

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
