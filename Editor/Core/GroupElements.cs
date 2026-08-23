using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class GroupHost
    {
        private readonly VisualElement content;
        private readonly TabGroupElement tabs;
        private readonly Toggle toggle;
        private readonly float[] sizes;
        private readonly bool horizontal;
        private bool toggleBound;

        public VisualElement Root { get; }

        public GroupHost(InspectorGroupAttribute descriptor)
        {
            sizes = descriptor.Sizes;
            switch (descriptor.Style)
            {
                case InspectorGroupStyle.Foldout:
                {
                    var foldout = new Foldout
                    {
                        text = descriptor.HideTitle ? string.Empty : descriptor.Title,
                        value = descriptor.Expanded,
                        viewDataKey = $"flexus-group-{descriptor.Path}",
                    };
                    foldout.AddToClassList("flexus-group-foldout");
                    Root = foldout;
                    content = foldout.contentContainer;
                    break;
                }
                case InspectorGroupStyle.Box:
                {
                    Root = new VisualElement();
                    Root.AddToClassList("flexus-ui-inspector__box");
                    if (!descriptor.HideTitle)
                    {
                        var header = new VisualElement();
                        header.AddToClassList("flexus-ui-inspector__group-header");
                        var accent = new VisualElement();
                        accent.AddToClassList("flexus-ui-inspector__group-accent");
                        var title = new Label(descriptor.Title);
                        title.AddToClassList("flexus-ui-inspector__group-title");
                        header.Add(accent);
                        header.Add(title);
                        Root.Add(header);
                    }
                    content = new VisualElement();
                    content.AddToClassList("flexus-ui-inspector__group-content");
                    Root.Add(content);
                    break;
                }
                case InspectorGroupStyle.Horizontal:
                {
                    horizontal = true;
                    Root = content = new VisualElement();
                    content.AddToClassList("flexus-horizontal-group");
                    content.style.flexDirection = FlexDirection.Row;
                    content.style.alignItems = Align.Stretch;
                    break;
                }
                case InspectorGroupStyle.Toggle:
                {
                    Root = new VisualElement();
                    Root.AddToClassList("flexus-ui-inspector__box");
                    toggle = new Toggle(descriptor.Title) { value = true };
                    toggle.AddToClassList("flexus-toggle-group__header");
                    content = new VisualElement();
                    content.AddToClassList("flexus-toggle-group__content");
                    toggle.RegisterValueChangedCallback(evt => content.SetEnabled(evt.newValue));
                    Root.Add(toggle);
                    Root.Add(content);
                    break;
                }
                case InspectorGroupStyle.Tabs:
                {
                    tabs = new TabGroupElement(descriptor.Path);
                    Root = content = tabs;
                    break;
                }
                default:
                    Root = content = new VisualElement();
                    break;
            }

            Root.name = $"group-{descriptor.Path}";
            Root.AddToClassList("flexus-ui-inspector__group");
            Root.AddToClassList($"flexus-ui-inspector__group--{descriptor.Style.ToString().ToLowerInvariant()}");
        }

        public void Add(MemberElement element, string tabName)
        {
            if (toggle != null && !toggleBound && element.Context.Descriptor.ValueType == typeof(bool))
            {
                toggleBound = true;
                toggle.SetValueWithoutNotify(element.Context.Value.GetValue() is bool enabled && enabled);
                toggle.RegisterValueChangedCallback(evt => element.Context.Value.SetValue(evt.newValue,
                    "Toggle Inspector Group"));
                if (element.Context.SerializedProperty != null)
                    toggle.TrackPropertyValue(element.Context.SerializedProperty,
                        property => toggle.SetValueWithoutNotify(property.boolValue));
                content.SetEnabled(toggle.value);
                return;
            }

            if (tabs != null)
                tabs.AddToTab(string.IsNullOrEmpty(tabName) ? "Default" : tabName, element);
            else
                AddChild(element);
        }

        public void AddGroup(VisualElement element) => AddChild(element);

        private void AddChild(VisualElement element)
        {
            if (horizontal)
            {
                var index = content.childCount;
                var weight = sizes != null && index < sizes.Length ? sizes[index] : 1f;
                if (float.IsNaN(weight) || float.IsInfinity(weight) || weight <= 0f) weight = 1f;

                element.AddToClassList("flexus-horizontal-group__item");
                element.style.flexBasis = 0;
                element.style.flexGrow = weight;
                element.style.flexShrink = 1;
                element.style.minWidth = 0;
            }
            content.Add(element);
        }
    }

    internal sealed class TabGroupElement : VisualElement
    {
        private readonly Toolbar toolbar = new Toolbar();
        private readonly VisualElement pages = new VisualElement();
        private readonly Dictionary<string, VisualElement> pageByName = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, ToolbarToggle> toggleByName = new Dictionary<string, ToolbarToggle>();
        private string selected;

        public TabGroupElement(string key)
        {
            viewDataKey = $"flexus-tabs-{key}";
            AddToClassList("flexus-tabs");
            toolbar.AddToClassList("flexus-tabs__toolbar");
            pages.AddToClassList("flexus-tabs__pages");
            Add(toolbar);
            Add(pages);
        }

        public void AddToTab(string tabName, VisualElement element)
        {
            if (!pageByName.TryGetValue(tabName, out var page))
            {
                page = new VisualElement { name = $"tab-{tabName}" };
                page.AddToClassList("flexus-tabs__page");
                var toggle = new ToolbarToggle { text = tabName };
                toggle.AddToClassList("flexus-tabs__tab");
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) Select(tabName);
                    else if (selected == tabName) toggle.SetValueWithoutNotify(true);
                });
                pageByName.Add(tabName, page);
                toggleByName.Add(tabName, toggle);
                toolbar.Add(toggle);
                pages.Add(page);
                if (selected == null) Select(tabName);
                else page.style.display = DisplayStyle.None;
            }
            page.Add(element);
        }

        private void Select(string tabName)
        {
            selected = tabName;
            foreach (var pair in pageByName)
                pair.Value.style.display = pair.Key == tabName ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var pair in toggleByName)
                pair.Value.SetValueWithoutNotify(pair.Key == tabName);
            FieldColumnLayoutController.RequestRefresh(this);
        }
    }
}
