using Microsoft.Data.Sqlite;
using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;

namespace JgransEconomySystem
{
	public static class Transaction
	{
		private static string path = Path.Combine(TShock.SavePath, "JgransEconomyBanks.sqlite");
		private static string transactionPath = Path.Combine(TShock.SavePath, "TransactionData.sqlite");
		private static EconomyDatabase bank = new EconomyDatabase(path);
		static JgransEconomySystemConfig config = new JgransEconomySystemConfig();
		private static readonly string connectionString = $"Data Source={transactionPath}";

		private static double TaxRate => config.TaxRate.Value;

		public const string ReceivedFromKillingNormalNPC = "Received from killing normal NPC";
		public const string ReceivedFromKillingSpecialNPC = "Received from killing special NPC";
		public const string ReceivedFromKillingHostileNPC = "Received from killing hostile NPC";
		public const string ReceivedFromPayment = "Received payment from ";
		public const string ReceivedFromKillingBossNPC = "Received from killing boss NPC";
		public const string ReceivedFromVoting = "Received from voting the server";
		public const string PurchasedFromShop = "Bought an item from shop";
		public const string SoldItemToShop = "Sold an item from shop";

		private static int CalculateTax(int amount)
		{
			var taxAmount = (int)Math.Ceiling(amount * TaxRate);
			return taxAmount;
		}

		public static async Task ProcessTransaction(int playerId, string playerName, int amount)
		{
			var taxAmount = CalculateTax(amount);
			var netAmount = amount - taxAmount;

			var currentBalance = await bank.GetCurrencyAmount(playerId);
			var newBalance = currentBalance + netAmount;

			await bank.SaveCurrencyAmount(playerId, newBalance);
			await RecordTransaction(playerName, "Received from transaction", amount);
			await RecordTaxTransaction(taxAmount);
		}

		public static async Task InitializeTransactionDataAsync()
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				using (var transactionCommand = connection.CreateCommand())
				{
					transactionCommand.CommandText = @"CREATE TABLE IF NOT EXISTS TransactionData (
													Id INTEGER PRIMARY KEY,
													PlayerName TEXT,
													Reason TEXT,
													Amount INTEGER,
													Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
												)";
					await transactionCommand.ExecuteNonQueryAsync();
				}
			}
		}

		public static async Task RecordTransaction(string playerName, string reason, int amount)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				using (var command = connection.CreateCommand())
				{
					command.CommandText = @"INSERT INTO TransactionData (PlayerName, Reason, Amount) 
										VALUES (@PlayerName, @Reason, @Amount)";
					command.Parameters.AddWithValue("@PlayerName", playerName);
					command.Parameters.AddWithValue("@Reason", reason);
					command.Parameters.AddWithValue("@Amount", amount);

					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public static async Task RecordTaxTransaction(int amount)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				using (var command = connection.CreateCommand())
				{
					command.CommandText = @"INSERT INTO TransactionData (PlayerName, Reason, Amount) 
										VALUES (@PlayerName, @Reason, @Amount)";
					command.Parameters.AddWithValue("@PlayerName", "ServerBank");
					command.Parameters.AddWithValue("@Reason", "Tax+Sales from transactions");
					command.Parameters.AddWithValue("@Amount", amount);

					await command.ExecuteNonQueryAsync();

					var currentBalance = await bank.GetCurrencyAmount(0);
					var newBalance = currentBalance + amount;
					await bank.SaveCurrencyAmount(0, newBalance);
				}
			}
		}

		public static async Task<List<TransactionData>> GetAllTransactionsAsync()
		{
			var transactions = new List<TransactionData>();

			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				using (var command = connection.CreateCommand())
				{
					command.CommandText = "SELECT * FROM TransactionData";

					using (var reader = await command.ExecuteReaderAsync())
					{
						while (await reader.ReadAsync())
						{
							var transaction = new TransactionData
							{
								Id = Convert.ToInt32(reader["Id"]),
								PlayerName = reader["PlayerName"].ToString(),
								Reason = reader["Reason"].ToString(),
								Amount = Convert.ToInt32(reader["Amount"]),
								Timestamp = Convert.ToDateTime(reader["Timestamp"])
							};
							transactions.Add(transaction);
						}
					}
				}
			}

			return transactions;
		}
	}

	public class TransactionData
	{
		public int Id { get; set; }
		public string PlayerName { get; set; }
		public string Reason { get; set; }
		public int Amount { get; set; }
		public DateTime Timestamp { get; set; }
	}
}
