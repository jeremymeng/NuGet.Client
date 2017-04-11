// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    public sealed class CancellableStreamReader : StreamReader
    {
        private readonly CancellationToken _cancellationToken;

        private CancellableStreamReader(StreamReader streamReader, CancellationToken cancellationToken)
            : base(streamReader.BaseStream)
        {
            _cancellationToken = cancellationToken;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            var readRequest = new ReadRequest(BaseRead, buffer, index, count);
            var task = Task<int>.Factory.StartNew(Read, readRequest, _cancellationToken);

            try
            {
                task.Wait(_cancellationToken);
            }
            catch (AggregateException ex) when (ex.InnerException != null)
            {
                // With Task.Wait(...) task exceptions will always be wrapped
                // in an AggregateException.  However, ultimately we're calling
                // a synchronous method whose exception should be observable here.
                throw ex.InnerException;
            }

            return task.Result;
        }

        public static CancellableStreamReader Create(StreamReader reader, CancellationToken cancellationToken)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return new CancellableStreamReader(reader, cancellationToken);
        }

        private int BaseRead(char[] buffer, int index, int count)
        {
            return base.Read(buffer, index, count);
        }

        private static int Read(object state)
        {
            var readRequest = (ReadRequest)state;

            return readRequest.Read();
        }

        private sealed class ReadRequest
        {
            private readonly Func<char[], int, int, int> _readFunc;
            private readonly char[] _buffer;
            private readonly int _index;
            private readonly int _count;

            internal ReadRequest(Func<char[], int, int, int> readFunc, char[] buffer, int index, int count)
            {
                _readFunc = readFunc;
                _buffer = buffer;
                _index = index;
                _count = count;
            }

            internal int Read()
            {
                return _readFunc(_buffer, _index, _count);
            }
        }
    }
}