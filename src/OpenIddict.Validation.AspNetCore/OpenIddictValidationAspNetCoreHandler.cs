﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Validation.OpenIddictValidationEvents;

namespace OpenIddict.Validation.AspNetCore
{
    /// <summary>
    /// Provides the logic necessary to extract, validate and handle OpenID Connect requests.
    /// </summary>
    public class OpenIddictValidationAspNetCoreHandler : AuthenticationHandler<OpenIddictValidationAspNetCoreOptions>,
        IAuthenticationRequestHandler
    {
        private readonly IOpenIddictValidationProvider _provider;

        /// <summary>
        /// Creates a new instance of the <see cref="OpenIddictValidationAspNetCoreHandler"/> class.
        /// </summary>
        public OpenIddictValidationAspNetCoreHandler(
            [NotNull] IOpenIddictValidationProvider provider,
            [NotNull] IOptionsMonitor<OpenIddictValidationAspNetCoreOptions> options,
            [NotNull] ILoggerFactory logger,
            [NotNull] UrlEncoder encoder,
            [NotNull] ISystemClock clock)
            : base(options, logger, encoder, clock)
            => _provider = provider;

        protected override async Task InitializeHandlerAsync()
        {
            // Note: the transaction may be already attached when replaying an ASP.NET Core request
            // (e.g when using the built-in status code pages middleware with the re-execute mode).
            var transaction = Context.Features.Get<OpenIddictValidationAspNetCoreFeature>()?.Transaction;
            if (transaction == null)
            {
                // Create a new transaction and attach the HTTP request to make it available to the ASP.NET Core handlers.
                transaction = await _provider.CreateTransactionAsync();
                transaction.Properties[typeof(HttpRequest).FullName] = new WeakReference<HttpRequest>(Request);

                // Attach the OpenIddict validation transaction to the ASP.NET Core features
                // so that it can retrieved while performing challenge/forbid operations.
                Context.Features.Set(new OpenIddictValidationAspNetCoreFeature { Transaction = transaction });
            }

            var context = new ProcessRequestContext(transaction);
            await _provider.DispatchAsync(context);

            // Store the context in the transaction so that it can be retrieved from HandleRequestAsync().
            transaction.SetProperty(typeof(ProcessRequestContext).FullName, context);
        }

        public async Task<bool> HandleRequestAsync()
        {
            var transaction = Context.Features.Get<OpenIddictValidationAspNetCoreFeature>()?.Transaction ??
                throw new InvalidOperationException("An unknown error occurred while retrieving the OpenIddict validation context.");

            var context = transaction.GetProperty<ProcessRequestContext>(typeof(ProcessRequestContext).FullName) ??
                throw new InvalidOperationException("An unknown error occurred while retrieving the OpenIddict validation context.");

            if (context.IsRequestHandled)
            {
                return true;
            }

            else if (context.IsRequestSkipped)
            {
                return false;
            }

            else if (context.IsRejected)
            {
                var notification = new ProcessErrorContext(transaction)
                {
                    Response = new OpenIddictResponse
                    {
                        Error = context.Error ?? Errors.InvalidRequest,
                        ErrorDescription = context.ErrorDescription,
                        ErrorUri = context.ErrorUri
                    }
                };

                await _provider.DispatchAsync(notification);

                if (notification.IsRequestHandled)
                {
                    return true;
                }

                else if (notification.IsRequestSkipped)
                {
                    return false;
                }

                throw new InvalidOperationException(new StringBuilder()
                    .Append("The OpenID Connect response was not correctly processed. This may indicate ")
                    .Append("that the event handler responsible of processing OpenID Connect responses ")
                    .Append("was not registered or was explicitly removed from the handlers list.")
                    .ToString());
            }

            return false;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var transaction = Context.Features.Get<OpenIddictValidationAspNetCoreFeature>()?.Transaction ??
                throw new InvalidOperationException("An unknown error occurred while retrieving the OpenIddict validation context.");

            // Note: in many cases, the authentication token was already validated by the time this action is called
            // (generally later in the pipeline, when using the pass-through mode). To avoid having to re-validate it,
            // the authentication context is resolved from the transaction. If it's not available, a new one is created.
            var context = transaction.GetProperty<ProcessAuthenticationContext>(typeof(ProcessAuthenticationContext).FullName);
            if (context == null)
            {
                context = new ProcessAuthenticationContext(transaction);
                await _provider.DispatchAsync(context);

                // Store the context object in the transaction so it can be later retrieved by handlers
                // that want to access the authentication result without triggering a new authentication flow.
                transaction.SetProperty(typeof(ProcessAuthenticationContext).FullName, context);
            }

            if (context.IsRequestHandled || context.IsRequestSkipped)
            {
                return AuthenticateResult.NoResult();
            }

            else if (context.IsRejected)
            {
                var properties = new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictValidationAspNetCoreConstants.Properties.Error] = context.Error,
                    [OpenIddictValidationAspNetCoreConstants.Properties.ErrorDescription] = context.ErrorDescription,
                    [OpenIddictValidationAspNetCoreConstants.Properties.ErrorUri] = context.ErrorUri
                });

                return AuthenticateResult.Fail("An error occurred while authenticating the current request.", properties);
            }

            return AuthenticateResult.Success(new AuthenticationTicket(
                context.Principal,
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme));
        }

        protected override async Task HandleChallengeAsync([CanBeNull] AuthenticationProperties properties)
        {
            var transaction = Context.Features.Get<OpenIddictValidationAspNetCoreFeature>()?.Transaction ??
                throw new InvalidOperationException("An unknown error occurred while retrieving the OpenIddict validation context.");

            transaction.Properties[typeof(AuthenticationProperties).FullName] = properties ?? new AuthenticationProperties();

            var context = new ProcessChallengeContext(transaction)
            {
                Response = new OpenIddictResponse()
            };

            await _provider.DispatchAsync(context);

            if (context.IsRequestHandled || context.IsRequestSkipped)
            {
                return;
            }

            else if (context.IsRejected)
            {
                var notification = new ProcessErrorContext(transaction)
                {
                    Response = new OpenIddictResponse
                    {
                        Error = context.Error ?? Errors.InvalidRequest,
                        ErrorDescription = context.ErrorDescription,
                        ErrorUri = context.ErrorUri
                    }
                };

                await _provider.DispatchAsync(notification);

                if (notification.IsRequestHandled || context.IsRequestSkipped)
                {
                    return;
                }

                throw new InvalidOperationException(new StringBuilder()
                    .Append("The OpenID Connect response was not correctly processed. This may indicate ")
                    .Append("that the event handler responsible of processing OpenID Connect responses ")
                    .Append("was not registered or was explicitly removed from the handlers list.")
                    .ToString());
            }
        }

        protected override Task HandleForbiddenAsync([CanBeNull] AuthenticationProperties properties)
            => HandleChallengeAsync(properties);
    }
}
