// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    public static class MessageUtilities
    {
        public static TPayload DeserializePayload<TPayload>(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.Payload == null)
            {
                return default(TPayload);
            }

            return JsonSerializationUtilities.ToObject<TPayload>(message.Payload);
        }
    }
}