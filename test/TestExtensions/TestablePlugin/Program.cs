// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NuGet.Test.TestExtensions.TestablePlugin
{
    internal static class Program
    {
        private const int _success = 0;
        private const int _error = 1;

        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;

                Arguments parsedArgs;
                if (!Arguments.TryParse(args, out parsedArgs))
                {
                    return _error;
                }

                Start(parsedArgs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());

                return _error;
            }

            return _success;
        }

        private static void Start(Arguments args)
        {
            var localEndPoint = new IPEndPoint(IPAddress.Loopback, args.PortNumber);

            using (var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                while (true)
                {
                    var handler = listener.Accept();
                    var data = string.Empty;

                    while (true)
                    {
                        var bytes = new byte[4096];
                        var bytesReceived = handler.Receive(bytes);

                        data += Encoding.UTF8.GetString(bytes, index: 0, count: bytesReceived);

                        string line;
                        string remainder;

                        if (TrySplitOnNewLine(data, out line, out remainder))
                        {
                            if (line == "quit")
                            {
                                break;
                            }

                            Console.WriteLine(line);

                            data = remainder;
                        }
                    }

                    handler.Shutdown(SocketShutdown.Both);
                }
            }
        }

        private static bool TrySplitOnNewLine(string text, out string line, out string remainder)
        {
            line = null;
            remainder = null;

            for (var i = 0; i < text.Length; ++i)
            {
                var c = text[i];

                if (c == '\n')
                {
                    line = text.Substring(startIndex: 0, length: i);
                    remainder = text.Substring(startIndex: i + 1);

                    return true;
                }

                if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        line = text.Substring(startIndex: 0, length: i);
                        remainder = text.Substring(startIndex: i + 2);

                        return true;
                    }

                    line = text.Substring(startIndex: 0, length: i);
                    remainder = text.Substring(startIndex: i + 1);

                    return true;
                }
            }

            return false;
        }
    }
}