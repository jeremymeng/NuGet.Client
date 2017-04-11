// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using NuGet.Versioning;

namespace NuGet.Protocol.Plugins
{
    public class HandshakeResponse
    {
        [JsonRequired]
        public MessageResponseCode ResponseCode { get; }
        public SemanticVersion ProtocolVersion { get; }

        [JsonConstructor]
        public HandshakeResponse(MessageResponseCode responseCode, SemanticVersion protocolVersion)
        {
            if (responseCode == MessageResponseCode.Success)
            {
                if (protocolVersion == null)
                {
                    throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(protocolVersion));
                }
            }
            else if (protocolVersion != null)
            {
                throw new ArgumentException("invalid protocol version", nameof(protocolVersion));
            }

            ResponseCode = responseCode;
            ProtocolVersion = protocolVersion;
        }
    }
}