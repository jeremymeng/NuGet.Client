// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    public class MessageDispatcher : IMessageDispatcher, IResponseHandler
    {
        private IConnection _connection;
        private readonly IIdGenerator _idGenerator;
        private bool _isDisposed;
        private readonly ConcurrentDictionary<string, RequestContext> _requestContexts;

        public IRequestHandlers RequestHandlers { get; }

        public MessageDispatcher(IIdGenerator idGenerator)
        {
            if (idGenerator == null)
            {
                throw new ArgumentNullException(nameof(idGenerator));
            }

            _idGenerator = idGenerator;

            RequestHandlers = new RequestHandlers();

            _requestContexts = new ConcurrentDictionary<string, RequestContext>();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public Task DispatchCancelAsync(Message request, CancellationToken cancellationToken)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchCancelAsync(connection, request, cancellationToken);
        }

        public Task DispatchFaultAsync(Message request, Fault fault, CancellationToken cancellationToken)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchFaultAsync(connection, request, fault, cancellationToken);
        }

        public Task DispatchProgressAsync(Message request, Progress progress, CancellationToken cancellationToken)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchProgressAsync(connection, request, progress, cancellationToken);
        }

        public Task<TInbound> DispatchRequestAsync<TOutbound, TInbound>(MessageMethod method, TOutbound payload, TimeSpan timeout, bool isKeepAlive, CancellationToken cancellationToken)
            where TOutbound : class
            where TInbound : class
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult<TInbound>(null);
            }

            return DispatchWithNewContextAsync<TOutbound, TInbound>(connection, MessageType.Request, method, payload, timeout, isKeepAlive, cancellationToken);
        }

        public Task DispatchResponseAsync<TOutbound>(Message request, TOutbound responsePayload, CancellationToken cancellationToken)
            where TOutbound : class
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchAsync(connection, MessageType.Response, request, responsePayload, cancellationToken: cancellationToken);
        }

        public void SetConnection(IConnection connection)
        {
            if (_connection == connection)
            {
                return;
            }

            if (_connection != null)
            {
                _connection.MessageReceived -= OnMessageReceived;
            }

            _connection = connection;

            if (_connection != null)
            {
                _connection.MessageReceived += OnMessageReceived;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_connection != null)
            {
                _connection.MessageReceived -= OnMessageReceived;
            }

            _isDisposed = true;
        }

        Task IResponseHandler.SendResponseAsync<TPayload>(Message request, TPayload responsePayload, CancellationToken cancellationToken)
        {
            return DispatchResponseAsync(request, responsePayload, cancellationToken);
        }

        private Message CreateMessage<TPayload>(MessageType type, MessageMethod method, TPayload payload)
        {
            var requestId = _idGenerator.GenerateUniqueId();
            var jsonPayload = payload == null ? null : JsonSerializationUtilities.FromObject(payload);

            return new Message(requestId, type, method, jsonPayload);
        }

        private async Task DispatchAsync<TOutgoing>(IConnection connection, MessageType type, Message request, TOutgoing responsePayload, CancellationToken cancellationToken)
            where TOutgoing : class
        {
            RequestContext requestContext;

            if (!_requestContexts.TryGetValue(request.RequestId, out requestContext))
            {
                return;
            }

            var jsonPayload = JsonSerializationUtilities.FromObject(responsePayload);
            var message = new Message(request.RequestId, type, request.Method, jsonPayload);

            try
            {
                await connection.SendAsync(message, cancellationToken);
            }
            finally
            {
                RemoveRequestContext(request.RequestId);
            }
        }

        private async Task DispatchCancelAsync(IConnection connection, Message request, CancellationToken cancellationToken)
        {
            var message = new Message(request.RequestId, MessageType.Cancel, request.Method);

            await DispatchWithExistingContextAsync(connection, message, cancellationToken);
        }

        private async Task DispatchFaultAsync(IConnection connection, Message request, Fault fault, CancellationToken cancellationToken)
        {
            Message message;

            var jsonPayload = JsonSerializationUtilities.FromObject(fault);

            if (request == null)
            {
                var requestId = _idGenerator.GenerateUniqueId();

                message = new Message(requestId, MessageType.Fault, MessageMethod.None, jsonPayload);

                await connection.SendAsync(message, cancellationToken);
            }
            else
            {
                message = new Message(request.RequestId, MessageType.Fault, request.Method, jsonPayload);

                await DispatchWithExistingContextAsync(connection, message, cancellationToken);
            }
        }

        private async Task DispatchProgressAsync(IConnection connection, Message request, Progress progress, CancellationToken cancellationToken)
        {
            var jsonPayload = JsonSerializationUtilities.FromObject(progress);

            var message = new Message(request.RequestId, MessageType.Progress, request.Method, jsonPayload);

            await DispatchWithExistingContextAsync(connection, message, cancellationToken);
        }

        private async Task DispatchWithoutContextAsync<TOutgoing>(IConnection connection, MessageType type, MessageMethod method, TOutgoing payload, CancellationToken cancellationToken)
            where TOutgoing : class
        {
            var message = CreateMessage(type, method, payload);

            await connection.SendAsync(message, cancellationToken);
        }

        private async Task DispatchWithExistingContextAsync(IConnection connection, Message response, CancellationToken cancellationToken)
        {
            RequestContext requestContext;

            if (!_requestContexts.TryGetValue(response.RequestId, out requestContext))
            {
                throw new InvalidOperationException();
            }

            await connection.SendAsync(response, cancellationToken);
        }

        private async Task<TIncoming> DispatchWithNewContextAsync<TOutgoing, TIncoming>(IConnection connection, MessageType type, MessageMethod method, TOutgoing payload, TimeSpan? timeout, bool isKeepAlive, CancellationToken cancellationToken)
            where TOutgoing : class
            where TIncoming : class
        {
            var message = CreateMessage(type, method, payload);
            var requestContext = CreateRequestContext<TIncoming>(message, timeout, isKeepAlive, cancellationToken);

            _requestContexts.TryAdd(message.RequestId, requestContext);

            switch (type)
            {
                case MessageType.Request:
                case MessageType.Response:
                case MessageType.Fault:
                    try
                    {
                        await connection.SendAsync(message, cancellationToken);

                        return await requestContext.CompletionTask;
                    }
                    finally
                    {
                        RemoveRequestContext(message.RequestId);
                    }

                default:
                    break;
            }

            return null;
        }

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return;
            }

            RequestContext requestContext;

            if (_requestContexts.TryGetValue(e.Message.RequestId, out requestContext))
            {
                switch (e.Message.Type)
                {
                    case MessageType.Response:
                        requestContext.HandleResponse(e.Message);
                        break;

                    case MessageType.Progress:
                        requestContext.HandleProgress(e.Message);
                        break;

                    case MessageType.Fault:
                        requestContext.HandleFault(e.Message);
                        break;

                    case MessageType.Cancel:
                        requestContext.HandleCancel();
                        break;

                    default:
                        throw new ArgumentException(); // dtivel:  TODO
                }

                return;
            }

            switch (e.Message.Type)
            {
                case MessageType.Request:
                    HandleInboundRequest(connection, e.Message);
                    break;

                case MessageType.Progress:
                    HandleInboundProgress(e.Message);
                    break;
            }
        }

        private void HandleInboundRequest(IConnection connection, Message message)
        {
            var requestHandler = GetInboundRequestHandler(message.Method);
            var requestContext = CreateRequestContext<HandshakeResponse>(
                message,
                connection.Options.HandshakeTimeout,
                isKeepAlive: false,
                cancellationToken: requestHandler.CancellationToken);

            _requestContexts.TryAdd(message.RequestId, requestContext);

            requestContext.BeginResponseAsync(message, requestHandler, this);
        }

        private void HandleInboundProgress(Message message)
        {
            var requestHandler = GetInboundRequestHandler(message.Method);
            var requestContext = GetRequestContext(message.RequestId);

            requestContext.BeginProgressAsync(message, requestHandler);
        }

        private IRequestHandler GetInboundRequestHandler(MessageMethod method)
        {
            IRequestHandler handler;

            if (!RequestHandlers.TryGet(method, out handler))
            {
                throw new ArgumentException(); // dtivel:  improve exception and message
            }

            return handler;
        }

        private RequestContext GetRequestContext(string requestId)
        {
            RequestContext requestContext;

            if (!_requestContexts.TryGetValue(requestId, out requestContext))
            {
                throw new ArgumentException(); // dtivel:  protocol error
            }

            return requestContext;
        }

        private void RemoveRequestContext(string requestId)
        {
            RequestContext requestContext;

            _requestContexts.TryRemove(requestId, out requestContext);
        }

        private static RequestContext<TOutgoing> CreateRequestContext<TOutgoing>(Message message, TimeSpan? timeout, bool isKeepAlive, CancellationToken cancellationToken)
        {
            return new RequestContext<TOutgoing>(
                message.RequestId,
                timeout,
                isKeepAlive,
                cancellationToken);
        }

        private abstract class RequestContext : IDisposable
        {
            internal string RequestId { get; }

            public RequestContext(string requestId)
            {
                RequestId = requestId;
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }

            protected abstract void Dispose(bool disposing);

            public abstract void BeginProgressAsync(Message message, IRequestHandler requestHandler);
            public abstract void HandleProgress(Message message);
            public abstract void BeginResponseAsync(Message message, IRequestHandler requestHandler, IResponseHandler responseHandler);
            public abstract void HandleResponse(Message message);
            public abstract void HandleFault(Message message);
            public abstract void HandleCancel();
        }

        private sealed class RequestContext<TResult> : RequestContext
        {
            private readonly CancellationTokenSource _timeoutCancellationTokenSource;
            private readonly CancellationTokenSource _combinedCancellationTokenSource;
            private bool _isDisposed;
            private bool _isKeepAlive;
            private readonly TimeSpan? _timeout;
            private Task _responseTask;
            private readonly TaskCompletionSource<TResult> _taskCompletionSource;

            internal Task<TResult> CompletionTask => _taskCompletionSource.Task;

            internal RequestContext(string requestId, TimeSpan? timeout, bool isKeepAlive, CancellationToken cancellationToken)
                : base(requestId)
            {
                _taskCompletionSource = new TaskCompletionSource<TResult>();
                _timeout = timeout;
                _isKeepAlive = isKeepAlive;

                if (timeout.HasValue)
                {
                    _timeoutCancellationTokenSource = new CancellationTokenSource(timeout.Value);
                    _combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_timeoutCancellationTokenSource.Token, cancellationToken);
                }
                else
                {
                    _combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }

                _combinedCancellationTokenSource.Token.Register(Close);
            }

            public override void BeginProgressAsync(Message message, IRequestHandler requestHandler)
            {
                Task.Factory.StartNew(
                    () => requestHandler.HandleProgressAsync(message, _combinedCancellationTokenSource.Token),
                        _combinedCancellationTokenSource.Token);
            }

            public override void HandleProgress(Message message)
            {
                var payload = MessageUtilities.DeserializePayload<Progress>(message);

                if (_timeout.HasValue && _isKeepAlive)
                {
                    _timeoutCancellationTokenSource.CancelAfter(_timeout.Value);
                }
            }

            public override void BeginResponseAsync(Message message, IRequestHandler requestHandler, IResponseHandler responseHandler)
            {
                _responseTask = Task.Factory.StartNew(
                    () => requestHandler.HandleRequestAsync(message, responseHandler, _combinedCancellationTokenSource.Token),
                        _combinedCancellationTokenSource.Token);
            }

            public override void HandleResponse(Message message)
            {
                var payload = MessageUtilities.DeserializePayload<TResult>(message);

                try
                {
                    _taskCompletionSource.SetResult(payload);
                }
                catch (Exception ex)
                {
                    _taskCompletionSource.TrySetException(ex);
                }
            }

            public override void HandleFault(Message message)
            {
                //_taskCompletionSource.TrySetException(ex);
                // dtivel:  TODO
            }

            public override void HandleCancel()
            {
                _combinedCancellationTokenSource.Cancel();
                // dtivel:  TODO send response/fault
            }

            protected override void Dispose(bool disposing)
            {
                if (_isDisposed)
                {
                    return;
                }

                if (disposing)
                {
                    Close();
                }

                _isDisposed = true;
            }

            private void Close()
            {
                _taskCompletionSource.TrySetCanceled();

                if (_timeoutCancellationTokenSource != null)
                {
                    using (_timeoutCancellationTokenSource)
                    {
                        _timeoutCancellationTokenSource.Cancel();
                    }
                }

                using (_combinedCancellationTokenSource)
                {
                    _combinedCancellationTokenSource.Cancel();
                }
            }
        }
    }
}