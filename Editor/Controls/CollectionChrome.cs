using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class CollectionChrome
    {
        private readonly Label arrow;
        private readonly Action<bool> expandedChanged;
        private bool expanded;

        public VisualElement Body { get; } = new VisualElement();
        public VisualElement Footer { get; } = new VisualElement();
        public Label Count { get; } = new Label();

        public CollectionChrome(VisualElement root, string title, bool initialExpanded,
            Action<bool> expandedChanged = null)
        {
            this.expandedChanged = expandedChanged;
            var header = new Button(Toggle);
            header.AddToClassList("flexus-collection__header");
            arrow = new Label();
            arrow.AddToClassList("flexus-collection__arrow");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("flexus-collection__title");
            Count.AddToClassList("flexus-collection__count");
            header.Add(arrow);
            header.Add(titleLabel);
            header.Add(Count);

            Body.AddToClassList("flexus-collection__body");
            Footer.AddToClassList("flexus-collection__footer");
            root.Add(header);
            root.Add(Body);
            root.Add(Footer);
            SetExpanded(initialExpanded, false);
        }

        public void SetExpanded(bool value, bool notify = true)
        {
            expanded = value;
            arrow.text = value ? "▾" : "›";
            Body.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            Footer.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            if (notify) expandedChanged?.Invoke(value);
        }

        public void AddFooterContent(VisualElement pager, VisualElement actions)
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("flexus-collection__footer-spacer");
            spacer.style.width = actions.childCount * 32;
            Footer.Add(spacer);
            Footer.Add(pager);
            Footer.Add(actions);
        }

        private void Toggle() => SetExpanded(!expanded);
    }

    internal static class CollectionDrag
    {
        public static void Attach(VisualElement handle, VisualElement rowHost, VisualElement row,
            int sourceIndex, Action<int, int> move)
        {
            var pointerId = -1;
            var targetIndex = sourceIndex;
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                pointerId = evt.pointerId;
                targetIndex = sourceIndex;
                handle.CapturePointer(pointerId);
                row.AddToClassList("flexus-collection-row--dragging");
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (pointerId != evt.pointerId || !handle.HasPointerCapture(pointerId)) return;
                var rows = rowHost.Children().Where(child =>
                    child.ClassListContains("flexus-list-item") || child.ClassListContains("flexus-table__row")).ToArray();
                for (var index = 0; index < rows.Length; index++)
                {
                    rows[index].RemoveFromClassList("flexus-collection-row--drop-target");
                    if (evt.position.y >= rows[index].worldBound.yMin && evt.position.y <= rows[index].worldBound.yMax)
                        targetIndex = index;
                }
                if (targetIndex >= 0 && targetIndex < rows.Length)
                    rows[targetIndex].AddToClassList("flexus-collection-row--drop-target");
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (pointerId != evt.pointerId) return;
                if (handle.HasPointerCapture(pointerId)) handle.ReleasePointer(pointerId);
                row.RemoveFromClassList("flexus-collection-row--dragging");
                foreach (var child in rowHost.Children())
                    child.RemoveFromClassList("flexus-collection-row--drop-target");
                if (targetIndex != sourceIndex) move(sourceIndex, targetIndex);
                pointerId = -1;
                evt.StopPropagation();
            });
        }
    }
}
