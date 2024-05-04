using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using TShockAPI;

namespace JgransEconomySystem
{
	public class EconomyDatabase
	{
		private readonly string connectionString;
		private const int ServerBankId = 0;

		public EconomyDatabase(string databasePath)
		{
			connectionString = $"Data Source={databasePath}";
			InitializeDatabase();
		}

		private async Task InitializeDatabase()
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				using var command = connection.CreateCommand();
				command.CommandText = @"CREATE TABLE IF NOT EXISTS EconomyData (PlayerId INTEGER PRIMARY KEY, CurrencyAmount INTEGER)";
				await command.ExecuteNonQueryAsync();

				using var transactionCommand = connection.CreateCommand();
				transactionCommand.CommandText = @"CREATE TABLE IF NOT EXISTS TransactionData (Id INTEGER PRIMARY KEY, PlayerName TEXT, Reason TEXT, Amount INTEGER, Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP)";
				await transactionCommand.ExecuteNonQueryAsync();

				using var shopCommand = connection.CreateCommand();
				shopCommand.CommandText = @"CREATE TABLE IF NOT EXISTS SwitchShop (Id INTEGER PRIMARY KEY, X INTEGER, Y INTEGER, Item INTEGER, Stack INTEGER, Price INTEGER, AllowedGroup TEXT, WorldID INTEGER)";
				await shopCommand.ExecuteNonQueryAsync();

				using var rankCommand = connection.CreateCommand();
				rankCommand.CommandText = @"CREATE TABLE IF NOT EXISTS Ranks (RankId INTEGER PRIMARY KEY, RankName TEXT, RequiredCurrencyAmount INTEGER, GroupName TEXT, NextRank TEXT)";
				await rankCommand.ExecuteNonQueryAsync();

				using var sellCommand = connection.CreateCommand();
				sellCommand.CommandText = @"CREATE TABLE IF NOT EXISTS CommandShop (ID INTEGER PRIMARY KEY AUTOINCREMENT, X INTEGER, Y INTEGER, Command TEXT NOT NULL, Price INTEGER NOT NULL, AllowedGroup TEXT, WorldID INTEGER)";
				await sellCommand.ExecuteNonQueryAsync();

				using var chestCommand = connection.CreateCommand();
				chestCommand.CommandText = @"CREATE TABLE IF NOT EXISTS BuyerChests (ID INTEGER PRIMARY KEY, X INTEGER, Y INTEGER)";
				await chestCommand.ExecuteNonQueryAsync();
				{
					command.ExecuteNonQuery();
				}
			}
		}

		public async Task SaveCurrencyAmount(int playerId, int currencyAmount)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var saveCommand = @"INSERT INTO EconomyData (PlayerId, CurrencyAmount)
							VALUES (@PlayerId, @CurrencyAmount)
							ON CONFLICT(PlayerId) DO UPDATE SET CurrencyAmount = @CurrencyAmount";

				using (var command = new SqliteCommand(saveCommand, connection))
				{
					command.Parameters.AddWithValue("@PlayerId", playerId);
					command.Parameters.AddWithValue("@CurrencyAmount", currencyAmount);
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task<int> GetCurrencyAmount(int playerId)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var selectCommand = "SELECT CurrencyAmount FROM EconomyData WHERE PlayerId = @PlayerId";

				using (var command = new SqliteCommand(selectCommand, connection))
				{
					command.Parameters.AddWithValue("@PlayerId", playerId);
					var result = await command.ExecuteScalarAsync();
					if (result != null && int.TryParse(result.ToString(), out int currencyAmount))
					{
						return currencyAmount;
					}
				}
			}

			return 0;
		}

		public async Task AddPlayerAccount(int playerId, int initialCurrencyAmount)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var insertCommand = @"INSERT INTO EconomyData (PlayerId, CurrencyAmount)
							VALUES (@PlayerId, @CurrencyAmount)";

				using (var command = new SqliteCommand(insertCommand, connection))
				{
					command.Parameters.AddWithValue("@PlayerId", playerId);
					command.Parameters.AddWithValue("@CurrencyAmount", initialCurrencyAmount);
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task DeletePlayerAccount(int playerId)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var deleteCommand = "DELETE FROM EconomyData WHERE PlayerId = @PlayerId";

				using (var command = new SqliteCommand(deleteCommand, connection))
				{
					command.Parameters.AddWithValue("@PlayerId", playerId);
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task<bool> PlayerAccountExists(int playerId)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var selectCommand = "SELECT COUNT(*) FROM EconomyData WHERE PlayerId = @PlayerId";

				using (var command = new SqliteCommand(selectCommand, connection))
				{
					command.Parameters.AddWithValue("@PlayerId", playerId);
					var result = await command.ExecuteScalarAsync();
					if (result != null && int.TryParse(result.ToString(), out int count) && count > 0)
					{
						return true;
					}
				}
			}
			return false;
		}

		public async Task ResetAllCurrencyAmounts()
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var updateCommand = "UPDATE EconomyData SET CurrencyAmount = 0";

				using (var command = new SqliteCommand(updateCommand, connection))
				{
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task<List<Rank>> GetRanks()
		{
			var ranks = new List<Rank>();

			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var selectCommand = "SELECT RankName, RequiredCurrencyAmount, GroupName, NextRank FROM Ranks";

				using (var command = new SqliteCommand(selectCommand, connection))
				{
					using (var reader = await command.ExecuteReaderAsync())
					{
						while (reader.Read())
						{
							var rank = new Rank
							{
								Name = reader.GetString(0),
								RequiredCurrencyAmount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
								GroupName = reader.GetString(2),
								NextRank = reader.IsDBNull(3) ? null : reader.GetString(3)
							};

							ranks.Add(rank);
						}
					}
				}
			}

			return ranks;
		}

		public async Task AddRank(string rankName, int requiredCurrencyAmount, string groupName)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var insertCommand = @"INSERT INTO Ranks (RankName, RequiredCurrencyAmount, GroupName)
								VALUES (@RankName, @RequiredCurrencyAmount, @GroupName)";

				using (var command = new SqliteCommand(insertCommand, connection))
				{
					command.Parameters.AddWithValue("@RankName", rankName);
					command.Parameters.AddWithValue("@RequiredCurrencyAmount", requiredCurrencyAmount);
					command.Parameters.AddWithValue("@GroupName", groupName);
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task DeleteRank(string rankName)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var deleteCommand = "DELETE FROM Ranks WHERE RankName = @RankName";

				using (var command = new SqliteCommand(deleteCommand, connection))
				{
					command.Parameters.AddWithValue("@RankName", rankName);
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task<Rank> GetRankByName(string rankName)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var selectCommand = "SELECT RankName, RequiredCurrencyAmount, GroupName, NextRank FROM Ranks WHERE RankName = @RankName";

				using (var command = new SqliteCommand(selectCommand, connection))
				{
					command.Parameters.AddWithValue("@RankName", rankName);

					using (var reader = await command.ExecuteReaderAsync())
					{
						if (reader.Read())
						{
							var rank = new Rank
							{
								Name = reader.GetString(0),
								RequiredCurrencyAmount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
								GroupName = reader.GetString(2),
								NextRank = reader.IsDBNull(3) ? null : reader.GetString(3)
							};

							return rank;
						}
					}
				}
			}

			return null;
		}

		public async Task<List<string>> GetAllRankNames()
		{
			var rankNames = new List<string>();

			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var selectCommand = "SELECT RankName FROM Ranks";

				using (var command = new SqliteCommand(selectCommand, connection))
				{
					using (var reader = await command.ExecuteReaderAsync())
					{
						while (reader.Read())
						{
							rankNames.Add(reader.GetString(0));
						}
					}
				}
			}

			return rankNames;
		}

		public async Task UpdateRankNextRank(string rankName, string nextRank)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var updateCommand = @"UPDATE Ranks 
							  SET NextRank = @NextRank
							  WHERE RankName = @RankName";

				using (var command = new SqliteCommand(updateCommand, connection))
				{
					command.Parameters.AddWithValue("@NextRank", nextRank);
					command.Parameters.AddWithValue("@RankName", rankName);
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task UpdateRankRequireCurrency(string rankName, int requiredCurrency)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var updateCommand = @"UPDATE Ranks 
							  SET RequiredCurrencyAmount = @RequiredCurrencyAmount
							  WHERE RankName = @RankName";

				using (var command = new SqliteCommand(updateCommand, connection))
				{
					command.Parameters.AddWithValue("@RequiredCurrencyAmount", requiredCurrency);
					command.Parameters.AddWithValue("@RankName", rankName);
					await command.ExecuteNonQueryAsync();
				}
			}
		}
	}
}
