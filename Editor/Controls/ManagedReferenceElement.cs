using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class ManagedReferenceElement : VisualElement
    {
        private readonly SerializedProperty property;
        private readonly Type declaredType;
        private readonly string label;
        private readonly bool showPicker;
        private readonly bool showHeader;
        private readonly Action<Type> typeChanged;
        private readonly VisualElement header = new VisualElement();
        private readonly VisualElement body = new VisualElement();
        private string lastTypeName;

        public ManagedReferenceElement(SerializedProperty property, Type declaredType, string label,
            bool showPicker = true, Action<Type> typeChanged = null, bool showHeader = true)
        {
            this.property = property.Copy();
            this.declaredType = declaredType ?? typeof(object);
            this.label = label;
            this.showPicker = showPicker;
            this.showHeader = showHeader;
            this.typeChanged = typeChanged;
            AddToClassList("flexus-managed-reference");
            header.AddToClassList("flexus-managed-reference__header");
            body.AddToClassList("flexus-managed-reference__body");
            if (showHeader) Add(header);
            Add(body);
            if (!showHeader) AddToClassList("flexus-managed-reference--body-only");
            Rebuild();
            schedule.Execute(CheckExternalChange).Every(300);
        }

        private void CheckExternalChange()
        {
            property.serializedObject.UpdateIfRequiredOrScript();
            var current = property.managedReferenceFullTypename ?? string.Empty;
            if (current == lastTypeName) return;
            Rebuild();
        }

        private void Rebuild()
        {
            property.serializedObject.UpdateIfRequiredOrScript();
            lastTypeName = property.managedReferenceFullTypename ?? string.Empty;
            header.Clear();
            body.Clear();

            var actualType = property.managedReferenceValue?.GetType();
            typeChanged?.Invoke(actualType);
            if (showHeader && !string.IsNullOrEmpty(label))
            {
                var fieldLabel = new Label(label);
                fieldLabel.AddToClassList("flexus-managed-reference__label");
                header.Add(fieldLabel);
            }

            if (showHeader && showPicker)
            {
                var picker = CreateTypePicker(false);
                picker.AddToClassList("flexus-managed-reference__picker");
                header.Add(picker);
            }
            else if (showHeader && actualType == null)
            {
                header.Add(InspectorVisuals.Badge("None", "muted"));
            }

            if (actualType == null)
            {
                var empty = InspectorVisuals.EmptyState("No implementation selected",
                    showPicker ? "Choose a concrete type from the selector above." : "Reference is currently null.");
                body.Add(empty);
                FieldColumnLayoutController.RequestRefresh(this);
                return;
            }

            var children = InspectorVisuals.DirectChildren(property).ToArray();
            if (children.Length == 0)
            {
                body.Add(InspectorVisuals.EmptyState("This type has no serialized fields."));
                FieldColumnLayoutController.RequestRefresh(this);
                return;
            }

            foreach (var child in children)
            {
                var reflectedField = InspectorVisuals.SerializedField(actualType, child.name);
                if (child.isArray && child.propertyType != SerializedPropertyType.String)
                {
                    var settings = reflectedField?.GetCustomAttributes(typeof(ListDrawerSettingsAttribute), true)
                        .OfType<ListDrawerSettingsAttribute>().FirstOrDefault() ?? new ListDrawerSettingsAttribute();
                    var list = new SerializedListElement(child, reflectedField?.FieldType ?? typeof(List<object>),
                        child.displayName, settings);
                    list.AddToClassList("flexus-managed-reference__field");
                    body.Add(list);
                }
                else if (child.propertyType == SerializedPropertyType.ManagedReference)
                {
                    var reference = new ManagedReferenceElement(child,
                        reflectedField?.FieldType ?? typeof(object), child.displayName);
                    reference.AddToClassList("flexus-managed-reference__field");
                    body.Add(reference);
                }
                else
                {
                    var field = DropdownFieldFactory.CreateSerializedOrDefault(child);
                    field.AddToClassList("flexus-managed-reference__field");
                    body.Add(field);
                }
            }
            body.Bind(property.serializedObject);
            FieldColumnLayoutController.RequestRefresh(this);
        }

        internal SearchDropdownElement CreateTypePicker(bool compact)
        {
            return new SearchDropdownElement(null,
                InspectorVisuals.TypeName(property.managedReferenceValue?.GetType()), TypeItems, SetType, compact);
        }

        private IEnumerable<SearchItem> TypeItems()
        {
            yield return new SearchItem("None", null, null, "Clear the current reference");
            foreach (var type in InspectorVisuals.CandidateTypes(declaredType)
                         .Where(type => type.IsSerializable && type.GetConstructor(
                             System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                             System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null)
                         .OrderBy(InspectorVisuals.TypePath))
                yield return new SearchItem(InspectorVisuals.TypePath(type), type, null, type.FullName);
        }

        private void SetType(object value)
        {
            Undo.RecordObjects(property.serializedObject.targetObjects, "Change Managed Reference Type");
            property.serializedObject.Update();
            property.managedReferenceValue = value is Type type ? Activator.CreateInstance(type, true) : null;
            property.serializedObject.ApplyModifiedProperties();
            Rebuild();
        }
    }
}
