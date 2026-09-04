using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class MethodButtonExtension : IInspectorExtension
    {
        public InspectorStage Stage => InspectorStage.Content;
        public int Order => -100;
        public bool CanApply(MemberContext context) => context.Descriptor.Kind == InspectorMemberKind.Method;

        public void Apply(MemberElement element, MemberContext context)
        {
            var method = (MethodInfo)context.Descriptor.Member;
            var attribute = context.Descriptor.GetAttribute<ButtonAttribute>();
            var parameters = method.GetParameters();
            var values = parameters.Select(DefaultValue).ToArray();
            var actionLabel = string.IsNullOrEmpty(attribute?.Label)
                ? ObjectNames.NicifyVariableName(method.Name)
                : attribute.Label;

            var root = new VisualElement();
            root.AddToClassList("flexus-method-action");
            if (parameters.Length > 0)
            {
                root.AddToClassList("flexus-method-action--with-parameters");
                var header = new VisualElement();
                header.AddToClassList("flexus-method-action__header");
                var body = new VisualElement();
                body.AddToClassList("flexus-method-action__parameters");
                var expanded = true;

                var toggle = new Button();
                toggle.AddToClassList("flexus-method-action__toggle");
                var arrow = new Label("▾");
                arrow.AddToClassList("flexus-method-action__arrow");
                var title = new Label(actionLabel);
                title.AddToClassList("flexus-method-action__title");
                toggle.Add(arrow);
                toggle.Add(title);
                toggle.Add(InspectorVisuals.Badge($"{parameters.Length} " +
                    (parameters.Length == 1 ? "parameter" : "parameters"), "muted"));
                toggle.clicked += () =>
                {
                    expanded = !expanded;
                    arrow.text = expanded ? "▾" : "›";
                    body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                };
                header.Add(toggle);

                void BuildParameterFields()
                {
                    body.Clear();
                    for (var index = 0; index < parameters.Length; index++)
                    {
                        var captured = index;
                        var field = DefaultFieldFactory.CreateForValue(parameters[index].ParameterType,
                            ObjectNames.NicifyVariableName(parameters[index].Name), values[index], false,
                            value => values[captured] = value);
                        field.AddToClassList("flexus-method-action__parameter");
                        body.Add(field);
                    }
                }

                var reset = InspectorVisuals.IconButton("↺", "Reset parameters to their default values", () =>
                {
                    for (var index = 0; index < parameters.Length; index++) values[index] = DefaultValue(parameters[index]);
                    BuildParameterFields();
                }, "reset");
                header.Add(reset);
                BuildParameterFields();
                root.Add(header);
                root.Add(body);
            }

            var button = new Button
            {
                text = parameters.Length == 0 ? actionLabel : $"Run {actionLabel}",
                tooltip = parameters.Length == 0 ? null : $"Invoke {method.Name} with the parameters above",
            };
            button.clicked += () => Invoke(context, method, values, attribute);
            button.AddToClassList("flexus-button");
            button.AddToClassList("flexus-button--primary");
            button.style.height = (float)(attribute?.Size ?? InspectorButtonSize.Medium);
            if (parameters.Length > 0)
            {
                var footer = new VisualElement();
                footer.AddToClassList("flexus-method-action__footer");
                footer.Add(button);
                root.Add(footer);
            }
            else root.Add(button);
            element.ReplaceContent(root);
        }

        private static object DefaultValue(ParameterInfo parameter)
        {
            if (parameter.HasDefaultValue && parameter.DefaultValue != DBNull.Value &&
                parameter.DefaultValue != Missing.Value) return parameter.DefaultValue;
            return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
        }

        private static void Invoke(MemberContext context, MethodInfo method, object[] values, ButtonAttribute attribute)
        {
            if (attribute?.Confirm == true && !EditorUtility.DisplayDialog("Confirm",
                    attribute.ConfirmationMessage, "Run", "Cancel")) return;

            context.Inspector.RecordUndo(method.Name);
            foreach (var target in context.Inspector.Targets)
            {
                try { method.Invoke(method.IsStatic ? null : target, values); }
                catch (Exception exception) { Debug.LogException(exception.GetBaseException()); }
            }
            context.Inspector.MarkDirty();
            context.Inspector.SerializedObject.Update();
        }
    }

    internal sealed class InlineButtonExtension : InspectorAttributeExtension<InlineButtonAttribute>
    {
        public override InspectorStage Stage => InspectorStage.Decorate;
        public override int Order => -50;

        protected override void Apply(MemberElement element, InlineButtonAttribute attribute, MemberContext context)
        {
            element.Content.style.flexDirection = FlexDirection.Row;
            if (element.Content.childCount > 0) element.Content[0].style.flexGrow = 1;
            var button = new Button(() =>
            {
                context.Inspector.RecordUndo(attribute.MethodName);
                foreach (var target in context.Inspector.Targets)
                {
                    if (!MemberSourceResolver.Invoke(target, attribute.MethodName, Array.Empty<object>(), out _, out var error))
                        Debug.LogError(error, target);
                }
                context.Inspector.MarkDirty();
            }) { text = string.IsNullOrEmpty(attribute.Label) ? attribute.MethodName : attribute.Label };
            button.AddToClassList("flexus-button");
            button.AddToClassList("flexus-button--inline");
            if (attribute.Width > 0) button.style.width = attribute.Width;
            element.Content.Add(button);
        }
    }

    internal sealed class EnumToggleButtonsExtension : InspectorAttributeExtension<EnumToggleButtonsAttribute>
    {
        public override InspectorStage Stage => InspectorStage.Content;

        protected override void Apply(MemberElement element, EnumToggleButtonsAttribute attribute, MemberContext context)
        {
            var type = context.Descriptor.ValueType;
            if (!type.IsEnum) return;
            var root = new VisualElement();
            root.AddToClassList("flexus-segmented-field");
            var label = new Label(context.Descriptor.DisplayName);
            label.AddToClassList("unity-base-field__label");
            root.Add(label);
            var row = new VisualElement();
            row.AddToClassList("flexus-segmented-control");
            var isFlags = type.IsDefined(typeof(FlagsAttribute), false);
            foreach (Enum value in Enum.GetValues(type))
            {
                var button = new Button { text = ObjectNames.NicifyVariableName(value.ToString()) };
                button.AddToClassList("flexus-segmented-control__button");
                if (row.childCount == 0) button.AddToClassList("flexus-segmented-control__button--first");
                button.style.flexGrow = 1;
                button.clicked += () =>
                {
                    var current = Convert.ToInt64(context.Value.GetValue() ?? 0);
                    var selected = Convert.ToInt64(value);
                    var next = isFlags ? current ^ selected : selected;
                    context.Value.SetValue(Enum.ToObject(type, next));
                    Refresh();
                };
                row.Add(button);
            }

            void Refresh()
            {
                var current = Convert.ToInt64(context.Value.GetValue() ?? 0);
                var index = 0;
                foreach (Enum value in Enum.GetValues(type))
                {
                    var selected = Convert.ToInt64(value);
                    row[index++].EnableInClassList("unity-button--checked",
                        isFlags ? selected != 0 && (current & selected) == selected : current == selected);
                }
            }

            Refresh();
            root.Add(row);
            element.ReplaceContent(root);
        }
    }
}
