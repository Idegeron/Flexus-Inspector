using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class InlinePropertyExtension : InspectorAttributeExtension<InlinePropertyAttribute>
    {
        public override InspectorStage Stage => InspectorStage.Content;
        public override int Order => -5;

        protected override void Apply(MemberElement element, InlinePropertyAttribute attribute, MemberContext context)
        {
            if (context.Descriptor.HasAttribute<UseUnityDrawerAttribute>()) return;
            if (context.SerializedProperty?.propertyType == SerializedPropertyType.ManagedReference) return;
            var root = new VisualElement();
            root.AddToClassList("flexus-inline-property");
            if (context.SerializedProperty != null)
            {
                foreach (var child in InspectorVisuals.DirectChildren(context.SerializedProperty))
                    root.Add(new PropertyField(child.Copy()));
                root.Bind(context.SerializedProperty.serializedObject);
            }
            else
            {
                var value = context.Value.GetValue();
                if (value == null) return;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var field in value.GetType().GetFields(flags))
                {
                    if (field.IsStatic || field.IsLiteral || field.IsInitOnly) continue;
                    root.Add(DefaultFieldFactory.CreateForValue(field.FieldType,
                        ObjectNames.NicifyVariableName(field.Name), field.GetValue(value), false,
                        changed =>
                        {
                            context.Inspector.RecordUndo("Edit Inline Property");
                            field.SetValue(value, changed);
                            context.Inspector.MarkDirty();
                        }));
                }
            }

            if (attribute.LabelWidth > 0)
                root.schedule.Execute(() => root.Query<Label>(className: "unity-base-field__label").ForEach(label =>
                {
                    label.AddToClassList("flexus-label--explicit-width");
                    label.style.width = attribute.LabelWidth;
                    label.style.minWidth = attribute.LabelWidth;
                    label.style.maxWidth = attribute.LabelWidth;
                    label.style.flexBasis = attribute.LabelWidth;
                    label.style.flexGrow = 0;
                    label.style.flexShrink = 0;
                })).ExecuteLater(0);
            element.ReplaceContent(root);
        }
    }
}
