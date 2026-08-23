using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal readonly struct SearchItem
    {
        public string Text { get; }
        public object Value { get; }
        public Texture2D Icon { get; }
        public string Description { get; }

        public SearchItem(string text, object value, Texture2D icon = null, string description = null)
        {
            Text = text;
            Value = value;
            Icon = icon;
            Description = description;
        }
    }

    internal sealed class SearchDropdownElement : VisualElement
    {
        private readonly Button button;
        private readonly Label valueLabel;
        private readonly Image valueIcon;
        private readonly Func<IEnumerable<SearchItem>> itemProvider;
        private readonly Action<object> selected;

        public SearchDropdownElement(string label, string current,
            Func<IEnumerable<SearchItem>> itemProvider, Action<object> selected, bool compact = false)
        {
            this.itemProvider = itemProvider;
            this.selected = selected;
            AddToClassList("flexus-search-dropdown");
            if (compact) AddToClassList("flexus-search-dropdown--compact");

            if (!string.IsNullOrEmpty(label))
            {
                var name = new Label(label);
                name.AddToClassList("unity-base-field__label");
                name.AddToClassList("flexus-search-dropdown__label");
                Add(name);
            }

            button = new Button(OpenPicker);
            button.AddToClassList("flexus-search-dropdown__button");
            valueIcon = new Image();
            valueIcon.AddToClassList("flexus-search-dropdown__value-icon");
            valueIcon.style.display = DisplayStyle.None;
            valueLabel = new Label();
            valueLabel.AddToClassList("flexus-search-dropdown__value");
            var chevron = new Label("▾");
            chevron.AddToClassList("flexus-search-dropdown__chevron");
            button.Add(valueIcon);
            button.Add(valueLabel);
            button.Add(chevron);
            Add(button);
            SetText(current);
        }

        public void SetText(string text, Texture2D icon = null)
        {
            valueLabel.text = string.IsNullOrEmpty(text) ? "None" : text;
            valueIcon.image = icon;
            valueIcon.style.display = icon ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void OpenPicker()
        {
            var items = itemProvider()?.ToList() ?? new List<SearchItem>();
            var window = ScriptableObject.CreateInstance<SearchDropdownWindow>();
            window.Initialize(items, item =>
            {
                selected(item.Value);
                SetText(LeafName(item.Text), item.Icon);
            });
            var rect = button.worldBound;
            var screenPoint = GUIUtility.GUIToScreenPoint(rect.position);
            window.ShowAsDropDown(new Rect(screenPoint, rect.size),
                new Vector2(Mathf.Max(360, rect.width), Mathf.Min(520, 96 + items.Count * 34)));
        }

        private static string LeafName(string text)
        {
            if (string.IsNullOrEmpty(text)) return "None";
            var index = text.LastIndexOf('/');
            return index >= 0 ? text.Substring(index + 1) : text;
        }
    }

    internal sealed class SearchDropdownWindow : EditorWindow
    {
        private List<SearchItem> allItems;
        private readonly List<SearchItem> filtered = new List<SearchItem>();
        private Action<SearchItem> selected;
        private ListView list;
        private Label count;

        public void Initialize(List<SearchItem> items, Action<SearchItem> onSelected)
        {
            allItems = items ?? new List<SearchItem>();
            filtered.AddRange(allItems);
            selected = onSelected;
        }

        public void CreateGUI()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.flexus.inspector/Editor/USS/FlexusUIInspector.uss");
            if (styleSheet != null) rootVisualElement.styleSheets.Add(styleSheet);
            rootVisualElement.AddToClassList("flexus-picker-window");
            rootVisualElement.AddToClassList(EditorGUIUtility.isProSkin ? "flexus-theme--dark" : "flexus-theme--light");

            var searchRow = new VisualElement();
            searchRow.AddToClassList("flexus-picker-window__search-row");
            var search = new TextField();
            search.AddToClassList("flexus-picker-window__search");
            search.RegisterValueChangedCallback(evt => Filter(evt.newValue));
            search.RegisterCallback<KeyDownEvent>(OnKeyDown);
            count = InspectorVisuals.Badge(allItems.Count.ToString(), "count");
            searchRow.Add(search);
            searchRow.Add(count);
            rootVisualElement.Add(searchRow);

            list = new ListView(filtered, 34, MakeItem, BindItem)
            {
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            list.AddToClassList("flexus-picker-window__list");
            list.selectionChanged += selection =>
            {
                var items = selection.OfType<SearchItem>().ToArray();
                if (items.Length > 0) Commit(items[0]);
            };
            rootVisualElement.Add(list);
            search.Focus();
        }

        private static VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("flexus-picker-item");
            row.Add(new Image { name = "icon" });
            var text = new VisualElement { name = "text" };
            text.Add(new Label { name = "label" });
            text.Add(new Label { name = "description" });
            row.Add(text);
            return row;
        }

        private void BindItem(VisualElement row, int index)
        {
            if (index < 0 || index >= filtered.Count) return;
            var item = filtered[index];
            row.EnableInClassList("flexus-picker-item--alternate", index % 2 == 1);
            row.Q<Label>("label").text = item.Text;
            var description = row.Q<Label>("description");
            description.text = item.Description;
            description.style.display = string.IsNullOrEmpty(item.Description) ? DisplayStyle.None : DisplayStyle.Flex;
            var icon = row.Q<Image>("icon");
            icon.image = item.Icon;
            icon.style.display = item.Icon ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Filter(string query)
        {
            filtered.Clear();
            if (string.IsNullOrWhiteSpace(query)) filtered.AddRange(allItems);
            else
            {
                var terms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                filtered.AddRange(allItems.Where(item => terms.All(term =>
                    (item.Text?.IndexOf(term, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (item.Description?.IndexOf(term, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)));
            }
            count.text = filtered.Count.ToString();
            list?.Rebuild();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape) Close();
            else if (evt.keyCode == KeyCode.DownArrow && filtered.Count > 0)
            {
                list.selectedIndex = 0;
                list.Focus();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Return && filtered.Count > 0)
                Commit(filtered[0]);
        }

        private void Commit(SearchItem item)
        {
            selected?.Invoke(item);
            Close();
        }
    }
}
