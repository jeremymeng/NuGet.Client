// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class ReceiverTests
    {
        [Fact]
        public void Constructor_ThrowsForNullReader()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new Receiver(reader: null, blockingReadCancellationTokenSource: cancellationTokenSource));

                Assert.Equal("reader", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_ThrowsForNullCancellationTokenSource()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new Receiver(TextReader.Null, blockingReadCancellationTokenSource: null));

            Assert.Equal("blockingReadCancellationTokenSource", exception.ParamName);
        }

        [Fact]
        public void Constructor_AllowsTextReaderNull()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                new Receiver(TextReader.Null, cancellationTokenSource);
            }
        }

        [Fact]
        public void Constructor_DoesNotThrowForCancelledTokenSource()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                cancellationTokenSource.Cancel();

                new Receiver(TextReader.Null, cancellationTokenSource);
            }
        }

        [Fact]
        public void Dispose_ClosesUnderlyingStream()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                Assert.True(stream.CanSeek);
                Assert.True(stream.CanRead);
                Assert.True(stream.CanWrite);

                var receiver = new Receiver(reader, cancellationTokenSource);

                receiver.Dispose();

                Assert.False(stream.CanSeek);
                Assert.False(stream.CanRead);
                Assert.False(stream.CanWrite);
            }
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(TextReader.Null, cancellationTokenSource))
            {
                receiver.Dispose();
                receiver.Dispose();
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfDisposed()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var receiver = new Receiver(TextReader.Null, cancellationTokenSource);

                receiver.Dispose();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.ConnectAsync(CancellationToken.None));

                Assert.Equal(nameof(Receiver), exception.ObjectName);
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfAlreadyConnected()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(TextReader.Null, cancellationTokenSource))
            {
                await receiver.ConnectAsync(CancellationToken.None);

                await Assert.ThrowsAsync<InvalidOperationException>(() => receiver.ConnectAsync(CancellationToken.None));
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfCancelled()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(TextReader.Null, cancellationTokenSource))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => receiver.ConnectAsync(new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task MessageReceived_RaisedForSingleMessageWithNonBlockingStream()
        {
            var json = "{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\"}";
            var requestId = "a";
            var type = MessageType.Response;
            var method = MessageMethod.None;

            using (var reader = new StringReader(json))
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(reader, cancellationTokenSource))
            {
                var completionSource = new TaskCompletionSource<bool>();
                Message message = null;

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    completionSource.SetResult(true);
                };

                await receiver.ConnectAsync(CancellationToken.None);

                await completionSource.Task;

                Assert.Equal(requestId, message.RequestId);
                Assert.Equal(type, message.Type);
                Assert.Equal(method, message.Method);
                Assert.Null(message.Payload);
            }
        }

        [Theory]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\"}", "a", MessageType.Response, MessageMethod.None, null)]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\",\"Payload\":null}", "a", MessageType.Response, MessageMethod.None, null)]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\",\"Payload\":{\"d\":\"e\"}}", "a", MessageType.Response, MessageMethod.None, "{\"d\":\"e\"}")]
        public async Task MessageReceived_RaisedForSingleMessageWithBlockingStream(string json, string requestId, MessageType type, MessageMethod method, string payload)
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var stream = new MemoryStream())
            using (var readWriteSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1))
            using (var dataWrittenEvent = new ManualResetEventSlim(initialState: false))
            using (var outboundStream = new SimulatedWriteOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var inboundStream = new SimulatedReadOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var reader = new SimulatedStreamReader(inboundStream))
            using (var receiver = new Receiver(reader, cancellationTokenSource))
            {
                var completionSource = new TaskCompletionSource<bool>();
                Message message = null;

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    completionSource.SetResult(true);
                };

                var bytes = Encoding.UTF8.GetBytes(json);

                outboundStream.Write(bytes, offset: 0, count: bytes.Length);

                await receiver.ConnectAsync(CancellationToken.None);

                await completionSource.Task;

                Assert.Equal(requestId, message.RequestId);
                Assert.Equal(type, message.Type);
                Assert.Equal(method, message.Method);
                Assert.Equal(payload, message.Payload?.ToString(Formatting.None));
            }
        }

        [Fact]
        public async Task MessageReceived_RaisedForSingleMessageInChunksWithBlockingStream()
        {
            var json = "{\"RequestId\":\"a\",\"Type\":\"Progress\",\"Method\":\"None\",\"Payload\":{\"d\":\"e\"}}";
            var requestId = "a";
            var type = MessageType.Progress;
            var method = MessageMethod.None;
            var payload = "{\"d\":\"e\"}";

            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var stream = new MemoryStream())
            using (var readWriteSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1))
            using (var dataWrittenEvent = new ManualResetEventSlim(initialState: false))
            using (var outboundStream = new SimulatedWriteOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var inboundStream = new SimulatedReadOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var reader = new SimulatedStreamReader(inboundStream))
            using (var receiver = new Receiver(reader, cancellationTokenSource))
            {
                var completionSource = new TaskCompletionSource<bool>();
                Message message = null;

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    completionSource.SetResult(true);
                };

                var bytes = Encoding.UTF8.GetBytes(json);

                for (var offset = 0; offset < bytes.Length; offset += 10)
                {
                    var count = Math.Min(bytes.Length - offset, 10);

                    outboundStream.Write(bytes, offset, count);
                }

                await receiver.ConnectAsync(CancellationToken.None);

                await completionSource.Task;

                Assert.Equal(requestId, message.RequestId);
                Assert.Equal(type, message.Type);
                Assert.Equal(method, message.Method);
                Assert.Equal(payload, message.Payload.ToString(Formatting.None));
            }
        }

        [Fact]
        public async Task MessageReceived_RaisedForMultipleMessagesWithNonBlockingStream()
        {
            var json = "{\"RequestId\":\"de08f561-50c1-4816-adc3-73d2c283d8cf\",\"Type\":\"Request\",\"Method\":\"Handshake\",\"Payload\":{\"ProtocolVersion\":\"3.0.0\",\"MinimumProtocolVersion\":\"1.0.0\"}}{\"RequestId\":\"e2db1e2d-0282-45c4-9004-b096e221230d\",\"Type\":\"Response\",\"Method\":\"Handshake\",\"Payload\":{\"ResponseCode\":0,\"ProtocolVersion\":\"2.0.0\"}}";

            using (var reader = new StringReader(json))
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(reader, cancellationTokenSource))
            {
                var completionSource = new TaskCompletionSource<bool>();
                var messages = new List<Message>();

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    messages.Add(e.Message);

                    if (messages.Count == 2)
                    {
                        completionSource.SetResult(true);
                    }
                };

                await receiver.ConnectAsync(CancellationToken.None);

                await completionSource.Task;
            }
        }

        [Fact]
        public async Task MessageReceived_RaisedForMultipleMessagesWithBlockingStream()
        {
            var json = "{\"RequestId\":\"de08f561-50c1-4816-adc3-73d2c283d8cf\",\"Type\":\"Request\",\"Method\":\"Handshake\",\"Payload\":{\"ProtocolVersion\":\"3.0.0\",\"MinimumProtocolVersion\":\"1.0.0\"}}{\"RequestId\":\"e2db1e2d-0282-45c4-9004-b096e221230d\",\"Type\":\"Response\",\"Method\":\"Handshake\",\"Payload\":{\"ResponseCode\":0,\"ProtocolVersion\":\"2.0.0\"}}";

            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var stream = new MemoryStream())
            using (var readWriteSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1))
            using (var dataWrittenEvent = new ManualResetEventSlim(initialState: false))
            using (var outboundStream = new SimulatedWriteOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var inboundStream = new SimulatedReadOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var reader = new SimulatedStreamReader(inboundStream))
            using (var receiver = new Receiver(reader, cancellationTokenSource))
            {
                var completionSource = new TaskCompletionSource<bool>();
                var messages = new List<Message>();

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    messages.Add(e.Message);

                    if (messages.Count == 2)
                    {
                        completionSource.SetResult(true);
                    }
                };

                var bytes = Encoding.UTF8.GetBytes(json);

                outboundStream.Write(bytes, offset: 0, count: bytes.Length);

                await receiver.ConnectAsync(CancellationToken.None);

                await completionSource.Task;
            }
        }

        [Fact]
        public async Task Faulted_RaisedForParseError()
        {
            var invalidJson = "text";

            using (var reader = new StringReader(invalidJson))
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(reader, cancellationTokenSource))
            {
                var completionSource = new TaskCompletionSource<bool>();
                Exception exception = null;

                receiver.Faulted += (object sender, ProtocolErrorEventArgs e) =>
                {
                    exception = e.Exception;

                    completionSource.SetResult(true);
                };

                await receiver.ConnectAsync(CancellationToken.None);

                await completionSource.Task;

                Assert.IsType<ProtocolException>(exception);
            }
        }

        [Theory]
        [InlineData("1")]
        [InlineData("[]")]
        public async Task Faulted_RaisedForDeserializationOfInvalidJson(string invalidJson)
        {
            using (var reader = new StringReader(invalidJson))
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(reader, cancellationTokenSource))
            {
                var completionSource = new TaskCompletionSource<bool>();
                Exception exception = null;

                receiver.Faulted += (object sender, ProtocolErrorEventArgs e) =>
                {
                    exception = e.Exception;

                    completionSource.SetResult(true);
                };

                await receiver.ConnectAsync(CancellationToken.None);

                await completionSource.Task;

                Assert.IsType<ProtocolException>(exception);
            }
        }

        [Fact]
        public async Task Faulted_RaisedForDeserializationError()
        {
            var json = "{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\",\"Payload\":\"{\\\"d\\\":\\\"e\\\"}\"}";

            using (var reader = new StringReader(json))
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(reader, cancellationTokenSource))
            {
                var completionSource = new TaskCompletionSource<bool>();
                Exception exception = null;

                receiver.Faulted += (object sender, ProtocolErrorEventArgs e) =>
                {
                    exception = e.Exception;

                    completionSource.SetResult(true);
                };

                await receiver.ConnectAsync(CancellationToken.None);

                await completionSource.Task;

                Assert.IsType<ProtocolException>(exception);
            }
        }

        [Theory]
        [InlineData("{\"Type\":\"Response\",\"Method\":\"None\"}")]
        [InlineData("{\"RequestId\":null,\"Type\":\"Response\",\"Method\":\"None\"}")]
        [InlineData("{\"RequestId\":\"\",\"Type\":\"Response\",\"Method\":\"None\"}")]
        [InlineData("{\"RequestId\":\"a\",\"Method\":\"None\"}")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":null,\"Method\":\"None\"}")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"\",\"Method\":\"None\"}")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\" \",\"Method\":\"None\"}")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"abc\",\"Method\":\"None\"}")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\"}")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":null}")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"\"}")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"abc\"}")]
        public async Task Faulted_RaisedForInvalidMessage(string json)
        {
            using (var reader = new StringReader(json))
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(reader, cancellationTokenSource))
            {
                var completionSource = new TaskCompletionSource<bool>();
                Exception exception = null;

                receiver.Faulted += (object sender, ProtocolErrorEventArgs e) =>
                {
                    exception = e.Exception;

                    completionSource.SetResult(true);
                };

                await receiver.ConnectAsync(CancellationToken.None);

                await completionSource.Task;

                Assert.IsType<ProtocolException>(exception);
            }
        }

        [Fact]
        public async Task CloseAsync_ThrowsIfDisposed()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var receiver = new Receiver(TextReader.Null, cancellationTokenSource);

                receiver.Dispose();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.CloseAsync());

                Assert.Equal(nameof(Receiver), exception.ObjectName);
            }
        }

        [Fact]
        public async Task CloseAsync_IsNotIdempotent()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(TextReader.Null, cancellationTokenSource))
            {
                await receiver.ConnectAsync(CancellationToken.None);

                await receiver.CloseAsync();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.CloseAsync());

                Assert.Equal(nameof(Receiver), exception.ObjectName);
            }
        }

        [Fact]
        public async Task CloseAsync_CanBeCalledWithoutConnectAsync()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(TextReader.Null, cancellationTokenSource))
            {
                await receiver.CloseAsync();
            }
        }

        [Fact]
        public async Task CloseAsync_ClosesUnderlyingStream()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var receiver = new Receiver(reader, cancellationTokenSource))
            {
                await receiver.ConnectAsync(CancellationToken.None);

                await receiver.CloseAsync();

                Assert.False(stream.CanSeek);
                Assert.False(stream.CanRead);
                Assert.False(stream.CanWrite);
            }
        }
    }
}