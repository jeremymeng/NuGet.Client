// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.Protocol
{
    public abstract class EmbeddedSignatureVerifier
    {
        public abstract bool IsValid(string filePath);

        public static EmbeddedSignatureVerifier CreateOrNull()
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                return new WindowsEmbeddedSignatureVerifier();
            }

            return null;
        }
    }
}