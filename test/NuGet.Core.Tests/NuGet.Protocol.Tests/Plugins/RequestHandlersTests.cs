// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class RequestHandlersTests
    {
        private readonly IRequestHandler _handler;
        private readonly RequestHandlers _handlers;

        public RequestHandlersTests()
        {
            _handler = Mock.Of<IRequestHandler>();
            _handlers = new RequestHandlers();
        }

        [Fact]
        public void TryAdd_ThrowsForNullHandler()
        {
            Assert.Throws<ArgumentNullException>(
                () => _handlers.TryAdd(MessageMethod.DownloadPackage, handler: null));
        }

        [Fact]
        public void TryAdd_ReturnsTrueIfAdded()
        {
            var wasAdded = _handlers.TryAdd(MessageMethod.DownloadPackage, _handler);

            Assert.True(wasAdded);
        }

        [Fact]
        public void TryAdd_ReturnsFalseIfNotAdded()
        {
            _handlers.TryAdd(MessageMethod.DownloadPackage, _handler);
            var wasAdded = _handlers.TryAdd(MessageMethod.DownloadPackage, _handler);

            Assert.False(wasAdded);
        }

        [Fact]
        public void TryGet_ReturnsTrueIfGotten()
        {
            _handlers.TryAdd(MessageMethod.DownloadPackage, _handler);

            IRequestHandler handler;
            var wasGotten = _handlers.TryGet(MessageMethod.DownloadPackage, out handler);

            Assert.True(wasGotten);
            Assert.Same(_handler, handler);
        }

        [Fact]
        public void TryGet_ReturnsFalseIfNotGotten()
        {
            IRequestHandler handler;
            var wasGotten = _handlers.TryGet(MessageMethod.DownloadPackage, out handler);

            Assert.False(wasGotten);
            Assert.Null(handler);
        }

        [Fact]
        public void TryRemove_ReturnsTrueIfRemoved()
        {
            _handlers.TryAdd(MessageMethod.DownloadPackage, _handler);

            var wasRemoved = _handlers.TryRemove(MessageMethod.DownloadPackage);

            Assert.True(wasRemoved);
        }

        [Fact]
        public void TryRemove_ReturnsFalseIfNotRemoved()
        {
            var wasRemoved = _handlers.TryRemove(MessageMethod.DownloadPackage);

            Assert.False(wasRemoved);
        }
    }
}