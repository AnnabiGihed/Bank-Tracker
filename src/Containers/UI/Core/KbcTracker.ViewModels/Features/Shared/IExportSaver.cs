namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The port through which a host hands an exported document to the owner: the web app as a download, a
///              phone through its share or save dialog.
/// </summary>
public interface IExportSaver
{
	#region Methods
	/// <summary>Saves or shares an exported document.</summary>
	/// <param name="fileName">The suggested file name.</param>
	/// <param name="contentType">The media type of the document, such as <c>text/csv</c>.</param>
	/// <param name="content">The document.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>A task that completes when the document has been handed over.</returns>
	Task SaveAsync(string fileName, string contentType, string content, CancellationToken cancellationToken);
	#endregion
}
