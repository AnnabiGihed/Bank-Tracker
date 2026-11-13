using System.Globalization;

namespace KbcTracker.ViewModels.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="Formats"/>: the amounts, months and dates read as the old pages showed them, and no
///              host culture changes them.
/// </summary>
public sealed class FormatsTests
{
	#region Format Tests
	/// <summary>Verifies amounts use Belgian French separators and the euro sign, with and without cents.</summary>
	[Fact]
	public void Money_BelgianFormats_CommaDecimalAndEuroSign()
	{
		Formats.Belgian.Money(12.5m).Should().Be("12,50 €");
		Formats.Belgian.Money(-8m).Should().Be("-8,00 €");
		Formats.Belgian.WholeMoney(940m).Should().Be("940 €");
		Formats.Belgian.Money(1234.5m).Should().StartWith("1").And.EndWith("234,50 €").And.NotContain(".");
	}

	/// <summary>Verifies months read as English names, whatever the formats' culture, with the short axis label.</summary>
	[Fact]
	public void MonthName_AnyCulture_EnglishNameAndShortLabel()
	{
		Formats.MonthName("2026-03").Should().Be("March 2026");
		Formats.ShortMonth("2026-03").Should().Be("Mar 26");
	}

	/// <summary>Verifies dates keep the day-first formats of the old pages and tolerate a missing date.</summary>
	[Fact]
	public void Dates_DayFirstFormats_EmptyForNone()
	{
		var date = new DateOnly(2026, 10, 3);

		Formats.Belgian.Day(date).Should().Be("03/10");
		Formats.Belgian.ShortDate(date).Should().Be("03/10/26");
		Formats.Belgian.FullDate(date).Should().Be("03/10/2026");
		Formats.Belgian.Day(null).Should().BeEmpty();
		Formats.Belgian.LocalDayTime(null).Should().BeEmpty();
	}

	/// <summary>Verifies the host's own culture, such as a phone set to another language, never changes the output.</summary>
	[Fact]
	public void Formats_HostCultureChanged_OutputUnchanged()
	{
		var original = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

			Formats.Belgian.Money(12.5m).Should().Be("12,50 €");
			Formats.Belgian.Day(new DateOnly(2026, 10, 3)).Should().Be("03/10");
			Formats.Belgian.Decimal2(12.5m).Should().Be("12,50");
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	/// <summary>Verifies a formats instance needs a culture.</summary>
	[Fact]
	public void Constructor_NullCulture_Throws()
	{
		var act = () => new Formats(null!);

		act.Should().Throw<ArgumentNullException>();
	}
	#endregion
}
