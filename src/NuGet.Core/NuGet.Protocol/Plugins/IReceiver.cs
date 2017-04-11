// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    public interface IReceiver : IDisposable
    {
        event EventHandler<MessageEventArgs> MessageReceived;
        event EventHandler<ProtocolErrorEventArgs> Faulted;

        Task ConnectAsync(CancellationToken cancellationToken);
        Task CloseAsync();
    }
}