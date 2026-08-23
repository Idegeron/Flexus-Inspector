# Flexus Inspector

A lightweight Unity 6 Inspector framework built entirely with UI Toolkit. It provides declarative attributes for
common editor workflows while keeping extension code identical to normal UI Toolkit editor code.

The package is independent from Tri Inspector and Odin. It uses only public Unity APIs and intentionally does not
include an IMGUI compatibility layer.

## Installation

Library distributed as git package ([How to install package from git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html))
<br>Git URL: `https://github.com/Idegeron/Flexus-Inspector.git`

Minimum supported editor: Unity 6000.0.

## Quick start

The package installs fallback inspectors for `MonoBehaviour`, `ScriptableObject`, and `ScriptedImporter`. Existing
custom editors still take precedence.

```csharp
using Flexus.Inspector;
using UnityEngine;

[InspectorGroup("stats", InspectorGroupStyle.Box, Title = "Stats")]
public sealed class Character : MonoBehaviour
{
    [Group("stats"), Slider(0, 100), Unit("%")]
    public float health = 100;

    public bool advanced;

    [ShowIf(nameof(advanced)), PropertyTextArea]
    public string notes;

    [ShowInInspector, ReadOnly]
    public string Summary => $"Health: {health:0}";

    [Button]
    private void Restore(float amount = 100) => health = amount;
}
```

Use `[UseUnityInspector]` on a type or `[UseUnityDrawer]` on a field to opt out.

## Features

### General

- `ShowInInspector`, `ReadOnly`, `PropertyOrder`, `OnValueChanged`
- `InlineProperty`, `HideMonoScript`, `HideReferencePicker`
- native serialized-property binding, Undo, prefab overrides, and multi-object values
- editable reflection-backed fields/properties
- pluggable `IInspectorValueBackend` for custom serialization

### Conditions

- `ShowIf`, `HideIf`, `EnableIf`, `DisableIf`
- edit-mode and play-mode visibility/enabled attributes
- member sources can be fields, properties, or parameterless methods

### Validation

- `Required` with optional fix action
- `RequiredGet` component lookup
- `ValidateInput` returning `InspectorValidationResult`, `bool`, or an error string
- `InfoBox`, `AssetsOnly`, `SceneObjectsOnly`
- validation is reactive and displayed with UI Toolkit `HelpBox`

### Layout and styling

- box, foldout, toggle, tab, horizontal, and vertical groups
- nested group paths
- horizontal `Sizes` values are flex weights (`1, 1` is equal width; invalid or non-positive values fall back to `1`)
- titles, labels, label width, tooltip, spacing, indent, colors, unit suffixes, custom USS classes
- toggle groups bind their first boolean member as the group switch

### Fields and pickers

- fixed and member-driven sliders and min/max sliders
- searchable dropdown values
- searchable project assets, scenes, layers, Animator parameters, and shader properties
- `System.Type`/`SerializableType` selector with `TypeConstraint` and `TypeName`
- polymorphic `SerializeReference` selector
- enum/flags toggle buttons and multiline text fields

### Collections

- compact, reorderable lists with semantic titles, empty states, and pagination
- polymorphic list elements with a searchable `SerializeReference` type selector
- table lists with quiet columns, alternating rows, row selection, per-row removal, and pagination
- editable dictionaries with row selection and a dedicated add-entry panel
- `SerializableDictionary<TKey,TValue>` for Unity-persisted dictionary data

Collection pages use a natural UI Toolkit layout rather than a nested virtualized scroll view. Their footer is always
laid out after the last element, with `+` and `−` actions in the bottom-right corner. Click or edit a row to select it;
click the empty collection background to clear that selection. The footer `−` action removes the selected row, or the
last row when nothing is selected. Right-click a collection for copy, compatible paste, and clear actions; right-click
a row to copy or paste a compatible element. Dictionary element paste keeps the destination key and replaces its value.
Expansion state is stored per element, so adding a new item never changes existing foldouts.

List item titles are resolved from `name`, `title`, `label`, `id`, or `key` when available. Otherwise the Inspector
uses the concrete type name. The array index is displayed separately, so complex collections do
not fall back to `Element 0`, `Element 1`, and similar labels. Polymorphic rows place their semantic title in the left
foldout half and the concrete-type picker in the right half.

### Actions and previews

- method buttons with editable/default parameters, confirmation, multi-target invocation, and Undo
- inline buttons beside fields
- inline UI Toolkit inspectors
- asset previews and interactive mesh previews without `IMGUIContainer`

### Tooling

- `Tools/Flexus UI Inspector/Diagnostics`
- `[ShowInspectorDiagnostics]` per-member diagnostics
- Package Manager feature sample
- Editor tests

## Extending the Inspector

Create an attribute and one extension. Extensions operate on normal UI Toolkit elements:

```csharp
using System;
using Flexus.Inspector.Editor;
using UnityEngine.UIElements;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class CharacterCountAttribute : Attribute { }

public sealed class CharacterCountExtension
    : InspectorAttributeExtension<CharacterCountAttribute>
{
    protected override void Apply(
        MemberElement element,
        CharacterCountAttribute attribute,
        MemberContext context)
    {
        var label = new Label();
        element.AddAfter(label);
        element.schedule.Execute(() =>
        {
            var text = context.Value.GetValue()?.ToString() ?? string.Empty;
            label.text = $"Characters: {text.Length}";
        }).Every(200);
    }
}
```

No registration attributes are needed. Unity `TypeCache` discovers the extension.

Extension stages are deterministic:

1. `Visibility`
2. `Enablement`
3. `Content`
4. `Decorate`
5. `Validate`

There is no recursive drawer chain. `MemberElement` exposes four stable slots: `Before`, `Content`, `After`, and
`Validation`.

## Custom value backends

Implement `IInspectorValueBackend` when a member is stored by a serializer other than Unity:

```csharp
public sealed class MyValueBackend : IInspectorValueBackend
{
    public int Priority => 500;
    public bool CanHandle(MemberContext context) => /* recognize your member */ false;
    public bool IsReadOnly(MemberContext context) => false;
    public object GetValue(MemberContext context, int targetIndex) => null;
    public void SetValue(MemberContext context, object value, string undoName) { }
    public bool HasMixedValues(MemberContext context) => false;
}
```

The highest-priority matching backend wins. Without a custom backend, the Inspector uses `SerializedProperty` when
available and reflection otherwise.

## Flexus Serialization integration

When `com.flexus.serialization` 1.0.0 or newer is installed alongside this package, the optional Editor integration
is enabled automatically. Private fields marked with `SerializationIncluded` are displayed as reflection-backed
members, and every Inspector mutation calls `ISerializable.SetDirty(true)` for all edited targets. Reflection-backed
lists and arrays support value editing, adding, removing, and reordering.

Install both Git packages in the consuming project's `Packages/manifest.json`; neither package needs to embed or
modify the other. Flexus Serialization currently persists fields only, so `SerializationIncluded` properties are not
treated as persistent Inspector members.

## Serialization notes

The Inspector does not replace Unity serialization. Standard fields, lists, arrays, and `SerializeReference` use
`SerializedProperty`. Properties and `[ShowInInspector]` reflection values are editor views unless another serializer
persists them. Use `SerializableDictionary<TKey,TValue>` or a custom backend for dictionary persistence.
