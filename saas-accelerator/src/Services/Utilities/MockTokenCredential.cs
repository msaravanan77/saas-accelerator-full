// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Azure.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.Services.Utilities;

/// <summary>
/// Mock TokenCredential for local development without Azure AD.
/// Returns a dummy token that satisfies the MarketplaceSaaSClient constructor
/// but doesn't perform actual authentication. Use with API emulator running
/// with REQUIRE_AUTH=false.
/// </summary>
public class MockTokenCredential : TokenCredential
{
    /// <summary>
    /// Gets an AccessToken with a dummy value for local testing.
    /// </summary>
    /// <param name="requestContext">The TokenRequestContext.</param>
    /// <param name="cancellationToken">The CancellationToken.</param>
    /// <returns>A dummy AccessToken.</returns>
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new AccessToken("mock-token-for-local-dev", DateTimeOffset.UtcNow.AddHours(1));
    }

    /// <summary>
    /// Gets an AccessToken asynchronously with a dummy value for local testing.
    /// </summary>
    /// <param name="requestContext">The TokenRequestContext.</param>
    /// <param name="cancellationToken">The CancellationToken.</param>
    /// <returns>A ValueTask containing a dummy AccessToken.</returns>
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new ValueTask<AccessToken>(new AccessToken("mock-token-for-local-dev", DateTimeOffset.UtcNow.AddHours(1)));
    }
}
