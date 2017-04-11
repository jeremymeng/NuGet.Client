// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Protocol.FuncTest
{
    public class ConnectionTests
    {
        private readonly string _pluginFilePath;
        private const ushort _portNumber = 11000;

        public ConnectionTests()
        {
            _pluginFilePath = "";
        }

        //[Fact]
        public void Test()
        {
            using (var pluginStub = PluginStub.Create(_pluginFilePath, _portNumber))
            {
            }
        }
    }
}