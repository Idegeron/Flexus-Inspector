# Changelog

## Unreleased

- Added an optional `com.flexus.serialization` Editor integration that exposes private
  `[SerializationIncluded]` fields and marks every edited `ISerializable` target dirty.
- Added serializer-neutral member-inclusion and Inspector-change extension points.
- Added editable reflection-backed lists and arrays, including pagination, polymorphic type selection,
  element mutation, add/remove, and reordering support.
- Added focused integration coverage for field discovery, persistence invalidation, reflection lists,
  and the field-only persistence contract.

## 0.4.0 - 2026-08-23

- Disabled Unity's competing `unity-base-field__aligned` geometry inside Flexus inspectors and made the Flexus
  controller own both sides of every field row. Late native style updates can no longer move an input on hover.
- Normalized bool fields separately: the checkbox stays compact while its left edge follows the same calculated
  input column as number, text, object, and composite fields, including before and after Inspector resize.
- Rebuilt field alignment as a transactional longest-label pass. Each consecutive field sequence shares the natural
  width of its longest label, with a 48 px label minimum, 80 px protected input, 6 px gap, and 45% row cap.
- Kept the Inspector hidden during its short initial layout-settling window, then applied every label width in one
  batch before revealing it. Lazy `PropertyField` binding no longer exposes intermediate columns.
- Limited normal recalculation to Inspector resize, dynamic-label changes, tab switches, and actual collection or
  managed-reference rebuilds. Hover and focus pseudo-states do not control sizing; an unexpected late native
  geometry mutation is detected directly and the Flexus column contract is restored.
- Gave every horizontal cell an independent alignment scope. Nested vertical sequences align internally without
  forcing labels in neighboring cells to the same width; composite `X/Y/Z` labels retain their compact override.
- Unified horizontal child insertion so direct members and nested groups receive the same `flex-basis`, grow, shrink,
  and minimum-width constraints.
- Defined `InspectorGroupAttribute.Sizes` as per-child flex weights. Missing, non-finite, zero, and negative values
  fall back to `1`.
- Expanded horizontal-group regression coverage to include nested group roots and invalid weights.
- Restored managed-reference collection paste on Unity 6000.3 by copying compatible serialized values recursively
  instead of using the cross-owner `CopyFromSerializedProperty` fast path.

## 0.3.8 - 2026-08-23

- Fixed the remaining first-hover width jump: descendant `AttachToPanelEvent` notifications from lazy numeric-field
  draggers no longer trigger field realignment.
- Replaced layout-dependent UI Toolkit label measurement with stable Unity Editor glyph measurement.
- Added explicit alignment invalidation for actual collection, managed-reference, and tab content rebuilds.
- Added compatible `Paste Element` actions to list, table-list, and dictionary-row context menus with Undo support.
- Added managed-reference element and dictionary-entry clipboard round-trip coverage.

## 0.3.7 - 2026-08-23

- Replaced the 350 ms field-alignment polling loop with event-driven attach and geometry refreshes, preventing hover
  or focus from changing an input's width.
- Changed collection footer removal to delete the selected element, or the last element when nothing is selected.
- Added right-click collection menus with `Copy Collection`, compatible `Paste Collection`, and `Clear Collection`.
- Added right-click `Copy Element` actions for list, table-list, and dictionary rows.
- Added versioned clipboard payloads and managed-reference/dictionary-safe serialized collection restoration.

## 0.3.6 - 2026-08-23

- Limited shared longest-label alignment to vertical field sequences. Horizontal groups and composite `X/Y/Z`
  sub-fields now retain independent compact, content-sized labels.
- Removed the field-width feedback term from label calculations; the only alignment cap is now 30% of the Inspector.
- Added per-label width caching so dynamic-label checks do not rewrite identical inline styles or invalidate layout.
- Kept resize, dynamic-label, and collection-rebuild support without hover-induced input-width flicker.

## 0.3.5 - 2026-08-23

- Added a responsive field-alignment controller that measures labels after UI Toolkit layout and aligns every
  consecutive field run to its longest label.
- Scoped alignment independently for root fields, groups, tabs, horizontal groups, list item bodies,
  managed-reference bodies, dictionary add panels, method parameters, foldout contents, and composite inputs.
- Capped label columns at 30% of the Inspector width and 45% of the narrowest field row, preserving usable input space
  in horizontal layouts.
- Separated `X/Y/Z` and other composite sub-labels from their outer field label so vector inputs remain compact.
- Added automatic recalculation for Inspector resize, dynamic labels, foldout changes, and rebuilt collection rows.

## 0.3.4 - 2026-08-23

- Added an isolated percentage grid to polymorphic list headers. After the fixed drag handle and index, semantic name,
  type picker, and remove action use `42% / 52% / 6%` of the available width.
- Added independent ellipsis clipping to both the semantic title and picker value so long class names cannot overlap
  or displace the remove action.
- Added a `44% / 56%` layout variant when per-row removal is hidden.

## 0.3.3 - 2026-08-23

- Replaced fixed field-label columns with content-sized labels and a small consistent gap. Inputs now consume all
  remaining width, including compact horizontal groups and composite vector fields.
- Neutralized Unity's inspector-wide `unity-base-field__aligned` offsets inside the package Inspector.
- Added background-click deselection for lists, table lists, and dictionaries; their footer remove action is disabled
  immediately after selection is cleared.
- Moved native Foldout toggle inputs inward for group and preview containers so their arrows no longer touch or cross
  the box border.
- Strengthened horizontal-group sizing so every cell and input can shrink independently without hiding later labels.

## 0.3.2 - 2026-08-23

- Added explicit single-row selection to lists, table lists, and dictionaries. The footer `−` action is disabled until
  a row is selected and removes that selected row rather than the last item.
- Added per-row remove actions to `TableList` and a consistent accent selection state across every collection type.
- Split polymorphic list headers into a semantic foldout title and a full-width managed-reference type picker.
- Fixed dictionary editing to mutate the actual inspected field rather than a boxed serialized copy.
- Reduced excessive field-label spacing, constrained horizontal-group children correctly, and restored the clipped
  foldout arrow inset.
- Centered collection footer glyphs and made picker hover/selection colors override alternating-row backgrounds.
- Rebuilt parameterized method actions as compact cards with a collapsible parameter area, default-value reset, and
  a dedicated invocation footer.
- Added regression coverage for selected-row deletion, table row actions, polymorphic header pickers, horizontal
  groups, and parameterized method controls.

## 0.3.1 - 2026-08-21

- Fixed serialized fields collapsing to their input width by keeping `PropertyField` containers stretched while
  centering only their actual `BaseField` rows.
- Restored normal labels for underscored fields and verified static `LabelText` overrides end to end.
- Added native Unity `TextAreaAttribute` support with a compact two-column UI Toolkit layout.
- Preserved managed-reference expansion state by stable managed-reference ID during collection rebuilds. Existing
  rows no longer open when an item is added, and new rows start collapsed.
- Rendered arrays nested inside managed-reference objects with the same custom list chrome instead of Unity's
  fallback `Element 0`, `Element 1` presentation.
- Aligned dictionary column headers with their inputs and optically centered pagination across the full collection
  width while keeping `+` / `−` actions in the bottom-right corner.
- Added regression tests for field labels, `LabelText`, `TextArea`, hidden members, footer order, nested collections,
  and collection expansion state.

## 0.3.0 - 2026-08-21

- Reworked the visual system into a compact, native Unity-style design with restrained borders, spacing, colors,
  tabs, group headers, validation, fields, buttons, and previews.
- Replaced collection `ListView` virtualization with a page-sized natural UI Toolkit layout. Dynamic-height rows can
  no longer overlap the footer.
- Moved list, table, and dictionary `+` / `−` controls to a dedicated bottom-right footer.
- Added lightweight pointer-based drag reordering without relying on `ListView` layout internals.
- Preserved expansion state independently per collection element. Adding an item no longer expands existing items,
  and newly added elements start collapsed.
- Aligned field labels and inputs vertically through shared minimum heights, centered alignment, and text metrics.
- Flattened nested managed-reference presentation while keeping its searchable type picker and semantic item title.
- Added regression coverage for footer order and managed-reference expansion state.

## 0.2.0 - 2026-08-21

- Introduced a complete adaptive dark/light visual system for the Inspector and popup windows.
- Redesigned groups, foldouts, tabs, fields, buttons, messages, inline editors, and previews.
- Rebuilt lists as reorderable item cards with semantic titles, index badges, duplicate/remove actions, empty states,
  count badges, and compact pagination.
- Added first-class `SerializeReference` controls inside collections. New polymorphic elements open expanded and always
  expose a searchable concrete-type picker instead of falling back to Unity's `Element N` rendering.
- Redesigned table lists and dictionaries with toolbars, alternating rows, inline actions, and dedicated add panels.
- Improved searchable pickers with type paths, descriptions, icons, count indicators, multi-term filtering, and
  keyboard handling.
- Added polymorphic-list coverage to the feature sample and Editor tests.

## 0.1.0 - 2026-08-21

- Initial independent UI Toolkit Inspector implementation.
- Added fallback editors, metadata cache, extension stages, and custom value backends.
- Added general, conditional, layout, styling, validation, button, picker, collection, and preview features.
- Added searchable polymorphic managed-reference selection.
- Added serializable dictionary and type wrappers.
- Added diagnostics, samples, tests, and documentation.
