﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace Microsoft.AspNetCore;

/// <summary>
/// Exposes companion extensions for the OpenIddict/ASP.NET Core integration.
/// </summary>
public static class OpenIddictServerAspNetCoreHelpers
{
    /// <summary>
    /// Retrieves the <see cref="HttpRequest"/> instance stored in the <see cref="OpenIddictServerTransaction"/> properties.
    /// </summary>
    /// <param name="transaction">The transaction instance.</param>
    /// <returns>The <see cref="HttpRequest"/> instance or <see langword="null"/> if it couldn't be found.</returns>
    public static HttpRequest? GetHttpRequest(this OpenIddictServerTransaction transaction!!)
        => transaction.Properties.TryGetValue(typeof(HttpRequest).FullName!, out object? property) &&
           property is WeakReference<HttpRequest> reference &&
           reference.TryGetTarget(out HttpRequest? request) ? request : null;

    /// <summary>
    /// Retrieves the <see cref="OpenIddictServerEndpointType"/> instance stored in <see cref="BaseContext"/>.
    /// </summary>
    /// <param name="context">The context instance.</param>
    /// <returns>The <see cref="OpenIddictServerEndpointType"/>.</returns>
    public static OpenIddictServerEndpointType GetOpenIddictServerEndpointType(this HttpContext context!!)
        => context.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction?.EndpointType ?? default;

    /// <summary>
    /// Retrieves the <see cref="OpenIddictRequest"/> instance stored in <see cref="BaseContext"/>.
    /// </summary>
    /// <param name="context">The context instance.</param>
    /// <returns>The <see cref="OpenIddictRequest"/> instance or <see langword="null"/> if it couldn't be found.</returns>
    public static OpenIddictRequest? GetOpenIddictServerRequest(this HttpContext context!!)
        => context.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction?.Request;

    /// <summary>
    /// Retrieves the <see cref="OpenIddictResponse"/> instance stored in <see cref="BaseContext"/>.
    /// </summary>
    /// <param name="context">The context instance.</param>
    /// <returns>The <see cref="OpenIddictResponse"/> instance or <see langword="null"/> if it couldn't be found.</returns>
    public static OpenIddictResponse? GetOpenIddictServerResponse(this HttpContext context!!)
        => context.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction?.Response;
}
