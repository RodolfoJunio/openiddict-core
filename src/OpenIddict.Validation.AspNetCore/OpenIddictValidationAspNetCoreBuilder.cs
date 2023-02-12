﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.ComponentModel;
using OpenIddict.Validation.AspNetCore;
using SR = OpenIddict.Abstractions.OpenIddictResources;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Exposes the necessary methods required to configure
    /// the OpenIddict validation ASP.NET Core integration.
    /// </summary>
    public class OpenIddictValidationAspNetCoreBuilder
    {
        /// <summary>
        /// Initializes a new instance of <see cref="OpenIddictValidationAspNetCoreBuilder"/>.
        /// </summary>
        /// <param name="services">The services collection.</param>
        public OpenIddictValidationAspNetCoreBuilder(IServiceCollection services)
            => Services = services ?? throw new ArgumentNullException(nameof(services));

        /// <summary>
        /// Gets the services collection.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public IServiceCollection Services { get; }

        /// <summary>
        /// Amends the default OpenIddict validation ASP.NET Core configuration.
        /// </summary>
        /// <param name="configuration">The delegate used to configure the OpenIddict options.</param>
        /// <remarks>This extension can be safely called multiple times.</remarks>
        /// <returns>The <see cref="OpenIddictValidationAspNetCoreBuilder"/>.</returns>
        public OpenIddictValidationAspNetCoreBuilder Configure(Action<OpenIddictValidationAspNetCoreOptions> configuration)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            Services.Configure(configuration);

            return this;
        }

        /// <summary>
        /// Sets the realm returned to the caller as part of the WWW-Authenticate header.
        /// </summary>
        /// <param name="realm">The issuer address.</param>
        /// <returns>The <see cref="OpenIddictValidationAspNetCoreBuilder"/>.</returns>
        public OpenIddictValidationAspNetCoreBuilder SetRealm(string realm)
        {
            if (string.IsNullOrEmpty(realm))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID1106), nameof(realm));
            }

            return Configure(options => options.Realm = realm);
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object? obj) => base.Equals(obj);

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() => base.GetHashCode();

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string? ToString() => base.ToString();
    }
}
