namespace KbcTracker.Components.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The pill that says where a fixed payment stands this month (paid, paid a different amount, due, late).
///              The state is always written out, so the colour is only a hint.
/// </summary>
public partial class StatusPill : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the status to show.</summary>
	[EditorRequired]
	[Parameter]
	public required FixedStatusPill Pill { get; set; }
	#endregion

	#region Properties
	/// <summary>Gets the style class of the pill's tone.</summary>
	private string ToneClass => Pill.Tone switch
	{
		PillTone.Ok => "ok",
		PillTone.Due => "due",
		PillTone.Bad => "bad",
		_ => "nil",
	};
	#endregion
}
