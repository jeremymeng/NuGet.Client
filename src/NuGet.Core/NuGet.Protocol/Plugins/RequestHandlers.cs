// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;

namespace NuGet.Protocol.Plugins
{
    public class RequestHandlers : IRequestHandlers
    {
        private readonly ConcurrentDictionary<MessageMethod, IRequestHandler> _handlers;

        public RequestHandlers()
        {
            _handlers = new ConcurrentDictionary<MessageMethod, IRequestHandler>();
        }

        public virtual bool TryAdd(MessageMethod method, IRequestHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return _handlers.TryAdd(method, handler);
        }

        public virtual bool TryGet(MessageMethod method, out IRequestHandler handler)
        {
            return _handlers.TryGetValue(method, out handler);
        }

        public virtual bool TryRemove(MessageMethod method)
        {
            IRequestHandler handler;

            return _handlers.TryRemove(method, out handler);
        }
    }
}