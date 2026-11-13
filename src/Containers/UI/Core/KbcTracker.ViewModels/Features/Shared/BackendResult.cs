namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The outcome of one backend call, for the UI to show: either the value or the problem to display.
/// </summary>
/// <typeparam name="T">The value of a successful call.</typeparam>
/// <param name="Value">The value, when the call succeeded; it may be <see langword="null"/> for a call without a body.</param>
/// <param name="Problem">The problem to show, or <see langword="null"/> when the call succeeded.</param>
public sealed record BackendResult<T>(T? Value, BackendProblem? Problem)
{
	#region Properties
	/// <summary>Gets a value indicating whether the call succeeded.</summary>
	public bool IsSuccess => Problem is null;
	#endregion
}
