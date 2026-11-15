using KbcTracker.ApiService.Features.Authentication;
using Microsoft.Extensions.Logging;

namespace KbcTracker.ApiService.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the log entry of a refused provider account carries the issuer and the subject, as a warning, and
///              nothing that looks like an email address.
/// </summary>
public sealed class OwnerRefusalLogTests
{
	#region Log Tests
	/// <summary>Verifies the entry is a warning with the issuer and subject and no email address.</summary>
	[Fact]
	public void LogAccountRefused_IssuerAndSubject_WarningWithBothAndNoEmail()
	{
		var logger = new CapturingLogger();

		logger.LogAccountRefused("https://accounts.google.com", "1234567890");

		logger.Level.Should().Be(LogLevel.Warning);
		logger.Message.Should().Contain("https://accounts.google.com").And.Contain("1234567890").And.Contain("Authentication:Owners").And.NotContain("@");
	}
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Keeps the last log entry written to it, so a test can check what was logged.
	/// </summary>
	private sealed class CapturingLogger : ILogger
	{
		#region Properties
		/// <summary>Gets the level of the last entry.</summary>
		public LogLevel Level { get; private set; }

		/// <summary>Gets the formatted text of the last entry.</summary>
		public string Message { get; private set; } = string.Empty;
		#endregion

		#region ILogger Implementation
		/// <inheritdoc />
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		/// <inheritdoc />
		public bool IsEnabled(LogLevel logLevel) => true;

		/// <inheritdoc />
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			Level = logLevel;
			Message = formatter(state, exception);
		}
		#endregion
	}
	#endregion
}
