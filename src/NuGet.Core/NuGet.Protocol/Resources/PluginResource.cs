// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Protocol.Core.Types
{
    public class PluginResource : INuGetResource
    {
        private readonly ServiceIndexResourceV3 _serviceIndex;
        private readonly IEnumerable<PluginFile> _pluginFiles;

        public PluginResource(ServiceIndexResourceV3 serviceIndex, IEnumerable<PluginFile> pluginFiles)
        {
            if (serviceIndex == null)
            {
                throw new ArgumentNullException(nameof(serviceIndex));
            }

            if (pluginFiles == null)
            {
                throw new ArgumentNullException(nameof(pluginFiles));
            }

            _serviceIndex = serviceIndex;
            _pluginFiles = pluginFiles;
        }

        public async Task<Plugin> GetPluginAsync(CancellationToken token)
        {
            var plugin = await Plugin.CreateAsync(_pluginFiles.Single().Path, ConnectionOptions.Default, token);

            return plugin;
        }
    }
}