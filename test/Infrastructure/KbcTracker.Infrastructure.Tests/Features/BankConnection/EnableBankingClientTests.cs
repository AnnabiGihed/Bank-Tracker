using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace KbcTracker.Infrastructure.Tests.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the Enable Banking client and the transaction source on top of it, without the network:
///              the query it sends, paging through continuation keys, the signed JWT, error handling, and mapping.
///
///              The API is a fake HTTP handler; the private key is a throwaway RSA key in a temporary file.
/// </summary>
public sealed class EnableBankingClientTests : IDisposable
{
	#region Fields
	/// <summary>
	/// The temporary PEM file of this test's throwaway key.
	/// </summary>
	private readonly string _keyFile = Path.GetTempFileName();

	/// <summary>
	/// Every request the fake API received.
	/// </summary>
	private readonly List<HttpRequestMessage> _requests = [];
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="EnableBankingClientTests"/> and writes the throwaway RSA key.
	/// </summary>
	public EnableBankingClientTests()
	{
		using var rsa = RSA.Create(2048);
		File.WriteAllText(_keyFile, rsa.ExportPkcs8PrivateKeyPem());
	}
	#endregion

	#region Transaction Tests
	/// <summary>
	/// Verifies that a full download asks for the longest history and that every request carries a signed JWT.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task GetTransactionsAsync_Longest_StrategyLongestAndSignedJwt()
	{
		var client = Client(_ => Json("""{"transactions":[]}"""));

		await client.GetTransactionsAsync("acc-1", new DateOnly(2025, 10, 1), longest: true);

		var request = _requests.Single();
		request.RequestUri!.ToString().Should().Be("https://api.enablebanking.com/accounts/acc-1/transactions?date_from=2025-10-01&strategy=longest");
		request.Headers.Authorization!.Scheme.Should().Be("Bearer");
		request.Headers.Authorization.Parameter!.Split('.').Should().HaveCount(3, "a JWT has a header, a payload and a signature");
	}

	/// <summary>
	/// Verifies that the client follows continuation keys until the API stops sending one.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task GetTransactionsAsync_ContinuationKey_ReadsEveryPage()
	{
		var client = Client(request => request.RequestUri!.Query.Contains("continuation_key=page%202", StringComparison.Ordinal)
			? Json("""{"transactions":[{"entry_reference":"b"}]}""")
			: Json("""{"transactions":[{"entry_reference":"a"}],"continuation_key":"page 2"}"""));

		var transactions = await client.GetTransactionsAsync("acc-1", from: null);

		transactions.Should().HaveCount(2);
		_requests.Should().HaveCount(2);
	}

	/// <summary>
	/// Verifies that an error status becomes an <see cref="InvalidOperationException"/> naming the status.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task GetTransactionsAsync_ErrorStatus_InvalidOperationException()
	{
		var client = Client(_ => Json("""{"error":"EXPIRED_SESSION"}""", HttpStatusCode.Unauthorized));

		var act = () => client.GetTransactionsAsync("acc-1", from: null);

		(await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("401");
	}

	/// <summary>
	/// Verifies that the transaction source maps the downloaded JSON to transactions of the account.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task TransactionSource_Download_MapsToAccountTransactions()
	{
		var source = new EnableBankingTransactionSource(Client(_ => Json("""
			{"transactions":[{"entry_reference":"x1","transaction_amount":{"currency":"EUR","amount":"10.00"},
			"credit_debit_indicator":"DBIT","booking_date":"2026-11-02","remittance_information":"Test"}]}
			""")));

		var transactions = await source.GetTransactionsAsync("acc-1", from: null, longest: false);

		var txn = transactions.Single();
		txn.AccountUid.Should().Be("acc-1");
		txn.Amount.Should().Be(-10m);
	}

	/// <summary>
	/// Verifies that a transaction the mapper cannot read is reported as <see cref="InvalidDataException"/>, not as
	/// the <see cref="InvalidOperationException"/> that means "the bank refused" and would make the sync retry.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task TransactionSource_UnreadableTransaction_InvalidDataException()
	{
		var source = new EnableBankingTransactionSource(Client(_ => Json("""
			{"transactions":[{"entry_reference":"x1","transaction_amount":"not an object","booking_date":"2026-11-02"}]}
			""")));

		var act = () => source.GetTransactionsAsync("acc-1", from: null, longest: false);

		await act.Should().ThrowAsync<InvalidDataException>();
	}
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose() => File.Delete(_keyFile);
	#endregion

	#region Private Helpers
	/// <summary>
	/// Creates a client whose API answers every request with <paramref name="respond"/>.
	/// </summary>
	private EnableBankingClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond)
	{
		var options = new EnableBankingOptions { ApplicationId = "test-app", PrivateKeyPath = _keyFile };
		return new EnableBankingClient(new HttpClient(new FakeApi(request => { _requests.Add(request); return respond(request); })), options);
	}

	/// <summary>
	/// A JSON response with the given status.
	/// </summary>
	private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
		new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Stands in for the Enable Banking API: answers every request through a delegate.
	/// </summary>
	private sealed class FakeApi : HttpMessageHandler
	{
		#region Fields
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
		#endregion

		#region Constructors
		/// <summary>
		/// Initialises a new <see cref="FakeApi"/>.
		/// </summary>
		/// <param name="respond">Builds the response to each request.</param>
		public FakeApi(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
		#endregion

		#region Overrides
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(_respond(request));
		#endregion
	}
	#endregion
}
