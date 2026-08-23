using System.Linq;
using System.Reflection;
using Flexus.Serialization;

namespace Flexus.Inspector.Editor.Integrations
{
    public sealed class FlexusSerializationMemberInclusionPolicy : IInspectorMemberInclusionPolicy
    {
        public int Priority => 500;

        // Flexus Serialization currently persists fields only. Properties must not be exposed as
        // persistent values until the serializer itself supports them.
        public bool Includes(MemberInfo member)
        {
            return member is FieldInfo &&
                   member.IsDefined(typeof(SerializationIncludedAttribute), false);
        }
    }

    public sealed class FlexusSerializationChangeHandler : IInspectorChangeHandler
    {
        public int Priority => 500;

        public bool CanHandle(InspectorContext context)
        {
            return context.Targets.Any(target => target is ISerializable);
        }

        public void OnChanged(InspectorContext context)
        {
            foreach (var target in context.Targets)
                if (target is ISerializable serializable)
                    serializable.SetDirty(true);
        }
    }
}
