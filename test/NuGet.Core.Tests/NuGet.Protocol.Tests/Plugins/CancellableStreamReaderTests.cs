// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class CancellableStreamReaderTests
    {
        [Fact]
        public void Create_ThrowsForNullReader()
        {
            Assert.Throws<ArgumentNullException>(
                () => CancellableStreamReader.Create(reader: null, cancellationToken: CancellationToken.None));
        }

        [Fact]
        public void Create_ThrowsIfCancelled()
        {
            Assert.Throws<OperationCanceledException>(
                () => CancellableStreamReader.Create(StreamReader.Null, new CancellationToken(canceled: true)));
        }

        [Fact]
        public void Read_IsCancellable()
        {
            using (var test = new CancellableStreamReaderTest())
            {
                test.CancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100));

                var buffer = new char[1];

                Assert.Throws<OperationCanceledException>(
                    () => test.CancellableStreamReader.Read(buffer, index: 0, count: buffer.Length));
            }
        }

        [Fact]
        public void Read_ExceptionPropagatesToCallingThread()
        {
            using (var test = new CancellableStreamReaderTest())
            {
                test.WriteData("a");

                var exception = Assert.Throws<ArgumentNullException>(
                    () => test.CancellableStreamReader.Read(buffer: null, index: 0, count: 1));

                Assert.Equal("buffer", exception.ParamName);
            }
        }

        [Fact]
        public void Read_ReadsStreamData()
        {
            using (var test = new CancellableStreamReaderTest())
            {
                test.WriteData("abc");

                var buffer = new [] { 'z', 'z', 'z', 'z', 'z' };

                var bytesRead = test.CancellableStreamReader.Read(buffer, index: 1, count: 4);

                Assert.Equal(3, bytesRead);
                Assert.Equal(new char[] { 'z', 'a', 'b', 'c', 'z' }, buffer);
            }
        }

        [Fact]
        public void JsonDeserialization_CallsStreamReaderRead()
        {
            var textReaderMock = new Mock<TextReader>(MockBehavior.Strict);
            var sourceBuffer = "{\"B\":7}".ToCharArray();
            var sourcePosition = 0;

            textReaderMock.As<IDisposable>().Setup(x => x.Dispose());
            textReaderMock.Protected().Setup("Dispose", ItExpr.IsAny<bool>());
            textReaderMock.Setup(x => x.Read(It.IsAny<char[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns((char[] buffer, int index, int count) =>
                {
                    var newSourcePosition = Math.Min(sourceBuffer.Length, count - index);
                    var charsToCopy = newSourcePosition - sourcePosition;

                    Array.Copy(sourceBuffer, sourcePosition, buffer, index, charsToCopy);

                    sourcePosition += charsToCopy;

                    return charsToCopy;
                });

            using (var reader = new JsonTextReader(textReaderMock.Object))
            {
                var deserializer = JsonSerializer.CreateDefault();

                Assert.True(reader.Read());

                var a = deserializer.Deserialize<A>(reader);

                Assert.Equal(7, a.B);
            }
        }

        private sealed class A
        {
            public int B { get; set; }
        }

        private sealed class CancellableStreamReaderTest : IDisposable
        {
            private readonly SimulatedReadOnlyFileStream _blockingFileStream;
            private readonly ManualResetEventSlim _dataWrittenEvent;
            private bool _isDisposed;
            private readonly SemaphoreSlim _readWriteSemaphore;
            private readonly MemoryStream _stream;
            private readonly StreamReader _streamReader;

            internal CancellableStreamReader CancellableStreamReader;
            internal CancellationTokenSource CancellationTokenSource { get; }

            internal CancellableStreamReaderTest()
            {
                _stream = new MemoryStream();
                _readWriteSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);
                _dataWrittenEvent = new ManualResetEventSlim(initialState: false);
                _blockingFileStream = new SimulatedReadOnlyFileStream(
                    _stream,
                    _readWriteSemaphore,
                    _dataWrittenEvent,
                    CancellationToken.None);
                _streamReader = new StreamReader(_blockingFileStream);
                CancellationTokenSource = new CancellationTokenSource();
                CancellableStreamReader = CancellableStreamReader.Create(_streamReader, CancellationTokenSource.Token);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    CancellableStreamReader.Dispose();
                    CancellationTokenSource.Dispose();
                    _streamReader.Dispose();
                    _blockingFileStream.Dispose();
                    _dataWrittenEvent.Dispose();
                    _readWriteSemaphore.Dispose();
                    _stream.Dispose();

                    _isDisposed = true;
                }
            }

            internal void WriteData(string data)
            {
                var bytes = Encoding.UTF8.GetBytes(data);

                try
                {
                    _readWriteSemaphore.Wait();

                    var originalPosition = _stream.Position;

                    _stream.Write(bytes, offset: (int)originalPosition, count: bytes.Length);

                    _stream.Position = originalPosition;

                    _dataWrittenEvent.Set();
                }
                finally
                {
                    _readWriteSemaphore.Release();
                }

                _dataWrittenEvent.Set();
            }
        }
    }
}