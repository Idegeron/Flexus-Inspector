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
    internal sealed class CollectionExtension : IInspectorExtension
    {
        public InspectorStage Stage => InspectorStage.Content;
        public int Order => -20;

        public bool CanApply(MemberContext context)
        {
            if (context.Descriptor.HasAttribute<UseUnityDrawerAttribute>()) return false;
            var type = context.Descriptor.ValueType;
            return context.SerializedProperty is { isArray: true, propertyType: not SerializedPropertyType.String } ||
                   typeof(IDictionary).IsAssignableFrom(type) ||
                   type.GetInterfaces().Any(candidate => candidate.IsGenericType &&
                                                         candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        public void Apply(MemberElement element, MemberContext context)
        {
            if (context.SerializedProperty is { isArray: true })
            {
                var settings = context.Descriptor.GetAttribute<ListDrawerSettingsAttribute>() ??
                               new ListDrawerSettingsAttribute();
                var firstIsManagedReference = context.SerializedProperty.arraySize > 0 &&
                                              context.SerializedProperty.GetArrayElementAtIndex(0).propertyType ==
                                              SerializedPropertyType.ManagedReference;
                element.ReplaceContent(context.Descriptor.HasAttribute<TableListAttribute>() && !firstIsManagedReference
                    ? new SerializedTableElement(context, settings)
                    : new SerializedListElement(context, settings));
                return;
            }

            var value = context.Descriptor.Member switch
            {
                FieldInfo field => field.GetValue(context.Inspector.PrimaryTarget),
                PropertyInfo property when property.GetMethod != null => property.GetValue(context.Inspector.PrimaryTarget),
                _ => context.Value.GetValue(),
            };
            if (value != null) element.ReplaceContent(new DictionaryElement(context, value));
        }
    }

    internal sealed class SerializedListElement : VisualElement
    {
        private readonly SerializedProperty property;
        private readonly ListDrawerSettingsAttribute settings;
        private readonly Type collectionType;
        private readonly Type elementType;
        private readonly VisualElement rows = new VisualElement();
        private readonly CollectionChrome chrome;
        private readonly Label pageLabel = new Label();
        private readonly Dictionary<string, bool> expansionStates = new Dictionary<string, bool>();
        private Button removeSelectedButton;
        private int selectedIndex = -1;
        private int page;
        private int observedSize = -1;

        public SerializedListElement(MemberContext context, ListDrawerSettingsAttribute settings)
            : this(context.SerializedProperty, context.Descriptor.ValueType,
                context.Descriptor.DisplayName, settings)
        {
        }

        internal SerializedListElement(SerializedProperty serializedProperty, Type collectionType,
            string displayName, ListDrawerSettingsAttribute settings)
        {
            this.settings = settings;
            property = serializedProperty.Copy();
            this.collectionType = collectionType;
            elementType = InspectorVisuals.ListElementType(collectionType);
            AddToClassList("flexus-collection");
            AddToClassList("flexus-list");

            chrome = new CollectionChrome(this, displayName,
                settings.AlwaysExpanded || property.isExpanded, value => property.isExpanded = value);
            CollectionContextMenus.AttachCollection(this, ClearCollection,
                () => CollectionClipboard.CopyCollection(property, this.collectionType),
                () => CollectionClipboard.CanPasteCollection(this.collectionType), PasteCollection);
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && !CollectionSelection.IsRowTarget(evt.target, this) &&
                    !CollectionSelection.IsFooterTarget(evt.target, this)) ClearSelection();
            }, TrickleDown.TrickleDown);
            rows.AddToClassList("flexus-collection__rows");
            rows.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && !CollectionSelection.IsRowTarget(evt.target, rows)) ClearSelection();
            }, TrickleDown.TrickleDown);
            chrome.Body.Add(rows);
            BuildFooter();
            Rebuild();
            schedule.Execute(CheckArraySize).Every(300);
        }

        private int PageSize => Mathf.Max(1, settings.ItemsPerPage);
        private int PageCount => Mathf.Max(1, Mathf.CeilToInt((float)property.arraySize / PageSize));

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
                removeSelectedButton = InspectorVisuals.IconButton("−", "Remove selected item, or last item",
                    RemoveSelectedItem, "remove");
                actions.Add(removeSelectedButton);
            }
            chrome.AddFooterContent(pager, actions);
            RefreshSelection();
        }

        private void CheckArraySize()
        {
            property.serializedObject.UpdateIfRequiredOrScript();
            if (observedSize != property.arraySize) Rebuild();
        }

        private void Rebuild()
        {
            property.serializedObject.UpdateIfRequiredOrScript();
            observedSize = property.arraySize;
            if (selectedIndex >= property.arraySize) selectedIndex = -1;
            page = Mathf.Clamp(page, 0, PageCount - 1);
            rows.Clear();
            var start = page * PageSize;
            var end = Mathf.Min(property.arraySize, start + PageSize);
            for (var index = start; index < end; index++) BuildItem(property.GetArrayElementAtIndex(index).Copy(), index);
            chrome.Count.text = $"{property.arraySize} {(property.arraySize == 1 ? "item" : "items")}";
            pageLabel.text = $"{page + 1} / {PageCount}";
            if (property.arraySize == 0)
                rows.Add(InspectorVisuals.EmptyState("List is empty", "Use + below to add an item."));
            RefreshSelection();
            FieldColumnLayoutController.RequestRefresh(this);
        }

        private void BuildItem(SerializedProperty item, int absoluteIndex)
        {
            var row = new VisualElement();
            row.AddToClassList("flexus-list-item");
            row.EnableInClassList("flexus-list-item--alternate",
                settings.AlternatingBackground && absoluteIndex % 2 == 1);
            row.userData = absoluteIndex;
            row.EnableInClassList("flexus-collection-row--selected", selectedIndex == absoluteIndex);
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0) Select(absoluteIndex);
            }, TrickleDown.TrickleDown);
            CollectionContextMenus.AttachElement(row,
                () => CollectionClipboard.CopyElement(item, elementType),
                () => CollectionClipboard.CanPasteElement(elementType),
                () => PasteElement(absoluteIndex));
            rows.Add(row);

            if (InspectorVisuals.IsSimple(item)) BuildSimpleItem(row, item, absoluteIndex);
            else BuildComplexItem(row, item, absoluteIndex);
        }

        private void BuildSimpleItem(VisualElement row, SerializedProperty item, int absoluteIndex)
        {
            row.AddToClassList("flexus-list-item--simple");
            var handle = CreateDragHandle();
            row.Add(handle);
            row.Add(CreateIndexLabel(absoluteIndex));
            var field = new PropertyField(item, string.Empty);
            field.AddToClassList("flexus-list-item__value");
            row.Add(field);
            row.Bind(property.serializedObject);
            if (settings.Draggable)
                CollectionDrag.Attach(handle, rows, row, absoluteIndex - page * PageSize,
                    (oldIndex, newIndex) => Move(page * PageSize + oldIndex, page * PageSize + newIndex));
        }

        private void BuildComplexItem(VisualElement row, SerializedProperty item, int absoluteIndex)
        {
            var header = new VisualElement();
            header.AddToClassList("flexus-list-item__header");
            var handle = CreateDragHandle();
            header.Add(handle);
            header.Add(CreateIndexLabel(absoluteIndex));
            var headerMain = new VisualElement();
            headerMain.AddToClassList("flexus-list-item__header-main");
            header.Add(headerMain);
            var body = new VisualElement();
            body.AddToClassList("flexus-list-item__body");
            var expansionKey = GetExpansionKey(item, absoluteIndex);
            var expanded = GetExpandedState(expansionKey, item, absoluteIndex);

            var expander = new Button();
            expander.AddToClassList("flexus-list-item__expander");
            var arrow = new Label(expanded ? "▾" : "›");
            arrow.AddToClassList("flexus-list-item__arrow");
            var title = new Label(settings.ShowElementLabels
                ? $"Element {absoluteIndex}" : InspectorVisuals.ItemTitle(item, absoluteIndex, elementType));
            title.AddToClassList("flexus-list-item__title");
            expander.Add(arrow);
            expander.Add(title);
            expander.clicked += () =>
            {
                expanded = !expanded;
                expansionStates[expansionKey] = expanded;
                item.isExpanded = expanded;
                arrow.text = expanded ? "▾" : "›";
                body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            };
            headerMain.Add(expander);

            if (item.propertyType == SerializedPropertyType.ManagedReference)
            {
                headerMain.AddToClassList("flexus-list-item__header-main--managed-reference");
                ManagedReferenceElement reference = null;
                SearchDropdownElement typePicker = null;
                reference = new ManagedReferenceElement(item, elementType, null, false, type =>
                {
                    typePicker?.SetText(InspectorVisuals.TypeName(type));
                    title.text = InspectorVisuals.ItemTitle(item, absoluteIndex, elementType);
                }, false);
                typePicker = reference.CreateTypePicker(true);
                typePicker.AddToClassList("flexus-list-item__type-picker");
                headerMain.Add(typePicker);
                body.Add(reference);
            }
            if (!settings.HideRemoveButton)
                headerMain.Add(InspectorVisuals.IconButton("×", "Remove item",
                    () => RemoveItemAt(absoluteIndex), "row-remove"));
            else
                headerMain.AddToClassList("flexus-list-item__header-main--without-remove");
            row.Add(header);
            row.Add(body);
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

            if (item.propertyType != SerializedPropertyType.ManagedReference)
            {
                foreach (var child in InspectorVisuals.DirectChildren(item))
                    body.Add(new PropertyField(child.Copy()));
                body.Bind(property.serializedObject);
            }

            if (settings.Draggable)
                CollectionDrag.Attach(handle, rows, row, absoluteIndex - page * PageSize,
                    (oldIndex, newIndex) => Move(page * PageSize + oldIndex, page * PageSize + newIndex));
        }

        private VisualElement CreateDragHandle()
        {
            var handle = new Label(settings.Draggable ? "═" : string.Empty);
            handle.AddToClassList("flexus-collection__drag-handle");
            return handle;
        }

        private static Label CreateIndexLabel(int index)
        {
            var label = new Label((index + 1).ToString());
            label.AddToClassList("flexus-list-item__index");
            return label;
        }

        private bool GetExpandedState(string key, SerializedProperty item, int absoluteIndex)
        {
            if (settings.AlwaysExpanded) return true;
            if (expansionStates.TryGetValue(key, out var expanded)) return expanded;
            expanded = item.propertyType == SerializedPropertyType.ManagedReference
                ? absoluteIndex == 0
                : item.isExpanded;
            expansionStates[key] = expanded;
            return expanded;
        }

        private static string GetExpansionKey(SerializedProperty item, int absoluteIndex)
        {
            if (item.propertyType == SerializedPropertyType.ManagedReference && item.managedReferenceId > 0)
                return $"managed:{item.managedReferenceId}";
            return $"index:{absoluteIndex}";
        }

        private void AddItem()
        {
            RecordUndo("Add List Item");
            property.serializedObject.Update();
            property.arraySize++;
            var created = property.GetArrayElementAtIndex(property.arraySize - 1);
            if (created.propertyType == SerializedPropertyType.ManagedReference)
                created.managedReferenceValue = null;
            created.isExpanded = false;
            property.serializedObject.ApplyModifiedProperties();
            selectedIndex = property.arraySize - 1;
            page = PageCount - 1;
            Rebuild();
        }

        private void RemoveSelectedItem()
        {
            if (property.arraySize == 0) return;
            RemoveItemAt(selectedIndex >= 0 && selectedIndex < property.arraySize
                ? selectedIndex : property.arraySize - 1);
        }

        private void ClearCollection()
        {
            if (property.arraySize == 0) return;
            RecordUndo("Clear List");
            property.serializedObject.Update();
            property.ClearArray();
            property.serializedObject.ApplyModifiedProperties();
            expansionStates.Clear();
            selectedIndex = -1;
            page = 0;
            Rebuild();
        }

        private void PasteElement(int index)
        {
            if (index < 0 || index >= property.arraySize || !CollectionClipboard.CanPasteElement(elementType)) return;
            RecordUndo("Paste List Element");
            property.serializedObject.Update();
            if (!CollectionClipboard.TryPasteElement(property.GetArrayElementAtIndex(index), elementType)) return;
            selectedIndex = index;
            Rebuild();
        }

        private void PasteCollection()
        {
            if (!CollectionClipboard.CanPasteCollection(collectionType)) return;
            RecordUndo("Paste List");
            if (!CollectionClipboard.TryPasteCollection(property, collectionType)) return;
            expansionStates.Clear();
            selectedIndex = -1;
            page = 0;
            Rebuild();
        }

        private void RemoveItemAt(int index)
        {
            if (index < 0 || index >= property.arraySize) return;
            RecordUndo("Remove List Item");
            property.serializedObject.Update();
            var previousSize = property.arraySize;
            property.DeleteArrayElementAtIndex(index);
            if (property.arraySize == previousSize) property.DeleteArrayElementAtIndex(index);
            property.serializedObject.ApplyModifiedProperties();
            if (selectedIndex == index) selectedIndex = -1;
            else if (selectedIndex > index) selectedIndex--;
            Rebuild();
        }

        private void Move(int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex || oldIndex < 0 || newIndex < 0 ||
                oldIndex >= property.arraySize || newIndex >= property.arraySize) return;
            RecordUndo("Reorder List");
            property.serializedObject.Update();
            property.MoveArrayElement(oldIndex, newIndex);
            property.serializedObject.ApplyModifiedProperties();
            if (selectedIndex == oldIndex) selectedIndex = newIndex;
            else if (oldIndex < selectedIndex && newIndex >= selectedIndex) selectedIndex--;
            else if (oldIndex > selectedIndex && newIndex <= selectedIndex) selectedIndex++;
            Rebuild();
        }

        private void Select(int index)
        {
            selectedIndex = index;
            RefreshSelection();
        }

        private void ClearSelection() => Select(-1);

        private void RefreshSelection()
        {
            foreach (var child in rows.Children())
                if (child.userData is int index)
                    child.EnableInClassList("flexus-collection-row--selected", index == selectedIndex);
            removeSelectedButton?.SetEnabled(property.arraySize > 0);
        }

        private void RecordUndo(string name) => Undo.RecordObjects(property.serializedObject.targetObjects, name);
    }

    internal sealed class SerializedTableElement : VisualElement
    {
        private readonly MemberContext context;
        private readonly SerializedProperty property;
        private readonly ListDrawerSettingsAttribute settings;
        private readonly VisualElement rows = new VisualElement();
        private readonly CollectionChrome chrome;
        private readonly Label pageLabel = new Label();
        private Button removeSelectedButton;
        private int selectedIndex = -1;
        private int page;
        private int observedSize = -1;

        public SerializedTableElement(MemberContext context, ListDrawerSettingsAttribute settings)
        {
            this.context = context;
            this.settings = settings;
            property = context.SerializedProperty;
            AddToClassList("flexus-collection");
            AddToClassList("flexus-table");
            chrome = new CollectionChrome(this, context.Descriptor.DisplayName,
                settings.AlwaysExpanded || property.isExpanded, value => property.isExpanded = value);
            CollectionContextMenus.AttachCollection(this, ClearCollection,
                () => CollectionClipboard.CopyCollection(property, context.Descriptor.ValueType),
                () => CollectionClipboard.CanPasteCollection(context.Descriptor.ValueType), PasteCollection);
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && !CollectionSelection.IsRowTarget(evt.target, this) &&
                    !CollectionSelection.IsFooterTarget(evt.target, this)) ClearSelection();
            }, TrickleDown.TrickleDown);
            rows.AddToClassList("flexus-table__rows");
            rows.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && !CollectionSelection.IsRowTarget(evt.target, rows)) ClearSelection();
            }, TrickleDown.TrickleDown);
            chrome.Body.Add(rows);
            BuildFooter();
            Rebuild();
            schedule.Execute(CheckSize).Every(300);
        }

        private int PageSize => Mathf.Max(1, settings.ItemsPerPage);
        private int PageCount => Mathf.Max(1, Mathf.CeilToInt((float)property.arraySize / PageSize));

        private void BuildFooter()
        {
            var pager = CreatePager();
            var actions = new VisualElement();
            actions.AddToClassList("flexus-collection__footer-actions");
            if (!settings.HideAddButton)
                actions.Add(InspectorVisuals.IconButton("+", "Add row", Add, "add"));
            if (!settings.HideRemoveButton)
            {
                removeSelectedButton = InspectorVisuals.IconButton("−", "Remove selected row, or last row",
                    RemoveSelected, "remove");
                actions.Add(removeSelectedButton);
            }
            chrome.AddFooterContent(pager, actions);
            RefreshSelection();
        }

        private VisualElement CreatePager()
        {
            var pager = new VisualElement();
            pager.AddToClassList("flexus-collection__pager");
            pager.Add(InspectorVisuals.IconButton("‹", "Previous page", () => { if (page > 0) { page--; Rebuild(); } }));
            pageLabel.AddToClassList("flexus-collection__page-label");
            pager.Add(pageLabel);
            pager.Add(InspectorVisuals.IconButton("›", "Next page", () =>
            {
                if (page + 1 < PageCount) { page++; Rebuild(); }
            }));
            return pager;
        }

        private void CheckSize()
        {
            property.serializedObject.UpdateIfRequiredOrScript();
            if (observedSize != property.arraySize) Rebuild();
        }

        private void Rebuild()
        {
            property.serializedObject.UpdateIfRequiredOrScript();
            observedSize = property.arraySize;
            if (selectedIndex >= property.arraySize) selectedIndex = -1;
            page = Mathf.Clamp(page, 0, PageCount - 1);
            rows.Clear();
            chrome.Count.text = $"{property.arraySize} {(property.arraySize == 1 ? "row" : "rows")}";
            pageLabel.text = $"{page + 1} / {PageCount}";
            if (property.arraySize == 0)
            {
                rows.Add(InspectorVisuals.EmptyState("Table is empty", "Use + below to add a row."));
                RefreshSelection();
                FieldColumnLayoutController.RequestRefresh(this);
                return;
            }

            var columns = InspectorVisuals.DirectChildren(property.GetArrayElementAtIndex(0))
                .Select(child => child.name).ToArray();
            var header = new VisualElement();
            header.AddToClassList("flexus-table__header");
            header.Add(new Label(string.Empty) { style = { width = 18 } });
            foreach (var column in columns)
            {
                var label = new Label(ObjectNames.NicifyVariableName(column));
                label.AddToClassList("flexus-table__cell");
                header.Add(label);
            }
            if (!settings.HideRemoveButton)
            {
                var actionSpacer = new VisualElement();
                actionSpacer.AddToClassList("flexus-table__action-cell");
                header.Add(actionSpacer);
            }
            rows.Add(header);

            var start = page * PageSize;
            var end = Mathf.Min(property.arraySize, start + PageSize);
            for (var index = start; index < end; index++) AddRow(index, columns);
            rows.Bind(property.serializedObject);
            RefreshSelection();
            FieldColumnLayoutController.RequestRefresh(this);
        }

        private void AddRow(int absoluteIndex, IEnumerable<string> columns)
        {
            var row = new VisualElement();
            row.AddToClassList("flexus-table__row");
            row.EnableInClassList("flexus-table__row--alternate",
                settings.AlternatingBackground && absoluteIndex % 2 == 1);
            row.userData = absoluteIndex;
            row.EnableInClassList("flexus-collection-row--selected", selectedIndex == absoluteIndex);
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0) Select(absoluteIndex);
            }, TrickleDown.TrickleDown);
            var handle = new Label(settings.Draggable ? "═" : string.Empty);
            handle.AddToClassList("flexus-collection__drag-handle");
            row.Add(handle);
            var item = property.GetArrayElementAtIndex(absoluteIndex);
            var elementType = InspectorVisuals.ListElementType(context.Descriptor.ValueType);
            CollectionContextMenus.AttachElement(row,
                () => CollectionClipboard.CopyElement(item, elementType),
                () => CollectionClipboard.CanPasteElement(elementType),
                () => PasteElement(absoluteIndex, elementType));
            foreach (var column in columns)
            {
                var child = item.FindPropertyRelative(column);
                var field = child != null ? new PropertyField(child.Copy(), string.Empty) : new PropertyField(item.Copy());
                field.AddToClassList("flexus-table__cell");
                row.Add(field);
            }
            if (!settings.HideRemoveButton)
                row.Add(InspectorVisuals.IconButton("×", "Remove row",
                    () => RemoveRowAt(absoluteIndex), "row-remove"));
            rows.Add(row);
            if (settings.Draggable)
                CollectionDrag.Attach(handle, rows, row, absoluteIndex - page * PageSize,
                    (oldIndex, newIndex) => Move(page * PageSize + oldIndex, page * PageSize + newIndex));
        }

        private void Add()
        {
            context.Inspector.RecordUndo("Add Table Row");
            property.serializedObject.Update();
            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).isExpanded = false;
            property.serializedObject.ApplyModifiedProperties();
            selectedIndex = property.arraySize - 1;
            page = PageCount - 1;
            Rebuild();
        }

        private void RemoveSelected()
        {
            if (property.arraySize == 0) return;
            RemoveRowAt(selectedIndex >= 0 && selectedIndex < property.arraySize
                ? selectedIndex : property.arraySize - 1);
        }

        private void ClearCollection()
        {
            if (property.arraySize == 0) return;
            context.Inspector.RecordUndo("Clear Table");
            property.serializedObject.Update();
            property.ClearArray();
            property.serializedObject.ApplyModifiedProperties();
            selectedIndex = -1;
            page = 0;
            Rebuild();
        }

        private void PasteElement(int index, Type elementType)
        {
            if (index < 0 || index >= property.arraySize || !CollectionClipboard.CanPasteElement(elementType)) return;
            context.Inspector.RecordUndo("Paste Table Element");
            property.serializedObject.Update();
            if (!CollectionClipboard.TryPasteElement(property.GetArrayElementAtIndex(index), elementType)) return;
            selectedIndex = index;
            Rebuild();
        }

        private void PasteCollection()
        {
            var collectionType = context.Descriptor.ValueType;
            if (!CollectionClipboard.CanPasteCollection(collectionType)) return;
            context.Inspector.RecordUndo("Paste Table");
            if (!CollectionClipboard.TryPasteCollection(property, collectionType)) return;
            selectedIndex = -1;
            page = 0;
            Rebuild();
        }

        private void RemoveRowAt(int index)
        {
            if (index < 0 || index >= property.arraySize) return;
            context.Inspector.RecordUndo("Remove Table Row");
            property.serializedObject.Update();
            var previousSize = property.arraySize;
            property.DeleteArrayElementAtIndex(index);
            if (property.arraySize == previousSize) property.DeleteArrayElementAtIndex(index);
            property.serializedObject.ApplyModifiedProperties();
            if (selectedIndex == index) selectedIndex = -1;
            else if (selectedIndex > index) selectedIndex--;
            Rebuild();
        }

        private void Move(int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex || oldIndex < 0 || newIndex < 0 ||
                oldIndex >= property.arraySize || newIndex >= property.arraySize) return;
            context.Inspector.RecordUndo("Move Table Row");
            property.MoveArrayElement(oldIndex, newIndex);
            property.serializedObject.ApplyModifiedProperties();
            if (selectedIndex == oldIndex) selectedIndex = newIndex;
            else if (oldIndex < selectedIndex && newIndex >= selectedIndex) selectedIndex--;
            else if (oldIndex > selectedIndex && newIndex <= selectedIndex) selectedIndex++;
            Rebuild();
        }

        private void Select(int index)
        {
            selectedIndex = index;
            RefreshSelection();
        }

        private void ClearSelection() => Select(-1);

        private void RefreshSelection()
        {
            foreach (var child in rows.Children())
                if (child.userData is int index)
                    child.EnableInClassList("flexus-collection-row--selected", index == selectedIndex);
            removeSelectedButton?.SetEnabled(property.arraySize > 0);
        }
    }

    internal sealed class DictionaryElement : VisualElement
    {
        private readonly MemberContext context;
        private readonly object dictionary;
        private readonly SerializedProperty property;
        private readonly Type collectionType;
        private readonly MethodInfo addMethod;
        private readonly MethodInfo removeMethod;
        private readonly MethodInfo clearMethod;
        private readonly PropertyInfo indexer;
        private readonly Type keyType;
        private readonly Type valueType;
        private readonly VisualElement rows = new VisualElement();
        private readonly VisualElement addArea;
        private readonly DictionaryDrawerSettingsAttribute settings;
        private readonly CollectionChrome chrome;
        private readonly Label pageLabel = new Label();
        private Button removeSelectedButton;
        private object selectedKey;
        private bool hasSelection;
        private int page;
        private object newKey;
        private object newValue;

        public DictionaryElement(MemberContext context, object dictionary)
        {
            this.context = context;
            this.dictionary = dictionary;
            property = context.SerializedProperty?.Copy();
            collectionType = context.Descriptor.ValueType;
            AddToClassList("flexus-collection");
            AddToClassList("flexus-dictionary");
            var dictionaryInterface = dictionary.GetType().GetInterfaces()
                .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>));
            if (dictionaryInterface == null)
            {
                Add(new HelpBox("Dictionary type is not supported.", HelpBoxMessageType.Error));
                return;
            }

            keyType = dictionaryInterface.GetGenericArguments()[0];
            valueType = dictionaryInterface.GetGenericArguments()[1];
            addMethod = dictionaryInterface.GetMethod("Add", new[] { keyType, valueType });
            removeMethod = dictionaryInterface.GetMethod("Remove", new[] { keyType });
            clearMethod = dictionary.GetType().GetMethod("Clear", Type.EmptyTypes);
            indexer = dictionaryInterface.GetProperty("Item");
            newKey = keyType.IsValueType ? Activator.CreateInstance(keyType) : null;
            newValue = valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
            settings = context.Descriptor.GetAttribute<DictionaryDrawerSettingsAttribute>() ??
                       new DictionaryDrawerSettingsAttribute();

            chrome = new CollectionChrome(this, context.Descriptor.DisplayName, settings.AlwaysExpanded);
            CollectionContextMenus.AttachCollection(this, ClearCollection, CopyCollection,
                () => CollectionClipboard.CanPasteCollection(collectionType), PasteCollection);
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && !CollectionSelection.IsRowTarget(evt.target, this) &&
                    !CollectionSelection.IsFooterTarget(evt.target, this)) ClearSelection();
            }, TrickleDown.TrickleDown);
            rows.AddToClassList("flexus-dictionary__rows");
            rows.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && !CollectionSelection.IsRowTarget(evt.target, rows)) ClearSelection();
            }, TrickleDown.TrickleDown);
            chrome.Body.Add(rows);
            addArea = CreateAddArea();
            addArea.style.display = DisplayStyle.None;
            chrome.Body.Add(addArea);
            BuildFooter();
            Rebuild();
        }

        private int PageSize => Mathf.Max(1, settings.ItemsPerPage);
        private int Count => ((IEnumerable)dictionary).Cast<object>().Count();
        private int PageCount => Mathf.Max(1, Mathf.CeilToInt((float)Count / PageSize));

        private void BuildFooter()
        {
            var pager = new VisualElement();
            pager.AddToClassList("flexus-collection__pager");
            pager.Add(InspectorVisuals.IconButton("‹", "Previous page", () => { if (page > 0) { page--; Rebuild(); } }));
            pageLabel.AddToClassList("flexus-collection__page-label");
            pager.Add(pageLabel);
            pager.Add(InspectorVisuals.IconButton("›", "Next page", () =>
            {
                if (page + 1 < PageCount) { page++; Rebuild(); }
            }));
            var actions = new VisualElement();
            actions.AddToClassList("flexus-collection__footer-actions");
            actions.Add(InspectorVisuals.IconButton("+", "Add dictionary entry", ToggleAddArea, "add"));
            removeSelectedButton = InspectorVisuals.IconButton("−", "Remove selected entry, or last entry",
                RemoveSelected, "remove");
            actions.Add(removeSelectedButton);
            chrome.AddFooterContent(pager, actions);
            RefreshSelection();
        }

        private VisualElement CreateAddArea()
        {
            var box = new VisualElement();
            box.AddToClassList("flexus-dictionary__add");
            var title = new Label("New entry");
            title.AddToClassList("flexus-dictionary__add-title");
            box.Add(title);
            box.Add(DefaultFieldFactory.CreateForValue(keyType, "Key", newKey, false, value => newKey = value));
            box.Add(DefaultFieldFactory.CreateForValue(valueType, "Value", newValue, false, value => newValue = value));
            var actions = new VisualElement();
            actions.AddToClassList("flexus-dictionary__add-actions");
            var add = new Button(AddEntry) { text = "Add entry" };
            add.AddToClassList("flexus-button");
            actions.Add(add);
            box.Add(actions);
            return box;
        }

        private void ToggleAddArea()
        {
            addArea.style.display = addArea.resolvedStyle.display == DisplayStyle.None
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Rebuild()
        {
            rows.Clear();
            page = Mathf.Clamp(page, 0, PageCount - 1);
            var count = Count;
            chrome.Count.text = $"{count} {(count == 1 ? "entry" : "entries")}";
            pageLabel.text = $"{page + 1} / {PageCount}";
            if (count == 0)
            {
                hasSelection = false;
                rows.Add(InspectorVisuals.EmptyState("Dictionary is empty", "Use + below to create an entry."));
                RefreshSelection();
                FieldColumnLayoutController.RequestRefresh(this);
                return;
            }

            var header = new VisualElement();
            header.AddToClassList("flexus-table__header");
            foreach (var text in new[] { "Key", "Value" })
            {
                var label = new Label(text);
                label.AddToClassList("flexus-table__cell");
                header.Add(label);
            }
            var actionSpacer = new VisualElement();
            actionSpacer.AddToClassList("flexus-table__action-cell");
            header.Add(actionSpacer);
            rows.Add(header);
            var entries = ((IEnumerable)dictionary).Cast<object>()
                .Skip(page * PageSize).Take(PageSize).ToArray();
            for (var index = 0; index < entries.Length; index++)
                AddEntryRow(entries[index], page * PageSize + index);
            RefreshSelection();
            FieldColumnLayoutController.RequestRefresh(this);
        }

        private void AddEntryRow(object entry, int absoluteIndex)
        {
            var entryType = entry.GetType();
            var key = entryType.GetProperty("Key")?.GetValue(entry);
            var value = entryType.GetProperty("Value")?.GetValue(entry);
            var row = new VisualElement();
            row.AddToClassList("flexus-table__row");
            row.EnableInClassList("flexus-table__row--alternate", absoluteIndex % 2 == 1);
            row.userData = key;
            row.EnableInClassList("flexus-collection-row--selected", hasSelection && Equals(selectedKey, key));
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0) Select(key);
            }, TrickleDown.TrickleDown);
            CollectionContextMenus.AttachElement(row, () => CopyElement(key, value, absoluteIndex),
                () => CollectionClipboard.CanPasteDictionaryElement(valueType), () => PasteElement(key));
            var keyField = DefaultFieldFactory.CreateForValue(keyType, string.Empty, key, false, _ => { });
            keyField.SetEnabled(false);
            keyField.AddToClassList("flexus-table__cell");
            var valueField = DefaultFieldFactory.CreateForValue(valueType, string.Empty, value, false,
                changed => SetValue(key, changed));
            valueField.AddToClassList("flexus-table__cell");
            var remove = InspectorVisuals.IconButton("×", "Remove entry", () => Remove(key), "row-remove");
            row.Add(keyField);
            row.Add(valueField);
            row.Add(remove);
            rows.Add(row);
        }

        private void AddEntry()
        {
            try
            {
                context.Inspector.RecordUndo("Add Dictionary Entry");
                addMethod.Invoke(dictionary, new[] { newKey, newValue });
                context.Inspector.MarkDirty();
                selectedKey = newKey;
                hasSelection = true;
                addArea.style.display = DisplayStyle.None;
                Rebuild();
            }
            catch (Exception exception) { Debug.LogException(exception.GetBaseException()); }
        }

        private void SetValue(object key, object value)
        {
            context.Inspector.RecordUndo("Edit Dictionary Value");
            indexer.SetValue(dictionary, value, new[] { key });
            context.Inspector.MarkDirty();
        }

        private void Remove(object key)
        {
            context.Inspector.RecordUndo("Remove Dictionary Entry");
            removeMethod.Invoke(dictionary, new[] { key });
            context.Inspector.MarkDirty();
            if (hasSelection && Equals(selectedKey, key))
            {
                selectedKey = null;
                hasSelection = false;
            }
            Rebuild();
        }

        private void RemoveSelected()
        {
            if (hasSelection)
            {
                Remove(selectedKey);
                return;
            }
            var last = ((IEnumerable)dictionary).Cast<object>().LastOrDefault();
            if (last == null) return;
            Remove(last.GetType().GetProperty("Key")?.GetValue(last));
        }

        private void ClearCollection()
        {
            if (Count == 0 || clearMethod == null) return;
            context.Inspector.RecordUndo("Clear Dictionary");
            clearMethod.Invoke(dictionary, null);
            context.Inspector.MarkDirty();
            selectedKey = null;
            hasSelection = false;
            page = 0;
            Rebuild();
        }

        private void CopyCollection()
        {
            if (dictionary is ISerializationCallbackReceiver receiver) receiver.OnBeforeSerialize();
            property?.serializedObject.Update();
            CollectionClipboard.CopyCollection(property, collectionType);
        }

        private void CopyElement(object key, object value, int absoluteIndex)
        {
            if (dictionary is ISerializationCallbackReceiver receiver) receiver.OnBeforeSerialize();
            property?.serializedObject.Update();
            CollectionClipboard.CopyDictionaryElement(property, valueType, absoluteIndex, $"{key}: {value}");
        }

        private void PasteElement(object key)
        {
            if (!CollectionClipboard.TryReadDictionaryElement(valueType, out var value)) return;
            context.Inspector.RecordUndo("Paste Dictionary Element");
            indexer.SetValue(dictionary, value, new[] { key });
            context.Inspector.MarkDirty();
            Select(key);
            Rebuild();
        }

        private void PasteCollection()
        {
            if (!CollectionClipboard.CanPasteCollection(collectionType)) return;
            context.Inspector.RecordUndo("Paste Dictionary");
            if (!CollectionClipboard.TryPasteCollection(property, collectionType)) return;
            if (dictionary is ISerializationCallbackReceiver receiver) receiver.OnAfterDeserialize();
            context.Inspector.MarkDirty();
            selectedKey = null;
            hasSelection = false;
            page = 0;
            Rebuild();
        }

        private void Select(object key)
        {
            selectedKey = key;
            hasSelection = true;
            RefreshSelection();
        }

        private void ClearSelection()
        {
            selectedKey = null;
            hasSelection = false;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            foreach (var child in rows.Children())
                if (child.ClassListContains("flexus-table__row"))
                    child.EnableInClassList("flexus-collection-row--selected",
                        hasSelection && Equals(child.userData, selectedKey));
            removeSelectedButton?.SetEnabled(Count > 0);
        }
    }

    internal static class CollectionSelection
    {
        public static bool IsRowTarget(IEventHandler target, VisualElement host)
        {
            for (var element = target as VisualElement; element != null && element != host; element = element.parent)
                if (element.ClassListContains("flexus-list-item") ||
                    element.ClassListContains("flexus-table__row")) return true;
            return false;
        }

        public static bool IsFooterTarget(IEventHandler target, VisualElement host)
        {
            for (var element = target as VisualElement; element != null && element != host; element = element.parent)
                if (element.ClassListContains("flexus-collection__footer")) return true;
            return false;
        }
    }
}
