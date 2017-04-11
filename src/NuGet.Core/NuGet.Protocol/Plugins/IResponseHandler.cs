// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    public interface IResponseHandler
    {
        Task SendResponseAsync<TPayload>(Message request, TPayload responsePayload, CancellationToken cancellationToken)
            where TPayload : class;
    }
}