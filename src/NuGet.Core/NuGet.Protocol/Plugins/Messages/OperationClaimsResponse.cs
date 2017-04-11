// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins.Messages
{
    public class OperationClaimsResponse
    {
        [JsonRequired]
        public OperationClaim[] Claims { get; }

        [JsonConstructor]
        public OperationClaimsResponse(OperationClaim[] claims)
        {
            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }

            Claims = (OperationClaim[])claims.Clone();
        }
    }
}