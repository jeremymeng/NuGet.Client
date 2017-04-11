// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.Protocol.Plugins
{
    public class ConnectionOptions
    {
        public static readonly ConnectionOptions Default;

        public TimeSpan HandshakeTimeout { get; }
        public SemanticVersion MinimumProtocolVersion { get; }
        public SemanticVersion ProtocolVersion { get; }

        static ConnectionOptions()
        {
            Default = new ConnectionOptions(
                protocolVersion: ProtocolConstants.CurrentVersion,
                minimumProtocolVersion: ProtocolConstants.CurrentVersion,
                handshakeTimeout: TimeSpan.FromSeconds(10));
        }

        public ConnectionOptions(
            SemanticVersion protocolVersion,
            SemanticVersion minimumProtocolVersion,
            TimeSpan handshakeTimeout)
        {
            if (protocolVersion == null)
            {
                throw new ArgumentNullException(nameof(protocolVersion));
            }

            if (minimumProtocolVersion == null)
            {
                throw new ArgumentNullException(nameof(minimumProtocolVersion));
            }

            if (minimumProtocolVersion > protocolVersion)
            {
                throw new ArgumentException();
            }

            if (!TimeoutUtilities.IsValid(handshakeTimeout))
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeTimeout));
            }

            ProtocolVersion = protocolVersion;
            MinimumProtocolVersion = minimumProtocolVersion;
            HandshakeTimeout = handshakeTimeout;
        }
    }
}