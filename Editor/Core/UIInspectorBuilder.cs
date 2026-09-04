using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal static class UIInspectorBuilder
    {
        public static VisualElement Build(SerializedObject serializedObject, UnityEngine.Object[] targets)
        {
            var root = new VisualElement { name = "flexus-ui-inspector" };
            root.AddToClassList("flexus-ui-inspector");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "flexus-theme--dark" : "flexus-theme--light");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.flexus.inspector/Editor/USS/FlexusUIInspector.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            if (targets == null || targets.Length == 0 || !targets[0])
            {
                root.Add(new HelpBox("Inspector target is missing.", HelpBoxMessageType.Error));
                return root;
            }

            var targetType = targets[0].GetType();
            if (targetType.IsDefined(typeof(UseUnityInspectorAttribute), true))
                return new InspectorElement(serializedObject);

            var type = InspectorMetadataCache.Get(targetType);
            var context = new InspectorContext(serializedObject, targets, type, root);

            if (!type.HideMonoScript)
            {
                var script = serializedObject.FindProperty("m_Script");
                if (script != null)
                {
                    var scriptField = new PropertyField(script);
                    scriptField.SetEnabled(false);
                    scriptField.AddToClassList("flexus-ui-inspector__script-field");
                    root.Add(scriptField);
                }
            }

            var groups = CreateGroups(type, root);
            foreach (var descriptor in type.Members)
            {
                var property = descriptor.Kind == InspectorMemberKind.Field
                    ? serializedObject.FindProperty(descriptor.Name)
                    : null;
                var memberContext = new MemberContext(context, descriptor, property);
                var element = CreateMember(memberContext);
                // Type-level ReadOnly protects data, but method buttons are actions rather than values.
                // Keep them interactive unless a condition or a member-level ReadOnly disables them.
                if (type.ReadOnly && descriptor.Kind != InspectorMemberKind.Method)
                    element.SetEnabled(false);

                if (!string.IsNullOrEmpty(descriptor.GroupPath) &&
                    groups.TryGetValue(descriptor.GroupPath, out var group))
                    group.Add(element, descriptor.TabName);
                else
                    root.Add(element);
            }

            root.Bind(serializedObject);
            FieldColumnLayoutController.Attach(root);
            return root;
        }

        private static MemberElement CreateMember(MemberContext context)
        {
            var element = new MemberElement(context);
            element.ReplaceContent(DefaultFieldFactory.Create(context));
            foreach (var extension in InspectorExtensionRegistry.Extensions)
            {
                try
                {
                    if (extension.CanApply(context)) extension.Apply(element, context);
                }
                catch (Exception exception)
                {
                    element.Validation.Add(new HelpBox(
                        $"{extension.GetType().Name}: {exception.GetBaseException().Message}",
                        HelpBoxMessageType.Error));
                    Debug.LogException(exception);
                }
            }
            return element;
        }

        private static Dictionary<string, GroupHost> CreateGroups(TypeDescriptor type, VisualElement root)
        {
            var result = new Dictionary<string, GroupHost>();
            var descriptors = type.Groups.OrderBy(group => Depth(group.Path)).ToArray();
            foreach (var descriptor in descriptors)
            {
                var host = new GroupHost(descriptor);
                result[descriptor.Path] = host;
                var parentPath = ParentPath(descriptor.Path);
                if (parentPath != null && result.TryGetValue(parentPath, out var parent))
                    parent.AddGroup(host.Root);
                else
                    root.Add(host.Root);
            }
            return result;
        }

        private static int Depth(string path) => string.IsNullOrEmpty(path) ? 0 : path.Count(character => character == '/');
        private static string ParentPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var index = path.LastIndexOf('/');
            return index < 0 ? null : path.Substring(0, index);
        }
    }
}
