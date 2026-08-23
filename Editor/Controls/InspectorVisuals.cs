using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal static class InspectorVisuals
    {
        private static readonly string[] PreferredTitleProperties = { "name", "title", "label", "id", "key" };

        public static Label Badge(string text, string modifier = null)
        {
            var badge = new Label(text ?? string.Empty);
            badge.AddToClassList("flexus-badge");
            if (!string.IsNullOrEmpty(modifier)) badge.AddToClassList($"flexus-badge--{modifier}");
            return badge;
        }

        public static Button IconButton(string text, string tooltip, Action action, string modifier = null)
        {
            var button = new Button(action) { text = text, tooltip = tooltip };
            button.AddToClassList("flexus-icon-button");
            if (!string.IsNullOrEmpty(modifier)) button.AddToClassList($"flexus-icon-button--{modifier}");
            return button;
        }

        public static VisualElement EmptyState(string title, string hint = null)
        {
            var root = new VisualElement();
            root.AddToClassList("flexus-empty-state");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("flexus-empty-state__title");
            root.Add(titleLabel);
            if (!string.IsNullOrEmpty(hint))
            {
                var hintLabel = new Label(hint);
                hintLabel.AddToClassList("flexus-empty-state__hint");
                root.Add(hintLabel);
            }
            return root;
        }

        public static string TypeName(Type type)
        {
            if (type == null) return "None";
            return type.GetCustomAttribute<TypeNameAttribute>()?.Name ?? ObjectNames.NicifyVariableName(type.Name);
        }

        public static string TypePath(Type type)
        {
            if (type == null) return "None";
            var label = TypeName(type);
            return string.IsNullOrEmpty(type.Namespace) ? label : $"{type.Namespace.Replace('.', '/')}/{label}";
        }

        public static IEnumerable<Type> CandidateTypes(Type baseType, bool allowAbstract = false,
            bool includeUnityObjectTypes = false)
        {
            if (baseType == null) yield break;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(type => type != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsGenericTypeDefinition || type.IsInterface ||
                        (!allowAbstract && type.IsAbstract) || !baseType.IsAssignableFrom(type) ||
                        (!includeUnityObjectTypes && typeof(UnityEngine.Object).IsAssignableFrom(type))) continue;
                    yield return type;
                }
            }
        }

        public static Type ListElementType(Type collectionType)
        {
            if (collectionType == null) return typeof(object);
            if (collectionType.IsArray) return collectionType.GetElementType() ?? typeof(object);
            if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(List<>))
                return collectionType.GetGenericArguments()[0];
            var enumerable = collectionType.GetInterfaces().FirstOrDefault(type => type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumerable?.GetGenericArguments()[0] ?? typeof(object);
        }

        public static FieldInfo SerializedField(Type ownerType, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;
            for (var current = ownerType; current != null; current = current.BaseType)
            {
                var field = current.GetField(fieldName, flags);
                if (field != null) return field;
            }
            return null;
        }

        public static bool IsSimple(SerializedProperty property) => property.propertyType switch
        {
            SerializedPropertyType.Generic => false,
            SerializedPropertyType.ManagedReference => false,
            _ => !property.hasVisibleChildren,
        };

        public static string ItemTitle(SerializedProperty property, int index, Type declaredType)
        {
            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                var actual = property.managedReferenceValue?.GetType();
                if (actual == null) return "Unassigned reference";
                var semanticTitle = ChildTitle(property);
                return string.IsNullOrWhiteSpace(semanticTitle) ? TypeName(actual) : semanticTitle;
            }

            if (property.propertyType == SerializedPropertyType.ObjectReference)
                return property.objectReferenceValue ? property.objectReferenceValue.name : $"Empty {TypeName(declaredType)}";

            if (property.hasVisibleChildren)
            {
                var semanticTitle = ChildTitle(property);
                if (!string.IsNullOrWhiteSpace(semanticTitle)) return semanticTitle;
            }

            return IsSimple(property) ? TypeName(declaredType) : $"{TypeName(declaredType)} {index + 1}";
        }

        private static string ChildTitle(SerializedProperty property)
        {
            foreach (var candidate in PreferredTitleProperties)
            {
                var child = property.FindPropertyRelative(candidate);
                if (child == null) continue;
                var value = child.propertyType switch
                {
                    SerializedPropertyType.String => child.stringValue,
                    SerializedPropertyType.Integer => child.longValue.ToString(),
                    SerializedPropertyType.Enum => child.enumDisplayNames.ElementAtOrDefault(child.enumValueIndex),
                    SerializedPropertyType.ObjectReference => child.objectReferenceValue ? child.objectReferenceValue.name : null,
                    _ => null,
                };
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return null;
        }

        public static IEnumerable<SerializedProperty> DirectChildren(SerializedProperty parent)
        {
            var copy = parent.Copy();
            var end = copy.GetEndProperty();
            var targetDepth = parent.depth + 1;
            if (!copy.NextVisible(true)) yield break;
            while (!SerializedProperty.EqualContents(copy, end))
            {
                if (copy.depth == targetDepth) yield return copy.Copy();
                if (!copy.NextVisible(false)) break;
            }
        }
    }
}
