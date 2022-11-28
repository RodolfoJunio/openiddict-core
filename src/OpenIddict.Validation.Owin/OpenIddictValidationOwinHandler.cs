﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Validation.OpenIddictValidationEvents;
using SR = OpenIddict.Abstractions.Resources.OpenIddictResources;

namespace OpenIddict.Validation.Owin
{
    /// <summary>
    /// Provides the entry point necessary to register the OpenIddict validation in an OWIN pipeline.
    /// </summary>
    public class OpenIddictValidationOwinHandler : AuthenticationHandler<OpenIddictValidationOwinOptions>
    {
        private readonly IOpenIddictValidationDispatcher _dispatcher;
        private readonly IOpenIddictValidationFactory _factory;

        /// <summary>
        /// Creates a new instance of the <see cref="OpenIddictValidationOwinHandler"/> class.
        /// </summary>
        /// <param name="dispatcher">The OpenIddict validation provider used by this instance.</param>
        /// <param name="factory">The OpenIddict validation factory used by this instance.</param>
        public OpenIddictValidationOwinHandler(
            [NotNull] IOpenIddictValidationDispatcher dispatcher,
            [NotNull] IOpenIddictValidationFactory factory)
        {
            _dispatcher = dispatcher;
            _factory = factory;
        }

        protected override async Task InitializeCoreAsync()
        {
            // Note: the transaction may be already attached when replaying an OWIN request
            // (e.g when using a status code pages middleware re-invoking the OWIN pipeline).
            var transaction = Context.Get<OpenIddictValidationTransaction>(typeof(OpenIddictValidationTransaction).FullName);
            if (transaction == null)
            {
                // Create a new transaction and attach the OWIN request to make it available to the OWIN handlers.
                transaction = await _factory.CreateTransactionAsync();
                transaction.Properties[typeof(IOwinRequest).FullName] = new WeakReference<IOwinRequest>(Request);

                // Attach the OpenIddict validation transaction to the OWIN shared dictionary
                // so that it can retrieved while performing sign-in/sign-out operations.
                Context.Set(typeof(OpenIddictValidationTransaction).FullName, transaction);
            }

            var context = new ProcessRequestContext(transaction);
            await _dispatcher.DispatchAsync(context);

            // Store the context in the transaction so that it can be retrieved from InvokeAsync().
            transaction.SetProperty(typeof(ProcessRequestContext).FullName, context);
        }

        public override async Task<bool> InvokeAsync()
        {
            // Note: due to internal differences between ASP.NET Core and Katana, the request MUST start being processed
            // in InitializeCoreAsync() to ensure the request context is available from AuthenticateCoreAsync() when
            // active authentication is used, as AuthenticateCoreAsync() is always called before InvokeAsync() in this case.

            var transaction = Context.Get<OpenIddictValidationTransaction>(typeof(OpenIddictValidationTransaction).FullName) ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID1165));

            var context = transaction.GetProperty<ProcessRequestContext>(typeof(ProcessRequestContext).FullName) ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID1165));

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

                await _dispatcher.DispatchAsync(notification);

                if (notification.IsRequestHandled)
                {
                    return true;
                }

                else if (notification.IsRequestSkipped)
                {
                    return false;
                }

                throw new InvalidOperationException(SR.GetResourceString(SR.ID1110));
            }

            return false;
        }

        protected override async Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            var transaction = Context.Get<OpenIddictValidationTransaction>(typeof(OpenIddictValidationTransaction).FullName) ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID1165));

            // Note: in many cases, the authentication token was already validated by the time this action is called
            // (generally later in the pipeline, when using the pass-through mode). To avoid having to re-validate it,
            // the authentication context is resolved from the transaction. If it's not available, a new one is created.
            var context = transaction.GetProperty<ProcessAuthenticationContext>(typeof(ProcessAuthenticationContext).FullName);
            if (context == null)
            {
                context = new ProcessAuthenticationContext(transaction);
                await _dispatcher.DispatchAsync(context);

                // Store the context object in the transaction so it can be later retrieved by handlers
                // that want to access the authentication result without triggering a new authentication flow.
                transaction.SetProperty(typeof(ProcessAuthenticationContext).FullName, context);
            }

            if (context.IsRequestHandled || context.IsRequestSkipped)
            {
                return null;
            }

            else if (context.IsRejected)
            {
                // Note: the missing_token error is special-cased to indicate to Katana
                // that no authentication result could be produced due to the lack of token.
                // This also helps reducing the logging noise when no token is specified.
                if (string.Equals(context.Error, Errors.MissingToken, StringComparison.Ordinal))
                {
                    return null;
                }

                var properties = new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictValidationOwinConstants.Properties.Error] = context.Error,
                    [OpenIddictValidationOwinConstants.Properties.ErrorDescription] = context.ErrorDescription,
                    [OpenIddictValidationOwinConstants.Properties.ErrorUri] = context.ErrorUri
                });

                return new AuthenticationTicket(null, properties);
            }

            else
            {
                // Store the token to allow any OWIN/Katana component (e.g a controller)
                // to retrieve it (e.g to make an API request to another application).
                var properties = new AuthenticationProperties(new Dictionary<string, string>
                {
                    [context.TokenType] = context.Token
                });

                return new AuthenticationTicket((ClaimsIdentity) context.Principal.Identity, properties);
            }
        }

        protected override async Task TeardownCoreAsync()
        {
            // Note: OWIN authentication handlers cannot reliabily write to the response stream
            // from ApplyResponseGrantAsync or ApplyResponseChallengeAsync because these methods
            // are susceptible to be invoked from AuthenticationHandler.OnSendingHeaderCallback,
            // where calling Write or WriteAsync on the response stream may result in a deadlock
            // on hosts using streamed responses. To work around this limitation, this handler
            // doesn't implement ApplyResponseGrantAsync but TeardownCoreAsync, which is never called
            // by AuthenticationHandler.OnSendingHeaderCallback. In theory, this would prevent
            // OpenIddictValidationOwinMiddleware from both applying the response grant and allowing
            // the next middleware in the pipeline to alter the response stream but in practice,
            // OpenIddictValidationOwinMiddleware is assumed to be the only middleware allowed to write
            // to the response stream when a response grant (sign-in/out or challenge) was applied.

            // Note: unlike the ASP.NET Core host, the OWIN host MUST check whether the status code
            // corresponds to a challenge response, as LookupChallenge() will always return a non-null
            // value when active authentication is used, even if no challenge was actually triggered.
            var challenge = Helper.LookupChallenge(Options.AuthenticationType, Options.AuthenticationMode);
            if (challenge != null && (Response.StatusCode == 401 || Response.StatusCode == 403))
            {
                var transaction = Context.Get<OpenIddictValidationTransaction>(typeof(OpenIddictValidationTransaction).FullName) ??
                    throw new InvalidOperationException(SR.GetResourceString(SR.ID1165));

                transaction.Properties[typeof(AuthenticationProperties).FullName] = challenge.Properties ?? new AuthenticationProperties();

                var context = new ProcessChallengeContext(transaction)
                {
                    Response = new OpenIddictResponse()
                };

                await _dispatcher.DispatchAsync(context);

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

                    await _dispatcher.DispatchAsync(notification);

                    if (notification.IsRequestHandled || context.IsRequestSkipped)
                    {
                        return;
                    }

                    throw new InvalidOperationException(SR.GetResourceString(SR.ID1110));
                }
            }
        }
    }
}
