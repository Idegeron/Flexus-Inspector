using System.Collections.Generic;
using System.Reflection;
using Flexus.Inspector.Editor;
using Flexus.Serialization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Tests.Integrations
{
    public sealed class FlexusSerializationIntegrationTests
    {
        private GameObject gameObject;
        private SerializationIntegrationComponent component;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Flexus Serialization integration tests");
            component = gameObject.AddComponent<SerializationIntegrationComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            if (gameObject) Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void IncludedPrivateFieldIsEditableAndPersisted()
        {
            var editor = UnityEditor.Editor.CreateEditor(component);
            var root = editor.CreateInspectorGUI();
            var member = root.Q("member-_customValue");

            Assert.NotNull(member);
            Assert.IsInstanceOf<MemberElement>(member);
            var memberElement = (MemberElement)member;
            Assert.IsNull(memberElement.Context.SerializedProperty);
            memberElement.Context.Value.SetValue(42);
            Assert.AreEqual(42, component.CustomValue);

            ((ISerializationCallbackReceiver)component).OnBeforeSerialize();
            StringAssert.Contains("_customValue", component.SerializationData);
            StringAssert.Contains("42", component.SerializationData);

            Object.DestroyImmediate(editor);
        }

        [Test]
        public void IncludedReflectionListSupportsMutationAndPersistence()
        {
            var editor = UnityEditor.Editor.CreateEditor(component);
            var root = editor.CreateInspectorGUI();
            var list = root.Q("member-_values")?.Q(className: "flexus-list--reflection");

            Assert.NotNull(list);
            var add = list.GetType().GetMethod("AddItem", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(add);
            add.Invoke(list, null);
            Assert.AreEqual(3, component.Values.Count);

            ((ISerializationCallbackReceiver)component).OnBeforeSerialize();
            StringAssert.Contains("_values", component.SerializationData);

            Object.DestroyImmediate(editor);
        }

        [Test]
        public void IncludedPropertyIsNotPresentedAsPersistedData()
        {
            var editor = UnityEditor.Editor.CreateEditor(component);
            var root = editor.CreateInspectorGUI();

            Assert.Null(root.Q("member-UnsupportedProperty"),
                "Flexus Serialization persists fields only, so its attribute must not imply property persistence.");

            Object.DestroyImmediate(editor);
        }
    }

    public sealed class SerializationIntegrationComponent : SerializableMonoBehaviour
    {
        [SerializationIncluded]
        private int _customValue = 7;

        [SerializationIncluded, ListDrawerSettings]
        private List<int> _values = new List<int> { 1, 2 };

        [SerializationIncluded]
        public int UnsupportedProperty { get; set; }

        public int CustomValue => _customValue;
        public IReadOnlyList<int> Values => _values;
    }
}
