using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal static class CollectionClipboard
    {
        private const string Format = "Flexus.Inspector.CollectionClipboard.v1";
        private const string LegacyFormat = "Flexus.UIInspector.CollectionClipboard.v1";

        [Serializable]
        private sealed class Payload
        {
            public string format;
            public string kind;
            public string valueType;
            public string ownerType;
            public string propertyPath;
            public string ownerJson;
            public string displayValue;
            public int elementIndex = -1;
        }

        public static bool HasContent => !string.IsNullOrWhiteSpace(EditorGUIUtility.systemCopyBuffer);

        public static void CopyCollection(SerializedProperty property, Type collectionType)
        {
            Write(property, collectionType, "collection", null);
        }

        public static void CopyElement(SerializedProperty property, Type valueType)
        {
            Write(property, valueType, "element", property.displayName);
        }

        public static void CopyDictionaryElement(SerializedProperty collectionProperty, Type valueType,
            int elementIndex, string displayValue)
        {
            Write(collectionProperty, valueType, "dictionary-element", displayValue, elementIndex);
        }

        public static bool CanPasteCollection(Type collectionType)
        {
            return TryRead(out var payload) && payload.kind == "collection" &&
                   AreCompatible(payload.valueType, collectionType);
        }

        public static bool TryPasteCollection(SerializedProperty destination, Type collectionType)
        {
            return TryPasteSerialized(destination, collectionType, "collection");
        }

        public static bool CanPasteElement(Type elementType)
        {
            return TryRead(out var payload) && payload.kind == "element" &&
                   AreCompatible(payload.valueType, elementType);
        }

        public static bool TryPasteElement(SerializedProperty destination, Type elementType)
        {
            return TryPasteSerialized(destination, elementType, "element");
        }

        public static bool CanPasteDictionaryElement(Type valueType)
        {
            return TryRead(out var payload) && payload.kind == "dictionary-element" && payload.elementIndex >= 0 &&
                   AreCompatible(payload.valueType, valueType);
        }

        public static bool TryReadDictionaryElement(Type valueType, out object value)
        {
            value = null;
            if (!CanPasteDictionaryElement(valueType) || !TryRead(out var payload)) return false;
            if (!TryCreatePayloadOwner(payload, out var temporaryOwner, out var temporaryHost)) return false;
            try
            {
                var collection = ResolveValue(temporaryOwner, payload.propertyPath) as IEnumerable;
                if (collection == null) return false;
                var index = 0;
                foreach (var entry in collection)
                {
                    if (index++ != payload.elementIndex) continue;
                    value = entry?.GetType().GetProperty("Value")?.GetValue(entry);
                    return true;
                }
                return false;
            }
            finally
            {
                DestroyTemporaryOwner(temporaryOwner, temporaryHost);
            }
        }

        private static bool TryPasteSerialized(SerializedProperty destination, Type valueType, string kind)
        {
            if (destination == null || !TryRead(out var payload) || payload.kind != kind ||
                !AreCompatible(payload.valueType, valueType)) return false;

            if (!TryCreatePayloadOwner(payload, out var temporaryOwner, out var temporaryHost)) return false;
            try
            {
                var sourceObject = new SerializedObject(temporaryOwner);
                sourceObject.Update();
                var source = sourceObject.FindProperty(payload.propertyPath);
                if (source == null) return false;

                destination.serializedObject.Update();
                // CopyFromSerializedProperty rejects otherwise compatible properties owned by
                // different SerializedObject instances in Unity 6000.3. Copy recursively so
                // arrays and managed-reference payloads keep their concrete values.
                if (!CopyValue(source, destination)) return false;
                destination.serializedObject.ApplyModifiedProperties();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                DestroyTemporaryOwner(temporaryOwner, temporaryHost);
            }
        }

        private static void Write(SerializedProperty property, Type valueType, string kind, string displayValue,
            int elementIndex = -1)
        {
            if (property?.serializedObject?.targetObject == null) return;
            var owner = property.serializedObject.targetObject;
            UnityEngine.Object temporaryOwner = null;
            GameObject temporaryHost = null;
            try
            {
                temporaryOwner = CreateTemporaryOwner(owner.GetType(), out temporaryHost);
                if (!temporaryOwner) return;
                property.serializedObject.UpdateIfRequiredOrScript();
                var snapshotProperty = kind == "element" ? FindArrayParent(property) ?? property : property;
                var temporarySerializedObject = new SerializedObject(temporaryOwner);
                temporarySerializedObject.CopyFromSerializedProperty(snapshotProperty);
                temporarySerializedObject.ApplyModifiedPropertiesWithoutUndo();
                if (ResolveValue(temporaryOwner, snapshotProperty.propertyPath) is ISerializationCallbackReceiver receiver)
                    receiver.OnAfterDeserialize();
                var payload = new Payload
                {
                    format = Format,
                    kind = kind,
                    valueType = valueType?.AssemblyQualifiedName,
                    ownerType = owner.GetType().AssemblyQualifiedName,
                    propertyPath = property.propertyPath,
                    ownerJson = EditorJsonUtility.ToJson(temporaryOwner, true),
                    displayValue = displayValue,
                    elementIndex = elementIndex,
                };
                EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(payload);
            }
            finally
            {
                if (temporaryHost) UnityEngine.Object.DestroyImmediate(temporaryHost);
                else if (temporaryOwner) UnityEngine.Object.DestroyImmediate(temporaryOwner);
            }
        }

        private static bool TryRead(out Payload payload)
        {
            payload = null;
            var text = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(text)) return false;
            try
            {
                payload = JsonUtility.FromJson<Payload>(text);
                return payload != null && (payload.format == Format || payload.format == LegacyFormat) &&
                       !string.IsNullOrEmpty(payload.ownerJson) &&
                       !string.IsNullOrEmpty(payload.propertyPath);
            }
            catch
            {
                return false;
            }
        }

        private static SerializedProperty FindArrayParent(SerializedProperty element)
        {
            const string marker = ".Array.data[";
            var markerIndex = element.propertyPath.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex <= 0) return null;
            return element.serializedObject.FindProperty(element.propertyPath.Substring(0, markerIndex));
        }

        private static bool AreCompatible(string sourceTypeName, Type destinationType)
        {
            var sourceType = Type.GetType(sourceTypeName);
            return sourceType != null && destinationType != null &&
                   (sourceType == destinationType || destinationType.IsAssignableFrom(sourceType));
        }

        private static UnityEngine.Object CreateTemporaryOwner(Type ownerType, out GameObject host)
        {
            host = null;
            if (typeof(Component).IsAssignableFrom(ownerType))
            {
                host = new GameObject("Flexus Collection Clipboard") { hideFlags = HideFlags.HideAndDontSave };
                return host.AddComponent(ownerType);
            }
            if (typeof(ScriptableObject).IsAssignableFrom(ownerType))
            {
                var instance = ScriptableObject.CreateInstance(ownerType);
                instance.hideFlags = HideFlags.HideAndDontSave;
                return instance;
            }
            return null;
        }

        private static bool TryCreatePayloadOwner(Payload payload, out UnityEngine.Object owner, out GameObject host)
        {
            owner = null;
            host = null;
            var ownerType = Type.GetType(payload.ownerType);
            if (ownerType == null) return false;
            try
            {
                owner = CreateTemporaryOwner(ownerType, out host);
                if (!owner) return false;
                EditorJsonUtility.FromJsonOverwrite(payload.ownerJson, owner);
                if (ResolveValue(owner, payload.propertyPath) is ISerializationCallbackReceiver receiver)
                    receiver.OnAfterDeserialize();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                DestroyTemporaryOwner(owner, host);
                owner = null;
                host = null;
                return false;
            }
        }

        private static void DestroyTemporaryOwner(UnityEngine.Object owner, GameObject host)
        {
            if (host) UnityEngine.Object.DestroyImmediate(host);
            else if (owner) UnityEngine.Object.DestroyImmediate(owner);
        }

        private static object ResolveValue(object owner, string propertyPath)
        {
            object current = owner;
            foreach (var segment in propertyPath.Split('.'))
            {
                if (current == null) return null;
                if (segment == "Array") continue;
                if (segment.StartsWith("data[", StringComparison.Ordinal))
                {
                    var closing = segment.IndexOf(']');
                    if (closing < 5 || !int.TryParse(segment.Substring(5, closing - 5), out var index) ||
                        current is not IList list || index < 0 || index >= list.Count) return null;
                    current = list[index];
                    continue;
                }
                var field = FindField(current.GetType(), segment);
                if (field == null) return null;
                current = field.GetValue(current);
            }
            return current;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        private static bool CopyValue(SerializedProperty source, SerializedProperty destination)
        {
            if (source.isArray && destination.isArray &&
                source.propertyType != SerializedPropertyType.String &&
                destination.propertyType != SerializedPropertyType.String)
            {
                destination.arraySize = source.arraySize;
                for (var index = 0; index < source.arraySize; index++)
                    if (!CopyValue(source.GetArrayElementAtIndex(index), destination.GetArrayElementAtIndex(index)))
                        return false;
                return true;
            }

            if (source.propertyType == SerializedPropertyType.ManagedReference &&
                destination.propertyType == SerializedPropertyType.ManagedReference)
            {
                destination.managedReferenceValue = source.managedReferenceValue;
                return true;
            }

            // Keep the destination object identity for inline serializable classes (notably
            // SerializableDictionary). Replacing the boxed object would leave an already-built
            // inspector pointing at the previous instance.
            if (source.propertyType == SerializedPropertyType.Generic &&
                destination.propertyType == SerializedPropertyType.Generic)
                return CopyChildren(source, destination);

            try
            {
                destination.boxedValue = source.boxedValue;
                return true;
            }
            catch (InvalidOperationException)
            {
                return CopyChildren(source, destination);
            }
            catch (ArgumentException)
            {
                return CopyChildren(source, destination);
            }
        }

        private static bool CopyChildren(SerializedProperty source, SerializedProperty destination)
        {
            foreach (var child in DirectChildren(source))
            {
                var targetChild = destination.FindPropertyRelative(child.name);
                if (targetChild == null || !CopyValue(child, targetChild)) return false;
            }
            return true;
        }

        private static IEnumerable<SerializedProperty> DirectChildren(SerializedProperty parent)
        {
            var iterator = parent.Copy();
            var end = iterator.GetEndProperty();
            var depth = parent.depth + 1;
            if (!iterator.NextVisible(true)) yield break;
            while (!SerializedProperty.EqualContents(iterator, end))
            {
                if (iterator.depth == depth) yield return iterator.Copy();
                if (!iterator.NextVisible(iterator.depth < depth)) break;
            }
        }
    }

    internal static class CollectionContextMenus
    {
        public static void AttachCollection(VisualElement target, Action clear, Action copy,
            Func<bool> canPaste, Action paste)
        {
            target.AddToClassList("flexus-context-menu--collection");
            target.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (CollectionSelection.IsRowTarget(evt.target, target)) return;
                evt.menu.AppendAction("Copy Collection", _ => copy());
                if (CollectionClipboard.HasContent)
                    evt.menu.AppendAction("Paste Collection", _ => paste(),
                        _ => canPaste() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Clear Collection", _ => clear());
                evt.StopPropagation();
            }));
        }

        public static void AttachElement(VisualElement target, Action copy, Func<bool> canPaste, Action paste)
        {
            target.AddToClassList("flexus-context-menu--element");
            target.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Copy Element", _ => copy());
                if (CollectionClipboard.HasContent)
                    evt.menu.AppendAction("Paste Element", _ => paste(),
                        _ => canPaste() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.StopPropagation();
            }));
        }
    }
}
