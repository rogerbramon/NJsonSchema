//-----------------------------------------------------------------------
// <copyright file="ReflectionExtensions.cs" company="NSwag">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/NSwag/NSwag/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NJsonSchema.Infrastructure
{
    /// <summary>Provides extension methods for reflection.</summary>
    public static class ReflectionExtensions
    {
        /// <summary>Tries to get the first object of the given type name.</summary>
        /// <param name="attributes">The attributes.</param>
        /// <param name="typeName">Type of the attribute.</param>
        /// <param name="typeNameStyle">The type name style.</param>
        /// <returns>May return null.</returns>
        public static T TryGetByObjectType<T>(this IEnumerable<T> attributes, string typeName, TypeNameStyle typeNameStyle = TypeNameStyle.FullName)
        {
            return attributes.FirstOrDefault(a => a.GetType().FullName == typeName);
        }

        /// <summary>Tries to get the first object which is assignable to the given type name.</summary>
        /// <param name="attributes">The attributes.</param>
        /// <param name="typeName">Type of the attribute.</param>
        /// <param name="typeNameStyle">The type name style.</param>
        /// <returns>May return null (not found).</returns>
        public static T TryGetIfAssignableTo<T>(this IEnumerable<T> attributes, string typeName, TypeNameStyle typeNameStyle = TypeNameStyle.FullName)
        {
            return attributes.FirstOrDefault(a => a.GetType().IsAssignableTo(typeName, typeNameStyle));
        }

        /// <summary>Checks whether the given type is assignable to the given type name.</summary>
        /// <param name="type">The type.</param>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="typeNameStyle">The type name style.</param>
        /// <returns></returns>
        public static bool IsAssignableTo(this Type type, string typeName, TypeNameStyle typeNameStyle)
        {
            if (typeNameStyle == TypeNameStyle.Name && type.Name == typeName)
                return true;

            if (typeNameStyle == TypeNameStyle.FullName && type.FullName == typeName)
                return true;

            return type.InheritsFrom(typeName, typeNameStyle);
        }

        /// <summary>Checks whether the given type inherits from the given type name.</summary>
        /// <param name="type">The type.</param>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="typeNameStyle">The type name style.</param>
        /// <returns>true if the type inherits from typeName.</returns>
        public static bool InheritsFrom(this Type type, string typeName, TypeNameStyle typeNameStyle)
        {
            var baseType = type.GetTypeInfo().BaseType;
            while (baseType != null)
            {
                if (typeNameStyle == TypeNameStyle.Name && baseType.Name == typeName)
                    return true;
                if (typeNameStyle == TypeNameStyle.FullName && baseType.FullName == typeName)
                    return true;

                baseType = baseType.GetTypeInfo().BaseType;
            }
            return false;
        }

        /// <summary>Gets the generic type arguments of a type.</summary>
        /// <param name="type">The type.</param>
        /// <returns>The type arguments.</returns>
        public static Type[] GetGenericTypeArguments(this Type type)
        {
#if !LEGACY

            var genericTypeArguments = type.GenericTypeArguments;
            while (type != null && type != typeof(object) && genericTypeArguments.Length == 0)
            {
                type = type.GetTypeInfo().BaseType;
                if (type != null)
                    genericTypeArguments = type.GenericTypeArguments;
            }
            return genericTypeArguments;

#else

            var genericTypeArguments = type.GetGenericArguments();
            while (type != null && type != typeof(object) && genericTypeArguments.Length == 0)
            {
                type = type.GetTypeInfo().BaseType;
                if (type != null)
                    genericTypeArguments = type.GetGenericArguments();
            }
            return genericTypeArguments;

#endif
        }

        internal static string GetSafeTypeName(Type type)
        {
#if !LEGACY
            if (type.IsConstructedGenericType)
                return type.Name.Split('`').First() + "Of" + GetSafeTypeName(type.GenericTypeArguments[0]);
#else
            if (type.IsGenericType)
                return type.Name.Split('`').First() + "Of" + GetSafeTypeName(type.GetGenericArguments()[0]);
#endif

            return type.Name;
        }

#if LEGACY

        internal static MethodInfo GetRuntimeMethod(this Type type, string name, Type[] types)
        {
            return type.GetMethod(name, types);
        }

        internal static PropertyInfo GetRuntimeProperty(this Type type, string name)
        {
            return type.GetProperty(name);
        }

        internal static FieldInfo GetDeclaredField(this Type type, string name)
        {
            return type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }

        internal static PropertyInfo[] GetRuntimeProperties(this Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        internal static Type GetTypeInfo(this Type type)
        {
            return type;
        }

        internal static Attribute[] GetCustomAttributes(this FieldInfo fieldInfo, bool inherit = true)
        {
            return fieldInfo.GetCustomAttributes(inherit).OfType<Attribute>().ToArray();
        }

        internal static Attribute[] GetCustomAttributes(this Type type, bool inherit = true)
        {
            return type.GetCustomAttributes(inherit).OfType<Attribute>().ToArray();
        }

        internal static Attribute[] GetCustomAttributes(this PropertyInfo propertyInfo, bool inherit = true)
        {
            return propertyInfo.GetCustomAttributes(inherit).OfType<Attribute>().ToArray();
        }

        internal static T[] GetCustomAttributes<T>(this Type type, bool inherit = true)
            where T : Attribute
        {
            return type.GetCustomAttributes(inherit).OfType<T>().ToArray();
        }

        internal static T[] GetCustomAttributes<T>(this PropertyInfo propertyInfo, bool inherit = true)
            where T : Attribute
        {
            return propertyInfo.GetCustomAttributes(inherit).OfType<T>().ToArray();
        }

        internal static T GetCustomAttribute<T>(this Type type)
            where T : Attribute
        {
            return type.GetCustomAttributes().OfType<T>().FirstOrDefault();
        }

        internal static T GetCustomAttribute<T>(this PropertyInfo propertyInfo)
            where T : Attribute
        {
            return propertyInfo.GetCustomAttributes().OfType<T>().FirstOrDefault();
        }

        internal static object GetValue(this PropertyInfo propertyInfo, object obj)
        {
            return propertyInfo.GetValue(obj, null);
        }

        internal static void SetValue(this PropertyInfo propertyInfo, object obj, object value)
        {
            propertyInfo.SetValue(obj, value, null);
        }

#endif
    }
}