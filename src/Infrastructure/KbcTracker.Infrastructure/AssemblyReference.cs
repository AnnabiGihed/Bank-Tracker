using System.Reflection;

namespace KbcTracker.Infrastructure;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Exposes the assembly of the infrastructure layer, for registration by scanning (Pivot.Framework
///              convention).
/// </summary>
public static class AssemblyReference
{
	#region Static Instances
	/// <summary>
	/// The assembly of the infrastructure layer.
	/// </summary>
	public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
	#endregion
}
