// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    public class PluginDiscoverer : IDisposable
    {
        private bool _isDisposed;
        private IEnumerable<PluginFile> _pluginFiles;
        private readonly string _rawPluginPaths;
        private readonly SemaphoreSlim _semaphore;
        private readonly EmbeddedSignatureVerifier _verifier;

        public PluginDiscoverer(string rawPluginPaths, EmbeddedSignatureVerifier verifier)
        {
            _rawPluginPaths = rawPluginPaths;
            _verifier = verifier;
            _semaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async Task<IEnumerable<PluginFile>> DiscoverAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                await _semaphore.WaitAsync(token);

                if (_pluginFiles == null)
                {
                    _pluginFiles = GetPluginFiles(token);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return _pluginFiles;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_isDisposed)
            {
                _semaphore.Dispose();

                _isDisposed = true;
            }
        }

        private IEnumerable<PluginFile> GetPluginFiles(CancellationToken token)
        {
            var filePaths = GetPluginFilePaths();

            if (filePaths.Any() && _verifier == null)
            {
                throw new PlatformNotSupportedException();
            }

            var files = new List<PluginFile>();

            foreach (var filePath in filePaths)
            {
                token.ThrowIfCancellationRequested();

                if (File.Exists(filePath))
                {
                    var isValid = _verifier.IsValid(filePath);
                    var state = isValid ? PluginFileState.Valid : PluginFileState.InvalidCodeSignature;

                    files.Add(new PluginFile(filePath, state));
                }
                else
                {
                    files.Add(new PluginFile(filePath, PluginFileState.NotFound));
                }
            }

            return files.AsEnumerable();
        }

        private IEnumerable<string> GetPluginFilePaths()
        {
            if (string.IsNullOrEmpty(_rawPluginPaths))
            {
                return Enumerable.Empty<string>();
            }

            return _rawPluginPaths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}