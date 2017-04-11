// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Protocol.Core.Types
{
    public class PluginResourceProvider : ResourceProvider
    {
        private const string _environmentVariable = "NUGET_PLUGIN_PATHS";
        private Lazy<PluginDiscoverer> _discoverer;

        public PluginResourceProvider()
            : base(typeof(PluginResource), nameof(PluginResource))
        {
            _discoverer = new Lazy<PluginDiscoverer>(InitializeDiscoverer);
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PluginResource resource = null;

            if (IsNoPluginAvailable())
            {
                var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

                if (serviceIndex != null)
                {
                    var pluginFiles = await _discoverer.Value.DiscoverAsync(token);

                    resource = new PluginResource(serviceIndex, pluginFiles);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }

        private static PluginDiscoverer InitializeDiscoverer()
        {
            var verifier = EmbeddedSignatureVerifier.CreateOrNull();

            return new PluginDiscoverer(Environment.GetEnvironmentVariable(_environmentVariable), verifier);
        }

        private static bool IsNoPluginAvailable()
        {
            return string.IsNullOrEmpty(Environment.GetEnvironmentVariable(_environmentVariable));
        }
    }
}