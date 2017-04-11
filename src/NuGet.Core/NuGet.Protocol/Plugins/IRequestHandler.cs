// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    public interface IRequestHandler
    {
        CancellationToken CancellationToken { get; }

        Task HandleCancelAsync(Message message, CancellationToken cancellationToken);
        Task HandleProgressAsync(Message message, CancellationToken cancellationToken);
        Task HandleRequestAsync(Message message, IResponseHandler responseHandler, CancellationToken cancellationToken);
    }
}