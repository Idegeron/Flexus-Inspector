using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Flexus.Inspector.Editor
{
    public enum InspectorStage
    {
        Visibility = 100,
        Enablement = 200,
        Content = 300,
        Decorate = 400,
        Validate = 500,
    }

    public interface IInspectorExtension
    {
        InspectorStage Stage { get; }
        int Order { get; }
        bool CanApply(MemberContext context);
        void Apply(MemberElement element, MemberContext context);
    }

    public abstract class InspectorAttributeExtension<TAttribute> : IInspectorExtension
        where TAttribute : Attribute
    {
        public virtual InspectorStage Stage => InspectorStage.Decorate;
        public virtual int Order => 0;

        public bool CanApply(MemberContext context) => context.Descriptor.HasAttribute<TAttribute>();

        public void Apply(MemberElement element, MemberContext context)
        {
            foreach (var attribute in context.Descriptor.GetAttributes<TAttribute>())
                Apply(element, attribute, context);
        }

        protected abstract void Apply(MemberElement element, TAttribute attribute, MemberContext context);
    }

    internal static class InspectorExtensionRegistry
    {
        private static IReadOnlyList<IInspectorExtension> extensions;

        public static IReadOnlyList<IInspectorExtension> Extensions => extensions ??= Discover();

        private static IReadOnlyList<IInspectorExtension> Discover()
        {
            var result = new List<IInspectorExtension>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IInspectorExtension>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                try
                {
                    if (Activator.CreateInstance(type) is IInspectorExtension extension)
                        result.Add(extension);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
            return result.OrderBy(extension => extension.Stage).ThenBy(extension => extension.Order).ToArray();
        }
    }
}
