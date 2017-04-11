// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a unidirectional communications channel to a target.
    /// </summary>
    /// <remarks>
    /// Any public static members of this type are thread safe.
    /// Any instance members are not guaranteed to be thread safe.
    /// </remarks>
    public class Sender : ISender
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;
        private readonly BlockingCollection<QueuedMessage> _sendQueue;
        private Task<Task> _sendThread;
        private System.Collections.Generic.List<Message> _sentMessages = new System.Collections.Generic.List<Message>();
        private readonly TextWriter _textWriter;

        public Sender(TextWriter writer, CancellationToken globalCancellationToken)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            _textWriter = writer;
            _sendQueue = new BlockingCollection<QueuedMessage>();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(globalCancellationToken);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_sendThread != null)
            {
                throw new InvalidOperationException();
            }

            cancellationToken.ThrowIfCancellationRequested();

            _sendThread = Task.Factory.StartNew(SendThreadAsync, cancellationToken,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

            return Task.FromResult(0);
        }

        public async Task SendAsync(Message message, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (message == null)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(message));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var queuedMessage = new QueuedMessage(message);

            cancellationToken.Register(() => queuedMessage.CompletionSource.TrySetCanceled());

            _sendQueue.Add(queuedMessage);

            await queuedMessage.CompletionSource.Task;
        }

        public async Task CloseAsync()
        {
            ThrowIfDisposed();

            _sendQueue.CompleteAdding();

            using (_cancellationTokenSource)
            {
                _cancellationTokenSource.Cancel();
            }

            if (_sendThread != null)
            {
                // Wait for the thread to exit.
                await _sendThread;
            }

            foreach (var queuedMessage in _sendQueue)
            {
                queuedMessage.CompletionSource.TrySetCanceled();
            }

            _sendQueue.Dispose();
            _textWriter.Dispose();

            _isDisposed = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                CloseAsync().GetAwaiter().GetResult();

                _isDisposed = true;
            }
        }

        private Task SendThreadAsync()
        {
            try
            {
                using (var jsonWriter = new JsonTextWriter(_textWriter))
                {
                    jsonWriter.CloseOutput = false;

                    foreach (var queuedMessage in _sendQueue.GetConsumingEnumerable(_cancellationTokenSource.Token))
                    {
                        try
                        {
                            _sentMessages.Add(queuedMessage.Message);

                            JsonSerializationUtilities.Serialize(jsonWriter, queuedMessage.Message);

                            _textWriter.WriteLine();
                            _textWriter.Flush();

                            queuedMessage.CompletionSource.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            queuedMessage.CompletionSource.TrySetException(ex);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return Task.FromResult(0);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(Sender));
            }
        }

        private sealed class QueuedMessage
        {
            internal TaskCompletionSource<bool> CompletionSource { get; }
            internal Message Message { get; }

            internal QueuedMessage(Message message)
            {
                Message = message;
                CompletionSource = new TaskCompletionSource<bool>();
            }
        }
    }
}