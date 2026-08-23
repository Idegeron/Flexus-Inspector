using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Flexus.Inspector
{
    public enum InspectorMessageType
    {
        None,
        Info,
        Warning,
        Error,
    }

    public enum InspectorGroupStyle
    {
        Vertical,
        Horizontal,
        Box,
        Foldout,
        Toggle,
        Tabs,
    }

    public enum InlineEditorMode
    {
        Inspector,
        InspectorAndHeader,
        InspectorAndPreview,
        Full,
    }

    public enum MeshPreviewRotation
    {
        Clamped,
        Free,
    }

    public enum InspectorButtonSize
    {
        Small = 18,
        Medium = 24,
        Large = 32,
    }

    public readonly struct InspectorValidationResult
    {
        public static readonly InspectorValidationResult Valid = new InspectorValidationResult(true, null);

        public bool IsValid { get; }
        public string Message { get; }
        public InspectorMessageType MessageType { get; }

        private InspectorValidationResult(bool valid, string message,
            InspectorMessageType messageType = InspectorMessageType.None)
        {
            IsValid = valid;
            Message = message;
            MessageType = messageType;
        }

        public static InspectorValidationResult Info(string message) =>
            new InspectorValidationResult(false, message, InspectorMessageType.Info);

        public static InspectorValidationResult Warning(string message) =>
            new InspectorValidationResult(false, message, InspectorMessageType.Warning);

        public static InspectorValidationResult Error(string message) =>
            new InspectorValidationResult(false, message, InspectorMessageType.Error);
    }

    public interface IInspectorDropdownItem
    {
        string Text { get; }
        object UntypedValue { get; }
    }

    public readonly struct InspectorDropdownItem<T> : IInspectorDropdownItem
    {
        public string Text { get; }
        public T Value { get; }
        public object UntypedValue => Value;

        public InspectorDropdownItem(string text, T value)
        {
            Text = text;
            Value = value;
        }
    }

    public sealed class InspectorDropdownList<T> : List<InspectorDropdownItem<T>>
    {
        public void Add(string text, T value) => Add(new InspectorDropdownItem<T>(text, value));
    }

    [Serializable]
    public sealed class SerializableType : ISerializationCallbackReceiver
    {
        [SerializeField] private string assemblyQualifiedName;
        [NonSerialized] private Type value;

        public Type Value
        {
            get
            {
                if (value == null && !string.IsNullOrEmpty(assemblyQualifiedName))
                    value = Type.GetType(assemblyQualifiedName);
                return value;
            }
            set
            {
                this.value = value;
                assemblyQualifiedName = value?.AssemblyQualifiedName;
            }
        }

        public void OnBeforeSerialize() => assemblyQualifiedName = value?.AssemblyQualifiedName ?? assemblyQualifiedName;
        public void OnAfterDeserialize() => value = string.IsNullOrEmpty(assemblyQualifiedName)
            ? null
            : Type.GetType(assemblyQualifiedName);

        public override string ToString() => Value?.FullName ?? "None";
        public static implicit operator Type(SerializableType type) => type?.Value;
    }

    [Serializable]
    public class SerializableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new List<TKey>();
        [SerializeField] private List<TValue> values = new List<TValue>();
        [NonSerialized] private Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

        public TValue this[TKey key] { get => dictionary[key]; set => dictionary[key] = value; }
        public ICollection<TKey> Keys => dictionary.Keys;
        public ICollection<TValue> Values => dictionary.Values;
        public int Count => dictionary.Count;
        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value) => dictionary.Add(key, value);
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);
        public bool Remove(TKey key) => dictionary.Remove(key);
        public bool TryGetValue(TKey key, out TValue value) => dictionary.TryGetValue(key, out value);
        public void Clear() => dictionary.Clear();
        public bool Contains(KeyValuePair<TKey, TValue> item) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Contains(item);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<TKey, TValue> item) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(item);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => dictionary.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var pair in dictionary)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            dictionary = new Dictionary<TKey, TValue>();
            var count = Math.Min(keys.Count, values.Count);
            for (var index = 0; index < count; index++)
            {
                if (!dictionary.ContainsKey(keys[index]))
                    dictionary.Add(keys[index], values[index]);
            }
        }
    }
}
