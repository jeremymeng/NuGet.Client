// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Plugins
{
    public interface IRequestHandlers
    {
        bool TryAdd(MessageMethod key, IRequestHandler handler);
        bool TryGet(MessageMethod key, out IRequestHandler handler);
        bool TryRemove(MessageMethod key);
    }
}