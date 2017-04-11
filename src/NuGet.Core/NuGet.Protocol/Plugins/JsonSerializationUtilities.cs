// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Plugins
{
    public static class JsonSerializationUtilities
    {
        public static JsonSerializer Serializer { get; }

        static JsonSerializationUtilities()
        {
            Serializer = JsonSerializer.Create(new JsonSerializerSettings()
            {
                Converters = new JsonConverter[]
                {
                    new SemanticVersionConverter(),
                    new StringEnumConverter()
                },
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        public static T Deserialize<T>(JsonReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            return Serializer.Deserialize<T>(reader);
        }

        public static JObject FromObject(object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return JObject.FromObject(value, Serializer);
        }

        public static void Serialize(JsonWriter writer, object value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            Serializer.Serialize(writer, value);
        }

        public static T ToObject<T>(JObject jObject)
        {
            if (jObject == null)
            {
                throw new ArgumentNullException(nameof(jObject));
            }

            return jObject.ToObject<T>(Serializer);
        }
    }
}