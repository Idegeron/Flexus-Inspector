using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Flexus.Inspector.Editor
{
    /// <summary>
    /// Allows an external serialization package to expose reflection-backed members without coupling
    /// the inspector core to that package's attributes.
    /// </summary>
    public interface IInspectorMemberInclusionPolicy
    {
        int Priority { get; }
        bool Includes(MemberInfo member);
    }

    /// <summary>
    /// Receives a notification whenever an inspector operation changes one of its targets.
    /// </summary>
    public interface IInspectorChangeHandler
    {
        int Priority { get; }
        bool CanHandle(InspectorContext context);
        void OnChanged(InspectorContext context);
    }

    internal static class InspectorMemberInclusionRegistry
    {
        private static IReadOnlyList<IInspectorMemberInclusionPolicy> policies;

        public static bool Includes(MemberInfo member)
        {
            foreach (var policy in policies ??= Discover<IInspectorMemberInclusionPolicy>())
            {
                try
                {
                    if (policy.Includes(member)) return true;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            return false;
        }

        private static IReadOnlyList<T> Discover<T>() where T : class
        {
            var result = new List<T>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) continue;
                try
                {
                    if (Activator.CreateInstance(type) is T instance) result.Add(instance);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            return result.OrderByDescending(instance =>
                instance is IInspectorMemberInclusionPolicy policy ? policy.Priority : 0).ToArray();
        }
    }

    internal static class InspectorChangeHandlerRegistry
    {
        private static IReadOnlyList<IInspectorChangeHandler> handlers;

        public static void Notify(InspectorContext context)
        {
            foreach (var handler in handlers ??= Discover())
            {
                try
                {
                    if (handler.CanHandle(context)) handler.OnChanged(context);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static IReadOnlyList<IInspectorChangeHandler> Discover()
        {
            var result = new List<IInspectorChangeHandler>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IInspectorChangeHandler>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) continue;
                try
                {
                    if (Activator.CreateInstance(type) is IInspectorChangeHandler handler) result.Add(handler);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            return result.OrderByDescending(handler => handler.Priority).ToArray();
        }
    }
}
