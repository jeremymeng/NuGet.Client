// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Plugins
{
    public class Message
    {
        [JsonRequired]
        public string RequestId { get; }
        [JsonRequired]
        public MessageType Type { get; }
        [JsonRequired]
        public MessageMethod Method { get; }
        public JObject Payload { get; }

        [JsonConstructor]
        public Message(string requestId, MessageType type, MessageMethod method, JObject payload = null)
        {
            if (string.IsNullOrEmpty(requestId))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(requestId));
            }

            if (!Enum.IsDefined(typeof(MessageType), type))
            {
                throw new ArgumentException("invalid enum value", nameof(type));
            }

            if (!Enum.IsDefined(typeof(MessageMethod), method))
            {
                throw new ArgumentException("invalid enum value", nameof(method));
            }

            RequestId = requestId;
            Type = type;
            Method = method;
            Payload = payload;
        }
    }
}