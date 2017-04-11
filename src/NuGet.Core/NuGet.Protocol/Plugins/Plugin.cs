// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    public class Plugin : IDisposable
    {
        private bool _isDisposed;
        private readonly Process _process;

        public Connection Connection { get; }
        public string Name { get; }
        public string FilePath { get; }

        private Plugin(string filePath, Process process, Connection connection)
        {
            Name = Path.GetFileNameWithoutExtension(filePath);
            FilePath = filePath;
            _process = process;
            Connection = connection;

            Connection.Faulted += OnFaulted;

            if (process != null)
            {
                process.Exited += OnExited;
            }
        }

        public static async Task<Plugin> CreateAsync(string filePath, ConnectionOptions options, CancellationToken sessionCancellationToken)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(filePath));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            sessionCancellationToken.ThrowIfCancellationRequested();

            var startInfo = new ProcessStartInfo(filePath)
            {
                Arguments = "-plugin",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            var process = Process.Start(startInfo);
            var readerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellationToken);
            var sender = new Sender(process.StandardInput, sessionCancellationToken);
            var cancellableStreamReader = CancellableStreamReader.Create(process.StandardOutput, readerCancellationTokenSource.Token);
            var receiver = new Receiver(cancellableStreamReader, readerCancellationTokenSource);
            var messageDispatcher = new MessageDispatcher(new RequestIdGenerator());
            var connection = new Connection(messageDispatcher, sender, receiver, options);

            // Wire up the Fault handler before calling ConnectAsync(...).
            var plugin = new Plugin(filePath, process, connection);

            try
            {
                await connection.ConnectAsync(sessionCancellationToken);
            }
            catch (Exception)
            {
                plugin.Dispose();

                throw;
            }

            return plugin;
        }

        public static async Task<Plugin> CreateFromCurrentProcessAsync(ConnectionOptions options, CancellationToken sessionCancellationToken)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            sessionCancellationToken.ThrowIfCancellationRequested();

            var standardInput = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var standardOutput = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8);
            var readerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellationToken);
            var sender = new Sender(standardOutput, sessionCancellationToken);
            var cancellableStreamReader = CancellableStreamReader.Create(standardInput, readerCancellationTokenSource.Token);
            var receiver = new Receiver(cancellableStreamReader, readerCancellationTokenSource);
            var messageDispatcher = new MessageDispatcher(new RequestIdGenerator());
            var connection = new Connection(messageDispatcher, sender, receiver, options);

            var process = Process.GetCurrentProcess();
            var filePath = process.MainModule.FileName;

            // Wire up event handlers before calling ConnectAsync(...).
            var plugin = new Plugin(filePath, process: null, connection: connection);

            try
            {
                await connection.ConnectAsync(sessionCancellationToken);
            }
            catch (Exception)
            {
                plugin.Dispose();

                throw;
            }

            return plugin;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_isDisposed)
            {
                Connection.Faulted -= OnFaulted;

                if (_process != null)
                {
                    _process.Exited -= OnExited;

                    SafeKill(_process);

                    _process.Dispose();
                }

                _isDisposed = true;
            }
        }

        private void OnExited(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnFaulted(object sender, MessageEventArgs e)
        {
            Connection.CloseAsync().GetAwaiter().GetResult();
        }

        private static void SafeKill(Process process)
        {
            try
            {
                process.Kill();
            }
            catch (Exception)
            {
            }
        }
    }
}