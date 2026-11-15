using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Marks every operation that needs the owner's access token with the bearer scheme in the OpenAPI document,
///              and leaves the anonymous ones (health) unmarked.
/// </summary>
public sealed class BearerSecurityOperationFilter : IOperationFilter
{
	#region Constants
	/// <summary>The name of the bearer security scheme in the document.</summary>
	public const string SchemeName = "Bearer";
	#endregion

	#region IOperationFilter Implementation
	/// <inheritdoc />
	public void Apply(OpenApiOperation operation, OperationFilterContext context)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(context);
		if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
		{
			return;
		}

		operation.Security ??= [];
		operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [] });
	}
	#endregion
}
