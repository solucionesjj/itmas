using System.ComponentModel.DataAnnotations;
using Itmas3.Api.Authorization;
using Itmas3.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.IntegrationTests.Infrastructure;

/// <summary>
/// Endpoints that exist ONLY in the test host to exercise cross-cutting behaviour
/// (authorization defaults, CSRF, validation, error handling) without adding fake features to the product.
/// </summary>
[ApiController]
[Route("api/test")]
public sealed class TestEndpointsController : ControllerBase
{
    [HttpGet("no-authorization-metadata")]
    public IActionResult NoAuthorizationMetadata() => Ok();

    [HttpGet("authenticated")]
    [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
    public IActionResult Authenticated() => Ok();

    [HttpGet("permission")]
    [RequirePermission("test.resources.read")]
    public IActionResult Permission() => Ok();

    [HttpPost("echo")]
    [AllowAnonymous]
    public ActionResult<EchoRequest> Echo(EchoRequest request) => request;

    [HttpGet("unexpected-error")]
    [AllowAnonymous]
    public IActionResult UnexpectedError() =>
        throw new InvalidOperationException("internal-detail Host=db;Password=super-secret");

    [HttpGet("domain-error")]
    [AllowAnonymous]
    public IActionResult DomainError() => throw new DomainException("La operación no cumple la regla de negocio.");
}

public sealed record EchoRequest([Required, StringLength(50)] string Message);
