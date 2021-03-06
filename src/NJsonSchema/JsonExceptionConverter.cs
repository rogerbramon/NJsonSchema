//-----------------------------------------------------------------------
// <copyright file="JsonExceptionConverter.cs" company="NJsonSchema">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/rsuter/NJsonSchema/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NJsonSchema.Infrastructure;

namespace NJsonSchema
{
    /// <summary>A converter to correctly serialize exception objects.</summary>
    public class JsonExceptionConverter : JsonConverter
    {
        private readonly DefaultContractResolver _defaultContractResolver = new DefaultContractResolver();

        /// <summary>Gets a value indicating whether this <see cref="T:Newtonsoft.Json.JsonConverter" /> can write JSON.</summary>
        public override bool CanWrite => true;

        /// <summary>Writes the JSON representation of the object.</summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var exception = value as Exception;
            if (exception != null)
            {
                var resolver = serializer.ContractResolver as DefaultContractResolver ?? _defaultContractResolver;

                var jObject = new JObject();
                jObject.Add(resolver.GetResolvedPropertyName("Message"), exception.Message);
                jObject.Add(resolver.GetResolvedPropertyName("StackTrace"), exception.StackTrace);
                jObject.Add(resolver.GetResolvedPropertyName("Source"), exception.Source);
                jObject.Add(resolver.GetResolvedPropertyName("InnerException"),
                    exception.InnerException != null ? JToken.FromObject(exception.InnerException, serializer) : null);

                foreach (var property in GetExceptionProperties(value.GetType()))
                {
                    var propertyValue = property.Key.GetValue(exception);
                    if (propertyValue != null)
                    {
                        jObject.AddFirst(new JProperty(resolver.GetResolvedPropertyName(property.Value),
                            JToken.FromObject(propertyValue, serializer)));
                    }
                }

                value = jObject;
            }

            serializer.Serialize(writer, value);
        }

        /// <summary>Determines whether this instance can convert the specified object type.</summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns><c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.</returns>
        public override bool CanConvert(Type objectType)
        {
            return typeof(Exception).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        /// <summary>Reads the JSON representation of the object.</summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The object value.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = serializer.Deserialize<JObject>(reader);
            if (jObject == null)
                return null;

            var originalResolver = serializer.ContractResolver;
            serializer.ContractResolver = (IContractResolver)Activator.CreateInstance(serializer.ContractResolver.GetType());

            GetField(typeof(DefaultContractResolver), "_sharedCache").SetValue(serializer.ContractResolver, false);

            dynamic resolver = serializer.ContractResolver = serializer.ContractResolver;
            resolver.IgnoreSerializableAttribute = true;
            resolver.IgnoreSerializableInterface = true;

            serializer.Converters.Remove(this);
            var value = jObject.ToObject(objectType, serializer);
            serializer.Converters.Add(this);

            foreach (var property in GetExceptionProperties(value.GetType()))
            {
                var jValue = jObject.GetValue(resolver.GetResolvedPropertyName(property.Value));
                var propertyValue = (object)jValue?.ToObject(property.Key.PropertyType);
#if !LEGACY
                if (property.Key.SetMethod != null)
                    property.Key.SetValue(value, propertyValue);
                else
                {
                    var field = GetField(objectType, "m_" + property.Value.Substring(0, 1).ToLowerInvariant() + property.Value.Substring(1));
                    if (field != null)
                        field.SetValue(value, propertyValue);
                }
#else
                if (property.Key.GetSetMethod() != null)
                    property.Key.SetValue(value, propertyValue);
                else
                {
                    var field = objectType.GetField("m_" + property.Value.Substring(0, 1).ToLowerInvariant() + property.Value.Substring(1), 
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field != null)
                        field.SetValue(value, propertyValue);
                }
#endif
            }

            SetExceptionFieldValue(jObject, "Message", value, "_message", resolver, serializer);
            SetExceptionFieldValue(jObject, "StackTrace", value, "_stackTraceString", resolver, serializer);
            SetExceptionFieldValue(jObject, "Source", value, "_source", resolver, serializer);
            SetExceptionFieldValue(jObject, "InnerException", value, "_innerException", resolver, serializer);

            serializer.ContractResolver = originalResolver;
            return value;
        }

        private FieldInfo GetField(Type type, string fieldName)
        {
            var field = type.GetTypeInfo().GetDeclaredField(fieldName);
            if (field == null && type.GetTypeInfo().BaseType != null)
                return GetField(type.GetTypeInfo().BaseType, fieldName);
            return field;
        }

        private IDictionary<PropertyInfo, string> GetExceptionProperties(Type exceptionType)
        {
            var result = new Dictionary<PropertyInfo, string>();
#if !LEGACY
            foreach (var property in exceptionType.GetRuntimeProperties().Where(p => p.GetMethod?.IsPublic == true))
#else
            foreach (var property in exceptionType.GetRuntimeProperties().Where(p => p.GetGetMethod() != null))
#endif
            {
                var attribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                var propertyName = attribute != null ? attribute.PropertyName : property.Name;

                if (!new[] { "Message", "StackTrace", "Source", "InnerException", "Data", "TargetSite", "HelpLink", "HResult" }.Contains(propertyName))
                    result[property] = propertyName;
            }
            return result;
        }

        private void SetExceptionFieldValue(JObject jObject, string propertyName, object value, string fieldName, DefaultContractResolver resolver, JsonSerializer serializer)
        {
            var field = typeof(Exception).GetTypeInfo().GetDeclaredField(fieldName);
            var fieldValue = jObject[resolver.GetResolvedPropertyName(propertyName)].ToObject(field.FieldType, serializer);
            field.SetValue(value, fieldValue);
        }
    }
}