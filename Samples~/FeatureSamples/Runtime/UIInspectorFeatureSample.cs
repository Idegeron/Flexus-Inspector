using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flexus.Inspector.Samples
{
    [CreateAssetMenu(menuName = "Flexus/UI Inspector Feature Sample")]
    [InspectorGroup("stats", InspectorGroupStyle.Box, Title = "Character Stats")]
    [InspectorGroup("advanced", InspectorGroupStyle.Foldout, Title = "Advanced", Expanded = true)]
    [InspectorGroup("tabs", InspectorGroupStyle.Tabs, Title = "Data")]
    [InspectorGroup("row", InspectorGroupStyle.Horizontal, Sizes = new[] { 1f, 1f })]
    public sealed class UIInspectorFeatureSample : ScriptableObject
    {
        [Group("stats"), Slider(0, 100), Unit("%"), OnValueChanged(nameof(OnHealthChanged))]
        public float health = 75;

        [Group("stats"), Slider(nameof(MinSpeed), nameof(MaxSpeed)), Unit("m/s")]
        public float speed = 5;

        [Group("row")]
        public int strength = 10;

        [Group("row")]
        public int agility = 12;

        [Group("advanced")]
        public bool showAdvanced;

        [Group("advanced"), ShowIf(nameof(showAdvanced)), PropertyTextArea(3, 8), Title("Designer Notes")]
        public string notes;

        [Group("tabs"), Tab("Pickers"), Dropdown(nameof(GetDifficulties))]
        public int difficulty = 1;

        [Group("tabs"), Tab("Pickers"), AssetDropdown("t:Material")]
        public Material material;

        [Group("tabs"), Tab("Pickers"), Scene]
        public string scene;

        [Group("tabs"), Tab("Pickers"), Layer]
        public int layer;

        [Group("tabs"), Tab("Collections"), ListDrawerSettings(ItemsPerPage = 8)]
        public List<Vector3> points = new List<Vector3>();

        [Group("tabs"), Tab("Collections"), TableList(ItemsPerPage = 8)]
        public List<SampleRow> table = new List<SampleRow>();

        [Group("tabs"), Tab("References"), SerializeReference]
        public SampleAction action;

        [Group("tabs"), Tab("References"), SerializeReference, ListDrawerSettings(ItemsPerPage = 6)]
        public List<SampleAction> actions = new List<SampleAction>
        {
            new LogSampleAction { label = "On Spawn", message = "Ready" },
            new MoveSampleAction { label = "Opening Move", offset = Vector3.forward },
        };

        [Group("tabs"), Tab("References"), PreviewObject(Height = 120)]
        public Texture2D texture;

        [Group("tabs"), Tab("References"), PreviewMesh(Height = 180)]
        public GameObject meshObject;

        [ShowInInspector, ReadOnly, PropertyOrder(100)]
        public string Summary => $"HP {health:0}, speed {speed:0.0}";

        [Button(Label = "Reset Character", Confirm = true)]
        private void ResetCharacter(float newHealth = 100)
        {
            health = newHealth;
            speed = 5;
        }

        private float MinSpeed => 0;
        private float MaxSpeed => 20;
        private int[] GetDifficulties() => new[] { 1, 2, 3, 4, 5 };
        private void OnHealthChanged() => Debug.Log($"Health changed to {health}", this);
    }

    [Serializable]
    public sealed class SampleRow
    {
        public string name;
        public int amount;
        public Color color = Color.white;
    }

    [Serializable]
    public abstract class SampleAction
    {
        public string label;
    }

    [Serializable, TypeName("Log Message")]
    public sealed class LogSampleAction : SampleAction
    {
        [PropertyTextArea] public string message;
    }

    [Serializable, TypeName("Move Object")]
    public sealed class MoveSampleAction : SampleAction
    {
        public Vector3 offset;
    }
}
