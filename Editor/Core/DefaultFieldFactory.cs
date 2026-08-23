using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal static class DefaultFieldFactory
    {
        public static VisualElement Create(MemberContext context)
        {
            if (context.SerializedProperty != null)
            {
                var propertyField = new PropertyField(context.SerializedProperty, context.Descriptor.DisplayName);
                propertyField.name = $"field-{context.Descriptor.Name}";
                propertyField.AddToClassList("flexus-value-field");
                return propertyField;
            }

            return CreateForValue(context.Descriptor.ValueType, context.Descriptor.DisplayName,
                context.Value.GetValue(), context.Value.HasMixedValues(),
                value => context.Value.SetValue(value));
        }

        public static VisualElement CreateForValue(Type type, string label, object value, bool mixed,
            Action<object> setter)
        {
            VisualElement field;
            if (type == typeof(int))
                field = Bind(new IntegerField(label) { value = value is int i ? i : 0 }, mixed, v => setter(v));
            else if (type == typeof(long))
                field = Bind(new LongField(label) { value = value is long l ? l : 0L }, mixed, v => setter(v));
            else if (type == typeof(float))
                field = Bind(new FloatField(label) { value = value is float f ? f : 0f }, mixed, v => setter(v));
            else if (type == typeof(double))
                field = Bind(new DoubleField(label) { value = value is double d ? d : 0d }, mixed, v => setter(v));
            else if (type == typeof(bool))
                field = Bind(new Toggle(label) { value = value is bool b && b }, mixed, v => setter(v));
            else if (type == typeof(string))
                field = Bind(new TextField(label) { value = value as string ?? string.Empty }, mixed, v => setter(v));
            else if (type.IsEnum)
            {
                var enumValue = value as Enum ?? (Enum)Enum.GetValues(type).GetValue(0);
                field = Bind(new EnumField(label, enumValue), mixed, v => setter(v));
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var objectField = new ObjectField(label)
                {
                    objectType = type,
                    allowSceneObjects = true,
                    value = value as UnityEngine.Object,
                };
                field = Bind(objectField, mixed, v => setter(v));
            }
            else if (type == typeof(Vector2))
                field = Bind(new Vector2Field(label) { value = value is Vector2 v ? v : default }, mixed, v => setter(v));
            else if (type == typeof(Vector3))
                field = Bind(new Vector3Field(label) { value = value is Vector3 v ? v : default }, mixed, v => setter(v));
            else if (type == typeof(Vector4))
                field = Bind(new Vector4Field(label) { value = value is Vector4 v ? v : default }, mixed, v => setter(v));
            else if (type == typeof(Vector2Int))
                field = Bind(new Vector2IntField(label) { value = value is Vector2Int v ? v : default }, mixed, v => setter(v));
            else if (type == typeof(Vector3Int))
                field = Bind(new Vector3IntField(label) { value = value is Vector3Int v ? v : default }, mixed, v => setter(v));
            else if (type == typeof(Color))
                field = Bind(new ColorField(label) { value = value is Color color ? color : Color.white }, mixed, v => setter(v));
            else if (type == typeof(Rect))
                field = Bind(new RectField(label) { value = value is Rect rect ? rect : default }, mixed, v => setter(v));
            else if (type == typeof(RectInt))
                field = Bind(new RectIntField(label) { value = value is RectInt rect ? rect : default }, mixed, v => setter(v));
            else if (type == typeof(Bounds))
                field = Bind(new BoundsField(label) { value = value is Bounds bounds ? bounds : default }, mixed, v => setter(v));
            else if (type == typeof(BoundsInt))
                field = Bind(new BoundsIntField(label) { value = value is BoundsInt bounds ? bounds : default }, mixed, v => setter(v));
            else if (type == typeof(AnimationCurve))
                field = Bind(new CurveField(label) { value = value as AnimationCurve ?? new AnimationCurve() }, mixed, v => setter(v));
            else if (type == typeof(Gradient))
                field = Bind(new GradientField(label) { value = value as Gradient ?? new Gradient() }, mixed, v => setter(v));
            else
            {
                var row = new VisualElement();
                row.AddToClassList("flexus-fallback-field");
                var name = new Label(label);
                name.AddToClassList("unity-base-field__label");
                row.Add(name);
                var valueLabel = new Label(value?.ToString() ?? "null");
                valueLabel.AddToClassList("flexus-fallback-field__value");
                row.Add(valueLabel);
                field = row;
            }

            field.name = "flexus-value-field";
            field.AddToClassList("flexus-value-field");
            return field;
        }

        private static BaseField<TValue> Bind<TValue>(BaseField<TValue> field, bool mixed, Action<TValue> setter)
        {
            field.showMixedValue = mixed;
            field.RegisterValueChangedCallback(evt => setter(evt.newValue));
            return field;
        }
    }
}
