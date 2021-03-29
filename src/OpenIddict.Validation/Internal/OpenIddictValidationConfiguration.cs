﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Text;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace OpenIddict.Validation.Internal
{
    /// <summary>
    /// Contains the methods required to ensure that the OpenIddict validation configuration is valid.
    /// Note: this API supports the OpenIddict infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future minor releases.
    /// </summary>
    public class OpenIddictValidationConfiguration : IConfigureOptions<AuthenticationOptions>,
                                                     IPostConfigureOptions<OpenIddictValidationOptions>
    {
        private readonly IDataProtectionProvider _dataProtectionProvider;

        /// <summary>
        /// Creates a new instance of the <see cref="OpenIddictValidationConfiguration"/> class.
        /// Note: this API supports the OpenIddict infrastructure and is not intended to be used
        /// directly from your code. This API may change or be removed in future minor releases.
        /// </summary>
        public OpenIddictValidationConfiguration([NotNull] IDataProtectionProvider dataProtectionProvider)
            => _dataProtectionProvider = dataProtectionProvider;

        /// <summary>
        /// Registers the OpenIddict validation handler in the global authentication options.
        /// </summary>
        /// <param name="options">The options instance to initialize.</param>
        public void Configure(AuthenticationOptions options)
        {
            // If a handler was already registered and the type doesn't correspond to the OpenIddict handler, throw an exception.
            if (options.SchemeMap.TryGetValue(OpenIddictValidationDefaults.AuthenticationScheme, out var builder) &&
                builder.HandlerType != typeof(OpenIddictValidationHandler))
            {
                throw new InvalidOperationException(new StringBuilder()
                    .AppendLine("The OpenIddict validation handler cannot be registered as an authentication scheme.")
                    .AppendLine("This may indicate that an instance of the OAuth validation or JWT bearer handler was registered.")
                    .Append("Make sure that neither 'services.AddAuthentication().AddOAuthValidation()' nor ")
                    .Append("'services.AddAuthentication().AddJwtBearer()' are called from 'ConfigureServices'.")
                    .ToString());
            }

            options.AddScheme<OpenIddictValidationHandler>(OpenIddictValidationDefaults.AuthenticationScheme, displayName: null);
        }

        /// <summary>
        /// Populates the default OpenIddict validation options and ensures
        /// that the configuration is in a consistent and valid state.
        /// </summary>
        /// <param name="name">The authentication scheme associated with the handler instance.</param>
        /// <param name="options">The options instance to initialize.</param>
        public void PostConfigure([NotNull] string name, [NotNull] OpenIddictValidationOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("The options instance name cannot be null or empty.", nameof(name));
            }

            if (options.EventsType == null || options.EventsType != typeof(OpenIddictValidationProvider))
            {
                throw new InvalidOperationException(new StringBuilder()
                    .AppendLine("OpenIddict can only be used with its built-in validation provider.")
                    .AppendLine("This error may indicate that 'OpenIddictValidationOptions.EventsType' was manually set.")
                    .Append("To execute custom request handling logic, consider registering an event handler using ")
                    .Append("the generic 'services.AddOpenIddict().AddValidation().AddEventHandler()' method.")
                    .ToString());
            }

            if (options.DataProtectionProvider == null)
            {
                options.DataProtectionProvider = _dataProtectionProvider;
            }

            if (options.UseReferenceTokens && options.AccessTokenFormat == null)
            {
                var protector = options.DataProtectionProvider.CreateProtector(
                    "OpenIdConnectServerHandler",
                    nameof(options.AccessTokenFormat),
                    nameof(options.UseReferenceTokens), "ASOS");

                options.AccessTokenFormat = new TicketDataFormat(protector);
            }
        }
    }
}
