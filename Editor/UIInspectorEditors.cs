using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
    internal sealed class UIInspectorMonoBehaviourEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => UIInspectorBuilder.Build(serializedObject, targets);
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    internal sealed class UIInspectorScriptableObjectEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => UIInspectorBuilder.Build(serializedObject, targets);
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptedImporter), true, isFallback = true)]
    internal sealed class UIInspectorScriptedImporterEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = UIInspectorBuilder.Build(serializedObject, targets);
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            toolbar.AddToClassList("flexus-importer-actions");
            var revert = new Button(() =>
            {
                foreach (var item in targets)
                    if (item is AssetImporter importer)
                        AssetDatabase.ImportAsset(importer.assetPath, ImportAssetOptions.ForceUpdate);
                serializedObject.Update();
            }) { text = "Revert" };
            revert.AddToClassList("flexus-button");
            var apply = new Button(() =>
            {
                serializedObject.ApplyModifiedProperties();
                foreach (var item in targets)
                    if (item is AssetImporter importer)
                    {
                        AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
                        AssetDatabase.ImportAsset(importer.assetPath, ImportAssetOptions.ForceUpdate);
                    }
            }) { text = "Apply" };
            apply.AddToClassList("flexus-button");
            apply.style.flexGrow = 1;
            revert.style.flexGrow = 1;
            toolbar.Add(revert);
            toolbar.Add(apply);
            root.Add(toolbar);
            return root;
        }
    }

    public abstract class UIInspectorEditor<T> : UnityEditor.Editor where T : UnityEngine.Object
    {
        public sealed override VisualElement CreateInspectorGUI()
        {
            var root = UIInspectorBuilder.Build(serializedObject, targets);
            BuildBeforeProperties(root);
            BuildAfterProperties(root);
            return root;
        }

        protected virtual void BuildBeforeProperties(VisualElement root) { }
        protected virtual void BuildAfterProperties(VisualElement root) { }
    }
}
