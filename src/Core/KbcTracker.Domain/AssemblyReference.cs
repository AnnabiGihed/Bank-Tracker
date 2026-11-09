using System.Reflection;

namespace KbcTracker.Domain;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Exposes the assembly of the domain layer, for registration by scanning (Pivot.Framework convention).
/// </summary>
public static class AssemblyReference
{
	#region Static Instances
	/// <summary>
	/// The assembly of the domain layer.
	/// </summary>
	public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
	#endregion
}
