using System.Globalization;

namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Formats amounts, months and dates the way the tabs show them, with the culture given explicitly so
///              a host's own culture (a phone's language, a server's locale) never changes what the owner reads.
///              Month names are always English, as before.
/// </summary>
public sealed class Formats
{
	#region Static Instances
	/// <summary>The formats of the owner: Belgian French numbers (space as thousands separator, comma as decimal separator).</summary>
	public static readonly Formats Belgian = new(CultureInfo.GetCultureInfo("fr-BE"));

	/// <summary>The culture of month names.</summary>
	private static readonly CultureInfo MonthCulture = CultureInfo.GetCultureInfo("en-GB");
	#endregion

	#region Fields
	/// <summary>The culture of numbers, dates and sorting.</summary>
	private readonly CultureInfo _culture;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="Formats"/> for a culture.</summary>
	/// <param name="culture">The culture of numbers, dates and the sort order of names.</param>
	/// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
	public Formats(CultureInfo culture)
	{
		ArgumentNullException.ThrowIfNull(culture);
		_culture = culture;
	}
	#endregion

	#region Properties
	/// <summary>Gets the comparer that orders names for pickers and lists.</summary>
	public StringComparer NameComparer => StringComparer.Create(_culture, ignoreCase: false);
	#endregion

	#region Public Methods
	/// <summary>Formats an amount of money with cents.</summary>
	/// <param name="amount">The amount in euro.</param>
	/// <returns>The amount, followed by the euro sign.</returns>
	public string Money(decimal amount) => amount.ToString("#,##0.00", _culture) + " €";

	/// <summary>Formats an amount of money without cents.</summary>
	/// <param name="amount">The amount in euro.</param>
	/// <returns>The rounded amount, followed by the euro sign.</returns>
	public string WholeMoney(decimal amount) => amount.ToString("#,##0", _culture) + " €";

	/// <summary>Formats a <c>yyyy-MM</c> month as its English name and year.</summary>
	/// <param name="month">The month, as <c>yyyy-MM</c>.</param>
	/// <returns>The month name and year, such as <c>March 2026</c>.</returns>
	public static string MonthName(string month)
	{
		ArgumentNullException.ThrowIfNull(month);
		return DateTime.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("MMMM yyyy", MonthCulture);
	}

	/// <summary>Formats a <c>yyyy-MM</c> month for a chart axis: the first three letters of its name and the two-digit year.</summary>
	/// <param name="month">The month, as <c>yyyy-MM</c>.</param>
	/// <returns>The label, such as <c>Mar 26</c>.</returns>
	public static string ShortMonth(string month) => MonthName(month)[..3] + " " + month[2..4];

	/// <summary>Formats a day and month.</summary>
	/// <param name="date">The date, or <see langword="null"/>.</param>
	/// <returns>The date as <c>dd/MM</c>, or an empty string for none.</returns>
	public string Day(DateOnly? date) => date?.ToString("dd/MM", _culture) ?? string.Empty;

	/// <summary>Formats a date with a two-digit year.</summary>
	/// <param name="date">The date, or <see langword="null"/>.</param>
	/// <returns>The date as <c>dd/MM/yy</c>, or an empty string for none.</returns>
	public string ShortDate(DateOnly? date) => date?.ToString("dd/MM/yy", _culture) ?? string.Empty;

	/// <summary>Formats a date with a four-digit year.</summary>
	/// <param name="date">The date, or <see langword="null"/>.</param>
	/// <returns>The date as <c>dd/MM/yyyy</c>, or an empty string for none.</returns>
	public string FullDate(DateOnly? date) => date?.ToString("dd/MM/yyyy", _culture) ?? string.Empty;

	/// <summary>Formats a moment in the local time zone of the device, with day and month only.</summary>
	/// <param name="moment">The moment, or <see langword="null"/>.</param>
	/// <returns>The moment as <c>dd/MM HH:mm</c>, or an empty string for none.</returns>
	public string LocalDayTime(DateTimeOffset? moment) => moment?.ToLocalTime().ToString("dd/MM HH:mm", _culture) ?? string.Empty;

	/// <summary>Formats a moment in the local time zone of the device, with the year.</summary>
	/// <param name="moment">The moment, or <see langword="null"/>.</param>
	/// <returns>The moment as <c>dd/MM/yyyy HH:mm</c>, or an empty string for none.</returns>
	public string LocalFullDayTime(DateTimeOffset? moment) => moment?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", _culture) ?? string.Empty;

	/// <summary>Formats a moment in the local time zone of the device as a date only.</summary>
	/// <param name="moment">The moment, or <see langword="null"/>.</param>
	/// <returns>The date as <c>dd/MM/yyyy</c>, or an empty string for none.</returns>
	public string LocalFullDate(DateTimeOffset? moment) => moment?.ToLocalTime().ToString("dd/MM/yyyy", _culture) ?? string.Empty;

	/// <summary>Formats a plain number such as a count.</summary>
	/// <param name="value">The number.</param>
	/// <returns>The number in the culture's digits.</returns>
	public string Number(int value) => value.ToString(_culture);

	/// <summary>Formats an amount with two decimals and no grouping, in the culture's decimal separator, as the search text of a payment holds it.</summary>
	/// <param name="amount">The amount.</param>
	/// <returns>The amount, such as <c>12,50</c>.</returns>
	public string Decimal2(decimal amount) => amount.ToString("0.00", _culture);

	/// <summary>Formats an amount for a text field, with two decimals and no grouping, so it parses back unchanged.</summary>
	/// <param name="amount">The amount.</param>
	/// <returns>The amount with a dot as decimal separator.</returns>
	public static string Plain(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
	#endregion
}
