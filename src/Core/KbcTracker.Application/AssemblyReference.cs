using System.Reflection;

namespace KbcTracker.Application;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Exposes the assembly of the application layer, for registration by scanning (Pivot.Framework
///              convention): command and query handlers and validators are found in it.
/// </summary>
public static class AssemblyReference
{
	#region Static Instances
	/// <summary>
	/// The assembly of the application layer.
	/// </summary>
	public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
	#endregion
}
