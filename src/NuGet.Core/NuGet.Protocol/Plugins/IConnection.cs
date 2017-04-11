// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGet.Protocol.Plugins
{
    public interface IConnection
    {
        IMessageDispatcher MessageDispatcher { get; }
        ConnectionOptions Options { get; }
        SemanticVersion ProtocolVersion { get; }

        event EventHandler<MessageEventArgs> Faulted;
        event EventHandler<MessageEventArgs> MessageReceived;

        Task SendAsync(Message message, CancellationToken cancellationToken);
        Task<TInbound> SendRequestAndReceiveResponseAsync<TOutbound, TInbound>(
            MessageMethod method,
            TOutbound payload,
            TimeSpan timeout,
            bool isKeepAlive,
            CancellationToken cancellationToken)
            where TOutbound : class
            where TInbound : class;
    }
}