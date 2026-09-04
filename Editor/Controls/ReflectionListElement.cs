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
    /// <summary>List editor for values persisted outside Unity's SerializedProperty system.</summary>
    internal sealed class ReflectionListElement : VisualElement
    {
        private readonly MemberContext context;
        private readonly Type collectionType;
        private readonly Type elementType;
        private readonly ListDrawerSettingsAttribute settings;
        private readonly VisualElement rows = new VisualElement();
        private readonly CollectionChrome chrome;
        private readonly Label pageLabel = new Label();
        private Button removeSelectedButton;
        private int selectedIndex = -1;
        private int page;

        public ReflectionListElement(MemberContext context)
        {
            this.context = context;
            collectionType = context.Descriptor.ValueType;
            elementType = InspectorVisuals.ListElementType(collectionType);
            settings = context.Descriptor.GetAttribute<ListDrawerSettingsAttribute>() ??
                       new ListDrawerSettingsAttribute();

            AddToClassList("flexus-collection");
            AddToClassList("flexus-list");
            AddToClassList("flexus-list--reflection");
            chrome = new CollectionChrome(this, context.Descriptor.DisplayName, settings.AlwaysExpanded);
            rows.AddToClassList("flexus-collection__rows");
            chrome.Body.Add(rows);
            BuildFooter();
            Rebuild();
        }

        private IList List => context.Value.GetValue() as IList;
        private int Count => List?.Count ?? 0;
        private int PageSize => Mathf.Max(1, settings.ItemsPerPage);
        private int PageCount => Mathf.Max(1, Mathf.CeilToInt((float)Count / PageSize));
        private bool IsReadOnly => context.Value.IsReadOnly || List is { IsReadOnly: true };

        private void BuildFooter()
        {
            var pager = new VisualElement();
            pager.AddToClassList("flexus-collection__pager");
            pager.Add(InspectorVisuals.IconButton("‹", "Previous page", () =>
            {
                if (page <= 0) return;
                page--;
                Rebuild();
            }));
            pageLabel.AddToClassList("flexus-collection__page-label");
            pager.Add(pageLabel);
            pager.Add(InspectorVisuals.IconButton("›", "Next page", () =>
            {
                if (page + 1 >= PageCount) return;
                page++;
                Rebuild();
            }));

            var actions = new VisualElement();
            actions.AddToClassList("flexus-collection__footer-actions");
            if (!settings.HideAddButton)
                actions.Add(InspectorVisuals.IconButton("+", "Add item", AddItem, "add"));
            if (!settings.HideRemoveButton)
            {
                removeSelectedButton = InspectorVisuals.IconButton("−",
                    "Remove selected item, or last item", RemoveSelectedItem, "remove");
                actions.Add(removeSelectedButton);
            }
            chrome.AddFooterContent(pager, actions);
        }

        private void Rebuild()
        {
            rows.Clear();
            if (selectedIndex >= Count) selectedIndex = -1;
            page = Mathf.Clamp(page, 0, PageCount - 1);
            chrome.Count.text = $"{Count} {(Count == 1 ? "item" : "items")}";
            pageLabel.text = $"{page + 1} / {PageCount}";

            if (Count == 0)
                rows.Add(InspectorVisuals.EmptyState("List is empty", "Use + below to add an item."));
            else
            {
                var start = page * PageSize;
                var end = Mathf.Min(Count, start + PageSize);
                for (var index = start; index < end; index++) BuildItem(index);
            }

            removeSelectedButton?.SetEnabled(!IsReadOnly && Count > 0);
            FieldColumnLayoutController.RequestRefresh(this);
        }

        private void BuildItem(int index)
        {
            var list = List;
            if (list == null || index < 0 || index >= list.Count) return;
            var value = list[index];
            var actualType = value?.GetType() ?? elementType;
            var row = new VisualElement { userData = index };
            row.AddToClassList("flexus-list-item");
            row.EnableInClassList("flexus-list-item--alternate",
                settings.AlternatingBackground && index % 2 == 1);
            row.EnableInClassList("flexus-collection-row--selected", selectedIndex == index);
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    selectedIndex = index;
                    RefreshSelection();
                }
            }, TrickleDown.TrickleDown);
            rows.Add(row);

            if (IsSimple(actualType)) BuildSimpleItem(row, index, value);
            else BuildComplexItem(row, index, value, actualType);
        }

        private void BuildSimpleItem(VisualElement row, int index, object value)
        {
            row.AddToClassList("flexus-list-item--simple");
            var handle = new Label(settings.Draggable ? "≡" : string.Empty);
            handle.AddToClassList("flexus-collection__drag-handle");
            row.Add(handle);
            var number = new Label((index + 1).ToString());
            number.AddToClassList("flexus-list-item__index");
            row.Add(number);
            var field = DefaultFieldFactory.CreateForValue(elementType, string.Empty, value, false,
                changed => SetItem(index, changed));
            field.AddToClassList("flexus-list-item__value");
            field.SetEnabled(!IsReadOnly);
            row.Add(field);
            if (!settings.HideRemoveButton)
            {
                var remove = InspectorVisuals.IconButton("×", "Remove item", () => RemoveItem(index), "row-remove");
                remove.SetEnabled(!IsReadOnly);
                row.Add(remove);
            }
            if (settings.Draggable && !IsReadOnly)
                CollectionDrag.Attach(handle, rows, row, index - page * PageSize,
                    (oldIndex, newIndex) => Move(page * PageSize + oldIndex, page * PageSize + newIndex));
        }

        private void BuildComplexItem(VisualElement row, int index, object value, Type actualType)
        {
            var header = new VisualElement();
            header.AddToClassList("flexus-list-item__header");
            var handle = new Label(settings.Draggable ? "≡" : string.Empty);
            handle.AddToClassList("flexus-collection__drag-handle");
            header.Add(handle);
            var number = new Label((index + 1).ToString());
            number.AddToClassList("flexus-list-item__index");
            header.Add(number);
            var title = new Label(ItemTitle(value, actualType, index));
            title.AddToClassList("flexus-list-item__title");
            header.Add(title);

            if (elementType.IsInterface || elementType.IsAbstract)
            {
                var picker = new SearchDropdownElement(null, InspectorVisuals.TypeName(value?.GetType()),
                    TypeItems, selected => SetItemType(index, selected as Type), true);
                picker.SetEnabled(!IsReadOnly);
                picker.AddToClassList("flexus-list-item__type-picker");
                header.Add(picker);
            }

            if (!settings.HideRemoveButton)
            {
                var remove = InspectorVisuals.IconButton("×", "Remove item", () => RemoveItem(index), "row-remove");
                remove.SetEnabled(!IsReadOnly);
                header.Add(remove);
            }
            row.Add(header);

            var body = new VisualElement();
            body.AddToClassList("flexus-list-item__body");
            row.Add(body);
            if (value == null)
                body.Add(InspectorVisuals.EmptyState("No implementation selected",
                    elementType.IsInterface || elementType.IsAbstract
                        ? "Choose a concrete type above."
                        : "The item is null."));
            else
                BuildObjectFields(body, index, value, actualType);

            if (settings.Draggable && !IsReadOnly)
                CollectionDrag.Attach(handle, rows, row, index - page * PageSize,
                    (oldIndex, newIndex) => Move(page * PageSize + oldIndex, page * PageSize + newIndex));
        }

        private void BuildObjectFields(VisualElement body, int index, object value, Type actualType)
        {
            foreach (var field in InspectableFields(actualType))
            {
                var captured = field;
                var fieldElement = DefaultFieldFactory.CreateForValue(field.FieldType,
                    ObjectNames.NicifyVariableName(field.Name.TrimStart('_')), field.GetValue(value), false, changed =>
                    {
                        context.Inspector.RecordUndo("Edit List Item");
                        var item = List?[index];
                        if (item == null) return;
                        captured.SetValue(item, changed);
                        if (actualType.IsValueType) List[index] = item;
                        context.Inspector.MarkDirty();
                    });
                fieldElement.SetEnabled(!IsReadOnly);
                body.Add(fieldElement);
            }

            if (body.childCount == 0)
                body.Add(InspectorVisuals.EmptyState("This type has no inspectable fields."));
        }

        private static IEnumerable<FieldInfo> InspectableFields(Type type)
        {
            var hierarchy = new Stack<Type>();
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
                hierarchy.Push(current);
            while (hierarchy.Count > 0)
            {
                var current = hierarchy.Pop();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                           BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (var field in current.GetFields(flags).OrderBy(field => field.MetadataToken))
                {
                    if (field.IsStatic || field.IsLiteral || field.IsInitOnly ||
                        field.IsDefined(typeof(HideInInspector), true)) continue;
                    if (field.IsPublic || field.IsDefined(typeof(SerializeField), true) ||
                        field.IsDefined(typeof(SerializeReference), true) ||
                        InspectorMemberInclusionRegistry.Includes(field))
                        yield return field;
                }
            }
        }

        private IEnumerable<SearchItem> TypeItems()
        {
            yield return new SearchItem("None", null, null, "Clear the current value");
            foreach (var type in InspectorVisuals.CandidateTypes(elementType)
                         .Where(type => type.IsSerializable && HasDefaultConstructor(type))
                         .OrderBy(InspectorVisuals.TypePath))
                yield return new SearchItem(InspectorVisuals.TypePath(type), type, null, type.FullName);
        }

        private void SetItemType(int index, Type type)
        {
            SetItem(index, type == null ? null : Activator.CreateInstance(type, true));
            Rebuild();
        }

        private void SetItem(int index, object value)
        {
            var list = List;
            if (IsReadOnly || list == null || index < 0 || index >= list.Count) return;
            context.Inspector.RecordUndo("Edit List Item");
            list[index] = value;
            context.Inspector.MarkDirty();
        }

        private void AddItem()
        {
            if (context.Value.IsReadOnly) return;
            var list = List;
            if (list == null)
            {
                context.Value.SetValue(CreateEmptyCollection(), "Create List");
                list = List;
            }
            if (list == null) return;

            var value = CreateDefaultValue(elementType);
            if (collectionType.IsArray || list.IsFixedSize)
            {
                var replacement = Array.CreateInstance(elementType, list.Count + 1);
                list.CopyTo(replacement, 0);
                replacement.SetValue(value, list.Count);
                context.Value.SetValue(replacement, "Add List Item");
            }
            else
            {
                context.Inspector.RecordUndo("Add List Item");
                list.Add(value);
                context.Inspector.MarkDirty();
            }
            Rebuild();
        }

        private void RemoveItem(int index)
        {
            var list = List;
            if (IsReadOnly || list == null || index < 0 || index >= list.Count) return;
            if (collectionType.IsArray || list.IsFixedSize)
            {
                var replacement = Array.CreateInstance(elementType, list.Count - 1);
                var destination = 0;
                for (var source = 0; source < list.Count; source++)
                    if (source != index) replacement.SetValue(list[source], destination++);
                context.Value.SetValue(replacement, "Remove List Item");
            }
            else
            {
                context.Inspector.RecordUndo("Remove List Item");
                list.RemoveAt(index);
                context.Inspector.MarkDirty();
            }
            selectedIndex = -1;
            Rebuild();
        }

        private void RemoveSelectedItem()
        {
            if (Count == 0) return;
            RemoveItem(selectedIndex >= 0 ? selectedIndex : Count - 1);
        }

        private void Move(int oldIndex, int newIndex)
        {
            var list = List;
            if (IsReadOnly || list == null || oldIndex < 0 || newIndex < 0 ||
                oldIndex >= list.Count || newIndex >= list.Count || oldIndex == newIndex) return;
            context.Inspector.RecordUndo("Reorder List");
            if (collectionType.IsArray || list.IsFixedSize)
            {
                var moving = list[oldIndex];
                if (oldIndex < newIndex)
                    for (var index = oldIndex; index < newIndex; index++) list[index] = list[index + 1];
                else
                    for (var index = oldIndex; index > newIndex; index--) list[index] = list[index - 1];
                list[newIndex] = moving;
            }
            else
            {
                var moving = list[oldIndex];
                list.RemoveAt(oldIndex);
                list.Insert(newIndex, moving);
            }
            context.Inspector.MarkDirty();
            selectedIndex = newIndex;
            Rebuild();
        }

        private object CreateEmptyCollection()
        {
            if (collectionType.IsArray) return Array.CreateInstance(elementType, 0);
            if (!collectionType.IsInterface && !collectionType.IsAbstract &&
                HasDefaultConstructor(collectionType))
                return Activator.CreateInstance(collectionType, true);
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
        }

        private static object CreateDefaultValue(Type type)
        {
            if (type == typeof(string)) return string.Empty;
            if (type.IsValueType) return Activator.CreateInstance(type);
            return !type.IsInterface && !type.IsAbstract && HasDefaultConstructor(type)
                ? Activator.CreateInstance(type, true)
                : null;
        }

        private static bool HasDefaultConstructor(Type type)
        {
            return type.IsValueType || type.GetConstructor(BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null;
        }

        private static bool IsSimple(Type type)
        {
            if (type == null) return false;
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
                   typeof(UnityEngine.Object).IsAssignableFrom(type) || type == typeof(Vector2) ||
                   type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Vector2Int) ||
                   type == typeof(Vector3Int) || type == typeof(Color) || type == typeof(Rect) ||
                   type == typeof(RectInt) || type == typeof(Bounds) || type == typeof(BoundsInt) ||
                   type == typeof(AnimationCurve) || type == typeof(Gradient);
        }

        private static string ItemTitle(object value, Type actualType, int index)
        {
            if (value != null)
            {
                foreach (var name in new[] { "name", "title", "label", "id", "key" })
                {
                    var field = actualType.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    var candidate = field?.GetValue(value)?.ToString();
                    if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
                }
            }
            return value == null ? "Unassigned reference" : $"{InspectorVisuals.TypeName(actualType)} {index + 1}";
        }

        private void RefreshSelection()
        {
            foreach (var child in rows.Children())
                if (child.userData is int index)
                    child.EnableInClassList("flexus-collection-row--selected", index == selectedIndex);
        }
    }
}
