using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Flexus.Inspector.Editor
{
    public interface IInspectorValueBackend
    {
        int Priority { get; }
        bool CanHandle(MemberContext context);
        bool IsReadOnly(MemberContext context);
        object GetValue(MemberContext context, int targetIndex);
        void SetValue(MemberContext context, object value, string undoName);
        bool HasMixedValues(MemberContext context);
    }

    internal static class InspectorValueBackendRegistry
    {
        private static IReadOnlyList<IInspectorValueBackend> backends;

        public static IInspectorValueBackend Find(MemberContext context)
        {
            backends ??= Discover();
            return backends.FirstOrDefault(backend => backend.CanHandle(context));
        }

        private static IReadOnlyList<IInspectorValueBackend> Discover()
        {
            var result = new List<IInspectorValueBackend>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IInspectorValueBackend>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) continue;
                try
                {
                    if (Activator.CreateInstance(type) is IInspectorValueBackend backend) result.Add(backend);
                }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            return result.OrderByDescending(backend => backend.Priority).ToArray();
        }
    }

    public sealed class InspectorValueAccessor
    {
        private readonly MemberContext context;
        private readonly IInspectorValueBackend customBackend;

        internal InspectorValueAccessor(MemberContext context)
        {
            this.context = context;
            customBackend = InspectorValueBackendRegistry.Find(context);
        }

        public bool IsSerialized => context.SerializedProperty != null;
        public Type ValueType => context.Descriptor.ValueType;
        public bool IsReadOnly => customBackend?.IsReadOnly(context) ??
                                  (context.Descriptor.Kind == InspectorMemberKind.Property &&
                                   ((PropertyInfo)context.Descriptor.Member).SetMethod == null);

        public object GetValue(int targetIndex = 0)
        {
            if (customBackend != null) return customBackend.GetValue(context, targetIndex);
            if (context.SerializedProperty != null && targetIndex == 0)
            {
                try { return context.SerializedProperty.boxedValue; }
                catch { return GetReflectionValue(context.Inspector.Targets[targetIndex]); }
            }
            return GetReflectionValue(context.Inspector.Targets[targetIndex]);
        }

        public void SetValue(object value, string undoName = "Inspector Change")
        {
            if (IsReadOnly)
                return;

            if (customBackend != null)
            {
                customBackend.SetValue(context, value, undoName);
                return;
            }

            context.Inspector.RecordUndo(undoName);
            if (context.SerializedProperty != null)
            {
                context.SerializedProperty.serializedObject.Update();
                SetSerializedValue(context.SerializedProperty, value);
                context.SerializedProperty.serializedObject.ApplyModifiedProperties();
            }
            else
            {
                foreach (var target in context.Inspector.Targets)
                    SetReflectionValue(target, value);
                context.Inspector.MarkDirty();
            }
        }

        public bool HasMixedValues()
        {
            if (customBackend != null) return customBackend.HasMixedValues(context);
            if (context.SerializedProperty != null)
                return context.SerializedProperty.hasMultipleDifferentValues;
            if (context.Inspector.Targets.Length < 2)
                return false;
            var first = GetReflectionValue(context.Inspector.Targets[0]);
            for (var index = 1; index < context.Inspector.Targets.Length; index++)
                if (!Equals(first, GetReflectionValue(context.Inspector.Targets[index]))) return true;
            return false;
        }

        private object GetReflectionValue(object target)
        {
            try
            {
                return context.Descriptor.Member switch
                {
                    FieldInfo field => field.GetValue(target),
                    PropertyInfo property => property.GetValue(target),
                    _ => null,
                };
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        private void SetReflectionValue(object target, object value)
        {
            try
            {
                switch (context.Descriptor.Member)
                {
                    case FieldInfo field:
                        field.SetValue(target, ConvertValue(value, field.FieldType));
                        break;
                    case PropertyInfo property when property.SetMethod != null:
                        property.SetValue(target, ConvertValue(value, property.PropertyType));
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static void SetSerializedValue(SerializedProperty property, object value)
        {
            try
            {
                property.boxedValue = value;
                return;
            }
            catch
            {
                // Some Unity property types do not expose boxedValue setters.
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: property.longValue = Convert.ToInt64(value); break;
                case SerializedPropertyType.Boolean: property.boolValue = Convert.ToBoolean(value); break;
                case SerializedPropertyType.Float: property.doubleValue = Convert.ToDouble(value); break;
                case SerializedPropertyType.String: property.stringValue = value?.ToString() ?? string.Empty; break;
                case SerializedPropertyType.Color: property.colorValue = (Color)value; break;
                case SerializedPropertyType.ObjectReference: property.objectReferenceValue = value as UnityEngine.Object; break;
                case SerializedPropertyType.Enum: property.enumValueIndex = Convert.ToInt32(value); break;
                case SerializedPropertyType.Vector2: property.vector2Value = (Vector2)value; break;
                case SerializedPropertyType.Vector3: property.vector3Value = (Vector3)value; break;
                case SerializedPropertyType.Vector4: property.vector4Value = (Vector4)value; break;
                case SerializedPropertyType.Vector2Int: property.vector2IntValue = (Vector2Int)value; break;
                case SerializedPropertyType.Vector3Int: property.vector3IntValue = (Vector3Int)value; break;
                case SerializedPropertyType.Rect: property.rectValue = (Rect)value; break;
                case SerializedPropertyType.Bounds: property.boundsValue = (Bounds)value; break;
                case SerializedPropertyType.ManagedReference: property.managedReferenceValue = value; break;
            }
        }

        private static object ConvertValue(object value, Type type)
        {
            if (value == null || type.IsInstanceOfType(value)) return value;
            if (type.IsEnum) return Enum.ToObject(type, value);
            return Convert.ChangeType(value, type);
        }
    }
}
