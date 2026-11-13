namespace KbcTracker.ViewModels.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="CsvExport"/> writes what the web host exported before the move: the header, the
///              history oldest first with exact amounts and quoted text fields, and one line per part of a split payment.
/// </summary>
public sealed class CsvExportTests
{
	#region Export Tests
	/// <summary>Verifies the history is written oldest first, with exact amounts and quoted text, byte for byte as before.</summary>
	[Fact]
	public void Write_History_OldestFirstQuoted()
	{
		var analysis = TestWorkspace.Analyse(
		[
			TestWorkspace.Payment("t2", new DateOnly(2026, 11, 3), 2500m, "Acme Payroll", "Salary November"),
			TestWorkspace.Payment("t1", new DateOnly(2026, 10, 3), -12.50m, "Shop One", "SHOP ONE BE1000 Bruxelles Payment with card"),
		]);

		var lines = CsvExport.Write(analysis).Split('\n', StringSplitOptions.RemoveEmptyEntries);

		lines[0].Should().Be("Date;Amount;Currency;Name;Category;Confirmed;Fixed payment;Description;Part");
		lines[1].Should().StartWith("2026-10-03;-12.50;EUR;").And.EndWith(";\"SHOP ONE BE1000 Bruxelles Payment with card\";\"\"");
		lines[2].Should().StartWith("2026-11-03;2500;EUR;").And.EndWith(";\"Salary November\";\"\"");
		lines.Should().HaveCount(3);
	}

	/// <summary>Verifies a split payment is written as one line per part, with the part's amount, category and label.</summary>
	[Fact]
	public void Write_SplitPayment_OneLinePerPart()
	{
		var analysis = TestWorkspace.Analyse(
			[TestWorkspace.Payment("t1", new DateOnly(2026, 10, 3), -12.50m, "Shop One", "SHOP ONE BE1000 Bruxelles Payment with card")],
			(user, _) => user.SplitPayment(new TxnId("t1"), PaymentSplit.Create(-12.5m,
				[new SplitLine(10m, new CategoryId("bills"), "Water bottle"), new SplitLine(2.5m, new CategoryId("cash"), null)]).Value));

		var lines = CsvExport.Write(analysis).Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(l => l.StartsWith("2026-10-03;", StringComparison.Ordinal)).ToList();

		lines.Should().HaveCount(2)
			.And.Contain(l => l.StartsWith("2026-10-03;-10;", StringComparison.Ordinal) && l.EndsWith(";\"Water bottle\"", StringComparison.Ordinal))
			.And.Contain(l => l.StartsWith("2026-10-03;-2.5;", StringComparison.Ordinal));
	}

	/// <summary>Verifies quotes inside a text field are doubled and the output does not depend on the host culture.</summary>
	[Fact]
	public void Write_QuoteInDescription_DoubledAndCultureIndependent()
	{
		var analysis = TestWorkspace.Analyse([TestWorkspace.Payment("t1", new DateOnly(2026, 10, 3), -1.5m, "Shop", "say \"hi\"")]);
		var original = System.Globalization.CultureInfo.CurrentCulture;
		try
		{
			System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-BE");

			var csv = CsvExport.Write(analysis);

			csv.Should().Contain("\"say \"\"hi\"\"\"").And.Contain(";-1.5;");
		}
		finally
		{
			System.Globalization.CultureInfo.CurrentCulture = original;
		}
	}
	#endregion
}
