﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

namespace Unofficial.CodeAnalysis.Scripting.Hosting
{
    internal sealed class CoreAssemblyLoaderImpl : AssemblyLoaderImpl
    {
        private readonly LoadContext _inMemoryAssemblyContext;

        internal CoreAssemblyLoaderImpl(InteractiveAssemblyLoader loader)
            : base(loader)
        {
            _inMemoryAssemblyContext = new LoadContext(Loader, null, true);
        }

        public override Assembly LoadFromStream(Stream peStream, Stream pdbStream)
        {
            return _inMemoryAssemblyContext.LoadFromStream(peStream, pdbStream);
        }

        public override AssemblyAndLocation LoadFromPath(string path)
        {
            // Create a new context that knows the directory where the assembly was loaded from
            // and uses it to resolve dependencies of the assembly. We could create one context per directory,
            // but there is no need to reuse contexts.
            var assembly = new LoadContext(Loader, Path.GetDirectoryName(path), false).LoadFromAssemblyPath(path);

            return new AssemblyAndLocation(assembly, path, GlobalAssemblyCache: false);
        }

        public override void Dispose()
        {
            _inMemoryAssemblyContext.Unload();
        }

        private sealed class LoadContext : AssemblyLoadContext
        {
            private readonly string? _loadDirectory;
            private readonly InteractiveAssemblyLoader _loader;

            internal LoadContext(InteractiveAssemblyLoader loader, string? loadDirectory, bool isCollectible) : base(isCollectible)
            {
                _loader = loader;
                _loadDirectory = loadDirectory;

                // CoreCLR resolves assemblies in steps:
                //
                //   1) Call AssemblyLoadContext.Load -- our context returns null
                //   2) TPA list
                //   3) Default.Resolving event
                //   4) AssemblyLoadContext.Resolving event -- hooked below
                // 
                // What we want is to let the default context load assemblies it knows about (this includes already loaded assemblies,
                // assemblies in AppPath, platform assemblies, assemblies explciitly resolved by the App by hooking Default.Resolving, etc.).
                // Only if the assembly can't be resolved that way, the interactive resolver steps in.
                //
                // This order is necessary to avoid loading assemblies twice (by the host App and by interactive loader).

                Resolving += (_, assemblyName) =>
                {
                    var identity = new AssemblyIdentity(
                        assemblyName.Name,
                        assemblyName.Version,
                        assemblyName.CultureName,
                        ImmutableArray.Create(assemblyName.GetPublicKeyToken()),
                        hasPublicKey: false,
                        isRetargetable: (assemblyName.Flags & AssemblyNameFlags.Retargetable) != 0,
                        contentType: assemblyName.ContentType);

                    return _loader.ResolveAssembly(identity, _loadDirectory);
                };
            }

            protected override Assembly? Load(AssemblyName assemblyName) => null;
        }
    }
}
#endif
