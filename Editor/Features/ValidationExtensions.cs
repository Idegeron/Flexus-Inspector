using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class InfoBoxExtension : InspectorAttributeExtension<InfoBoxAttribute>
    {
        public override InspectorStage Stage => InspectorStage.Decorate;
        public override int Order => -100;

        protected override void Apply(MemberElement element, InfoBoxAttribute attribute, MemberContext context)
        {
            var box = new HelpBox(ResolveText(), Convert(attribute.MessageType));
            box.AddToClassList("flexus-message-box");
            element.AddBefore(box);

            void Refresh()
            {
                box.text = ResolveText();
                box.style.display = IsVisible() ? DisplayStyle.Flex : DisplayStyle.None;
            }

            bool IsVisible()
            {
                if (string.IsNullOrEmpty(attribute.VisibleIf)) return true;
                return MemberSourceResolver.TryGetValue(context.Inspector.PrimaryTarget, attribute.VisibleIf,
                    out var value, out _) && value is bool visible && visible;
            }

            string ResolveText()
            {
                if (!attribute.DynamicText) return attribute.Text;
                return MemberSourceResolver.TryGetValue(context.Inspector.PrimaryTarget, attribute.Text,
                    out var value, out _) ? value?.ToString() ?? string.Empty : attribute.Text;
            }

            Refresh();
            if (attribute.DynamicText || !string.IsNullOrEmpty(attribute.VisibleIf))
                element.schedule.Execute(Refresh).Every(250);
        }

        private static HelpBoxMessageType Convert(InspectorMessageType type) => type switch
        {
            InspectorMessageType.Warning => HelpBoxMessageType.Warning,
            InspectorMessageType.Error => HelpBoxMessageType.Error,
            InspectorMessageType.None => HelpBoxMessageType.None,
            _ => HelpBoxMessageType.Info,
        };
    }

    internal sealed class ValidationExtension : IInspectorExtension
    {
        public InspectorStage Stage => InspectorStage.Validate;
        public int Order => 0;

        public bool CanApply(MemberContext context)
        {
            var descriptor = context.Descriptor;
            return descriptor.HasAttribute<RequiredAttribute>() ||
                   descriptor.HasAttribute<RequiredGetAttribute>() ||
                   descriptor.HasAttribute<ValidateInputAttribute>() ||
                   descriptor.HasAttribute<AssetsOnlyAttribute>() ||
                   descriptor.HasAttribute<SceneObjectsOnlyAttribute>();
        }

        public void Apply(MemberElement element, MemberContext context)
        {
            void Refresh()
            {
                element.Validation.Clear();
                var value = context.Value.GetValue();
                var required = context.Descriptor.GetAttribute<RequiredAttribute>();
                if (required != null && IsMissing(value))
                {
                    var message = new VisualElement();
                    message.AddToClassList("flexus-validation-message");
                    var helpBox = new HelpBox(required.Message ?? $"{context.Descriptor.DisplayName} is required.",
                        HelpBoxMessageType.Error);
                    helpBox.AddToClassList("flexus-message-box");
                    message.Add(helpBox);
                    if (!string.IsNullOrEmpty(required.FixMethod))
                    {
                        var fix = new Button(() => InvokeFix(required.FixMethod)) { text = required.FixLabel };
                        fix.AddToClassList("flexus-button");
                        message.Add(fix);
                    }
                    element.Validation.Add(message);
                }

                var requiredGet = context.Descriptor.GetAttribute<RequiredGetAttribute>();
                if (requiredGet != null && IsMissing(value))
                {
                    var row = new VisualElement();
                    row.AddToClassList("flexus-validation-message");
                    var helpBox = new HelpBox("Component reference is missing.", HelpBoxMessageType.Error);
                    helpBox.AddToClassList("flexus-message-box");
                    row.Add(helpBox);
                    var find = new Button(() => ResolveComponent(requiredGet)) { text = "Find component" };
                    find.AddToClassList("flexus-button");
                    row.Add(find);
                    element.Validation.Add(row);
                }

                var validate = context.Descriptor.GetAttribute<ValidateInputAttribute>();
                if (validate != null)
                    AddCustomValidation(validate, value);

                if (context.Descriptor.HasAttribute<AssetsOnlyAttribute>() &&
                    value is UnityEngine.Object asset && asset && !EditorUtility.IsPersistent(asset))
                    element.Validation.Add(new HelpBox("Only project assets are allowed.", HelpBoxMessageType.Error));

                if (context.Descriptor.HasAttribute<SceneObjectsOnlyAttribute>() &&
                    value is UnityEngine.Object sceneObject && sceneObject && EditorUtility.IsPersistent(sceneObject))
                    element.Validation.Add(new HelpBox("Only scene objects are allowed.", HelpBoxMessageType.Error));
            }

            void InvokeFix(string method)
            {
                context.Inspector.RecordUndo(method);
                foreach (var target in context.Inspector.Targets)
                    MemberSourceResolver.Invoke(target, method, Array.Empty<object>(), out _, out _);
                context.Inspector.MarkDirty();
                Refresh();
            }

            void ResolveComponent(RequiredGetAttribute settings)
            {
                var requestedType = context.Descriptor.ValueType;
                var isArray = requestedType.IsArray;
                var componentType = isArray ? requestedType.GetElementType() : requestedType;
                if (componentType == null || !typeof(Component).IsAssignableFrom(componentType)) return;

                foreach (var target in context.Inspector.Targets)
                {
                    var component = target as Component;
                    var gameObject = component ? component.gameObject : target as GameObject;
                    if (!gameObject) continue;

                    object found;
                    if (isArray)
                    {
                        Component[] components;
                        if (settings.InParents)
                            components = gameObject.GetComponentsInParent(componentType, true);
                        else if (settings.InChildren)
                            components = gameObject.GetComponentsInChildren(componentType, true);
                        else
                            components = gameObject.GetComponents(componentType);
                        var array = Array.CreateInstance(componentType, components.Length);
                        Array.Copy(components, array, components.Length);
                        found = array;
                    }
                    else if (settings.InParents)
                        found = gameObject.GetComponentInParent(componentType, true);
                    else if (settings.InChildren)
                        found = gameObject.GetComponentInChildren(componentType, true);
                    else
                        found = gameObject.GetComponent(componentType);

                    if (found != null) context.Value.SetValue(found, "Find Required Component");
                }
                Refresh();
            }

            void AddCustomValidation(ValidateInputAttribute validate, object value)
            {
                object result;
                string error;
                if (!MemberSourceResolver.Invoke(context.Inspector.PrimaryTarget, validate.MethodName,
                        new[] { value }, out result, out error) &&
                    !MemberSourceResolver.Invoke(context.Inspector.PrimaryTarget, validate.MethodName,
                        Array.Empty<object>(), out result, out error))
                {
                    element.Validation.Add(new HelpBox(error, HelpBoxMessageType.Error));
                    return;
                }

                switch (result)
                {
                    case InspectorValidationResult validation when !validation.IsValid:
                        element.Validation.Add(new HelpBox(validation.Message, Convert(validation.MessageType)));
                        break;
                    case bool valid when !valid:
                        element.Validation.Add(new HelpBox("Validation failed.", HelpBoxMessageType.Error));
                        break;
                    case string message when !string.IsNullOrEmpty(message):
                        element.Validation.Add(new HelpBox(message, HelpBoxMessageType.Error));
                        break;
                }
            }

            Refresh();
            element.schedule.Execute(Refresh).Every(350);
        }

        private static bool IsMissing(object value)
        {
            if (value == null) return true;
            if (value is UnityEngine.Object unityObject) return !unityObject;
            if (value is string text) return string.IsNullOrWhiteSpace(text);
            if (value is ICollection collection) return collection.Count == 0;
            return false;
        }

        private static HelpBoxMessageType Convert(InspectorMessageType type) => type switch
        {
            InspectorMessageType.Warning => HelpBoxMessageType.Warning,
            InspectorMessageType.Error => HelpBoxMessageType.Error,
            InspectorMessageType.None => HelpBoxMessageType.None,
            _ => HelpBoxMessageType.Info,
        };
    }
}
