// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Protocol.Plugins.Tests
{
    public class ConnectionTests
    {
        private static ITestOutputHelper _logger;
        private static ConnectAsyncTest _test;

        public ConnectionTests(ITestOutputHelper helper)
        {
            _logger = helper;
        }

        [Fact]
        public void Constructor_ThrowsForNullDispatcher()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                Assert.Throws<ArgumentNullException>(() => new Connection(
                    dispatcher: null,
                    sender: new Sender(TextWriter.Null, CancellationToken.None),
                    receiver: new Receiver(TextReader.Null, cancellationTokenSource),
                    options: ConnectionOptions.Default));
            }
        }

        [Fact]
        public void Constructor_ThrowsForNullSender()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                Assert.Throws<ArgumentNullException>(() => new Connection(
                    new MessageDispatcher(new RequestIdGenerator()),
                    sender: null,
                    receiver: new Receiver(TextReader.Null, cancellationTokenSource),
                    options: ConnectionOptions.Default));
            }
        }

        [Fact]
        public void Constructor_ThrowsForNullReceiver()
        {
            Assert.Throws<ArgumentNullException>(() => new Connection(
                new MessageDispatcher(new RequestIdGenerator()),
                new Sender(TextWriter.Null, CancellationToken.None),
                receiver: null,
                options: ConnectionOptions.Default));
        }

        [Fact]
        public void Constructor_ThrowsForNullOptions()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                Assert.Throws<ArgumentNullException>(() => new Connection(
                    new MessageDispatcher(new RequestIdGenerator()),
                    new Sender(TextWriter.Null, CancellationToken.None),
                    new Receiver(TextReader.Null, cancellationTokenSource),
                    options: null));
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfCancelled()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var sender = new Sender(TextWriter.Null, CancellationToken.None))
            using (var receiver = new Receiver(TextReader.Null, cancellationTokenSource))
            using (var dispatcher = new MessageDispatcher(new RequestIdGenerator()))
            using (var connection = new Connection(dispatcher, sender, receiver, ConnectionOptions.Default))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => connection.ConnectAsync(new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task ConnectAsync_HandshakeNegotiatesProtocolVersionForIdenticalVersionRanges()
        {
            _logger.WriteLine($"{nameof(ConnectAsync_HandshakeNegotiatesProtocolVersionForIdenticalVersionRanges)}:  Starting");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using (var test = new ConnectAsyncTest(ConnectionOptions.Default, ConnectionOptions.Default))
            {
                _test = test;

                await Task.WhenAll(
                    test.RemoteToLocalConnection.ConnectAsync(test.CancellationToken),
                    test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken));

                _logger.WriteLine($"{nameof(ConnectAsync_HandshakeNegotiatesProtocolVersionForIdenticalVersionRanges)}:  Asserting");

                Assert.NotNull(test.RemoteToLocalConnection.ProtocolVersion);
                Assert.NotNull(test.LocalToRemoteConnection.ProtocolVersion);
                Assert.Equal(test.RemoteToLocalConnection.ProtocolVersion, test.LocalToRemoteConnection.ProtocolVersion);

                _logger.WriteLine($"{nameof(ConnectAsync_HandshakeNegotiatesProtocolVersionForIdenticalVersionRanges)}:  Disposing");
            }

            _logger.WriteLine($"{nameof(ConnectAsync_HandshakeNegotiatesProtocolVersionForIdenticalVersionRanges)}:  Finished ({stopwatch.Elapsed})");
        }

        [Fact]
        public async Task ConnectAsync_HandshakeNegotiatesHighestCompatibleProtocolVersion()
        {
            _logger.WriteLine($"{nameof(ConnectAsync_HandshakeNegotiatesHighestCompatibleProtocolVersion)}:  Starting");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var localToRemoteOptions = new ConnectionOptions(new SemanticVersion(2, 0, 0), new SemanticVersion(1, 0, 0), TimeSpan.FromSeconds(10));
            var remoteToLocalOptions = new ConnectionOptions(new SemanticVersion(3, 0, 0), new SemanticVersion(1, 0, 0), TimeSpan.FromSeconds(10));

            using (var test = new ConnectAsyncTest(localToRemoteOptions, remoteToLocalOptions))
            {
                _test = test;

                await Task.WhenAll(
                    test.RemoteToLocalConnection.ConnectAsync(test.CancellationToken),
                    test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken));

                _logger.WriteLine($"{nameof(ConnectAsync_HandshakeNegotiatesHighestCompatibleProtocolVersion)}:  Asserting");

                Assert.NotNull(test.RemoteToLocalConnection.ProtocolVersion);
                Assert.NotNull(test.LocalToRemoteConnection.ProtocolVersion);
                Assert.Equal(test.RemoteToLocalConnection.ProtocolVersion, test.LocalToRemoteConnection.ProtocolVersion);
                Assert.Equal(test.RemoteToLocalConnection.ProtocolVersion, localToRemoteOptions.ProtocolVersion);

                _logger.WriteLine($"{nameof(ConnectAsync_HandshakeNegotiatesHighestCompatibleProtocolVersion)}:  Disposing");
            }

            _logger.WriteLine($"{nameof(ConnectAsync_HandshakeNegotiatesHighestCompatibleProtocolVersion)}:  Finished ({stopwatch.Elapsed})");
        }

        [Fact]
        public async Task ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfVersionRangesDoNotOverlap()
        {
            _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfVersionRangesDoNotOverlap)}:  Starting");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var localToRemoteOptions = new ConnectionOptions(new SemanticVersion(2, 0, 0), new SemanticVersion(2, 0, 0), TimeSpan.FromSeconds(10));
            var remoteToLocalOptions = new ConnectionOptions(new SemanticVersion(1, 0, 0), new SemanticVersion(1, 0, 0), TimeSpan.FromSeconds(10));

            using (var test = new ConnectAsyncTest(localToRemoteOptions, remoteToLocalOptions))
            {
                _test = test;

                try
                {
                    _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfVersionRangesDoNotOverlap)}:  Elapsed ({stopwatch.Elapsed})");

                    await Task.WhenAll(
                        test.RemoteToLocalConnection.ConnectAsync(test.CancellationToken),
                        test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken));
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfVersionRangesDoNotOverlap)}:  Elapsed ({stopwatch.Elapsed})");
                    _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfVersionRangesDoNotOverlap)}:  Asserting");

                    Assert.IsType<DivideByZeroException>(ex);
                }

                Assert.Null(test.RemoteToLocalConnection.ProtocolVersion);
                Assert.Null(test.LocalToRemoteConnection.ProtocolVersion);

                _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfVersionRangesDoNotOverlap)}:  Disposing");
            }

            _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfVersionRangesDoNotOverlap)}:  Finished ({stopwatch.Elapsed})");
        }

        [Fact]
        public async Task ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfOnePartyFailsToRespondWithinTimeoutPeriod()
        {
            _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfOnePartyFailsToRespondWithinTimeoutPeriod)}:  Starting");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var protocolVersion = new SemanticVersion(1, 0, 0);
            var localToRemoteOptions = new ConnectionOptions(protocolVersion, protocolVersion, TimeSpan.FromSeconds(1));
            var remoteToLocalOptions = new ConnectionOptions(protocolVersion, protocolVersion, TimeSpan.FromSeconds(10));

            using (var test = new ConnectAsyncTest(localToRemoteOptions, remoteToLocalOptions))
            {
                _test = test;

                try
                {
                    _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfOnePartyFailsToRespondWithinTimeoutPeriod)}:  Elapsed ({stopwatch.Elapsed})");

                    await test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfOnePartyFailsToRespondWithinTimeoutPeriod)}:  Elapsed ({stopwatch.Elapsed})");
                    _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfOnePartyFailsToRespondWithinTimeoutPeriod)}:  Asserting");

                    Assert.IsType<TaskCanceledException>(ex);
                }

                Assert.Null(test.LocalToRemoteConnection.ProtocolVersion);

                _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfOnePartyFailsToRespondWithinTimeoutPeriod)}:  Disposing");
            }

            _logger.WriteLine($"{nameof(ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfOnePartyFailsToRespondWithinTimeoutPeriod)}:  Finished ({stopwatch.Elapsed})");
        }

        private sealed class ConnectAsyncTest : IDisposable
        {
            private readonly CancellationTokenSource _remoteCancellationTokenSource;
            private readonly CancellationTokenSource _localCancellationTokenSource;
            private readonly CancellationTokenSource _combinedCancellationTokenSource;
            private readonly SimulatedIpc _simulatedIpc;
            private readonly Sender _remoteSender;
            private readonly Receiver _remoteReceiver;
            private readonly MessageDispatcher _remoteDispatcher;
            private readonly Sender _localSender;
            private readonly Receiver _localReceiver;
            private readonly MessageDispatcher _localDispatcher;
            private bool _isDisposed;

            internal Connection RemoteToLocalConnection;
            internal Connection LocalToRemoteConnection;
            internal CancellationToken CancellationToken;

            internal ConnectAsyncTest(ConnectionOptions localToRemoteOptions, ConnectionOptions remoteToLocalOptions)
            {
                _remoteCancellationTokenSource = new CancellationTokenSource();
                _localCancellationTokenSource = new CancellationTokenSource();
                _combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    _remoteCancellationTokenSource.Token, _localCancellationTokenSource.Token);
                _simulatedIpc = SimulatedIpc.Create(_combinedCancellationTokenSource.Token);
                _remoteSender = new Sender(_simulatedIpc.RemoteStandardOutputForRemote, _remoteCancellationTokenSource.Token);
                _remoteReceiver = new Receiver(_simulatedIpc.RemoteStandardInputForRemote, _remoteCancellationTokenSource);
                _remoteDispatcher = new MessageDispatcher(new RequestIdGenerator());
                LocalToRemoteConnection = new Connection(_remoteDispatcher, _remoteSender, _remoteReceiver, localToRemoteOptions);
                _localSender = new Sender(_simulatedIpc.RemoteStandardInputForLocal, _localCancellationTokenSource.Token);
                _localReceiver = new Receiver(_simulatedIpc.RemoteStandardOutputForLocal, _localCancellationTokenSource);
                _localDispatcher = new MessageDispatcher(new RequestIdGenerator());
                RemoteToLocalConnection = new Connection(_localDispatcher, _localSender, _localReceiver, remoteToLocalOptions);
                CancellationToken = _combinedCancellationTokenSource.Token;
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                _logger.WriteLine($"Cancelling {nameof(_combinedCancellationTokenSource)} (Elapsed: {stopwatch.Elapsed})");
                _combinedCancellationTokenSource.Cancel();

                _logger.WriteLine($"Disposing {nameof(_simulatedIpc)} (Elapsed: {stopwatch.Elapsed})");
                _simulatedIpc.Dispose();
                _logger.WriteLine($"Disposing {nameof(LocalToRemoteConnection)} (Elapsed: {stopwatch.Elapsed})");
                LocalToRemoteConnection.Dispose();
                _logger.WriteLine($"Disposing {nameof(RemoteToLocalConnection)} (Elapsed: {stopwatch.Elapsed})");
                RemoteToLocalConnection.Dispose();
                _logger.WriteLine($"Disposing {nameof(_remoteSender)} (Elapsed: {stopwatch.Elapsed})");
                _remoteSender.Dispose();
                _logger.WriteLine($"Disposing {nameof(_remoteReceiver)} (Elapsed: {stopwatch.Elapsed})");
                _remoteReceiver.Dispose();
                _logger.WriteLine($"Disposing {nameof(_remoteDispatcher)} (Elapsed: {stopwatch.Elapsed})");
                _remoteDispatcher.Dispose();
                _logger.WriteLine($"Disposing {nameof(_localSender)} (Elapsed: {stopwatch.Elapsed})");
                _localSender.Dispose();
                _logger.WriteLine($"Disposing {nameof(_localReceiver)} (Elapsed: {stopwatch.Elapsed})");
                _localReceiver.Dispose();
                _logger.WriteLine($"Disposing {nameof(_localDispatcher)} (Elapsed: {stopwatch.Elapsed})");
                _localDispatcher.Dispose();
                _logger.WriteLine($"Disposing {nameof(_combinedCancellationTokenSource)} (Elapsed: {stopwatch.Elapsed})");
                _combinedCancellationTokenSource.Dispose();
                _logger.WriteLine($"Disposing {nameof(_remoteCancellationTokenSource)} (Elapsed: {stopwatch.Elapsed})");
                _remoteCancellationTokenSource.Dispose();
                _logger.WriteLine($"Disposing {nameof(_localCancellationTokenSource)} (Elapsed: {stopwatch.Elapsed})");
                _localCancellationTokenSource.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }
    }
}