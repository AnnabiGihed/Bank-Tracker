using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace KbcTracker.ApiService.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Models the loopback proxy connection that TestServer does not otherwise populate.
/// </summary>
public sealed class LoopbackTestStartupFilter : IStartupFilter
{
	#region IStartupFilter Implementation
	/// <inheritdoc />
	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
	{
		app.Use(async (context, continuation) =>
		{
			context.Connection.RemoteIpAddress = IPAddress.Loopback;
			await continuation(context).ConfigureAwait(false);
		});
		next(app);
	};
	#endregion
}
