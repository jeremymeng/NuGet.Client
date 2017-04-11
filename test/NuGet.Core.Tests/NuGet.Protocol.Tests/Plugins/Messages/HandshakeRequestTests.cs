// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class HandshakeRequestTests
    {
        private static readonly SemanticVersion _version1_0_0 = new SemanticVersion(major: 1, minor: 0, patch: 0);
        private static readonly SemanticVersion _version2_0_0 = new SemanticVersion(major: 2, minor: 0, patch: 0);

        [Fact]
        public void Constructor_ThrowsForNullProtocolVersion()
        {
            Assert.Throws<ArgumentNullException>(
                () => new HandshakeRequest(protocolVersion: null, minimumProtocolVersion: _version1_0_0));
        }

        [Fact]
        public void Constructor_ThrowsForNullMinimumProtocolVersion()
        {
            Assert.Throws<ArgumentNullException>(
                () => new HandshakeRequest(_version1_0_0, minimumProtocolVersion: null));
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var request = new HandshakeRequest(_version2_0_0, _version1_0_0);

            Assert.Equal(_version2_0_0, request.ProtocolVersion);
            Assert.Equal(_version1_0_0, request.MinimumProtocolVersion);
        }

        [Fact]
        public void JsonSerialization_ReturnsCorrectJson()
        {
            var request = new HandshakeRequest(_version2_0_0, _version1_0_0);

            var json = TestUtilities.Serialize(request);

            Assert.Equal("{\"ProtocolVersion\":\"2.0.0\",\"MinimumProtocolVersion\":\"1.0.0\"}", json);
        }

        [Fact]
        public void JsonDeserialization_ReturnsCorrectObject()
        {
            var json = "{\"ProtocolVersion\":\"2.0.0\",\"MinimumProtocolVersion\":\"1.0.0\"}";

            var request = TestUtilities.Deserialize<HandshakeRequest>(json);

            Assert.Equal(_version2_0_0, request.ProtocolVersion);
            Assert.Equal(_version1_0_0, request.MinimumProtocolVersion);
        }

        [Theory]
        [InlineData("{\"MinimumProtocolVersion\":\"1.0.0\"}")]
        [InlineData("{\"ProtocolVersion\":null,\"MinimumProtocolVersion\":\"1.0.0\"}")]
        [InlineData("{\"ProtocolVersion\":\"1.0.0\"}")]
        [InlineData("{\"ProtocolVersion\":\"1.0.0\",\"MinimumProtocolVersion\":null}")]
        public void JsonDeserialization_ThrowsForNullVersion(string json)
        {
            Assert.Throws<ArgumentNullException>(() => TestUtilities.Deserialize<HandshakeRequest>(json));
        }

        [Theory]
        [InlineData("{\"ProtocolVersion\":\"\",\"MinimumProtocolVersion\":\"1.0.0\"}")]
        [InlineData("{\"ProtocolVersion\":\" \",\"MinimumProtocolVersion\":\"1.0.0\"}")]
        [InlineData("{\"ProtocolVersion\":\"a\",\"MinimumProtocolVersion\":\"1.0.0\"}")]
        [InlineData("{\"ProtocolVersion\":3,\"MinimumProtocolVersion\":\"1.0.0\"}")]
        [InlineData("{\"ProtocolVersion\":false,\"MinimumProtocolVersion\":\"1.0.0\"}")]
        [InlineData("{\"ProtocolVersion\":\"1.0.0\",\"MinimumProtocolVersion\":\"\"}")]
        [InlineData("{\"ProtocolVersion\":\"1.0.0\",\"MinimumProtocolVersion\":\" \"}")]
        [InlineData("{\"ProtocolVersion\":\"1.0.0\",\"MinimumProtocolVersion\":\"a\"}")]
        [InlineData("{\"ProtocolVersion\":\"1.0.0\",\"MinimumProtocolVersion\":3,}")]
        [InlineData("{\"ProtocolVersion\":\"1.0.0\",\"MinimumProtocolVersion\":false}")]
        public void JsonDeserialization_ThrowsForInvalidVersion(string json)
        {
            Assert.Throws<ArgumentException>(() => TestUtilities.Deserialize<HandshakeRequest>(json));
        }
    }
}