using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class ConditionExtension : IInspectorExtension
    {
        public InspectorStage Stage => InspectorStage.Visibility;
        public int Order => 0;

        public bool CanApply(MemberContext context)
        {
            var attributes = context.Descriptor.Attributes;
            return attributes.Any(attribute => attribute is ShowIfAttribute ||
                                               attribute is ShowInPlayModeAttribute ||
                                               attribute is HideInPlayModeAttribute ||
                                               attribute is ShowInEditModeAttribute ||
                                               attribute is HideInEditModeAttribute ||
                                               attribute is EnableInPlayModeAttribute ||
                                               attribute is DisableInPlayModeAttribute ||
                                               attribute is EnableInEditModeAttribute ||
                                               attribute is DisableInEditModeAttribute);
        }

        public void Apply(MemberElement element, MemberContext context)
        {
            void Refresh()
            {
                var target = context.Inspector.PrimaryTarget;
                var visible = true;
                var enabled = true;

                foreach (var attribute in context.Descriptor.Attributes)
                {
                    switch (attribute)
                    {
                        case HideIfAttribute hide:
                            visible &= !Matches(target, hide);
                            break;
                        case ShowIfAttribute show when attribute is not EnableIfAttribute:
                            visible &= Matches(target, show);
                            break;
                        case DisableIfAttribute disable:
                            enabled &= !Matches(target, disable);
                            break;
                        case EnableIfAttribute enable:
                            enabled &= Matches(target, enable);
                            break;
                        case ShowInPlayModeAttribute:
                            visible &= EditorApplication.isPlaying;
                            break;
                        case HideInPlayModeAttribute:
                            visible &= !EditorApplication.isPlaying;
                            break;
                        case ShowInEditModeAttribute:
                            visible &= !EditorApplication.isPlaying;
                            break;
                        case HideInEditModeAttribute:
                            visible &= EditorApplication.isPlaying;
                            break;
                        case EnableInPlayModeAttribute:
                            enabled &= EditorApplication.isPlaying;
                            break;
                        case DisableInPlayModeAttribute:
                            enabled &= !EditorApplication.isPlaying;
                            break;
                        case EnableInEditModeAttribute:
                            enabled &= !EditorApplication.isPlaying;
                            break;
                        case DisableInEditModeAttribute:
                            enabled &= EditorApplication.isPlaying;
                            break;
                    }
                }

                element.SetVisible(visible);
                element.SetEnabled(enabled && !context.Descriptor.HasAttribute<ReadOnlyAttribute>());
            }

            Refresh();
            element.schedule.Execute(Refresh).Every(150);
        }

        private static bool Matches(object target, ShowIfAttribute condition)
        {
            if (!MemberSourceResolver.TryGetValue(target, condition.MemberName, out var actual, out _))
                return false;
            if (actual is UnityEngine.Object unityObject && condition.ExpectedValue == null)
                return !unityObject;
            return Equals(actual, condition.ExpectedValue);
        }
    }

    internal sealed class StylingExtension : IInspectorExtension
    {
        public InspectorStage Stage => InspectorStage.Decorate;
        public int Order => 0;
        public bool CanApply(MemberContext context) => true;

        public void Apply(MemberElement element, MemberContext context)
        {
            var descriptor = context.Descriptor;
            if (descriptor.HasAttribute<ReadOnlyAttribute>() || context.Value.IsReadOnly)
                element.SetEnabled(false);

            var title = descriptor.GetAttribute<TitleAttribute>();
            if (title != null)
            {
                var label = new Label(ResolveText(context, title.Text, title.Dynamic));
                label.AddToClassList("flexus-ui-inspector__title");
                element.AddBefore(label);
                if (title.Line)
                {
                    var line = new VisualElement();
                    line.AddToClassList("flexus-ui-inspector__title-line");
                    element.AddBefore(line);
                }
                if (title.Dynamic)
                    element.schedule.Execute(() => label.text = ResolveText(context, title.Text, true)).Every(250);
            }

            var tooltip = descriptor.GetAttribute<PropertyTooltipAttribute>();
            if (tooltip != null)
            {
                element.tooltip = ResolveText(context, tooltip.Text, tooltip.Dynamic);
                if (tooltip.Dynamic)
                    element.schedule.Execute(() => element.tooltip = ResolveText(context, tooltip.Text, true)).Every(500);
            }

            var spacing = descriptor.GetAttribute<PropertySpaceAttribute>();
            if (spacing != null)
            {
                element.style.marginTop = spacing.Before;
                element.style.marginBottom = spacing.After;
            }

            var indent = descriptor.GetAttribute<IndentAttribute>();
            if (indent != null) element.style.marginLeft = indent.Level * 15f;

            var customClass = descriptor.GetAttribute<InspectorClassAttribute>();
            if (customClass != null && !string.IsNullOrWhiteSpace(customClass.ClassName))
                element.AddToClassList(customClass.ClassName);

            var color = descriptor.GetAttribute<GUIColorAttribute>();
            if (color != null)
            {
                void ApplyColor()
                {
                    var resolved = color.Color;
                    if (!string.IsNullOrEmpty(color.DynamicMember) &&
                        MemberSourceResolver.TryGetValue(context.Inspector.PrimaryTarget,
                            color.DynamicMember, out var value, out _) && value is Color dynamicColor)
                        resolved = dynamicColor;
                    element.style.color = resolved;
                }
                ApplyColor();
                if (!string.IsNullOrEmpty(color.DynamicMember))
                    element.schedule.Execute(ApplyColor).Every(250);
            }

            var unit = descriptor.GetAttribute<UnitAttribute>();
            if (unit != null)
            {
                element.Content.style.flexDirection = FlexDirection.Row;
                var field = element.Content.childCount > 0 ? element.Content[0] : null;
                if (field != null) field.style.flexGrow = 1;
                var unitLabel = new Label(ResolveText(context, unit.Unit, unit.Dynamic));
                unitLabel.AddToClassList("flexus-ui-inspector__unit");
                element.Content.Add(unitLabel);
                if (unit.Dynamic)
                    element.schedule.Execute(() => unitLabel.text = ResolveText(context, unit.Unit, true)).Every(250);
            }

            if (descriptor.HasAttribute<HideLabelAttribute>())
                element.schedule.Execute(() => SetLabelStyle(element, DisplayStyle.None, null)).ExecuteLater(0);

            var width = descriptor.GetAttribute<LabelWidthAttribute>();
            if (width != null)
                element.schedule.Execute(() => SetLabelStyle(element, null, width.Width)).ExecuteLater(0);

            var labelText = descriptor.GetAttribute<LabelTextAttribute>();
            if (labelText is { Dynamic: true })
            {
                void RefreshLabel()
                {
                    var text = ResolveText(context, labelText.Text, true);
                    var changed = false;
                    element.Query<Label>(className: "unity-base-field__label").ForEach(label =>
                    {
                        if (label.text == text) return;
                        label.text = text;
                        changed = true;
                    });
                    if (changed) FieldColumnLayoutController.RequestRefresh(element);
                }
                element.schedule.Execute(RefreshLabel).Every(250);
            }

            var onValueChanged = descriptor.GetAttribute<OnValueChangedAttribute>();
            if (onValueChanged != null && context.SerializedProperty != null)
            {
                element.TrackPropertyValue(context.SerializedProperty, changedProperty =>
                {
                    foreach (var target in context.Inspector.Targets)
                        MemberSourceResolver.Invoke(target, onValueChanged.MethodName, Array.Empty<object>(),
                            out var ignoredResult, out var ignoredError);
                });
            }
            else if (onValueChanged != null)
            {
                var previous = context.Value.GetValue();
                element.schedule.Execute(() =>
                {
                    var current = context.Value.GetValue();
                    if (Equals(previous, current)) return;
                    previous = current;
                    foreach (var target in context.Inspector.Targets)
                        MemberSourceResolver.Invoke(target, onValueChanged.MethodName, Array.Empty<object>(),
                            out var ignoredResult, out var ignoredError);
                }).Every(150);
            }
        }

        private static string ResolveText(MemberContext context, string text, bool dynamic)
        {
            if (!dynamic) return text ?? string.Empty;
            return MemberSourceResolver.TryGetValue(context.Inspector.PrimaryTarget, text, out var value, out _)
                ? value?.ToString() ?? string.Empty
                : text ?? string.Empty;
        }

        private static void SetLabelStyle(VisualElement root, DisplayStyle? display, float? width)
        {
            root.Query<Label>(className: "unity-base-field__label").ForEach(label =>
            {
                if (display.HasValue) label.style.display = display.Value;
                if (width.HasValue)
                {
                    label.AddToClassList("flexus-label--explicit-width");
                    label.style.width = width.Value;
                    label.style.minWidth = width.Value;
                    label.style.maxWidth = width.Value;
                    label.style.flexBasis = width.Value;
                    label.style.flexGrow = 0;
                    label.style.flexShrink = 0;
                }
            });
        }
    }
}
