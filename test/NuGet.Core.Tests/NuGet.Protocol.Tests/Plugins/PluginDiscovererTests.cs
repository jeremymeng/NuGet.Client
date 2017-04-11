// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginDiscovererTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_AcceptsAnyString(string rawPluginPaths)
        {
            using (new PluginDiscoverer(rawPluginPaths, Mock.Of<EmbeddedSignatureVerifier>()))
            {
            }
        }

        [Fact]
        public void DiscoverAsync_AcceptsNullEmbeddedSignatureVerifier()
        {
            using (var discoverer = new PluginDiscoverer(rawPluginPaths: "", verifier: null))
            {
            }
        }

        [Fact]
        public async Task DiscoverAsync_ThrowsIfCancelled()
        {
            using (var discoverer = new PluginDiscoverer(rawPluginPaths: "", verifier: Mock.Of<EmbeddedSignatureVerifier>()))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => discoverer.DiscoverAsync(new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task DiscoverAsync_ThrowsPlatformNotSupportedIfEmbeddedSignatureVerifierIsBothRequiredAndNull()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPath = Path.Combine(testDirectory.Path, "a");

                File.WriteAllText(pluginPath, string.Empty);

                using (var discoverer = new PluginDiscoverer(pluginPath, verifier: null))
                {
                    await Assert.ThrowsAsync<PlatformNotSupportedException>(
                        () => discoverer.DiscoverAsync(CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task DiscoverAsync_DoesNotThrowIfNoValidFilePathsAndNullEmbeddedSignatureVerifier()
        {
            using (var discoverer = new PluginDiscoverer(rawPluginPaths: "", verifier: null))
            {
                var pluginFiles = await discoverer.DiscoverAsync(CancellationToken.None);

                Assert.Empty(pluginFiles);
            }
        }

        [Fact]
        public async Task DiscoverAsync_HandlesNonExistentAndValidAndInvalidFiles()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPaths = new[] { "b", "c" }
                    .Select(fileName => Path.Combine(testDirectory.Path, fileName))
                    .ToArray();

                foreach (var pluginPath in pluginPaths)
                {
                    File.WriteAllText(pluginPath, string.Empty);
                }

                var responses = new Dictionary<string, bool>()
                {
                    { "a", false },
                    { pluginPaths[0], false },
                    { pluginPaths[1], true },
                    { "d", false },
                };
                var verifierStub = new EmbeddedSignatureVerifierStub(responses);
                var rawPluginPaths = string.Join(";", responses.Keys);

                using (var discoverer = new PluginDiscoverer(rawPluginPaths, verifierStub))
                {
                    var pluginFiles = (await discoverer.DiscoverAsync(CancellationToken.None)).ToArray();

                    Assert.Equal(4, pluginFiles.Length);
                    Assert.Equal("a", pluginFiles[0].Path);
                    Assert.Equal(PluginFileState.NotFound, pluginFiles[0].State);
                    Assert.Equal(pluginPaths[0], pluginFiles[1].Path);
                    Assert.Equal(PluginFileState.InvalidCodeSignature, pluginFiles[1].State);
                    Assert.Equal(pluginPaths[1], pluginFiles[2].Path);
                    Assert.Equal(PluginFileState.Valid, pluginFiles[2].State);
                    Assert.Equal("d", pluginFiles[3].Path);
                    Assert.Equal(PluginFileState.NotFound, pluginFiles[3].State);
                }
            }
        }

        [Fact]
        public async Task DiscoverAsync_IsIdempotent()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPath = Path.Combine(testDirectory.Path, "a");

                File.WriteAllText(pluginPath, string.Empty);

                var verifierSpy = new Mock<EmbeddedSignatureVerifier>();

                verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                    .Returns(true);

                using (var discoverer = new PluginDiscoverer(pluginPath, verifierSpy.Object))
                {
                    var firstResult = await discoverer.DiscoverAsync(CancellationToken.None);

                    verifierSpy.Verify(spy => spy.IsValid(It.IsAny<string>()),
                        Times.Once);

                    var secondResult = await discoverer.DiscoverAsync(CancellationToken.None);

                    verifierSpy.Verify(spy => spy.IsValid(It.IsAny<string>()),
                        Times.Once);

                    Assert.Same(firstResult, secondResult);
                }
            }
        }

        private sealed class EmbeddedSignatureVerifierStub : EmbeddedSignatureVerifier
        {
            private readonly Dictionary<string, bool> _responses;

            public EmbeddedSignatureVerifierStub(Dictionary<string, bool> responses)
            {
                _responses = responses;
            }

            public override bool IsValid(string filePath)
            {
                bool value;

                Assert.True(_responses.TryGetValue(filePath, out value));

                return value;
            }
        }
    }
}