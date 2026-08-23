using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class DiagnosticsExtension : InspectorAttributeExtension<ShowInspectorDiagnosticsAttribute>
    {
        public override InspectorStage Stage => InspectorStage.Validate;
        public override int Order => 1000;

        protected override void Apply(MemberElement element, ShowInspectorDiagnosticsAttribute attribute,
            MemberContext context)
        {
            var foldout = new Foldout { text = "Inspector diagnostics", value = false };
            foldout.Add(new Label($"Member: {context.Descriptor.Member.DeclaringType?.FullName}.{context.Descriptor.Name}"));
            foldout.Add(new Label($"Value type: {context.Descriptor.ValueType.FullName}"));
            foldout.Add(new Label($"Serialized path: {context.SerializedProperty?.propertyPath ?? "Reflection"}"));
            foldout.Add(new Label("Attributes: " + string.Join(", ",
                context.Descriptor.Attributes.Select(item => item.GetType().Name))));
            foldout.Add(new Label("Extensions: " + string.Join(", ", InspectorExtensionRegistry.Extensions
                .Where(item => item.CanApply(context)).Select(item => item.GetType().Name))));
            element.AddAfter(foldout);
        }
    }

    internal sealed class InspectorDiagnosticsWindow : EditorWindow
    {
        [MenuItem("Tools/FLEXUS Inspector/Diagnostics")]
        private static void Open() => GetWindow<InspectorDiagnosticsWindow>("UI Inspector Diagnostics");

        public void CreateGUI()
        {
            rootVisualElement.Add(new Button(Refresh) { text = "Inspect current selection" });
            Refresh();
        }

        private void Refresh()
        {
            while (rootVisualElement.childCount > 1) rootVisualElement.RemoveAt(1);
            var selected = Selection.activeObject;
            if (!selected)
            {
                rootVisualElement.Add(new HelpBox("Select a MonoBehaviour or ScriptableObject.",
                    HelpBoxMessageType.Info));
                return;
            }
            var descriptor = InspectorMetadataCache.Get(selected.GetType());
            rootVisualElement.Add(new Label(descriptor.Type.FullName)
                { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            rootVisualElement.Add(new Label($"Members: {descriptor.Members.Count}, groups: {descriptor.Groups.Count}"));
            foreach (var member in descriptor.Members)
            {
                var foldout = new Foldout { text = $"{member.DisplayName} : {member.ValueType.Name}" };
                foldout.Add(new Label($"Kind: {member.Kind}; order: {member.Order}; group: {member.GroupPath ?? "root"}"));
                foldout.Add(new Label("Attributes: " + string.Join(", ", member.Attributes.Select(a => a.GetType().Name))));
                rootVisualElement.Add(foldout);
            }
        }
    }
}
