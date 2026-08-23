using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class FieldColumnLayoutController
    {
        internal const float MinimumLabelWidth = 48f;
        internal const float MinimumInputWidth = 80f;
        internal const float LabelInputGap = 6f;
        internal const float MaximumLabelRatio = 0.45f;

        private const int MinimumInitializationPasses = 3;
        private const int MaximumInitializationPasses = 12;
        private const int RequiredStablePasses = 2;
        private const int LayoutDelayMilliseconds = 16;

        private static readonly ConditionalWeakTable<VisualElement, FieldColumnLayoutController> Controllers =
            new ConditionalWeakTable<VisualElement, FieldColumnLayoutController>();

        private static readonly string[] ScopeClasses =
        {
            "flexus-list-item__body",
            "flexus-managed-reference__body",
            "flexus-dictionary__add",
            "flexus-method-action__parameters",
            "flexus-tabs__page",
            "flexus-ui-inspector__group-content",
            "flexus-toggle-group__content",
            "unity-foldout__content",
            "flexus-horizontal-group__item",
            "flexus-ui-inspector__group",
        };

        private readonly VisualElement root;
        private const string UnityAlignedFieldClass = "unity-base-field__aligned";
        private const string UnityBaseFieldClass = "unity-base-field";
        private const string UnityBaseFieldInputClass = "unity-base-field__input";
        private const string UnityToggleClass = "unity-toggle";
        private const string FlexusFieldClass = "flexus-field-layout";
        private const string FlexusFieldInputClass = "flexus-field-layout__input";
        private const string FlexusToggleFieldClass = "flexus-field-layout--toggle";
        private const string FlexusObservedGeometryClass = "flexus-field-layout--observed";

        private float previousRootWidth = -1f;
        private int initializationGeneration;
        private int initializationPass;
        private int previousEligibleLabelCount = -1;
        private int previousLayoutFingerprint;
        private int stablePasses;
        private bool initialized;
        private bool refreshScheduled;

        private FieldColumnLayoutController(VisualElement root) => this.root = root;

        public static void Attach(VisualElement root)
        {
            var controller = new FieldColumnLayoutController(root);
            Controllers.Remove(root);
            Controllers.Add(root, controller);
            root.AddToClassList("flexus-ui-inspector--layout-pending");
            root.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                if (ReferenceEquals(evt.target, root)) controller.BeginInitialization();
            });
            root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (!ReferenceEquals(evt.target, root)) return;
                controller.initializationGeneration++;
                controller.initialized = false;
                root.AddToClassList("flexus-ui-inspector--layout-pending");
            });
            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (!controller.initialized ||
                    Mathf.Abs(evt.newRect.width - controller.previousRootWidth) <= 0.5f) return;
                controller.ScheduleRefresh();
            });
            if (root.panel != null) controller.BeginInitialization();
        }

        public static void RequestRefresh(VisualElement element)
        {
            var root = element;
            while (root != null && !root.ClassListContains("flexus-ui-inspector")) root = root.parent;
            if (root != null && Controllers.TryGetValue(root, out var controller)) controller.ScheduleRefresh();
        }

        internal static float CalculateColumnWidth(float desiredWidth, float rowWidth)
        {
            if (!IsFinitePositive(rowWidth)) return 0f;
            var ratioCap = rowWidth * MaximumLabelRatio;
            var inputCap = rowWidth - MinimumInputWidth - LabelInputGap;
            var cap = Mathf.Max(0f, Mathf.Min(ratioCap, inputCap));
            return Mathf.Floor(Mathf.Min(Mathf.Max(desiredWidth, MinimumLabelWidth), cap));
        }

        internal static float MeasureNaturalWidth(Label label)
        {
            var editorLabel = label.panel != null ? EditorStyles.label : null;
            var measured = editorLabel != null
                ? editorLabel.CalcSize(new GUIContent(label.text)).x
                : (label.text?.Length ?? 0) * 7f;
            if (!IsFinitePositive(measured)) measured = MinimumLabelWidth;
            var padding = NonNegativeFinite(label.resolvedStyle.paddingLeft) +
                          NonNegativeFinite(label.resolvedStyle.paddingRight);
            return Mathf.Ceil(measured + padding + 1f);
        }

        private void BeginInitialization()
        {
            initialized = false;
            refreshScheduled = false;
            initializationPass = 0;
            previousEligibleLabelCount = -1;
            previousLayoutFingerprint = 0;
            stablePasses = 0;
            root.AddToClassList("flexus-ui-inspector--layout-pending");
            var generation = ++initializationGeneration;
            ScheduleInitializationPass(generation);
        }

        private void ScheduleInitializationPass(int generation)
        {
            root.schedule.Execute(() => RunInitializationPass(generation))
                .ExecuteLater(LayoutDelayMilliseconds);
        }

        private void RunInitializationPass(int generation)
        {
            if (generation != initializationGeneration || root.panel == null) return;
            initializationPass++;
            NormalizeNativeFields(root);
            ObserveFieldGeometry();
            var state = CaptureLayoutState();
            if (state.Ready && state.EligibleLabelCount == previousEligibleLabelCount &&
                state.Fingerprint == previousLayoutFingerprint)
                stablePasses++;
            else
                stablePasses = 0;
            previousEligibleLabelCount = state.EligibleLabelCount;
            previousLayoutFingerprint = state.Fingerprint;

            var stable = initializationPass >= MinimumInitializationPasses &&
                         stablePasses >= RequiredStablePasses;
            if (!stable && initializationPass < MaximumInitializationPasses)
            {
                ScheduleInitializationPass(generation);
                return;
            }

            Refresh();
            root.schedule.Execute(() =>
            {
                if (generation != initializationGeneration || root.panel == null) return;
                initialized = true;
                previousRootWidth = root.resolvedStyle.width;
                root.RemoveFromClassList("flexus-ui-inspector--layout-pending");
            }).ExecuteLater(0);
        }

        private void ScheduleRefresh()
        {
            if (!initialized || refreshScheduled || root.panel == null) return;
            refreshScheduled = true;
            root.schedule.Execute(() => root.schedule.Execute(() =>
            {
                refreshScheduled = false;
                if (root.panel != null) Refresh();
            }).ExecuteLater(0)).ExecuteLater(0);
        }

        private void Refresh()
        {
            if (root.panel == null || !IsFinitePositive(root.resolvedStyle.width)) return;
            NormalizeNativeFields(root);
            ObserveFieldGeometry();
            previousRootWidth = root.resolvedStyle.width;
            var labels = EligibleLabels().ToList();

            var groups = new Dictionary<VisualElement, List<LabelEntry>>();
            foreach (var label in labels)
            {
                var owner = FindFieldOwner(label);
                if (owner == null || !IsFinitePositive(owner.resolvedStyle.width)) continue;
                var scope = FindScope(owner) ?? root;
                if (!groups.TryGetValue(scope, out var entries))
                    groups.Add(scope, entries = new List<LabelEntry>());
                entries.Add(new LabelEntry(label, owner, FindDirectBranch(owner, scope)));
            }

            foreach (var pair in groups)
            foreach (var run in ConsecutiveRuns(pair.Key, pair.Value))
            {
                var desiredWidth = run.Max(entry => MeasureNaturalWidth(entry.Label));
                var narrowestRow = run.Min(entry => entry.Owner.resolvedStyle.width);
                var columnWidth = CalculateColumnWidth(desiredWidth, narrowestRow);
                foreach (var entry in run) ApplyColumnWidth(entry.Label, columnWidth);
            }
        }

        private LayoutState CaptureLayoutState()
        {
            var count = 0;
            var ready = IsFinitePositive(root.resolvedStyle.width);
            var fingerprint = ready ? Mathf.RoundToInt(root.resolvedStyle.width * 2f) : 0;
            foreach (var label in EligibleLabels())
            {
                count++;
                var owner = FindFieldOwner(label);
                if (owner == null || !IsFinitePositive(owner.resolvedStyle.width))
                {
                    ready = false;
                    continue;
                }
                unchecked
                {
                    fingerprint = fingerprint * 397 ^ Mathf.RoundToInt(owner.resolvedStyle.width * 2f);
                    fingerprint = fingerprint * 397 ^ (label.text?.GetHashCode() ?? 0);
                }
            }
            return new LayoutState(count, ready, fingerprint);
        }

        private IEnumerable<Label> EligibleLabels()
        {
            var labels = root.Query<Label>(className: "unity-base-field__label").ToList();
            foreach (var referenceLabel in root.Query<Label>(className: "flexus-managed-reference__label").ToList())
                if (!labels.Contains(referenceLabel)) labels.Add(referenceLabel);

            foreach (var label in labels)
            {
                if (string.IsNullOrWhiteSpace(label.text) ||
                    label.resolvedStyle.display == DisplayStyle.None ||
                    label.ClassListContains("flexus-label--explicit-width") ||
                    HasAncestorClass(label, "unity-foldout__toggle", root) ||
                    HasAncestorClass(label, "unity-composite-field__field", root)) continue;
                yield return label;
            }
        }

        internal static void ApplyColumnWidth(Label label, float width)
        {
            // Unity may rewrite aligned-field inline styles after binding, on the first pointer
            // interaction, or after a panel resize. Always assert the complete column contract;
            // remembering only the last requested width leaves stale native styles in place.
            label.style.width = width;
            label.style.minWidth = width;
            label.style.maxWidth = width;
            label.style.flexBasis = width;
            label.style.flexGrow = 0;
            label.style.flexShrink = 0;
        }

        internal static void NormalizeNativeFields(VisualElement container)
        {
            foreach (var aligned in container.Query<VisualElement>(className: UnityAlignedFieldClass).ToList())
                aligned.RemoveFromClassList(UnityAlignedFieldClass);

            foreach (var field in container.Query<VisualElement>(className: UnityBaseFieldClass).ToList())
            {
                field.RemoveFromClassList(UnityAlignedFieldClass);
                field.AddToClassList(FlexusFieldClass);

                var isToggle = field.ClassListContains(UnityToggleClass);
                if (isToggle)
                    field.AddToClassList(FlexusToggleFieldClass);
                else
                    field.RemoveFromClassList(FlexusToggleFieldClass);

                foreach (var child in field.Children())
                {
                    if (!child.ClassListContains(UnityBaseFieldInputClass)) continue;
                    child.AddToClassList(FlexusFieldInputClass);
                    child.style.minWidth = 0;
                    child.style.marginLeft = 0;
                    if (isToggle)
                    {
                        child.style.width = StyleKeyword.Auto;
                        child.style.flexBasis = StyleKeyword.Auto;
                        child.style.flexGrow = 0;
                        child.style.flexShrink = 0;
                    }
                    else
                    {
                        child.style.width = StyleKeyword.Auto;
                        child.style.flexBasis = 0;
                        child.style.flexGrow = 1;
                        child.style.flexShrink = 1;
                    }
                    break;
                }
            }
        }

        private void ObserveFieldGeometry()
        {
            ObserveGeometry(root.Query<VisualElement>(className: UnityBaseFieldClass).ToList());
            ObserveGeometry(root.Query<VisualElement>(className: UnityBaseFieldInputClass).ToList());
            ObserveGeometry(root.Query<Label>(className: "unity-base-field__label").ToList()
                .Cast<VisualElement>().ToList());
        }

        private void ObserveGeometry(IEnumerable<VisualElement> elements)
        {
            foreach (var element in elements)
            {
                if (element.ClassListContains(FlexusObservedGeometryClass)) continue;
                element.AddToClassList(FlexusObservedGeometryClass);
                element.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    if (initialized) ScheduleRefresh();
                });
            }
        }

        private VisualElement FindScope(VisualElement owner)
        {
            for (var current = owner.parent; current != null && current != root; current = current.parent)
                if (ScopeClasses.Any(current.ClassListContains)) return current;
            return root;
        }

        private static VisualElement FindDirectBranch(VisualElement owner, VisualElement scope)
        {
            var branch = owner;
            while (branch.parent != null && branch.parent != scope) branch = branch.parent;
            return branch;
        }

        private static IEnumerable<List<LabelEntry>> ConsecutiveRuns(VisualElement scope,
            IReadOnlyCollection<LabelEntry> entries)
        {
            var byBranch = entries.GroupBy(entry => entry.Branch)
                .ToDictionary(group => group.Key, group => group.ToList());
            List<LabelEntry> currentRun = null;
            var previousIndex = -2;
            for (var index = 0; index < scope.childCount; index++)
            {
                if (!byBranch.TryGetValue(scope[index], out var branchEntries)) continue;
                if (currentRun == null || index != previousIndex + 1)
                {
                    if (currentRun != null) yield return currentRun;
                    currentRun = new List<LabelEntry>();
                }
                currentRun.AddRange(branchEntries);
                previousIndex = index;
            }
            if (currentRun != null) yield return currentRun;
        }

        private static VisualElement FindFieldOwner(VisualElement label)
        {
            for (var current = label.parent; current != null; current = current.parent)
            {
                if (current.ClassListContains("unity-base-field") ||
                    current.ClassListContains("flexus-search-dropdown") ||
                    current.ClassListContains("flexus-managed-reference__header")) return current;
                if (current.ClassListContains("flexus-ui-inspector")) break;
            }
            return null;
        }

        private static bool HasAncestorClass(VisualElement element, string className, VisualElement stop)
        {
            for (var current = element.parent; current != null && current != stop; current = current.parent)
                if (current.ClassListContains(className)) return true;
            return false;
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static float NonNegativeFinite(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);

        private readonly struct LayoutState
        {
            public int EligibleLabelCount { get; }
            public bool Ready { get; }
            public int Fingerprint { get; }

            public LayoutState(int eligibleLabelCount, bool ready, int fingerprint)
            {
                EligibleLabelCount = eligibleLabelCount;
                Ready = ready;
                Fingerprint = fingerprint;
            }
        }

        private readonly struct LabelEntry
        {
            public Label Label { get; }
            public VisualElement Owner { get; }
            public VisualElement Branch { get; }

            public LabelEntry(Label label, VisualElement owner, VisualElement branch)
            {
                Label = label;
                Owner = owner;
                Branch = branch;
            }
        }
    }
}
