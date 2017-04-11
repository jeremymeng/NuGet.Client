// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    public interface ISender : IDisposable
    {
        Task ConnectAsync(CancellationToken cancellationToken);
        Task SendAsync(Message message, CancellationToken cancellationToken);
        Task CloseAsync();
    }
}