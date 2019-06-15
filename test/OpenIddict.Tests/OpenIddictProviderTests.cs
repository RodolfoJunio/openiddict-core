﻿using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using AspNet.Security.OpenIdConnect.Extensions;
using AspNet.Security.OpenIdConnect.Primitives;
using AspNet.Security.OpenIdConnect.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Moq;
using Newtonsoft.Json;
using OpenIddict.Core;
using OpenIddict.Models;

namespace OpenIddict.Tests {
    public partial class OpenIddictProviderTests {
        public const string AuthorizationEndpoint = "/connect/authorize";
        public const string ConfigurationEndpoint = "/.well-known/openid-configuration";
        public const string IntrospectionEndpoint = "/connect/introspect";
        public const string LogoutEndpoint = "/connect/logout";
        public const string RevocationEndpoint = "/connect/revoke";
        public const string TokenEndpoint = "/connect/token";
        public const string UserinfoEndpoint = "/connect/userinfo";

        private static TestServer CreateAuthorizationServer(Action<OpenIddictBuilder> configuration = null) {
            var builder = new WebHostBuilder();

            builder.UseEnvironment("Testing");

            builder.ConfigureLogging(options => options.AddDebug());

            builder.ConfigureServices(services => {
                services.AddAuthentication();
                services.AddOptions();

                var instance = services.AddOpenIddict()
                    // Disable the transport security requirement during testing.
                    .DisableHttpsRequirement()

                    // Enable the tested endpoints.
                    .EnableAuthorizationEndpoint(AuthorizationEndpoint)
                    .EnableIntrospectionEndpoint(IntrospectionEndpoint)
                    .EnableLogoutEndpoint(LogoutEndpoint)
                    .EnableRevocationEndpoint(RevocationEndpoint)
                    .EnableTokenEndpoint(TokenEndpoint)
                    .EnableUserinfoEndpoint(UserinfoEndpoint)

                    // Enable the tested flows.
                    .AllowAuthorizationCodeFlow()
                    .AllowClientCredentialsFlow()
                    .AllowImplicitFlow()
                    .AllowPasswordFlow()
                    .AllowRefreshTokenFlow()

                    // Register the X.509 certificate used to sign the identity tokens.
                    .AddSigningCertificate(
                        assembly: typeof(OpenIddictProviderTests).GetTypeInfo().Assembly,
                        resource: "OpenIddict.Tests.Certificate.pfx",
                        password: "OpenIddict")

                    // Note: overriding the default data protection provider is not necessary for the tests to pass,
                    // but is useful to ensure unnecessary keys are not persisted in testing environments, which also
                    // helps make the unit tests run faster, as no registry or disk access is required in this case.
                    .UseDataProtectionProvider(new EphemeralDataProtectionProvider());

                // Replace the default application/token managers.
                services.AddSingleton(CreateApplicationManager());
                services.AddSingleton(CreateTokenManager());

                // Run the configuration delegate
                // registered by the unit tests.
                configuration?.Invoke(instance);
            });

            builder.Configure(app => {
                app.UseStatusCodePages(context => {
                    context.HttpContext.Response.Headers[HeaderNames.ContentType] = "application/json";

                    return context.HttpContext.Response.WriteAsync(JsonConvert.SerializeObject(new {
                        error_custom = OpenIdConnectConstants.Errors.InvalidRequest
                    }));
                });

                app.Use(next => context => {
                    if (context.Request.Path != "/authorize-status-code-middleware" &&
                        context.Request.Path != "/logout-status-code-middleware") {
                        var feature = context.Features.Get<IStatusCodePagesFeature>();
                        feature.Enabled = false;
                    }

                    return next(context);
                });

                app.UseCookieAuthentication();

                // Note: the following client_id/client_secret are fake and are only
                // used to test the metadata returned by the discovery endpoint.
                app.UseFacebookAuthentication(new FacebookOptions {
                    ClientId = "16018790-E88E-4553-8036-BB342579FF19",
                    ClientSecret = "3D6499AF-5607-489B-815A-F3ACF1617296",
                    SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme
                });

                app.UseGoogleAuthentication(new GoogleOptions {
                    ClientId = "BAF437A5-87FA-4D06-8EFD-F9BA96CCEDC4",
                    ClientSecret = "27DF07D3-6B03-4EE0-95CD-3AC16782216B",
                    SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme
                });

                app.UseOpenIddict();

                app.Run(context => {
                    var request = context.GetOpenIdConnectRequest();

                    if (context.Request.Path == AuthorizationEndpoint || context.Request.Path == TokenEndpoint) {
                        var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
                        identity.AddClaim(ClaimTypes.NameIdentifier, "Bob le Magnifique");

                        var ticket = new AuthenticationTicket(
                            new ClaimsPrincipal(identity),
                            new AuthenticationProperties(),
                            OpenIdConnectServerDefaults.AuthenticationScheme);

                        ticket.SetScopes(request.GetScopes());

                        return context.Authentication.SignInAsync(ticket.AuthenticationScheme, ticket.Principal, ticket.Properties);
                    }

                    else if (context.Request.Path == LogoutEndpoint) {
                        return context.Authentication.SignOutAsync(OpenIdConnectServerDefaults.AuthenticationScheme);
                    }

                    else if (context.Request.Path == UserinfoEndpoint) {
                        context.Response.Headers[HeaderNames.ContentType] = "application/json";

                        return context.Response.WriteAsync(JsonConvert.SerializeObject(new {
                            access_token = request.AccessToken,
                            sub = "Bob le Bricoleur"
                        }));
                    }

                    return Task.FromResult(0);
                });
            });

            return new TestServer(builder);
        }

        private static OpenIddictApplicationManager<OpenIddictApplication> CreateApplicationManager(Action<Mock<OpenIddictApplicationManager<OpenIddictApplication>>> setup = null) {
            var manager = new Mock<OpenIddictApplicationManager<OpenIddictApplication>>(
                Mock.Of<IOpenIddictApplicationStore<OpenIddictApplication>>(),
                Mock.Of<ILogger<OpenIddictApplicationManager<OpenIddictApplication>>>());

            setup?.Invoke(manager);

            return manager.Object;
        }

        private static OpenIddictTokenManager<OpenIddictToken> CreateTokenManager(Action<Mock<OpenIddictTokenManager<OpenIddictToken>>> setup = null) {
            var manager = new Mock<OpenIddictTokenManager<OpenIddictToken>>(
                Mock.Of<IOpenIddictTokenStore<OpenIddictToken>>(),
                Mock.Of<ILogger<OpenIddictTokenManager<OpenIddictToken>>>());

            setup?.Invoke(manager);

            return manager.Object;
        }
    }
}
