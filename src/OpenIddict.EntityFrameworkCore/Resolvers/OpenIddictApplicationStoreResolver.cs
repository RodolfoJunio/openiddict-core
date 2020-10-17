﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Concurrent;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.EntityFrameworkCore.Models;

namespace OpenIddict.EntityFrameworkCore
{
    /// <summary>
    /// Exposes a method allowing to resolve an application store.
    /// </summary>
    public class OpenIddictApplicationStoreResolver : IOpenIddictApplicationStoreResolver
    {
        private static readonly ConcurrentDictionary<Type, Type> _cache = new ConcurrentDictionary<Type, Type>();
        private readonly IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> _options;
        private readonly IServiceProvider _provider;

        public OpenIddictApplicationStoreResolver(
            [NotNull] IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> options,
            [NotNull] IServiceProvider provider)
        {
            _options = options;
            _provider = provider;
        }

        /// <summary>
        /// Returns an application store compatible with the specified application type or throws an
        /// <see cref="InvalidOperationException"/> if no store can be built using the specified type.
        /// </summary>
        /// <typeparam name="TApplication">The type of the Application entity.</typeparam>
        /// <returns>An <see cref="IOpenIddictApplicationStore{TApplication}"/>.</returns>
        public IOpenIddictApplicationStore<TApplication> Get<TApplication>() where TApplication : class
        {
            var store = _provider.GetService<IOpenIddictApplicationStore<TApplication>>();
            if (store != null)
            {
                return store;
            }

            var type = _cache.GetOrAdd(typeof(TApplication), key =>
            {
                var root = OpenIddictCoreHelpers.FindGenericBaseType(key, typeof(OpenIddictApplication<,,>));
                if (root == null)
                {
                    throw new InvalidOperationException(new StringBuilder()
                        .AppendLine("The specified application type is not compatible with the Entity Framework Core stores.")
                        .Append("When enabling the Entity Framework Core stores, make sure you use the built-in ")
                        .Append("'OpenIddictApplication' entity (from the 'OpenIddict.EntityFrameworkCore.Models' package) ")
                        .Append("or a custom entity that inherits from the generic 'OpenIddictApplication' entity.")
                        .ToString());
                }

                var context = _options.CurrentValue.ContextType;
                if (context == null)
                {
                    throw new InvalidOperationException(new StringBuilder()
                        .AppendLine("No Entity Framework Core context was specified in the OpenIddict options.")
                        .Append("To configure the OpenIddict Entity Framework Core stores to use a specific 'DbContext', ")
                        .Append("use 'options.AddEntityFrameworkCoreStores().UseContext<TContext>()'.")
                        .ToString());
                }

                return typeof(OpenIddictApplicationStore<,,,,>).MakeGenericType(
                    /* TApplication: */ key,
                    /* TAuthorization: */ root.GenericTypeArguments[1],
                    /* TToken: */ root.GenericTypeArguments[2],
                    /* TContext: */ context,
                    /* TKey: */ root.GenericTypeArguments[0]);
            });

            return (IOpenIddictApplicationStore<TApplication>) _provider.GetRequiredService(type);
        }
    }
}
