// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    public interface IMessageDispatcher : IDisposable
    {
        IRequestHandlers RequestHandlers { get; }

        Task DispatchCancelAsync(Message request, CancellationToken cancellationToken);
        Task DispatchFaultAsync(Message request, Fault fault, CancellationToken cancellationToken);
        Task DispatchProgressAsync(Message request, Progress progress, CancellationToken cancellationToken);
        Task<TInbound> DispatchRequestAsync<TOutbound, TInbound>(
            MessageMethod method,
            TOutbound payload,
            TimeSpan timeout,
            bool isKeepAlive,
            CancellationToken cancellationToken)
            where TOutbound : class
            where TInbound : class;
        Task DispatchResponseAsync<TOutbound>(Message request, TOutbound responsePayload, CancellationToken cancellationToken)
            where TOutbound : class;

        void SetConnection(IConnection connection);
    }
}