// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    public class PluginFile
    {
        public string Path { get; }
        public PluginFileState State { get; }

        public PluginFile(string filePath, PluginFileState state)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(filePath));
            }

            Path = filePath;
            State = state;
        }
    }
}