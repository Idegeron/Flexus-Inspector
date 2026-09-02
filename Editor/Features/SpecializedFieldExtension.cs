using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class SpecializedFieldExtension : IInspectorExtension
    {
        public InspectorStage Stage => InspectorStage.Content;
        public int Order => -10;

        public bool CanApply(MemberContext context)
        {
            var descriptor = context.Descriptor;
            if (descriptor.HasAttribute<UseUnityDrawerAttribute>()) return false;
            return descriptor.HasAttribute<DropdownAttribute>() || descriptor.HasAttribute<AssetDropdownAttribute>() ||
                   descriptor.HasAttribute<SceneAttribute>() || descriptor.HasAttribute<LayerAttribute>() ||
                   descriptor.HasAttribute<AnimatorParameterAttribute>() || descriptor.HasAttribute<MaterialPropertyAttribute>() ||
                   descriptor.HasAttribute<SliderAttribute>() || descriptor.HasAttribute<MinMaxSliderAttribute>() ||
                   descriptor.HasAttribute<PropertyTextAreaAttribute>() || descriptor.HasAttribute<TextAreaAttribute>() ||
                   descriptor.HasAttribute<DisplayAsStringAttribute>() ||
                   descriptor.ValueType == typeof(Type) || descriptor.ValueType == typeof(SerializableType) ||
                   context.SerializedProperty?.propertyType == SerializedPropertyType.ManagedReference;
        }

        public void Apply(MemberElement element, MemberContext context)
        {
            var descriptor = context.Descriptor;
            if (descriptor.HasAttribute<DisplayAsStringAttribute>())
            {
                var label = new Label();
                void Refresh() => label.text = $"{descriptor.DisplayName}: {FormatValue(context.Value.GetValue())}";
                Refresh();
                element.schedule.Execute(Refresh).Every(250);
                element.ReplaceContent(label);
                return;
            }

            var textArea = descriptor.GetAttribute<PropertyTextAreaAttribute>();
            var unityTextArea = descriptor.GetAttribute<TextAreaAttribute>();
            if ((textArea != null || unityTextArea != null) && descriptor.ValueType == typeof(string))
            {
                var field = new TextField(descriptor.DisplayName)
                {
                    multiline = true,
                    value = context.Value.GetValue() as string ?? string.Empty,
                };
                field.AddToClassList("flexus-text-area-field");
                var minLines = textArea?.MinLines ?? unityTextArea.minLines;
                var maxLines = textArea?.MaxLines ?? unityTextArea.maxLines;
                field.style.minHeight = Mathf.Max(2, minLines) * 18;
                field.style.maxHeight = Mathf.Max(minLines, maxLines) * 18;
                field.RegisterValueChangedCallback(evt => context.Value.SetValue(evt.newValue));
                element.ReplaceContent(field);
                return;
            }

            var slider = descriptor.GetAttribute<SliderAttribute>();
            if (slider != null)
            {
                var (min, max) = ResolveRange(context, slider.Min, slider.Max, slider.MinMember, slider.MaxMember);
                if (descriptor.ValueType == typeof(int))
                {
                    var field = new SliderInt(descriptor.DisplayName, Mathf.RoundToInt(min), Mathf.RoundToInt(max))
                        { value = Convert.ToInt32(context.Value.GetValue() ?? 0), showInputField = true };
                    field.RegisterValueChangedCallback(evt => context.Value.SetValue(evt.newValue));
                    element.schedule.Execute(() =>
                    {
                        var range = ResolveRange(context, slider.Min, slider.Max, slider.MinMember, slider.MaxMember);
                        field.lowValue = Mathf.RoundToInt(range.min);
                        field.highValue = Mathf.RoundToInt(range.max);
                        if (slider.AutoClamp)
                        {
                            var clamped = Mathf.Clamp(field.value, field.lowValue, field.highValue);
                            if (clamped != field.value) context.Value.SetValue(clamped, "Clamp Slider Value");
                            field.SetValueWithoutNotify(clamped);
                        }
                    }).Every(250);
                    element.ReplaceContent(field);
                }
                else
                {
                    var field = new Slider(descriptor.DisplayName, min, max)
                        { value = Convert.ToSingle(context.Value.GetValue() ?? 0f), showInputField = true };
                    field.RegisterValueChangedCallback(evt => context.Value.SetValue(evt.newValue));
                    element.schedule.Execute(() =>
                    {
                        var range = ResolveRange(context, slider.Min, slider.Max, slider.MinMember, slider.MaxMember);
                        field.lowValue = range.min;
                        field.highValue = range.max;
                        if (slider.AutoClamp)
                        {
                            var clamped = Mathf.Clamp(field.value, field.lowValue, field.highValue);
                            if (!Mathf.Approximately(clamped, field.value)) context.Value.SetValue(clamped, "Clamp Slider Value");
                            field.SetValueWithoutNotify(clamped);
                        }
                    }).Every(250);
                    element.ReplaceContent(field);
                }
                return;
            }

            var minMax = descriptor.GetAttribute<MinMaxSliderAttribute>();
            if (minMax != null)
            {
                var (min, max) = ResolveRange(context, minMax.Min, minMax.Max, minMax.MinMember, minMax.MaxMember);
                var current = context.Value.GetValue();
                var vector = current is Vector2Int vi ? new Vector2(vi.x, vi.y) : current is Vector2 vf ? vf : new Vector2(min, max);
                var field = new MinMaxSlider(descriptor.DisplayName, vector.x, vector.y, min, max);
                field.RegisterValueChangedCallback(evt => context.Value.SetValue(descriptor.ValueType == typeof(Vector2Int)
                    ? new Vector2Int(Mathf.RoundToInt(evt.newValue.x), Mathf.RoundToInt(evt.newValue.y))
                    : evt.newValue));
                element.schedule.Execute(() =>
                {
                    var range = ResolveRange(context, minMax.Min, minMax.Max, minMax.MinMember, minMax.MaxMember);
                    field.lowLimit = range.min;
                    field.highLimit = range.max;
                    if (minMax.AutoClamp)
                    {
                        var clamped = new Vector2(Mathf.Clamp(field.value.x, range.min, range.max),
                            Mathf.Clamp(field.value.y, range.min, range.max));
                        if (clamped != field.value)
                            context.Value.SetValue(context.Descriptor.ValueType == typeof(Vector2Int)
                                ? new Vector2Int(Mathf.RoundToInt(clamped.x), Mathf.RoundToInt(clamped.y))
                                : clamped, "Clamp Min Max Value");
                        field.SetValueWithoutNotify(clamped);
                    }
                }).Every(250);
                element.ReplaceContent(field);
                return;
            }

            if (context.SerializedProperty?.propertyType == SerializedPropertyType.ManagedReference)
            {
                var label = context.Descriptor.HasAttribute<HideLabelAttribute>()
                    ? null
                    : context.Descriptor.DisplayName;
                var reference = new ManagedReferenceElement(context.SerializedProperty,
                    context.Descriptor.ValueType, label,
                    !context.Descriptor.HasAttribute<HideReferencePickerAttribute>());
                if (context.Descriptor.HasAttribute<InlinePropertyAttribute>())
                    reference.AddToClassList("flexus-managed-reference--inline");
                element.ReplaceContent(reference);
                return;
            }

            if (descriptor.ValueType == typeof(Type) || descriptor.ValueType == typeof(SerializableType))
            {
                element.ReplaceContent(CreateTypeField(context));
                return;
            }

            var dropdown = descriptor.GetAttribute<DropdownAttribute>();
            if (dropdown != null)
            {
                element.ReplaceContent(CreateDropdown(context, dropdown));
                return;
            }

            var assetDropdown = descriptor.GetAttribute<AssetDropdownAttribute>();
            if (assetDropdown != null)
            {
                element.ReplaceContent(CreateAssetDropdown(context, assetDropdown));
                return;
            }

            if (descriptor.HasAttribute<SceneAttribute>())
            {
                element.ReplaceContent(CreateSceneDropdown(context, descriptor.GetAttribute<SceneAttribute>()));
                return;
            }
            if (descriptor.HasAttribute<LayerAttribute>())
            {
                element.ReplaceContent(CreateLayerDropdown(context));
                return;
            }
            var animator = descriptor.GetAttribute<AnimatorParameterAttribute>();
            if (animator != null)
            {
                element.ReplaceContent(CreateAnimatorDropdown(context, animator));
                return;
            }
            var material = descriptor.GetAttribute<MaterialPropertyAttribute>();
            if (material != null)
                element.ReplaceContent(CreateMaterialDropdown(context, material));
        }

        private static VisualElement CreateDropdown(MemberContext context, DropdownAttribute attribute)
        {
            IEnumerable<SearchItem> Items()
            {
                if (!MemberSourceResolver.TryGetValue(context.Inspector.PrimaryTarget, attribute.SourceMember,
                        out var source, out _) || source is not IEnumerable enumerable) yield break;
                foreach (var item in enumerable)
                {
                    if (item is IInspectorDropdownItem dropdownItem)
                        yield return new SearchItem(dropdownItem.Text, dropdownItem.UntypedValue);
                    else
                        yield return new SearchItem(item?.ToString() ?? "None", item);
                }
            }
            return new SearchDropdownElement(context.Descriptor.DisplayName, FormatValue(context.Value.GetValue()),
                Items, value => context.Value.SetValue(value));
        }

        private static VisualElement CreateAssetDropdown(MemberContext context, AssetDropdownAttribute attribute)
        {
            IEnumerable<SearchItem> Items()
            {
                if (attribute.AllowNone) yield return new SearchItem("None", null);
                foreach (var guid in AssetDatabase.FindAssets(attribute.Filter, attribute.Folders))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath(path, context.Descriptor.ValueType);
                    if (asset)
                        yield return new SearchItem(attribute.GetDisplayName(asset), asset, AssetPreview.GetMiniThumbnail(asset));
                }
            }
            var current = context.Value.GetValue() as UnityEngine.Object;
            return new SearchDropdownElement(context.Descriptor.DisplayName, current ? current.name : "None",
                Items, value => context.Value.SetValue(value));
        }

        private static VisualElement CreateSceneDropdown(MemberContext context, SceneAttribute attribute)
        {
            IEnumerable<SearchItem> Items()
            {
                var paths = attribute.BuildScenesOnly
                    ? EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path)
                    : AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath);
                foreach (var path in paths)
                    yield return new SearchItem(System.IO.Path.GetFileNameWithoutExtension(path), path);
            }
            return new SearchDropdownElement(context.Descriptor.DisplayName, FormatValue(context.Value.GetValue()),
                Items, value => context.Value.SetValue(context.Descriptor.ValueType == typeof(string)
                    ? System.IO.Path.GetFileNameWithoutExtension(value?.ToString()) : value));
        }

        private static VisualElement CreateLayerDropdown(MemberContext context)
        {
            IEnumerable<SearchItem> Items()
            {
                for (var index = 0; index < 32; index++)
                {
                    var name = LayerMask.LayerToName(index);
                    if (!string.IsNullOrEmpty(name)) yield return new SearchItem(name, index);
                }
            }
            var layer = Convert.ToInt32(context.Value.GetValue() ?? 0);
            return new SearchDropdownElement(context.Descriptor.DisplayName, LayerMask.LayerToName(layer),
                Items, value => context.Value.SetValue(value));
        }

        private static VisualElement CreateAnimatorDropdown(MemberContext context, AnimatorParameterAttribute attribute)
        {
            IEnumerable<SearchItem> Items()
            {
                if (!MemberSourceResolver.TryGetValue(context.Inspector.PrimaryTarget, attribute.AnimatorMember,
                        out var value, out _) || value is not Animator animator || !animator.runtimeAnimatorController) yield break;
                foreach (var parameter in animator.parameters)
                {
                    if (attribute.ParameterType.HasValue && parameter.type != attribute.ParameterType.Value) continue;
                    yield return new SearchItem(parameter.name,
                        context.Descriptor.ValueType == typeof(int) ? parameter.nameHash : parameter.name);
                }
            }
            return new SearchDropdownElement(context.Descriptor.DisplayName, FormatValue(context.Value.GetValue()),
                Items, value => context.Value.SetValue(value));
        }

        private static VisualElement CreateMaterialDropdown(MemberContext context, MaterialPropertyAttribute attribute)
        {
            IEnumerable<SearchItem> Items()
            {
                if (!MemberSourceResolver.TryGetValue(context.Inspector.PrimaryTarget, attribute.MaterialMember,
                        out var value, out _) || value is not Material material || !material.shader) yield break;
                for (var index = 0; index < material.shader.GetPropertyCount(); index++)
                {
                    var name = material.shader.GetPropertyName(index);
                    yield return new SearchItem(name,
                        context.Descriptor.ValueType == typeof(int) ? Shader.PropertyToID(name) : name);
                }
            }
            return new SearchDropdownElement(context.Descriptor.DisplayName, FormatValue(context.Value.GetValue()),
                Items, value => context.Value.SetValue(value));
        }

        private static VisualElement CreateTypeField(MemberContext context)
        {
            var constraint = context.Descriptor.GetAttribute<TypeConstraintAttribute>();
            var current = context.Value.GetValue();
            var currentType = current is SerializableType serializable ? serializable.Value : current as Type;
            IEnumerable<SearchItem> Items()
            {
                yield return new SearchItem("None", null);
                foreach (var type in InspectorVisuals.CandidateTypes(
                             constraint?.BaseType ?? typeof(object), constraint?.AllowAbstract == true)
                         .OrderBy(InspectorVisuals.TypePath))
                    yield return new SearchItem(InspectorVisuals.TypePath(type), type, null, type.FullName);
            }
            return new SearchDropdownElement(context.Descriptor.DisplayName, InspectorVisuals.TypeName(currentType), Items, value =>
            {
                if (context.Descriptor.ValueType == typeof(SerializableType))
                {
                    var wrapper = current as SerializableType ?? new SerializableType();
                    wrapper.Value = value as Type;
                    context.Value.SetValue(wrapper);
                }
                else context.Value.SetValue(value);
            });
        }

        private static (float min, float max) ResolveRange(MemberContext context, float min, float max,
            string minMember, string maxMember)
        {
            if (!string.IsNullOrEmpty(minMember) && MemberSourceResolver.TryGetValue(
                    context.Inspector.PrimaryTarget, minMember, out var minValue, out _)) min = Convert.ToSingle(minValue);
            if (!string.IsNullOrEmpty(maxMember) && MemberSourceResolver.TryGetValue(
                    context.Inspector.PrimaryTarget, maxMember, out var maxValue, out _)) max = Convert.ToSingle(maxValue);
            return (min, max);
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "None";
            if (value is IEnumerable enumerable && value is not string)
                return string.Join(", ", enumerable.Cast<object>().Select(item => item?.ToString() ?? "null"));
            return value.ToString();
        }
    }
}
