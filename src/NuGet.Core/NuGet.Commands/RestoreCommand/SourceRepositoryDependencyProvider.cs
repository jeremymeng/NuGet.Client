﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class SourceRepositoryDependencyProvider : IRemoteDependencyProvider
    {
        private readonly object _lock = new object();
        private readonly SourceRepository _sourceRepository;
        private readonly ILogger _logger;
        private readonly SourceCacheContext _cacheContext;
        private FindPackageByIdResource _findPackagesByIdResource;
        private bool _ignoreFailedSources;
        private bool _ignoreWarning;

        // Limiting concurrent requests to limit the amount of files open at a time on Mac OSX
        // the default is 256 which is easy to hit if we don't limit concurrency
        private readonly static SemaphoreSlim _throttle =
            RuntimeEnvironmentHelper.IsMacOSX
                ? new SemaphoreSlim(ConcurrencyLimit, ConcurrencyLimit)
                : null;

        // In order to avoid too many open files error, set concurrent requests number to 16 on Mac
        private const int ConcurrencyLimit = 16;

        public SourceRepositoryDependencyProvider(
            SourceRepository sourceRepository,
            ILogger logger,
            SourceCacheContext cacheContext,
            bool ignoreFailedSources,
            bool ignoreWarning)
        {
            _sourceRepository = sourceRepository;
            _logger = logger;
            _cacheContext = cacheContext;
            _ignoreFailedSources = ignoreFailedSources;
            _ignoreWarning = ignoreWarning;
        }

        public bool IsHttp => _sourceRepository.PackageSource.IsHttp;

        /// <summary>
        /// Discovers all versions of a package from a source and selects the best match.
        /// This does not download the package.
        /// </summary>
        public async Task<LibraryIdentity> FindLibraryAsync(
            LibraryRange libraryRange,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            await EnsureResource();

            // Discover all versions from the feed
            var packageVersions = await GetPackageVersions(libraryRange.Name, cacheContext, logger, cancellationToken);

            // Select the best match
            var packageVersion = packageVersions?.FindBestMatch(libraryRange.VersionRange, version => version);

            if (packageVersion != null)
            {
                return new LibraryIdentity
                {
                    Name = libraryRange.Name,
                    Version = packageVersion,
                    Type = LibraryType.Package
                };
            }

            return null;
        }

        public async Task<LibraryDependencyInfo> GetDependenciesAsync(
            LibraryIdentity match,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            await EnsureResource();

            FindPackageByIdDependencyInfo packageInfo = null;
            try
            {
                if (_throttle != null)
                {
                    await _throttle.WaitAsync();
                }

                // Read package info, this will download the package if needed.
                packageInfo = await _findPackagesByIdResource.GetDependencyInfoAsync(
                    match.Name,
                    match.Version,
                    cacheContext,
                    logger,
                    cancellationToken);
            }
            catch (FatalProtocolException e) when (_ignoreFailedSources )
            {
                if (!_ignoreWarning)
                {
                    _logger.LogWarning(e.Message);
                }
            }
            finally
            {
                _throttle?.Release();
            }

            if (packageInfo == null)
            {
                // Package was not found
                return LibraryDependencyInfo.CreateUnresolved(match, targetFramework);
            }
            else
            {
                // Package found
                var originalIdentity = new LibraryIdentity(
                    packageInfo.PackageIdentity.Id,
                    packageInfo.PackageIdentity.Version,
                    match.Type);

                var dependencies = GetDependencies(packageInfo, targetFramework);

                return LibraryDependencyInfo.Create(originalIdentity, targetFramework, dependencies);
            }
        }

        public async Task CopyToAsync(
            LibraryIdentity identity,
            Stream stream,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            await EnsureResource();

            try
            {
                if (_throttle != null)
                {
                    await _throttle.WaitAsync();
                }

                cancellationToken.ThrowIfCancellationRequested();

                // If the stream is already available, do not stop in the middle of copying the stream
                // Pass in CancellationToken.None
                await _findPackagesByIdResource.CopyNupkgToStreamAsync(
                    identity.Name,
                    identity.Version,
                    stream,
                    cacheContext,
                    logger,
                    CancellationToken.None);
            }
            catch (FatalProtocolException e) when (_ignoreFailedSources)
            {
                if (!_ignoreWarning)
                {
                    _logger.LogWarning(e.Message);
                }
            }
            finally
            {
                _throttle?.Release();
            }
        }

        private IEnumerable<LibraryDependency> GetDependencies(FindPackageByIdDependencyInfo packageInfo, NuGetFramework targetFramework)
        {
            if (packageInfo == null)
            {
                return Enumerable.Empty<LibraryDependency>();
            }

            var dependencies = NuGetFrameworkUtility.GetNearest(packageInfo.DependencyGroups,
                targetFramework,
                item => item.TargetFramework);

            return GetDependencies(targetFramework, dependencies);
        }

        private static IEnumerable<LibraryDependency> GetDependencies(NuGetFramework targetFramework,
            PackageDependencyGroup dependencies)
        {
            if (dependencies != null)
            {
                return dependencies.Packages.Select(PackagingUtility.GetLibraryDependencyFromNuspec).ToArray();
            }

            return Enumerable.Empty<LibraryDependency>();
        }

        private async Task EnsureResource()
        {
            if (_findPackagesByIdResource == null)
            {
                var resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

                lock (_lock)
                {
                    if (_findPackagesByIdResource == null)
                    {
                        _findPackagesByIdResource = resource;
                    }
                }
            }
        }

        /// <summary>
        /// Discover all package versions from a feed.
        /// </summary>
        private async Task<IEnumerable<NuGetVersion>> GetPackageVersions(string id,
                                                                    SourceCacheContext cacheContext,
                                                                    ILogger logger,
                                                                    CancellationToken cancellationToken)
        {
            IEnumerable<NuGetVersion> packageVersions = null;
            try
            {
                if (_throttle != null)
                {
                    await _throttle.WaitAsync();
                }
                packageVersions = await _findPackagesByIdResource.GetAllVersionsAsync(
                    id,
                    cacheContext,
                    logger,
                    cancellationToken);
            }
            catch (FatalProtocolException e) when (_ignoreFailedSources)
            {
                if (!_ignoreWarning)
                {
                    _logger.LogWarning(e.Message);
                }
                return null;
            }
            finally
            {
                _throttle?.Release();
            }

            return packageVersions;
        }
    }
}
