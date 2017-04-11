// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task CreateAsync_ThrowsForNullOrEmptyFilePath(string filePath)
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => Plugin.CreateAsync(filePath, ConnectionOptions.Default, CancellationToken.None));

            Assert.Equal("filePath", exception.ParamName);
        }

        [Fact]
        public async Task CreateAsync_ThrowsForNullConnectionOptions()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => Plugin.CreateAsync(
                    filePath: "a",
                    options: null,
                    sessionCancellationToken: CancellationToken.None));

            Assert.Equal("options", exception.ParamName);
        }

        [Fact]
        public async Task CreateAsync_ThrowsIfCancelled()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => Plugin.CreateAsync(
                    filePath: "a",
                    options: ConnectionOptions.Default,
                    sessionCancellationToken: new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task CreateFromCurrentProcessAsync_ThrowsForNullConnectionOptions()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => Plugin.CreateFromCurrentProcessAsync(
                    options: null,
                    sessionCancellationToken: CancellationToken.None));

            Assert.Equal("options", exception.ParamName);
        }

        [Fact]
        public async Task CreateFromCurrentProcessAsync_ThrowsIfCancelled()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => Plugin.CreateFromCurrentProcessAsync(
                    ConnectionOptions.Default,
                    sessionCancellationToken: new CancellationToken(canceled: true)));
        }
    }
}