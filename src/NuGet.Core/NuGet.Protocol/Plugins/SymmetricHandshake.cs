// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGet.Protocol.Plugins
{
    public class SymmetricHandshake : IRequestHandler, IDisposable
    {
        private readonly IConnection _connection;
        private readonly HandshakeResponse _handshakeFailedResponse;
        private readonly TimeSpan _handshakeTimeout;
        private bool _isDisposed;
        private readonly SemanticVersion _minimumProtocolVersion;
        private HandshakeRequest _outboundHandshakeRequest;
        private readonly SemanticVersion _protocolVersion;
        private TaskCompletionSource<int> _responseSentTaskCompletionSource;
        private readonly CancellationTokenSource _timeoutCancellationTokenSource;

        public CancellationToken CancellationToken { get; }

        public SymmetricHandshake(IConnection connection, TimeSpan handshakeTimeout, SemanticVersion protocolVersion, SemanticVersion minimumProtocolVersion)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (!TimeoutUtilities.IsValid(handshakeTimeout))
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeTimeout));
            }

            if (protocolVersion == null)
            {
                throw new ArgumentNullException(nameof(protocolVersion));
            }

            if (minimumProtocolVersion == null)
            {
                throw new ArgumentNullException(nameof(minimumProtocolVersion));
            }

            _connection = connection;
            _handshakeTimeout = handshakeTimeout;
            _protocolVersion = protocolVersion;
            _minimumProtocolVersion = minimumProtocolVersion;
            _handshakeFailedResponse = new HandshakeResponse(MessageResponseCode.Error, protocolVersion: null);
            _responseSentTaskCompletionSource = new TaskCompletionSource<int>();
            _timeoutCancellationTokenSource = new CancellationTokenSource(handshakeTimeout);

            _timeoutCancellationTokenSource.Token.Register(() =>
            {
                _responseSentTaskCompletionSource.TrySetCanceled();
            });

            CancellationToken = _timeoutCancellationTokenSource.Token;

            _connection.MessageDispatcher.RequestHandlers.TryAdd(MessageMethod.Handshake, this);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task<SemanticVersion> HandshakeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _outboundHandshakeRequest = new HandshakeRequest(_protocolVersion, _minimumProtocolVersion);

            var response = await _connection.SendRequestAndReceiveResponseAsync<HandshakeRequest, HandshakeResponse>(
                MessageMethod.Handshake,
                _outboundHandshakeRequest,
                _handshakeTimeout,
                isKeepAlive: false,
                cancellationToken: cancellationToken);

            if (response.ResponseCode == MessageResponseCode.Success)
            {
                if (IsSupportedVersion(response.ProtocolVersion))
                {
                    await _responseSentTaskCompletionSource.Task;

                    return response.ProtocolVersion;
                }
            }

            await _responseSentTaskCompletionSource.Task;

            return null;
        }

        public Task HandleCancelAsync(Message message, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task HandleProgressAsync(Message message, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task HandleRequestAsync(Message request, IResponseHandler responseHandler, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (responseHandler == null)
            {
                throw new ArgumentNullException(nameof(responseHandler));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var response = _handshakeFailedResponse;
            var handshakeRequest = MessageUtilities.DeserializePayload<HandshakeRequest>(request);

            if (handshakeRequest != null)
            {
                if (!(handshakeRequest.MinimumProtocolVersion > handshakeRequest.ProtocolVersion ||
                    handshakeRequest.ProtocolVersion < _minimumProtocolVersion ||
                    handshakeRequest.MinimumProtocolVersion > _protocolVersion))
                {
                    SemanticVersion negotiatedProtocolVersion;

                    if (_protocolVersion <= handshakeRequest.ProtocolVersion)
                    {
                        negotiatedProtocolVersion = _protocolVersion;
                    }
                    else
                    {
                        negotiatedProtocolVersion = handshakeRequest.ProtocolVersion;
                    }

                    response = new HandshakeResponse(MessageResponseCode.Success, negotiatedProtocolVersion);
                }
            }

            await responseHandler.SendResponseAsync(request, response, cancellationToken)
                .ContinueWith(task => _responseSentTaskCompletionSource.TrySetResult(0));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                _connection.MessageDispatcher.RequestHandlers.TryRemove(MessageMethod.Handshake);
            }

            _isDisposed = true;
        }

        private bool IsSupportedVersion(SemanticVersion requestedProtocolVersion)
        {
            return _minimumProtocolVersion <= requestedProtocolVersion && requestedProtocolVersion <= _protocolVersion;
        }
    }
}