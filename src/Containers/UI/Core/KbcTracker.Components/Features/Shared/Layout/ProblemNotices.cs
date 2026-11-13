namespace KbcTracker.Components.Features.Shared.Layout;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Shows a failed action as an error notification with the problem's own words, so every component reports
///              a refused or unreachable backend the same way.
/// </summary>
public static class ProblemNotices
{
	#region Constants
	/// <summary>How long an error stays on screen, in milliseconds.</summary>
	private const double ErrorDuration = 8000;
	#endregion

	#region Public Methods
	/// <summary>Shows a problem as an error notification.</summary>
	/// <param name="notifications">The notification service of the host.</param>
	/// <param name="problem">The problem to show.</param>
	/// <exception cref="ArgumentNullException"><paramref name="notifications"/> or <paramref name="problem"/> is <see langword="null"/>.</exception>
	public static void Report(NotificationService notifications, BackendProblem problem)
	{
		ArgumentNullException.ThrowIfNull(notifications);
		ArgumentNullException.ThrowIfNull(problem);
		notifications.Notify(new NotificationMessage
		{
			Severity = NotificationSeverity.Error,
			Summary = problem.Title,
			Detail = problem.Detail,
			Duration = ErrorDuration,
		});
	}
	#endregion
}
