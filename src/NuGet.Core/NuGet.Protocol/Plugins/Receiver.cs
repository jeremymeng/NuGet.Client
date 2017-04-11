// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a unidirectional communications channel from a target.
    /// </summary>
    /// <remarks>
    /// Any public static members of this type are thread safe.
    /// Any instance members are not guaranteed to be thread safe.
    /// </remarks>
    public class Receiver : IReceiver
    {
        private readonly CancellationTokenSource _blockingReadCancellationTokenSource;
        private readonly TextReader _reader;
        private Task _receiveThread;
        private System.Collections.Generic.List<Message> _receivedMessages = new System.Collections.Generic.List<Message>();
        private bool _isDisposed;

        public event EventHandler<MessageEventArgs> MessageReceived;
        public event EventHandler<ProtocolErrorEventArgs> Faulted;

        public Receiver(TextReader reader, CancellationTokenSource blockingReadCancellationTokenSource)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (blockingReadCancellationTokenSource == null)
            {
                throw new ArgumentNullException(nameof(blockingReadCancellationTokenSource));
            }

            _reader = reader;
            _blockingReadCancellationTokenSource = blockingReadCancellationTokenSource;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_receiveThread != null)
            {
                throw new InvalidOperationException();
            }

            cancellationToken.ThrowIfCancellationRequested();

            _receiveThread = Task.Factory.StartNew(ReceiveThreadAsync,
                cancellationToken, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

            return Task.FromResult(0);
        }

        public async Task CloseAsync()
        {
            ThrowIfDisposed();

            using (_blockingReadCancellationTokenSource)
            {
                _blockingReadCancellationTokenSource.Cancel();

                if (_receiveThread != null)
                {
                    // Wait for the thread to exit.
                    await _receiveThread;
                }
            }

            _reader.Dispose();

            _isDisposed = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_isDisposed)
            {
                CloseAsync().GetAwaiter().GetResult();

                _isDisposed = true;
            }
        }

        private Task ReceiveThreadAsync()
        {
            try
            {
                while (true)
                {
                    _blockingReadCancellationTokenSource.Token.ThrowIfCancellationRequested();

                    try
                    {
                        using (var jsonReader = new JsonTextReader(_reader))
                        {
                            jsonReader.SupportMultipleContent = true;
                            jsonReader.CloseInput = false;

                            while (jsonReader.Read())
                            {
                                var message = JsonSerializationUtilities.Deserialize<Message>(jsonReader);

                                if (message == null)
                                {
                                    break;
                                }

                                _receivedMessages.Add(message);

                                FireMessageReceivedEventAndForget(message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (Faulted != null)
                        {
                            return Task.Factory.StartNew(() =>
                            {
                                var exception = new ProtocolException("protocol error", ex);
                                var eventArgs = new ProtocolErrorEventArgs(exception);

                                Faulted?.Invoke(this, eventArgs);
                            });
                        }

                        break;
                    }
                }
            }
            catch (Exception)
            {
            }

            return Task.FromResult(0);
        }

        private void FireMessageReceivedEventAndForget(Message message)
        {
            var messageReceived = MessageReceived;

            if (messageReceived != null)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        messageReceived(this, new MessageEventArgs(message));
                    }
                    catch (Exception)
                    {
                    }
                });
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(Receiver));
            }
        }
    }
}