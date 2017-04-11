// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class ConnectionOptionsTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
        private static readonly SemanticVersion _version1_0_0 = new SemanticVersion(major: 1, minor: 0, patch: 0);
        private static readonly SemanticVersion _version2_0_0 = new SemanticVersion(major: 2, minor: 0, patch: 0);

        [Fact]
        public void Constructor_ThrowsForNullProtocolVersion()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ConnectionOptions(
                    protocolVersion: null,
                    minimumProtocolVersion: _version2_0_0,
                    handshakeTimeout: _timeout));
        }

        [Fact]
        public void Constructor_ThrowsForNullMinimumProtocolVersion()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ConnectionOptions(_version1_0_0, minimumProtocolVersion: null, handshakeTimeout: _timeout));
        }

        [Fact]
        public void Constructor_ThrowsForInvalidVersionRange()
        {
            Assert.Throws<ArgumentException>(() => new ConnectionOptions(_version1_0_0, _version2_0_0, _timeout));
        }

        [Fact]
        public void Constructor_ThrowsForZeroTimeout()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConnectionOptions(_version2_0_0, _version1_0_0, TimeSpan.Zero));
        }

        [Fact]
        public void Constructor_ThrowsForNegativeTimeout()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConnectionOptions(_version2_0_0, _version1_0_0, TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void Constructor_ThrowsForTooLargeTimeout()
        {
            var milliseconds = int.MaxValue + 1L;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConnectionOptions(_version2_0_0, _version1_0_0, TimeSpan.FromMilliseconds(milliseconds)));
        }

        [Fact]
        public void Constructor_AcceptsEqualVersions()
        {
            var options = new ConnectionOptions(_version1_0_0, _version1_0_0, _timeout);

            Assert.Equal(options.ProtocolVersion, options.MinimumProtocolVersion);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var options = new ConnectionOptions(_version2_0_0, _version1_0_0, TimeSpan.FromMilliseconds(int.MaxValue));

            Assert.Equal(_version2_0_0, options.ProtocolVersion);
            Assert.Equal(_version1_0_0, options.MinimumProtocolVersion);
            Assert.Equal(TimeSpan.FromMilliseconds(int.MaxValue), options.HandshakeTimeout);
        }

        [Fact]
        public void Default_HasCorrectProtocolVersion()
        {
            Assert.Equal(ProtocolConstants.CurrentVersion, ConnectionOptions.Default.ProtocolVersion);
        }

        [Fact]
        public void Default_HasCorrectMinimumProtocolVersion()
        {
            Assert.Equal(ProtocolConstants.CurrentVersion, ConnectionOptions.Default.MinimumProtocolVersion);
        }

        [Fact]
        public void Default_HasCorrectHandshakeTimeout()
        {
            Assert.Equal(TimeSpan.FromSeconds(10), ConnectionOptions.Default.HandshakeTimeout);
        }
    }
}