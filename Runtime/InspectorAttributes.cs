using System;
using UnityEngine;

namespace Flexus.Inspector
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class UseUIInspectorAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class UseUnityInspectorAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class UseUnityDrawerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ShowInInspectorAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method |
                    AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class ReadOnlyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class PropertyOrderAttribute : Attribute
    {
        public int Order { get; }
        public PropertyOrderAttribute(int order) => Order = order;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class OnValueChangedAttribute : Attribute
    {
        public string MethodName { get; }
        public OnValueChangedAttribute(string methodName) => MethodName = methodName;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property |
                    AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class InlinePropertyAttribute : Attribute
    {
        public float LabelWidth { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class HideMonoScriptAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class HideReferencePickerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RequiredAttribute : Attribute
    {
        public string Message { get; set; }
        public string FixMethod { get; set; }
        public string FixLabel { get; set; } = "Fix";
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RequiredGetAttribute : Attribute
    {
        public bool InParents { get; set; }
        public bool InChildren { get; set; }
        public bool IncludeSelf { get; set; } = true;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ValidateInputAttribute : Attribute
    {
        public string MethodName { get; }
        public ValidateInputAttribute(string methodName) => MethodName = methodName;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class InfoBoxAttribute : Attribute
    {
        public string Text { get; }
        public InspectorMessageType MessageType { get; }
        public string VisibleIf { get; set; }
        public bool DynamicText { get; set; }

        public InfoBoxAttribute(string text, InspectorMessageType messageType = InspectorMessageType.Info)
        {
            Text = text;
            MessageType = messageType;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class AssetsOnlyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SceneObjectsOnlyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class ShowIfAttribute : Attribute
    {
        public string MemberName { get; }
        public object ExpectedValue { get; }
        public ShowIfAttribute(string memberName) : this(memberName, true) { }
        public ShowIfAttribute(string memberName, object expectedValue)
        {
            MemberName = memberName;
            ExpectedValue = expectedValue;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HideIfAttribute : ShowIfAttribute
    {
        public HideIfAttribute(string memberName) : base(memberName) { }
        public HideIfAttribute(string memberName, object expectedValue) : base(memberName, expectedValue) { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class EnableIfAttribute : ShowIfAttribute
    {
        public EnableIfAttribute(string memberName) : base(memberName) { }
        public EnableIfAttribute(string memberName, object expectedValue) : base(memberName, expectedValue) { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class DisableIfAttribute : EnableIfAttribute
    {
        public DisableIfAttribute(string memberName) : base(memberName) { }
        public DisableIfAttribute(string memberName, object expectedValue) : base(memberName, expectedValue) { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class ShowInPlayModeAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class HideInPlayModeAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class EnableInPlayModeAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class DisableInPlayModeAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class ShowInEditModeAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class HideInEditModeAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class EnableInEditModeAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class DisableInEditModeAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class InspectorGroupAttribute : Attribute
    {
        public string Path { get; }
        public InspectorGroupStyle Style { get; }
        public string Title { get; set; }
        public bool Expanded { get; set; }
        public bool HideTitle { get; set; }
        public bool Collapsible { get; set; } = true;
        public float[] Sizes { get; set; }

        public InspectorGroupAttribute(string path, InspectorGroupStyle style = InspectorGroupStyle.Vertical)
        {
            Path = path;
            Style = style;
            Title = path;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class GroupAttribute : Attribute
    {
        public string Path { get; }
        public GroupAttribute(string path) => Path = path;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class GroupNextAttribute : Attribute
    {
        public string Path { get; }
        public GroupNextAttribute(string path) => Path = path;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class UngroupNextAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class TabAttribute : Attribute
    {
        public string Name { get; }
        public TabAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ButtonAttribute : Attribute
    {
        public string Label { get; set; }
        public InspectorButtonSize Size { get; set; } = InspectorButtonSize.Medium;
        public bool Confirm { get; set; }
        public string ConfirmationMessage { get; set; } = "Run this action?";
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class InlineButtonAttribute : Attribute
    {
        public string MethodName { get; }
        public string Label { get; set; }
        public float Width { get; set; }
        public InlineButtonAttribute(string methodName) => MethodName = methodName;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class EnumToggleButtonsAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class TitleAttribute : Attribute
    {
        public string Text { get; }
        public bool Dynamic { get; set; }
        public bool Line { get; set; } = true;
        public TitleAttribute(string text) => Text = text;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class HideLabelAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class LabelTextAttribute : Attribute
    {
        public string Text { get; }
        public bool Dynamic { get; set; }
        public LabelTextAttribute(string text) => Text = text;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class LabelWidthAttribute : Attribute
    {
        public float Width { get; }
        public LabelWidthAttribute(float width) => Width = width;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method |
                    AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class GUIColorAttribute : Attribute
    {
        public Color Color { get; }
        public string DynamicMember { get; }

        public GUIColorAttribute(float r, float g, float b, float a = 1f) => Color = new Color(r, g, b, a);
        public GUIColorAttribute(string dynamicMember) => DynamicMember = dynamicMember;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class IndentAttribute : Attribute
    {
        public int Level { get; }
        public IndentAttribute(int level = 1) => Level = level;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class PropertySpaceAttribute : Attribute
    {
        public float Before { get; set; }
        public float After { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PropertyTooltipAttribute : Attribute
    {
        public string Text { get; }
        public bool Dynamic { get; set; }
        public PropertyTooltipAttribute(string text) => Text = text;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InspectorClassAttribute : Attribute
    {
        public string ClassName { get; }
        public InspectorClassAttribute(string className) => ClassName = className;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class UnitAttribute : Attribute
    {
        public string Unit { get; }
        public bool Dynamic { get; set; }
        public UnitAttribute(string unit) => Unit = unit;

        public const string Meter = "m";
        public const string Centimeter = "cm";
        public const string Kilogram = "kg";
        public const string Second = "s";
        public const string Degree = "°";
        public const string MeterPerSecond = "m/s";
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class DropdownAttribute : Attribute
    {
        public string SourceMember { get; }
        public DropdownAttribute(string sourceMember) => SourceMember = sourceMember;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AssetDropdownAttribute : Attribute
    {
        public string Filter { get; }
        public string[] Folders { get; set; }
        public bool AllowNone { get; set; } = true;
        public AssetDropdownAttribute(string filter) => Filter = filter;
        public virtual string GetDisplayName(UnityEngine.Object asset) => asset ? asset.name : "None";
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SceneAttribute : Attribute
    {
        public bool BuildScenesOnly { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class LayerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class AnimatorParameterAttribute : Attribute
    {
        public string AnimatorMember { get; }
        public AnimatorControllerParameterType? ParameterType { get; }
        public AnimatorParameterAttribute(string animatorMember) => AnimatorMember = animatorMember;
        public AnimatorParameterAttribute(string animatorMember, AnimatorControllerParameterType parameterType)
        {
            AnimatorMember = animatorMember;
            ParameterType = parameterType;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class MaterialPropertyAttribute : Attribute
    {
        public string MaterialMember { get; }
        public MaterialPropertyAttribute(string materialMember) => MaterialMember = materialMember;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SliderAttribute : Attribute
    {
        public float Min { get; } = 0f;
        public float Max { get; } = 1f;
        public string MinMember { get; }
        public string MaxMember { get; }
        public bool AutoClamp { get; set; }

        public SliderAttribute() { }
        public SliderAttribute(float min, float max) { Min = min; Max = max; }
        public SliderAttribute(string minMember, string maxMember)
        {
            MinMember = minMember;
            MaxMember = maxMember;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class MinMaxSliderAttribute : Attribute
    {
        public float Min { get; } = 0f;
        public float Max { get; } = 1f;
        public string MinMember { get; }
        public string MaxMember { get; }
        public bool AutoClamp { get; set; }

        public MinMaxSliderAttribute() { }
        public MinMaxSliderAttribute(float min, float max) { Min = min; Max = max; }
        public MinMaxSliderAttribute(string minMember, string maxMember)
        {
            MinMember = minMember;
            MaxMember = maxMember;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PropertyTextAreaAttribute : Attribute
    {
        public int MinLines { get; }
        public int MaxLines { get; }
        public PropertyTextAreaAttribute(int minLines = 3, int maxLines = 12)
        {
            MinLines = minLines;
            MaxLines = maxLines;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class DisplayAsStringAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property |
                    AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class InlineEditorAttribute : Attribute
    {
        public InlineEditorMode Mode { get; set; } = InlineEditorMode.Inspector;
        public float PreviewHeight { get; set; } = 100f;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PreviewObjectAttribute : Attribute
    {
        public float Height { get; set; } = 100f;
        public bool DrawField { get; set; } = true;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PreviewMeshAttribute : Attribute
    {
        public float Height { get; set; } = 200f;
        public bool Foldout { get; set; } = true;
        public MeshPreviewRotation Rotation { get; set; } = MeshPreviewRotation.Clamped;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ListDrawerSettingsAttribute : Attribute
    {
        public bool Draggable { get; set; } = true;
        public bool HideAddButton { get; set; }
        public bool HideRemoveButton { get; set; }
        public bool AlwaysExpanded { get; set; }
        public bool ShowElementLabels { get; set; }
        public bool AlternatingBackground { get; set; } = true;
        public int ItemsPerPage { get; set; } = 50;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableListAttribute : ListDrawerSettingsAttribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class DictionaryDrawerSettingsAttribute : Attribute
    {
        public bool AlwaysExpanded { get; set; }
        public int ItemsPerPage { get; set; } = 50;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TypeConstraintAttribute : Attribute
    {
        public Type BaseType { get; }
        public bool AllowAbstract { get; set; }
        public TypeConstraintAttribute(Type baseType) => BaseType = baseType;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
    public sealed class TypeNameAttribute : Attribute
    {
        public string Name { get; }
        public TypeNameAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ShowInspectorDiagnosticsAttribute : Attribute { }
}
