using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using TShockAPI;
using System.Collections.Concurrent;

namespace JgransEconomySystem
{
	public class EconomyDatabase
	{
		private readonly string connectionString;
		private const int ServerBankId = 0;
		private readonly ConcurrentDictionary<int, int> currencyCache = new ConcurrentDictionary<int, int>();

		public EconomyDatabase(string databasePath)
		{
			connectionString = $"Data Source={databasePath};Pooling=True";
			InitializeDatabase();
		}

		private async Task InitializeDatabase()
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var commands = new[]
				{
					"CREATE TABLE IF NOT EXISTS EconomyData (PlayerId INTEGER PRIMARY KEY, CurrencyAmount INTEGER)",
					"CREATE TABLE IF NOT EXISTS TransactionData (Id INTEGER PRIMARY KEY, PlayerName TEXT, Reason TEXT, Amount INTEGER, Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP)",
					"CREATE TABLE IF NOT EXISTS SwitchShop (Id INTEGER PRIMARY KEY, X INTEGER, Y INTEGER, Item INTEGER, Stack INTEGER, Price INTEGER, AllowedGroup TEXT, WorldID INTEGER)",
					"CREATE TABLE IF NOT EXISTS Ranks (RankId INTEGER PRIMARY KEY, RankName TEXT, RequiredCurrencyAmount INTEGER, GroupName TEXT, NextRank TEXT)",
					"CREATE TABLE IF NOT EXISTS CommandShop (ID INTEGER PRIMARY KEY AUTOINCREMENT, X INTEGER, Y INTEGER, Command TEXT NOT NULL, Price INTEGER NOT NULL, AllowedGroup TEXT, WorldID INTEGER)",
					"CREATE TABLE IF NOT EXISTS BuyerChests (ID INTEGER PRIMARY KEY, X INTEGER, Y INTEGER)"
				};

				foreach (var cmdText in commands)
				{
					using var command = connection.CreateCommand();
					command.CommandText = cmdText;
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		private async Task ExecuteNonQueryAsync(string commandText, params (string, object)[] parameters)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();
				using (var command = new SqliteCommand(commandText, connection))
				{
					foreach (var (name, value) in parameters)
					{
						command.Parameters.AddWithValue(name, value);
					}
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		private async Task<T> ExecuteScalarAsync<T>(string commandText, params (string, object)[] parameters)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();
				using (var command = new SqliteCommand(commandText, connection))
				{
					foreach (var (name, value) in parameters)
					{
						command.Parameters.AddWithValue(name, value);
					}
					var result = await command.ExecuteScalarAsync();
					return result != null && result is T tResult ? tResult : default;
				}
			}
		}

		public async Task SaveCurrencyAmount(int playerId, int currencyAmount)
		{
			var commandText = @"INSERT INTO EconomyData (PlayerId, CurrencyAmount)
								VALUES (@PlayerId, @CurrencyAmount)
								ON CONFLICT(PlayerId) DO UPDATE SET CurrencyAmount = @CurrencyAmount";
			await ExecuteNonQueryAsync(commandText, ("@PlayerId", playerId), ("@CurrencyAmount", currencyAmount));
			currencyCache[playerId] = currencyAmount;
		}

		public async Task<int> GetCurrencyAmount(int playerId)
		{
			if (currencyCache.TryGetValue(playerId, out int cachedAmount))
			{
				return cachedAmount;
			}

			var commandText = "SELECT CurrencyAmount FROM EconomyData WHERE PlayerId = @PlayerId";
			var currencyAmount = await ExecuteScalarAsync<int>(commandText, ("@PlayerId", playerId));
			currencyCache[playerId] = currencyAmount;
			return currencyAmount;
		}

		public async Task AddPlayerAccount(int playerId, int initialCurrencyAmount)
		{
			// Check if the PlayerId already exists
			var commandText = "SELECT COUNT(1) FROM EconomyData WHERE PlayerId = @PlayerId";
			var parameters = new (string, object)[] { ("@PlayerId", playerId) };

			var exists = (long)await ExecuteScalarAsync<long>(commandText, parameters);
			if (exists > 0)
			{
				// Handle the case where the PlayerId already exists
				// For example, you can update the existing record or skip the insertion
				// Here, we will skip the insertion
				return;
			}

			// Insert the new player account
			commandText = "INSERT INTO EconomyData (PlayerId, CurrencyAmount) VALUES (@PlayerId, @CurrencyAmount)";
			parameters = new (string, object)[] { ("@PlayerId", playerId), ("@CurrencyAmount", initialCurrencyAmount) };

			await ExecuteNonQueryAsync(commandText, parameters);
		}

		public async Task DeletePlayerAccount(int playerId)
		{
			var commandText = "DELETE FROM EconomyData WHERE PlayerId = @PlayerId";
			await ExecuteNonQueryAsync(commandText, ("@PlayerId", playerId));
			currencyCache.TryRemove(playerId, out _);
		}

		public async Task<bool> PlayerAccountExists(int playerId)
		{
			const string commandText = "SELECT COUNT(1) FROM EconomyData WHERE PlayerId = @playerId";
			var parameters = new (string, object)[]
			{
				("@playerId", playerId)
			};

			var result = await ExecuteScalarAsync<int>(commandText, parameters);
			return result > 0;
		}

		public async Task ResetAllCurrencyAmounts()
		{
			var commandText = "UPDATE EconomyData SET CurrencyAmount = 0";
			await ExecuteNonQueryAsync(commandText);
			currencyCache.Clear();
		}

		public async Task<List<Rank>> GetRanks()
		{
			var ranks = new List<Rank>();
			var commandText = "SELECT RankName, RequiredCurrencyAmount, GroupName, NextRank FROM Ranks";

			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();
				using (var command = new SqliteCommand(commandText, connection))
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
			var commandText = @"INSERT INTO Ranks (RankName, RequiredCurrencyAmount, GroupName)
								VALUES (@RankName, @RequiredCurrencyAmount, @GroupName)";
			await ExecuteNonQueryAsync(commandText, ("@RankName", rankName), ("@RequiredCurrencyAmount", requiredCurrencyAmount), ("@GroupName", groupName));
		}

		public async Task DeleteRank(string rankName)
		{
			var commandText = "DELETE FROM Ranks WHERE RankName = @RankName";
			await ExecuteNonQueryAsync(commandText, ("@RankName", rankName));
		}

		public async Task<Rank> GetRankByName(string rankName)
		{
			var commandText = "SELECT RankName, RequiredCurrencyAmount, GroupName, NextRank FROM Ranks WHERE RankName = @RankName";
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();
				using (var command = new SqliteCommand(commandText, connection))
				{
					command.Parameters.AddWithValue("@RankName", rankName);
					using (var reader = await command.ExecuteReaderAsync())
					{
						if (reader.Read())
						{
							return new Rank
							{
								Name = reader.GetString(0),
								RequiredCurrencyAmount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
								GroupName = reader.GetString(2),
								NextRank = reader.IsDBNull(3) ? null : reader.GetString(3)
							};
						}
					}
				}
			}
			return null;
		}

		public async Task<List<string>> GetAllRankNames()
		{
			var rankNames = new List<string>();
			var commandText = "SELECT RankName FROM Ranks";

			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();
				using (var command = new SqliteCommand(commandText, connection))
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
			var commandText = @"UPDATE Ranks 
								SET NextRank = @NextRank
								WHERE RankName = @RankName";
			await ExecuteNonQueryAsync(commandText, ("@NextRank", nextRank), ("@RankName", rankName));
		}

		public async Task UpdateRankRequireCurrency(string rankName, int requiredCurrency)
		{
			var commandText = @"UPDATE Ranks 
								SET RequiredCurrencyAmount = @RequiredCurrencyAmount
								WHERE RankName = @RankName";
			await ExecuteNonQueryAsync(commandText, ("@RequiredCurrencyAmount", requiredCurrency), ("@RankName", rankName));
		}
	}
}
