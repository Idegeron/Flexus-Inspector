using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    internal sealed class PreviewExtension : IInspectorExtension
    {
        public InspectorStage Stage => InspectorStage.Decorate;
        public int Order => 100;

        public bool CanApply(MemberContext context) =>
            !context.Descriptor.HasAttribute<UseUnityDrawerAttribute>() &&
            (context.Descriptor.HasAttribute<InlineEditorAttribute>() ||
             context.Descriptor.HasAttribute<PreviewObjectAttribute>() ||
             context.Descriptor.HasAttribute<PreviewMeshAttribute>());

        public void Apply(MemberElement element, MemberContext context)
        {
            var inline = context.Descriptor.GetAttribute<InlineEditorAttribute>();
            if (inline != null) element.AddAfter(new InlineEditorElement(context, inline));

            var objectPreview = context.Descriptor.GetAttribute<PreviewObjectAttribute>();
            if (objectPreview != null)
            {
                if (!objectPreview.DrawField) element.Content.style.display = DisplayStyle.None;
                element.AddAfter(new ObjectPreviewElement(context, objectPreview.Height));
            }

            var meshPreview = context.Descriptor.GetAttribute<PreviewMeshAttribute>();
            if (meshPreview != null)
            {
                var preview = new MeshPreviewElement(context, meshPreview.Height, meshPreview.Rotation);
                if (meshPreview.Foldout)
                {
                    var foldout = new Foldout
                    {
                        text = "Mesh Preview",
                        value = true,
                        viewDataKey = $"mesh-preview-{context.Descriptor.Name}",
                    };
                    foldout.AddToClassList("flexus-preview-card");
                    foldout.Add(preview);
                    element.AddAfter(foldout);
                }
                else element.AddAfter(preview);
            }
        }
    }

    internal sealed class InlineEditorElement : VisualElement
    {
        private UnityEditor.Editor nestedEditor;
        private readonly MemberContext context;
        private readonly InlineEditorAttribute settings;
        private UnityEngine.Object current;

        public InlineEditorElement(MemberContext context, InlineEditorAttribute settings)
        {
            this.context = context;
            this.settings = settings;
            AddToClassList("flexus-ui-inspector__box");
            AddToClassList("flexus-inline-editor");
            RegisterCallback<DetachFromPanelEvent>(_ => DisposeEditor());
            Refresh();
            schedule.Execute(CheckTarget).Every(300);
        }

        private void CheckTarget()
        {
            var value = context.Value.GetValue() as UnityEngine.Object;
            if (value != current) Refresh();
        }

        private void Refresh()
        {
            Clear();
            DisposeEditor();
            current = context.Value.GetValue() as UnityEngine.Object;
            if (!current) return;

            nestedEditor = UnityEditor.Editor.CreateEditor(current);
            if (settings.Mode is InlineEditorMode.InspectorAndHeader or InlineEditorMode.Full)
                Add(new Label(current.name) { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            var gui = nestedEditor.CreateInspectorGUI();
            if (gui != null) Add(gui);
            else Add(new HelpBox(
                $"{nestedEditor.GetType().Name} does not provide CreateInspectorGUI(). IMGUI fallback is intentionally disabled.",
                HelpBoxMessageType.Info));

            if (settings.Mode is InlineEditorMode.InspectorAndPreview or InlineEditorMode.Full)
                Add(new ObjectPreviewElement(context, settings.PreviewHeight));
        }

        private void DisposeEditor()
        {
            if (nestedEditor) UnityEngine.Object.DestroyImmediate(nestedEditor);
            nestedEditor = null;
        }
    }

    internal sealed class ObjectPreviewElement : Image
    {
        private readonly MemberContext context;
        private UnityEngine.Object current;

        public ObjectPreviewElement(MemberContext context, float height)
        {
            this.context = context;
            scaleMode = ScaleMode.ScaleToFit;
            AddToClassList("flexus-object-preview");
            style.height = Mathf.Max(32, height);
            style.marginTop = 3;
            Refresh();
            schedule.Execute(Refresh).Every(250);
        }

        private void Refresh()
        {
            var value = context.Value.GetValue() as UnityEngine.Object;
            if (value == current && image) return;
            current = value;
            image = value ? AssetPreview.GetAssetPreview(value) ?? AssetPreview.GetMiniThumbnail(value) : null;
        }
    }

    internal sealed class MeshPreviewElement : Image
    {
        private readonly MemberContext context;
        private readonly MeshPreviewRotation rotationMode;
        private PreviewRenderUtility preview;
        private Mesh mesh;
        private Material material;
        private Vector2 rotation = new Vector2(20, -25);
        private float zoom = 1f;
        private Vector2 lastPointer;

        public MeshPreviewElement(MemberContext context, float height, MeshPreviewRotation rotationMode)
        {
            this.context = context;
            this.rotationMode = rotationMode;
            style.height = Mathf.Max(64, height);
            AddToClassList("flexus-mesh-preview");
            scaleMode = ScaleMode.StretchToFill;
            preview = new PreviewRenderUtility();
            preview.camera.fieldOfView = 30f;
            RegisterCallback<GeometryChangedEvent>(_ => Render());
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(evt => this.ReleasePointer(evt.pointerId));
            RegisterCallback<WheelEvent>(evt =>
            {
                zoom = Mathf.Clamp(zoom + evt.delta.y * 0.03f, 0.25f, 4f);
                Render();
                evt.StopPropagation();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => Cleanup());
            schedule.Execute(CheckMesh).Every(350);
            CheckMesh();
        }

        private void CheckMesh()
        {
            var value = context.Value.GetValue();
            Mesh nextMesh = null;
            Material nextMaterial = null;
            if (value is Mesh directMesh) nextMesh = directMesh;
            else if (value is GameObject gameObject)
            {
                var filter = gameObject.GetComponentInChildren<MeshFilter>();
                var renderer = gameObject.GetComponentInChildren<MeshRenderer>();
                nextMesh = filter ? filter.sharedMesh : null;
                nextMaterial = renderer ? renderer.sharedMaterial : null;
            }
            else if (value is Component component)
            {
                var filter = component.GetComponentInChildren<MeshFilter>();
                var renderer = component.GetComponentInChildren<MeshRenderer>();
                nextMesh = filter ? filter.sharedMesh : null;
                nextMaterial = renderer ? renderer.sharedMaterial : null;
            }
            if (mesh == nextMesh && material == nextMaterial) return;
            mesh = nextMesh;
            material = nextMaterial;
            Render();
        }

        private void Render()
        {
            if (preview == null || !mesh || contentRect.width < 2 || contentRect.height < 2) return;
            var drawMaterial = material ? material : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            if (!drawMaterial) return;

            var bounds = mesh.bounds;
            var size = Mathf.Max(0.01f, bounds.extents.magnitude) * zoom;
            preview.camera.transform.position = new Vector3(0, 0, -size * 3.5f);
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.nearClipPlane = size * 0.01f;
            preview.camera.farClipPlane = size * 10f;
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
            preview.ambientColor = new Color(0.35f, 0.35f, 0.35f);

            var rect = new Rect(0, 0, contentRect.width, contentRect.height);
            preview.BeginPreview(rect, GUIStyle.none);
            var matrix = Matrix4x4.TRS(-bounds.center,
                Quaternion.Euler(rotation.y, rotation.x, 0), Vector3.one);
            preview.DrawMesh(mesh, matrix, drawMaterial, 0);
            preview.camera.Render();
            image = preview.EndPreview();
            MarkDirtyRepaint();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            lastPointer = evt.position;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId)) return;
            var delta = (Vector2)evt.position - lastPointer;
            lastPointer = evt.position;
            rotation.x += delta.x;
            rotation.y -= delta.y;
            if (rotationMode == MeshPreviewRotation.Clamped)
                rotation.y = Mathf.Clamp(rotation.y, -89, 89);
            Render();
            evt.StopPropagation();
        }

        private void Cleanup()
        {
            preview?.Cleanup();
            preview = null;
        }
    }
}
