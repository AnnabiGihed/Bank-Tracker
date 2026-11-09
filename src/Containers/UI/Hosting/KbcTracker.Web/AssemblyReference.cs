using System.Reflection;

namespace KbcTracker.Web;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Exposes the assembly of the web host, for registration by scanning (Pivot.Framework convention).
/// </summary>
public static class AssemblyReference
{
	#region Static Instances
	/// <summary>
	/// The assembly of the web host.
	/// </summary>
	public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
	#endregion
}
