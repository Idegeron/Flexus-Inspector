using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    public enum InspectorMemberKind
    {
        Field,
        Property,
        Method,
    }

    public sealed class MemberDescriptor
    {
        public MemberInfo Member { get; }
        public InspectorMemberKind Kind { get; }
        public Type ValueType { get; }
        public string Name => Member.Name;
        public string DisplayName { get; internal set; }
        public int Order { get; }
        public int DeclarationIndex { get; }
        public string GroupPath { get; internal set; }
        public string TabName { get; }
        public IReadOnlyList<Attribute> Attributes { get; }

        public MemberDescriptor(MemberInfo member, InspectorMemberKind kind, Type valueType,
            int declarationIndex, IReadOnlyList<Attribute> attributes)
        {
            Member = member;
            Kind = kind;
            ValueType = valueType;
            DeclarationIndex = declarationIndex;
            Attributes = attributes;
            var displaySource = member.Name.TrimStart('_');
            DisplayName = ObjectNames.NicifyVariableName(string.IsNullOrEmpty(displaySource) ? member.Name : displaySource);
            Order = attributes.OfType<PropertyOrderAttribute>().FirstOrDefault()?.Order ?? declarationIndex;
            GroupPath = attributes.OfType<GroupAttribute>().FirstOrDefault()?.Path;
            TabName = attributes.OfType<TabAttribute>().FirstOrDefault()?.Name;
            var label = attributes.OfType<LabelTextAttribute>().FirstOrDefault();
            if (label != null && !label.Dynamic)
                DisplayName = label.Text;
        }

        public T GetAttribute<T>() where T : Attribute => Attributes.OfType<T>().FirstOrDefault();
        public IEnumerable<T> GetAttributes<T>() where T : Attribute => Attributes.OfType<T>();
        public bool HasAttribute<T>() where T : Attribute => Attributes.Any(attribute => attribute is T);
    }

    public sealed class TypeDescriptor
    {
        public Type Type { get; }
        public IReadOnlyList<MemberDescriptor> Members { get; }
        public IReadOnlyList<InspectorGroupAttribute> Groups { get; }
        public bool HideMonoScript { get; }
        public bool ReadOnly { get; }

        public TypeDescriptor(Type type, IReadOnlyList<MemberDescriptor> members)
        {
            Type = type;
            Members = members;
            Groups = type.GetCustomAttributes<InspectorGroupAttribute>(true).ToArray();
            HideMonoScript = type.IsDefined(typeof(HideMonoScriptAttribute), true);
            ReadOnly = type.IsDefined(typeof(ReadOnlyAttribute), true);
        }
    }

    internal static class InspectorMetadataCache
    {
        private static readonly Dictionary<Type, TypeDescriptor> Cache = new Dictionary<Type, TypeDescriptor>();

        public static TypeDescriptor Get(Type type)
        {
            if (!Cache.TryGetValue(type, out var descriptor))
                Cache[type] = descriptor = Build(type);
            return descriptor;
        }

        public static void Clear() => Cache.Clear();

        private static TypeDescriptor Build(Type type)
        {
            var hierarchy = new Stack<Type>();
            for (var current = type; current != null && current != typeof(UnityEngine.Object); current = current.BaseType)
                hierarchy.Push(current);

            var result = new List<MemberDescriptor>();
            var declarationIndex = 0;
            string activeGroup = null;

            while (hierarchy.Count > 0)
            {
                var current = hierarchy.Pop();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.DeclaredOnly;
                var members = current.GetMembers(flags)
                    .Where(member => member.MemberType == MemberTypes.Field ||
                                     member.MemberType == MemberTypes.Property ||
                                     member.MemberType == MemberTypes.Method)
                    .OrderBy(member => member.MetadataToken);

                foreach (var member in members)
                {
                    var attributes = member.GetCustomAttributes(true).OfType<Attribute>().ToArray();
                    var groupNext = attributes.OfType<GroupNextAttribute>().FirstOrDefault();
                    if (groupNext != null)
                        activeGroup = groupNext.Path;
                    if (attributes.Any(attribute => attribute is UngroupNextAttribute))
                        activeGroup = null;

                    MemberDescriptor descriptor = null;
                    switch (member)
                    {
                        case FieldInfo field when IsInspectableField(field):
                            descriptor = new MemberDescriptor(field, InspectorMemberKind.Field, field.FieldType,
                                declarationIndex++, attributes);
                            break;
                        case PropertyInfo property when IsInspectableProperty(property):
                            descriptor = new MemberDescriptor(property, InspectorMemberKind.Property, property.PropertyType,
                                declarationIndex++, attributes);
                            break;
                        case MethodInfo method when method.IsDefined(typeof(ButtonAttribute), true) &&
                                                    !method.IsSpecialName && !method.ContainsGenericParameters:
                            descriptor = new MemberDescriptor(method, InspectorMemberKind.Method, method.ReturnType,
                                declarationIndex++, attributes);
                            break;
                    }

                    if (descriptor == null)
                        continue;
                    if (string.IsNullOrEmpty(descriptor.GroupPath))
                        descriptor.GroupPath = activeGroup;
                    result.Add(descriptor);
                }
            }

            return new TypeDescriptor(type, result.OrderBy(member => member.Order)
                .ThenBy(member => member.DeclarationIndex).ToArray());
        }

        private static bool IsInspectableField(FieldInfo field)
        {
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsDefined(typeof(HideInInspector), true))
                return false;
            if (field.IsDefined(typeof(ShowInInspectorAttribute), true))
                return true;
            return field.IsPublic || field.IsDefined(typeof(SerializeField), true) ||
                   field.IsDefined(typeof(SerializeReference), true) ||
                   InspectorMemberInclusionRegistry.Includes(field);
        }

        private static bool IsInspectableProperty(PropertyInfo property)
        {
            if (property.GetIndexParameters().Length != 0 || property.GetMethod == null)
                return false;
            return property.IsDefined(typeof(ShowInInspectorAttribute), true) ||
                   InspectorMemberInclusionRegistry.Includes(property);
        }
    }

    public sealed class InspectorContext
    {
        public SerializedObject SerializedObject { get; }
        public UnityEngine.Object[] Targets { get; }
        public UnityEngine.Object PrimaryTarget => Targets.Length == 0 ? null : Targets[0];
        public TypeDescriptor Type { get; }
        public VisualElement Root { get; }

        internal InspectorContext(SerializedObject serializedObject, UnityEngine.Object[] targets,
            TypeDescriptor type, VisualElement root)
        {
            SerializedObject = serializedObject;
            Targets = targets;
            Type = type;
            Root = root;
        }

        public void RecordUndo(string name)
        {
            if (Targets.Length > 0)
                Undo.RecordObjects(Targets, name);
        }

        public void MarkDirty()
        {
            NotifyChanged();
            foreach (var target in Targets)
                if (target) EditorUtility.SetDirty(target);
        }

        public void NotifyChanged()
        {
            InspectorChangeHandlerRegistry.Notify(this);
        }
    }

    public sealed class MemberContext
    {
        public InspectorContext Inspector { get; }
        public MemberDescriptor Descriptor { get; }
        public SerializedProperty SerializedProperty { get; }
        public InspectorValueAccessor Value { get; }

        internal MemberContext(InspectorContext inspector, MemberDescriptor descriptor,
            SerializedProperty serializedProperty)
        {
            Inspector = inspector;
            Descriptor = descriptor;
            SerializedProperty = serializedProperty;
            Value = new InspectorValueAccessor(this);
        }
    }

    public sealed class MemberElement : VisualElement
    {
        public VisualElement Before { get; } = new VisualElement { name = "before" };
        public VisualElement Content { get; } = new VisualElement { name = "content" };
        public VisualElement After { get; } = new VisualElement { name = "after" };
        public VisualElement Validation { get; } = new VisualElement { name = "validation" };
        public MemberContext Context { get; }

        public MemberElement(MemberContext context)
        {
            Context = context;
            name = $"member-{context.Descriptor.Name}";
            AddToClassList("flexus-ui-inspector__member");
            AddToClassList($"flexus-ui-inspector__member--{context.Descriptor.Kind.ToString().ToLowerInvariant()}");
            if (context.SerializedProperty != null) AddToClassList("flexus-ui-inspector__member--serialized");
            else AddToClassList("flexus-ui-inspector__member--reflection");
            Before.AddToClassList("flexus-ui-inspector__before");
            Content.AddToClassList("flexus-ui-inspector__content");
            After.AddToClassList("flexus-ui-inspector__after");
            Validation.AddToClassList("flexus-ui-inspector__validation");
            hierarchy.Add(Before);
            hierarchy.Add(Content);
            hierarchy.Add(After);
            hierarchy.Add(Validation);
        }

        public void ReplaceContent(VisualElement element)
        {
            Content.Clear();
            if (element != null) Content.Add(element);
        }

        public void AddBefore(VisualElement element) => Before.Add(element);
        public void AddAfter(VisualElement element) => After.Add(element);
        public void SetVisible(bool value) => style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
