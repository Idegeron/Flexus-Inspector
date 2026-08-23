using System;
using Flexus.Inspector.Editor;
using UnityEngine.UIElements;

namespace Flexus.Inspector.Samples.Editor
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class CharacterCountAttribute : Attribute { }

    public sealed class CharacterCountExtension : InspectorAttributeExtension<CharacterCountAttribute>
    {
        protected override void Apply(MemberElement element, CharacterCountAttribute attribute, MemberContext context)
        {
            var counter = new Label();
            counter.AddToClassList("character-count");
            element.AddAfter(counter);
            element.schedule.Execute(() =>
            {
                var text = context.Value.GetValue()?.ToString() ?? string.Empty;
                counter.text = $"Characters: {text.Length}";
            }).Every(200);
        }
    }
}
