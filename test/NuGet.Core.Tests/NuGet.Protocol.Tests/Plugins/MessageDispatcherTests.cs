// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class MessageDispatcherTests
    {
        private readonly Mock<IConnection> _connection = new Mock<IConnection>(MockBehavior.Strict);
        private readonly ConstantIdGenerator _idGenerator = new ConstantIdGenerator();
        private readonly MessageMethod _method = MessageMethod.None;
        private readonly TimeSpan _timeout = TimeSpan.FromHours(1);

        [Fact]
        public void Constructor_ThrowsForNullIdGenerator()
        {
            Assert.Throws<ArgumentNullException>(() => new MessageDispatcher(idGenerator: null));
        }

        [Fact]
        public void Constructor_InitializesProperty()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            {
                Assert.NotNull(dispatcher.RequestHandlers);
            }
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            {
                dispatcher.Dispose();
                dispatcher.Dispose();
            }
        }

        [Fact]
        public void SetConnection_SubscribesToMessageReceived()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            {
                var spy = new ConnectionEventRegistrationSpy();

                Assert.Equal(0, spy.MessageReceivedAddCount);

                dispatcher.SetConnection(spy);

                Assert.Equal(1, spy.MessageReceivedAddCount);
            }
        }

        [Fact]
        public void SetConnection_UnsubscribesFromMessageReceived()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            {
                var firstSpy = new ConnectionEventRegistrationSpy();

                dispatcher.SetConnection(firstSpy);

                var secondSpy = new ConnectionEventRegistrationSpy();

                dispatcher.SetConnection(secondSpy);

                Assert.Equal(1, firstSpy.MessageReceivedRemoveCount);
                Assert.Equal(1, secondSpy.MessageReceivedAddCount);
            }
        }

        [Fact]
        public void SetConnection_IsIdempotent()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            {
                var spy = new ConnectionEventRegistrationSpy();

                dispatcher.SetConnection(spy);
                dispatcher.SetConnection(spy);
                dispatcher.SetConnection(spy);

                Assert.Equal(1, spy.MessageReceivedAddCount);
                Assert.Equal(0, spy.MessageReceivedRemoveCount);
            }
        }

        [Fact]
        public async Task DispatchCancelAsync_NoOpsIfNoConnection()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            {
                var request = new Message(_idGenerator.Id, MessageType.Request, _method);

                await dispatcher.DispatchCancelAsync(request, CancellationToken.None);
            }
        }

        [Fact]
        public async Task DispatchCancelAsync_ThrowsWithoutAssociatedRequestId()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            using (var connection = new ConnectionMock())
            using (var messageReceivedEvent = new ManualResetEventSlim(initialState: false))
            {
                dispatcher.SetConnection(connection);

                var request = new Message(_idGenerator.Id, MessageType.Request, _method);

                Message message = null;

                connection.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    messageReceivedEvent.Set();
                };

                await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchCancelAsync(request, CancellationToken.None));
            }
        }

        [Fact]
        public async Task DispatchCancelAsync_SendsProgressWithAssociatedRequestId()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            using (var connection = new ConnectionMock())
            using (var messageReceivedEvent = new ManualResetEventSlim(initialState: false))
            {
                dispatcher.SetConnection(connection);

                var request = new Message(_idGenerator.Id, MessageType.Request, MessageMethod.DownloadPackage);

                var requestTask = dispatcher.DispatchRequestAsync<Request, Response>(
                    request.Method,
                    new Request(),
                    _timeout,
                    isKeepAlive: false,
                    cancellationToken: CancellationToken.None);

                Message message = null;

                connection.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    messageReceivedEvent.Set();
                };

                await dispatcher.DispatchCancelAsync(request, CancellationToken.None);

                Assert.NotNull(message);
                Assert.Equal(_idGenerator.Id, message.RequestId);
                Assert.Equal(MessageType.Cancel, message.Type);
                Assert.Equal(request.Method, message.Method);
                Assert.Null(message.Payload);
            }
        }

        [Fact]
        public async Task DispatchFaultAsync_NoOpsIfNoConnection()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            {
                await dispatcher.DispatchFaultAsync(
                    request: null,
                    fault: new Fault(message: "a"),
                    cancellationToken: CancellationToken.None);
            }
        }

        [Fact]
        public async Task DispatchFaultAsync_SendsFaultWithoutAssociatedRequestId()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            using (var connection = new ConnectionMock())
            using (var messageReceivedEvent = new ManualResetEventSlim(initialState: false))
            {
                dispatcher.SetConnection(connection);

                var fault = new Fault(message: "a");

                Message message = null;

                connection.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    messageReceivedEvent.Set();
                };

                await dispatcher.DispatchFaultAsync(
                    request: null,
                    fault: fault,
                    cancellationToken: CancellationToken.None);

                Assert.NotNull(message);
                Assert.Equal(_idGenerator.Id, message.RequestId);
                Assert.Equal(MessageType.Fault, message.Type);
                Assert.Equal(MessageMethod.None, message.Method);
                Assert.Equal("{\"Message\":\"a\"}", message.Payload.ToString(Formatting.None));
            }
        }

        [Fact]
        public async Task DispatchFaultAsync_SendsFaultWithAssociatedRequestId()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            using (var connection = new ConnectionMock())
            using (var messageReceivedEvent = new ManualResetEventSlim(initialState: false))
            {
                dispatcher.SetConnection(connection);

                var request = new Message(
                    _idGenerator.Id,
                    MessageType.Request,
                    MessageMethod.DownloadPackage,
                    JObject.FromObject(new Request()));

                var requestTask = dispatcher.DispatchRequestAsync<Request, Response>(
                    request.Method,
                    new Request(),
                    _timeout,
                    isKeepAlive: false,
                    cancellationToken: CancellationToken.None);

                var fault = new Fault(message: "a");

                Message message = null;

                connection.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    messageReceivedEvent.Set();
                };

                await dispatcher.DispatchFaultAsync(request, fault, CancellationToken.None);

                Assert.NotNull(message);
                Assert.Equal(_idGenerator.Id, message.RequestId);
                Assert.Equal(MessageType.Fault, message.Type);
                Assert.Equal(request.Method, message.Method);
                Assert.Equal("{\"Message\":\"a\"}", message.Payload.ToString(Formatting.None));
            }
        }

        [Fact]
        public async Task DispatchProgressAsync_NoOpsIfNoConnection()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            {
                var payload = JObject.FromObject(new Request());
                var request = new Message(_idGenerator.Id, MessageType.Request, _method, payload);

                await dispatcher.DispatchProgressAsync(
                    request,
                    new Progress(progressPercent: 0.5),
                    CancellationToken.None);
            }
        }

        [Fact]
        public async Task DispatchProgressAsync_ThrowsWithoutAssociatedRequestId()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            using (var connection = new ConnectionMock())
            using (var messageReceivedEvent = new ManualResetEventSlim(initialState: false))
            {
                dispatcher.SetConnection(connection);

                var payload = JObject.FromObject(new Request());
                var request = new Message(_idGenerator.Id, MessageType.Request, _method, payload);
                var progress = new Progress(progressPercent: 0.5);

                Message message = null;

                connection.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    messageReceivedEvent.Set();
                };

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => dispatcher.DispatchProgressAsync(request, progress, CancellationToken.None));
            }
        }

        [Fact]
        public async Task DispatchProgressAsync_SendsProgressWithAssociatedRequestId()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            using (var connection = new ConnectionMock())
            using (var messageReceivedEvent = new ManualResetEventSlim(initialState: false))
            {
                dispatcher.SetConnection(connection);

                var payload = JObject.FromObject(new Request());
                var request = new Message(_idGenerator.Id, MessageType.Request, MessageMethod.DownloadPackage, payload);
                var progress = new Progress(progressPercent: 0.5);

                var requestTask = dispatcher.DispatchRequestAsync<Request, Response>(
                    request.Method,
                    new Request(),
                    _timeout,
                    isKeepAlive: false,
                    cancellationToken: CancellationToken.None);

                Message message = null;

                connection.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    messageReceivedEvent.Set();
                };

                await dispatcher.DispatchProgressAsync(request, progress, CancellationToken.None);

                Assert.NotNull(message);
                Assert.Equal(_idGenerator.Id, message.RequestId);
                Assert.Equal(MessageType.Progress, message.Type);
                Assert.Equal(request.Method, message.Method);
                Assert.Equal("{\"ProgressPercent\":0.5}", message.Payload.ToString(Formatting.None));
            }
        }

        [Fact]
        public async Task DispatchRequestAsync_NoOpsIfNoConnection()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            {
                var version = new SemanticVersion(major: 1, minor: 0, patch: 0);
                var result = await dispatcher.DispatchRequestAsync<HandshakeRequest, HandshakeResponse>(
                    MessageMethod.Handshake,
                    new HandshakeRequest(version, version),
                    _timeout,
                    isKeepAlive: false,
                    cancellationToken: CancellationToken.None);

                Assert.Null(result);
            }
        }

        [Fact]
        public async Task DispatchRequestAsync_ReturnsResponse()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            using (var connection = new ConnectionMock())
            {
                var response = new Message(
                    _idGenerator.Id,
                    MessageType.Response,
                    _method,
                    JObject.FromObject(new Response()));

                dispatcher.SetConnection(connection);

                var requestTask = dispatcher.DispatchRequestAsync<Request, Response>(
                    _method,
                    new Request(),
                    _timeout,
                    isKeepAlive: false,
                    cancellationToken: CancellationToken.None);

                var responseTask = connection.SendAsync(response, CancellationToken.None);

                await Task.WhenAll(requestTask, responseTask);

                Assert.IsType<Response>(requestTask.Result);
            }
        }

        [Fact]
        public async Task DispatchResponseAsync_NoOpsIfNoConnection()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            {
                var version = new SemanticVersion(major: 1, minor: 0, patch: 0);
                var payload = JObject.FromObject(new HandshakeRequest(version, version));
                var request = new Message(_idGenerator.Id, MessageType.Request, _method, payload);

                await dispatcher.DispatchResponseAsync(
                    request,
                    new HandshakeResponse(MessageResponseCode.Error, protocolVersion: null),
                    CancellationToken.None);
            }
        }

        [Fact]
        public async Task DispatchResponseAsync_ReturnsResponse()
        {
            using (var dispatcher = new MessageDispatcher(_idGenerator))
            using (var connection = new ConnectionMock())
            {
                dispatcher.SetConnection(connection);

                var requestTask = dispatcher.DispatchRequestAsync<Request, Response>(
                    _method,
                    new Request(),
                    _timeout,
                    isKeepAlive: false,
                    cancellationToken: CancellationToken.None);

                var request = new Message(
                    _idGenerator.Id,
                    MessageType.Request,
                    _method,
                    JObject.FromObject(new Request()));

                var responseTask = dispatcher.DispatchResponseAsync(request, new Response(), CancellationToken.None);

                await Task.WhenAll(requestTask, responseTask);

                Assert.IsType<Response>(requestTask.Result);
            }
        }

        private sealed class ConstantIdGenerator : IIdGenerator
        {
            internal string Id { get; }

            internal ConstantIdGenerator()
            {
                Id = "0";
            }

            public string GenerateUniqueId()
            {
                return Id;
            }
        }

        private sealed class ConnectionMock : IConnection, IDisposable
        {
            private readonly ManualResetEventSlim _event;
            private bool _isDisposed;

            public IMessageDispatcher MessageDispatcher => throw new NotImplementedException();
            public ConnectionOptions Options => throw new NotImplementedException();
            public SemanticVersion ProtocolVersion => throw new NotImplementedException();

#pragma warning disable 67
            public event EventHandler<MessageEventArgs> Faulted;
#pragma warning restore 67
            public event EventHandler<MessageEventArgs> MessageReceived;

            public ConnectionMock()
            {
                _event = new ManualResetEventSlim(initialState: false);
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    _event.Dispose();
                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            public Task SendAsync(Message message, CancellationToken cancellationToken)
            {
                switch (message.Type)
                {
                    case MessageType.Fault:
                        MessageReceived?.Invoke(this, new MessageEventArgs(message));
                        break;

                    case MessageType.Request:
                        _event.Set();
                        break;

                    case MessageType.Cancel:
                    case MessageType.Progress:
                    case MessageType.Response:
                        _event.Wait();
                        MessageReceived?.Invoke(this, new MessageEventArgs(message));
                        break;
                }

                return Task.FromResult(0);
            }

            public Task<TInbound> SendRequestAndReceiveResponseAsync<TOutbound, TInbound>(MessageMethod method, TOutbound payload, TimeSpan timeout, bool isKeepAlive, CancellationToken cancellationToken)
                where TOutbound : class
                where TInbound : class
            {
                throw new NotImplementedException();
            }
        }

        private sealed class ConnectionEventRegistrationSpy : IConnection
        {
            internal int FaultedAddCount { get; set; }
            internal int FaultedRemoveCount { get; set; }
            internal int MessageReceivedAddCount { get; set; }
            internal int MessageReceivedRemoveCount { get; set; }

            public IMessageDispatcher MessageDispatcher => throw new NotImplementedException();
            public ConnectionOptions Options => throw new NotImplementedException();
            public SemanticVersion ProtocolVersion => throw new NotImplementedException();

            public event EventHandler<MessageEventArgs> Faulted
            {
                add
                {
                    ++FaultedAddCount;
                }
                remove
                {
                    ++FaultedRemoveCount;
                }
            }

            public event EventHandler<MessageEventArgs> MessageReceived
            {
                add
                {
                    ++MessageReceivedAddCount;
                }
                remove
                {
                    ++MessageReceivedRemoveCount;
                }
            }

            public Task SendAsync(Message message, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TInbound> SendRequestAndReceiveResponseAsync<TOutbound, TInbound>(MessageMethod method, TOutbound payload, TimeSpan timeout, bool isKeepAlive, CancellationToken cancellationToken)
                where TOutbound : class
                where TInbound : class
            {
                throw new NotImplementedException();
            }
        }

        private sealed class Request
        {
        }

        private sealed class Response
        {
        }
    }
}