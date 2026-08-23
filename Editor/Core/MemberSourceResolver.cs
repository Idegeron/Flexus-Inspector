using System;
using System.Collections.Generic;
using System.Reflection;

namespace Flexus.Inspector.Editor
{
    internal static class MemberSourceResolver
    {
        private static readonly Dictionary<(Type, string), MemberInfo> Cache =
            new Dictionary<(Type, string), MemberInfo>();

        public static bool TryGetValue(object target, string memberName, out object value, out string error)
        {
            value = null;
            error = null;
            if (target == null)
            {
                error = "Target is null.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(memberName))
            {
                error = "Member name is empty.";
                return false;
            }

            var type = target.GetType();
            if (!Cache.TryGetValue((type, memberName), out var member))
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                                           BindingFlags.Public | BindingFlags.NonPublic;
                member = (MemberInfo)type.GetField(memberName, flags) ??
                         type.GetProperty(memberName, flags) ??
                         (MemberInfo)type.GetMethod(memberName, flags, null, Type.EmptyTypes, null);
                Cache[(type, memberName)] = member;
            }

            if (member == null)
            {
                error = $"Member '{memberName}' was not found on {type.Name}.";
                return false;
            }

            try
            {
                value = member switch
                {
                    FieldInfo field => field.GetValue(field.IsStatic ? null : target),
                    PropertyInfo property => property.GetValue(property.GetMethod?.IsStatic == true ? null : target),
                    MethodInfo method => method.Invoke(method.IsStatic ? null : target, null),
                    _ => null,
                };
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                return false;
            }
        }

        public static bool Invoke(object target, string methodName, object[] arguments, out object result, out string error)
        {
            result = null;
            error = null;
            if (target == null)
            {
                error = "Target is null.";
                return false;
            }
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.Public | BindingFlags.NonPublic;
            var methods = target.GetType().GetMethods(flags);
            MethodInfo selected = null;
            foreach (var method in methods)
            {
                if (method.Name == methodName && method.GetParameters().Length == (arguments?.Length ?? 0))
                {
                    selected = method;
                    break;
                }
            }
            if (selected == null)
            {
                error = $"Method '{methodName}' was not found on {target.GetType().Name}.";
                return false;
            }
            try
            {
                result = selected.Invoke(selected.IsStatic ? null : target, arguments);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                return false;
            }
        }
    }
}
