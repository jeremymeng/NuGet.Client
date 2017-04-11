// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    public class InitializeRequest
    {
        [JsonRequired]
        public string ClientVersion { get; }
        [JsonRequired]
        public string Culture { get; }
        [JsonRequired]
        public Verbosity Verbosity { get; }
        [JsonRequired]
        public TimeSpan RequestTimeout { get; }

        [JsonConstructor]
        public InitializeRequest(string clientVersion, string culture, Verbosity verbosity, TimeSpan requestTimeout)
        {
            if (string.IsNullOrEmpty(clientVersion))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(clientVersion));
            }

            if (string.IsNullOrEmpty(culture))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(culture));
            }

            if (!Enum.IsDefined(typeof(Verbosity), verbosity))
            {
                throw new ArgumentException("invalid enum", nameof(verbosity));
            }

            if (requestTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentException("invalid request duration", nameof(requestTimeout));
            }

            ClientVersion = clientVersion;
            Culture = culture;
            Verbosity = verbosity;
            RequestTimeout = requestTimeout;
        }
    }
}