// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class ProtocolErrorEventArgsTests
    {
        [Fact]
        public void Constructor_ThrowsForNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ProtocolErrorEventArgs(exception: null));
        }

        [Fact]
        public void Constructor_InitializesProperty()
        {
            var exception = new DivideByZeroException();
            var args = new ProtocolErrorEventArgs(exception);

            Assert.Same(exception, args.Exception);
        }
    }
}