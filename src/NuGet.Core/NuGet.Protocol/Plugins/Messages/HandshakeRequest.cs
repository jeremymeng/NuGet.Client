// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using NuGet.Versioning;

namespace NuGet.Protocol.Plugins
{
    public class HandshakeRequest
    {
        [JsonRequired]
        public SemanticVersion ProtocolVersion { get; }
        [JsonRequired]
        public SemanticVersion MinimumProtocolVersion { get; }

        [JsonConstructor]
        public HandshakeRequest(SemanticVersion protocolVersion, SemanticVersion minimumProtocolVersion)
        {
            if (protocolVersion == null)
            {
                throw new ArgumentNullException(nameof(protocolVersion));
            }

            if (minimumProtocolVersion == null)
            {
                throw new ArgumentNullException(nameof(minimumProtocolVersion));
            }

            ProtocolVersion = protocolVersion;
            MinimumProtocolVersion = minimumProtocolVersion;
        }
    }
}