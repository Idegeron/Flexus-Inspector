# Architecture

FLEXUS Inspector has four small layers:

1. `InspectorMetadataCache` converts reflected members to immutable `MemberDescriptor` objects.
2. `UIInspectorBuilder` creates group containers and one `MemberElement` per descriptor.
3. `InspectorValueAccessor` selects a custom backend, a `SerializedProperty`, or reflection.
4. `IInspectorExtension` implementations modify stable UI Toolkit slots in deterministic stages.
5. Optional integrations contribute `IInspectorMemberInclusionPolicy` and `IInspectorChangeHandler`
   implementations without adding serializer dependencies to the core assembly.

## Build flow

```text
Target type
  -> cached TypeDescriptor
  -> group tree
  -> MemberContext
  -> default PropertyField/BaseField
  -> extension stages
  -> Bind(SerializedObject)
```

No extension wraps or recursively calls another extension. A content extension can replace `MemberElement.Content`;
a decorator adds UI to `Before` or `After`; a validator owns `Validation`.

## Reactivity

Serialized callbacks use `TrackPropertyValue`. Sources that Unity does not expose as a `SerializedProperty` are
checked by a scheduled task attached to the member element. UI Toolkit automatically pauses scheduled work when the
element detaches from a panel.

## Compatibility

The default serialized renderer is `PropertyField`, so public UI Toolkit `PropertyDrawer` implementations continue to
work. `[UseUnityDrawer]` forces this route. Existing custom editors override the package fallback editor normally.

## Serialization boundary

Serialization is represented by `IInspectorValueBackend`; UI rendering never depends on a particular serializer.
Custom backends are discovered with `TypeCache`, ordered by `Priority`, and receive the same `MemberContext` exposed to
extensions.

External serializers can expose otherwise hidden reflection members through `IInspectorMemberInclusionPolicy`.
`IInspectorChangeHandler` receives both reflection-backed mutations and tracked `SerializedObject` changes, allowing
an integration to invalidate its persistence payload once per Inspector change.
