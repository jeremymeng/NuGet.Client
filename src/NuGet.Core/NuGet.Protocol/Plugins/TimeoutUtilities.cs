// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    public static class TimeoutUtilities
    {
        public static bool IsValid(TimeSpan timeout)
        {
            if (timeout > TimeSpan.Zero &&
                (long)timeout.TotalMilliseconds <= int.MaxValue) // Required by CancellationTokenSource's constructor.
            {
                return true;
            }

            return false;
        }
    }
}