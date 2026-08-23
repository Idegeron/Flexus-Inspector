using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Flexus.Inspector.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Tests
{
    public sealed class UIInspectorTests
    {
        private GameObject gameObject;
        private TestComponent component;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("UI Inspector Tests");
            component = gameObject.AddComponent<TestComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            if (gameObject) UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void FallbackEditorBuildsExpectedElements()
        {
            var editor = UnityEditor.Editor.CreateEditor(component);
            var root = editor.CreateInspectorGUI();
            Assert.NotNull(root);
            Assert.NotNull(root.Q("group-main"));
            Assert.NotNull(root.Q("member-value"));
            Assert.NotNull(root.Q(className: "flexus-managed-reference"));
            Assert.NotNull(root.Q(className: "flexus-collection"));
            Assert.Greater(root.Query<Button>().ToList().Count, 0);

            var inlineReference = root.Q("member-inlineAction")
                .Q(className: "flexus-managed-reference");
            Assert.NotNull(inlineReference);
            Assert.True(inlineReference.ClassListContains("flexus-managed-reference--inline"));
            Assert.NotNull(inlineReference.Q(className: "flexus-managed-reference__picker"),
                "A null inline managed reference must retain its type picker.");
            Assert.NotNull(inlineReference.Q(className: "flexus-empty-state"));
            Assert.Null(inlineReference.Q(className: "flexus-managed-reference__label"),
                "HideLabel must hide the custom managed-reference label.");

            AssertFieldLabel(root, "_int", "Int");
            AssertFieldLabel(root, "_float", "Float");
            AssertFieldLabel(root, "_string", "String");
            AssertFieldLabel(root, "_text", "Text");
            AssertFieldLabel(root, "_testText", "Test");
            Assert.Null(root.Q("member-_hidden"));

            var polymorphicList = root.Q("member-actions").Q(className: "flexus-list");
            Assert.NotNull(polymorphicList);
            Assert.True(polymorphicList.ClassListContains("flexus-context-menu--collection"));
            Assert.NotNull(polymorphicList.Q(className: "flexus-managed-reference"));
            Assert.False(polymorphicList.Query<Label>().ToList().Any(label => label.text == "Element 0"));
            Assert.NotNull(polymorphicList.Q(className: "flexus-list-item__type-picker"));
            var managedHeader = polymorphicList.Q(className:
                "flexus-list-item__header-main--managed-reference");
            Assert.NotNull(managedHeader);
            Assert.NotNull(managedHeader.Q(className: "flexus-list-item__expander"));
            Assert.NotNull(managedHeader.Q(className: "flexus-list-item__type-picker"));
            Assert.NotNull(managedHeader.Q(className: "flexus-icon-button--row-remove"));
            Assert.True(polymorphicList.Q(className: "flexus-managed-reference")
                .ClassListContains("flexus-managed-reference--body-only"));
            Assert.True(polymorphicList.Q(className: "flexus-list-item")
                .ClassListContains("flexus-context-menu--element"));

            var table = root.Q("member-table").Q(className: "flexus-table");
            Assert.NotNull(table);
            Assert.AreEqual(2, table.Query<Button>(className: "flexus-icon-button--row-remove").ToList().Count,
                "Every visible TableList row must have a remove button.");
            var horizontal = root.Q("group-row");
            Assert.NotNull(horizontal);
            Assert.AreEqual(2, horizontal.Children().Count());
            Assert.True(horizontal.Children().All(child => child.style.flexGrow.value > 0));
            AssertFieldLabel(root, "strength", "Strength");
            AssertFieldLabel(root, "agility", "Agility");
            Assert.NotNull(root.Q(className: "flexus-method-action--with-parameters"));
            Assert.NotNull(root.Q(className: "flexus-method-action__footer"));

            var body = polymorphicList.Children()
                .Single(child => child.ClassListContains("flexus-collection__body"));
            var footer = polymorphicList.Children()
                .Single(child => child.ClassListContains("flexus-collection__footer"));
            Assert.Greater(polymorphicList.IndexOf(footer), polymorphicList.IndexOf(body),
                "Collection footer must be laid out after its body.");

            var serializedObject = new SerializedObject(component);
            var actions = serializedObject.FindProperty(nameof(TestComponent.actions));
            var expansionStates = (Dictionary<string, bool>)polymorphicList.GetType()
                .GetField("expansionStates", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(polymorphicList);
            Assert.NotNull(expansionStates);
            var existingKey = expansionStates.Keys.First();
            expansionStates[existingKey] = false;
            polymorphicList.GetType().GetMethod("AddItem", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(polymorphicList, null);
            serializedObject.Update();
            Assert.AreEqual(3, actions.arraySize);
            Assert.IsTrue(expansionStates.TryGetValue(existingKey, out var remainedExpanded));
            Assert.False(remainedExpanded,
                "Adding an item must preserve existing expansion state.");
            Assert.False(actions.GetArrayElementAtIndex(2).isExpanded,
                "A newly added item must start collapsed.");
            var firstBody = body.Q(className: "flexus-collection__rows")
                .Children().First(child => child.ClassListContains("flexus-list-item"))
                .Q(className: "flexus-list-item__body");
            Assert.AreEqual(DisplayStyle.None, firstBody.style.display.value,
                "The rebuilt visual must keep the existing item collapsed.");
            UnityEngine.Object.DestroyImmediate(editor);
        }

        [Test]
        public void TypeConstraintDropdownIncludesUnityObjectDescendants()
        {
            var editor = UnityEditor.Editor.CreateEditor(component);
            var root = editor.CreateInspectorGUI();
            var dropdown = root.Q("member-componentType").Q<SearchDropdownElement>();
            Assert.NotNull(dropdown);
            var provider = (Func<IEnumerable<SearchItem>>)typeof(SearchDropdownElement)
                .GetField("itemProvider", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(dropdown);

            Assert.NotNull(provider);
            Assert.True(provider().Any(item => Equals(item.Value, typeof(TestComponent))));
            UnityEngine.Object.DestroyImmediate(editor);
        }

        [Test]
        public void MethodButtonInvokesAttributedMethod()
        {
            var editor = UnityEditor.Editor.CreateEditor(component);
            var root = editor.CreateInspectorGUI();
            var button = root.Q("member-Increment").Q<Button>(className: "flexus-button--primary");

            Assert.NotNull(button);
            Assert.True(button.enabledInHierarchy);
            var invoke = button.clickable.GetType().GetMethod("Invoke",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(invoke);
            invoke.Invoke(button.clickable, new object[] { null });

            Assert.AreEqual(1, component.value);
            Assert.AreEqual(1, editor.serializedObject.FindProperty(nameof(TestComponent.value)).intValue,
                "The inspector's SerializedObject must reflect changes made by the invoked method.");
            UnityEngine.Object.DestroyImmediate(editor);
        }

        [Test]
        public void TypeReadOnlyDoesNotDisableMethodButton()
        {
            var readOnlyObject = new GameObject("Read-only button test");
            var readOnlyComponent = readOnlyObject.AddComponent<ReadOnlyButtonComponent>();
            var editor = UnityEditor.Editor.CreateEditor(readOnlyComponent);
            var root = editor.CreateInspectorGUI();
            var button = root.Q("member-Increment").Q<Button>(className: "flexus-button--primary");

            Assert.NotNull(button);
            Assert.True(button.enabledInHierarchy);
            var invoke = button.clickable.GetType().GetMethod("Invoke",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(invoke);
            invoke.Invoke(button.clickable, new object[] { null });
            Assert.AreEqual(1, readOnlyComponent.value);

            UnityEngine.Object.DestroyImmediate(editor);
            UnityEngine.Object.DestroyImmediate(readOnlyObject);
        }

        [Test]
        public void CollectionFooterRemovesSelectedRowsOrFallsBackToLastRows()
        {
            var editor = UnityEditor.Editor.CreateEditor(component);
            var root = editor.CreateInspectorGUI();

            var list = root.Q("member-actions").Q(className: "flexus-list");
            SetPrivate(list, "selectedIndex", 0);
            InvokePrivate(list, "ClearSelection");
            Assert.AreEqual(-1, GetPrivate<int>(list, "selectedIndex"));
            Assert.True(GetPrivate<Button>(list, "removeSelectedButton").enabledSelf);
            SetPrivate(list, "selectedIndex", 0);
            InvokePrivate(list, "RemoveSelectedItem");
            Assert.AreEqual(1, component.actions.Count);
            Assert.AreEqual("Second", component.actions[0].label);
            InvokePrivate(list, "ClearSelection");
            InvokePrivate(list, "RemoveSelectedItem");
            Assert.AreEqual(0, component.actions.Count,
                "Without a selection the list footer must remove the last item.");

            var table = root.Q("member-table").Q(className: "flexus-table");
            SetPrivate(table, "selectedIndex", 0);
            InvokePrivate(table, "RemoveSelected");
            Assert.AreEqual(1, component.table.Count);
            Assert.AreEqual("Second row", component.table[0].name);
            InvokePrivate(table, "ClearSelection");
            InvokePrivate(table, "RemoveSelected");
            Assert.AreEqual(0, component.table.Count);

            var dictionary = root.Q("member-lookup").Q(className: "flexus-dictionary");
            SetPrivate(dictionary, "selectedKey", "one");
            SetPrivate(dictionary, "hasSelection", true);
            InvokePrivate(dictionary, "RemoveSelected");
            Assert.False(component.lookup.ContainsKey("one"));
            Assert.True(component.lookup.ContainsKey("two"));
            InvokePrivate(dictionary, "ClearSelection");
            InvokePrivate(dictionary, "RemoveSelected");
            Assert.AreEqual(0, component.lookup.Count);

            UnityEngine.Object.DestroyImmediate(editor);
        }

        [Test]
        public void HorizontalGroupSizesNestedGroupsAndNormalizesInvalidWeights()
        {
            var descriptor = new InspectorGroupAttribute("row", InspectorGroupStyle.Horizontal)
            {
                Sizes = new[] { 2f, float.NaN, 0f },
            };
            var host = new GroupHost(descriptor);
            var first = new VisualElement();
            var second = new VisualElement();
            var third = new VisualElement();

            host.AddGroup(first);
            host.AddGroup(second);
            host.AddGroup(third);

            Assert.AreEqual(2f, first.style.flexGrow.value);
            Assert.AreEqual(1f, second.style.flexGrow.value,
                "Invalid horizontal weights must fall back to one.");
            Assert.AreEqual(1f, third.style.flexGrow.value,
                "Non-positive horizontal weights must fall back to one.");
            Assert.True(host.Root.Children().All(child =>
                child.ClassListContains("flexus-horizontal-group__item")));
            Assert.True(host.Root.Children().All(child => child.style.flexShrink.value > 0));
        }

        [Test]
        public void FieldColumnWidthUsesLongestLabelLimitsAndPreservesInputSpace()
        {
            Assert.AreEqual(48f, FieldColumnLayoutController.CalculateColumnWidth(20f, 300f),
                "Short labels must keep the minimum label column.");
            Assert.AreEqual(135f, FieldColumnLayoutController.CalculateColumnWidth(200f, 300f),
                "A long label must be capped at 45% of its row.");
            Assert.AreEqual(64f, FieldColumnLayoutController.CalculateColumnWidth(200f, 150f),
                "A narrow row must preserve the 80 px input and 6 px gap before preserving the label minimum.");

            var label = new Label("X") { style = { width = 240 } };
            Assert.Less(FieldColumnLayoutController.MeasureNaturalWidth(label), 40f,
                "Natural text measurement must not feed the current layout width back into alignment.");
        }

        [Test]
        public void NativeFieldNormalizationOwnsInputLayoutAndKeepsToggleCompact()
        {
            var root = new VisualElement();
            var number = new IntegerField("Amount");
            var toggle = new Toggle("Enabled");
            number.AddToClassList("unity-base-field__aligned");
            toggle.AddToClassList("unity-base-field__aligned");
            root.Add(number);
            root.Add(toggle);

            FieldColumnLayoutController.NormalizeNativeFields(root);

            Assert.False(number.ClassListContains("unity-base-field__aligned"));
            Assert.False(toggle.ClassListContains("unity-base-field__aligned"));
            Assert.True(number.ClassListContains("flexus-field-layout"));
            Assert.True(toggle.ClassListContains("flexus-field-layout--toggle"));

            var numberInput = DirectInput(number);
            var toggleInput = DirectInput(toggle);
            Assert.NotNull(numberInput);
            Assert.NotNull(toggleInput);
            Assert.AreEqual(1f, numberInput.style.flexGrow.value);
            Assert.AreEqual(0f, numberInput.style.flexBasis.value.value);
            Assert.AreEqual(0f, toggleInput.style.flexGrow.value,
                "A bool input must begin in the shared input column without stretching its checkbox.");
            Assert.AreEqual(0f, toggleInput.style.marginLeft.value.value);
        }

        [Test]
        public void FieldColumnContractIsRestoredAfterLateUnityStyleMutation()
        {
            var label = new Label("Amount");
            FieldColumnLayoutController.ApplyColumnWidth(label, 64f);

            // Reproduce a late native aligned-field update, which can occur on first hover or resize.
            label.style.width = 180f;
            label.style.minWidth = 180f;
            label.style.flexBasis = 180f;
            FieldColumnLayoutController.ApplyColumnWidth(label, 64f);

            Assert.AreEqual(64f, label.style.width.value.value);
            Assert.AreEqual(64f, label.style.minWidth.value.value);
            Assert.AreEqual(64f, label.style.maxWidth.value.value);
            Assert.AreEqual(64f, label.style.flexBasis.value.value);
            Assert.AreEqual(0f, label.style.flexGrow.value);
            Assert.AreEqual(0f, label.style.flexShrink.value);
        }

        [Test]
        public void CollectionClipboardRoundTripsManagedReferenceLists()
        {
            var previousClipboard = EditorGUIUtility.systemCopyBuffer;
            try
            {
                var serializedObject = new SerializedObject(component);
                var actions = serializedObject.FindProperty(nameof(TestComponent.actions));
                CollectionClipboard.CopyCollection(actions, typeof(List<TestAction>));
                Assert.True(CollectionClipboard.HasContent);
                Assert.True(CollectionClipboard.CanPasteCollection(typeof(List<TestAction>)));

                actions.ClearArray();
                serializedObject.ApplyModifiedProperties();
                Assert.AreEqual(0, component.actions.Count);

                serializedObject.Update();
                actions = serializedObject.FindProperty(nameof(TestComponent.actions));
                Assert.True(CollectionClipboard.TryPasteCollection(actions, typeof(List<TestAction>)));
                Assert.AreEqual(2, component.actions.Count);
                Assert.AreEqual("First", component.actions[0].label);
                Assert.AreEqual("Second", component.actions[1].label);

                serializedObject.Update();
                CollectionClipboard.CopyElement(actions.GetArrayElementAtIndex(0), typeof(TestAction));
                Assert.False(CollectionClipboard.CanPasteCollection(typeof(List<TestAction>)),
                    "An element payload must not be accepted as a whole collection.");
                Assert.True(CollectionClipboard.CanPasteElement(typeof(TestAction)));
                Assert.True(CollectionClipboard.TryPasteElement(actions.GetArrayElementAtIndex(1),
                    typeof(TestAction)));
                Assert.AreEqual("First", component.actions[1].label,
                    "Paste Element must replace the destination row value.");

                component.lookup.OnBeforeSerialize();
                serializedObject.Update();
                var lookup = serializedObject.FindProperty(nameof(TestComponent.lookup));
                CollectionClipboard.CopyCollection(lookup, typeof(SerializableDictionary<string, int>));
                component.lookup.Clear();
                component.lookup.OnBeforeSerialize();
                serializedObject.Update();
                lookup = serializedObject.FindProperty(nameof(TestComponent.lookup));
                Assert.True(CollectionClipboard.TryPasteCollection(lookup,
                    typeof(SerializableDictionary<string, int>)));
                component.lookup.OnAfterDeserialize();
                Assert.AreEqual(2, component.lookup.Count);
                Assert.AreEqual(1, component.lookup["one"]);
                Assert.AreEqual(2, component.lookup["two"]);

                component.lookup.OnBeforeSerialize();
                serializedObject.Update();
                lookup = serializedObject.FindProperty(nameof(TestComponent.lookup));
                CollectionClipboard.CopyDictionaryElement(lookup, typeof(int), 0, "one: 1");
                Assert.True(CollectionClipboard.CanPasteDictionaryElement(typeof(int)));
                Assert.True(CollectionClipboard.TryReadDictionaryElement(typeof(int), out var copiedValue));
                Assert.AreEqual(1, copiedValue);
            }
            finally
            {
                EditorGUIUtility.systemCopyBuffer = previousClipboard;
            }
        }

        private static void SetPrivate(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
        }

        private static T GetPrivate<T>(object target, string field)
        {
            return (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
        }

        private static void AssertFieldLabel(VisualElement root, string memberName, string expected)
        {
            var member = root.Q($"member-{memberName}");
            Assert.NotNull(member, $"Member {memberName} was not created.");
            var actual = member.Q<PropertyField>()?.label ?? member.Q<TextField>()?.label;
            Assert.AreEqual(expected, actual, $"Unexpected label for {memberName}.");
        }

        private static VisualElement DirectInput(VisualElement field) => field.Children()
            .FirstOrDefault(child => child.ClassListContains("unity-base-field__input"));

        [Test]
        public void SerializableDictionaryRestoresSerializedPairs()
        {
            var dictionary = new SerializableDictionary<string, int> { { "one", 1 }, { "two", 2 } };
            dictionary.OnBeforeSerialize();
            dictionary.Clear();
            dictionary.OnAfterDeserialize();
            Assert.AreEqual(2, dictionary.Count);
            Assert.AreEqual(1, dictionary["one"]);
        }

        [InspectorGroup("main", InspectorGroupStyle.Box)]
        [InspectorGroup("row", InspectorGroupStyle.Horizontal, Sizes = new[] { 1f, 1f })]
        public sealed class TestComponent : MonoBehaviour
        {
            [Group("main"), Slider(0, 10)] public int value;
            [Group("row")] public int strength;
            [Group("row")] public int agility;
            [ListDrawerSettings] public List<Vector3> points = new List<Vector3>();
            [TableList] public List<TestRow> table = new List<TestRow>
            {
                new TestRow { name = "First row", amount = 1 },
                new TestRow { name = "Second row", amount = 2 },
            };
            public SerializableDictionary<string, int> lookup = new SerializableDictionary<string, int>
            {
                { "one", 1 },
                { "two", 2 },
            };
            [SerializeReference] public TestAction action = new TestAction { label = "Primary" };
            [SerializeReference, InlineProperty, HideLabel] public TestAction inlineAction;
            [SerializeReference, ListDrawerSettings] public List<TestAction> actions = new List<TestAction>
            {
                new TestAction { label = "First" },
                new TestAction { label = "Second" },
            };
            [SerializeField] private int _int;
            [SerializeField] private float _float;
            [SerializeField] private string _string;
            [SerializeField, TextArea] private string _text;
            [SerializeField, LabelText("Test")] private string _testText;
            [SerializeField, HideInInspector] private bool _hidden;
            [ShowInInspector, ReadOnly] public int DoubleValue => value * 2;
            [TypeConstraint(typeof(MonoBehaviour))] public SerializableType componentType = new SerializableType();
            [Button] private void Increment() => value++;
            [Button] private void ApplyValues(int amount = 1, string message = "Ready") { }
        }

        [Flexus.Inspector.ReadOnly]
        public sealed class ReadOnlyButtonComponent : MonoBehaviour
        {
            public int value;
            [Button] private void Increment() => value++;
        }

        [Serializable]
        public sealed class TestRow
        {
            public string name;
            public int amount;
        }

        [Serializable]
        public sealed class TestAction
        {
            public string label;
            public int amount;
            public List<int> parameters = new List<int> { 1, 2 };
        }
    }
}
