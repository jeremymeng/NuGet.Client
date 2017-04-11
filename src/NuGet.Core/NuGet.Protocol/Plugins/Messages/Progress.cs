// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    public class Progress
    {
        [JsonRequired]
        public double ProgressPercent { get; }

        [JsonConstructor]
        public Progress(double progressPercent)
        {
            if (!IsValidProgressPercent(progressPercent))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(progressPercent));
            }

            ProgressPercent = progressPercent;
        }

        private static bool IsValidProgressPercent(double progressPercent)
        {
            if (double.IsNaN(progressPercent) || double.IsInfinity(progressPercent) ||
                progressPercent < 0 || progressPercent > 1)
            {
                return false;
            }

            return true;
        }
    }
}