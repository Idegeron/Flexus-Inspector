using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal static class DropdownFieldFactory
    {
        public static SearchDropdownElement Create(MemberContext context, DropdownAttribute attribute)
        {
            var dropdown = Create(context.Descriptor.DisplayName, context.Value.GetValue(), attribute,
                new object[] { context.Inspector.PrimaryTarget }, value => context.Value.SetValue(value));
            if (context.SerializedProperty != null)
                dropdown.name = "dropdown-" + context.SerializedProperty.propertyPath;
            return dropdown;
        }

        public static VisualElement CreateSerializedOrDefault(SerializedProperty property, string label = null)
        {
            var copy = property.Copy();
            if (!SerializedPropertyOwnerResolver.TryResolve(copy, out var owner, out var field) || field == null)
                return new UnityEditor.UIElements.PropertyField(copy, label);

            var attribute = field.GetCustomAttributes(typeof(DropdownAttribute), true)
                .OfType<DropdownAttribute>().FirstOrDefault();
            if (attribute == null)
                return new UnityEditor.UIElements.PropertyField(copy, label);

            return CreateSerialized(copy, label ?? copy.displayName, attribute, owner);
        }

        public static VisualElement CreateReflectionOrDefault(object owner, FieldInfo field, string label,
            object rootTarget, Action<object> setter)
        {
            var attribute = field.GetCustomAttributes(typeof(DropdownAttribute), true)
                .OfType<DropdownAttribute>().FirstOrDefault();
            if (attribute == null)
                return DefaultFieldFactory.CreateForValue(field.FieldType, label, field.GetValue(owner), false, setter);

            var targets = ReferenceEquals(owner, rootTarget) || rootTarget == null
                ? new[] { owner }
                : new[] { owner, rootTarget };
            var dropdown = Create(label, field.GetValue(owner), attribute, targets, setter);
            dropdown.name = "dropdown-reflection-" + field.DeclaringType?.FullName + "." + field.Name;
            return dropdown;
        }

        public static SearchDropdownElement CreateSerialized(SerializedProperty property, string label,
            DropdownAttribute attribute, object owner = null)
        {
            var copy = property.Copy();
            if (owner == null)
                SerializedPropertyOwnerResolver.TryResolve(copy, out owner, out _);
            var root = copy.serializedObject.targetObject;
            var targets = ReferenceEquals(owner, root) || owner == null
                ? new[] { root }
                : new[] { owner, root };
            var dropdown = Create(label, GetValue(copy), attribute, targets, value =>
            {
                Undo.RecordObjects(copy.serializedObject.targetObjects, "Dropdown Change");
                copy.serializedObject.Update();
                InspectorValueAccessor.SetSerializedValue(copy, value);
                copy.serializedObject.ApplyModifiedProperties();
            });
            dropdown.name = "dropdown-" + copy.propertyPath;
            return dropdown;
        }

        internal static bool TryGetItems(IEnumerable<object> targets, DropdownAttribute attribute,
            out List<SearchItem> items, out string error)
        {
            items = new List<SearchItem>();
            error = null;
            foreach (var target in targets.Where(candidate => candidate != null).Distinct())
            {
                if (!MemberSourceResolver.TryGetValue(target, attribute.SourceMember, out var source,
                        out var candidateError))
                {
                    error = candidateError;
                    continue;
                }
                if (source is not IEnumerable enumerable || source is string)
                {
                    error = $"Member '{attribute.SourceMember}' on {target.GetType().Name} must return IEnumerable.";
                    continue;
                }

                foreach (var item in enumerable)
                {
                    if (item is IInspectorDropdownItem dropdownItem)
                        items.Add(new SearchItem(dropdownItem.Text, dropdownItem.UntypedValue));
                    else
                        items.Add(new SearchItem(item?.ToString() ?? "None", item));
                }
                return true;
            }
            error ??= $"Dropdown source '{attribute.SourceMember}' could not be resolved.";
            return false;
        }

        private static SearchDropdownElement Create(string label, object current, DropdownAttribute attribute,
            IReadOnlyList<object> targets, Action<object> setter)
        {
            IEnumerable<SearchItem> Items()
            {
                if (TryGetItems(targets, attribute, out var items, out var error)) return items;
                Debug.LogError(error);
                return Array.Empty<SearchItem>();
            }

            var initialText = FormatValue(current);
            if (TryGetItems(targets, attribute, out var initialItems, out var initialError))
            {
                var selected = initialItems.FirstOrDefault(item => Equals(item.Value, current));
                if (!string.IsNullOrEmpty(selected.Text)) initialText = selected.Text;
            }
            var dropdown = new SearchDropdownElement(label, initialText, Items, setter);
            if (!string.IsNullOrEmpty(initialError)) dropdown.tooltip = initialError;
            return dropdown;
        }

        private static object GetValue(SerializedProperty property)
        {
            try { return property.boxedValue; }
            catch
            {
                return property.propertyType switch
                {
                    SerializedPropertyType.Integer => property.longValue,
                    SerializedPropertyType.Boolean => property.boolValue,
                    SerializedPropertyType.Float => property.doubleValue,
                    SerializedPropertyType.String => property.stringValue,
                    SerializedPropertyType.Enum => property.enumValueIndex,
                    SerializedPropertyType.ObjectReference => property.objectReferenceValue,
                    _ => null,
                };
            }
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "None";
            if (value is IEnumerable enumerable && value is not string)
                return string.Join(", ", enumerable.Cast<object>().Select(item => item?.ToString() ?? "null"));
            return value.ToString();
        }
    }

    internal static class SerializedPropertyOwnerResolver
    {
        public static bool TryResolve(SerializedProperty property, out object owner, out FieldInfo field)
        {
            owner = null;
            field = null;
            object current = property.serializedObject.targetObject;
            if (current == null) return false;

            var parts = property.propertyPath.Split('.');
            for (var index = 0; index < parts.Length; index++)
            {
                var part = parts[index];
                if (part == "Array") continue;
                if (TryParseIndex(part, out var elementIndex))
                {
                    if (!TryGetElement(current, elementIndex, out current)) return field != null;
                    continue;
                }

                if (current == null) return false;
                var nextField = InspectorVisuals.SerializedField(current.GetType(), part);
                if (nextField == null) return false;
                owner = current;
                field = nextField;
                current = nextField.GetValue(current);
            }
            return field != null;
        }

        private static bool TryParseIndex(string part, out int index)
        {
            index = -1;
            if (!part.StartsWith("data[", StringComparison.Ordinal) || !part.EndsWith("]", StringComparison.Ordinal))
                return false;
            return int.TryParse(part.Substring(5, part.Length - 6), out index);
        }

        private static bool TryGetElement(object collection, int index, out object element)
        {
            element = null;
            if (collection is not IEnumerable enumerable || index < 0) return false;
            var position = 0;
            foreach (var item in enumerable)
            {
                if (position++ != index) continue;
                element = item;
                return true;
            }
            return false;
        }
    }

    [CustomPropertyDrawer(typeof(DropdownAttribute))]
    internal sealed class DropdownPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return DropdownFieldFactory.CreateSerialized(property, property.displayName,
                (DropdownAttribute)attribute);
        }
    }
}
