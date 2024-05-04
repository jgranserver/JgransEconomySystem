using Microsoft.Data.Sqlite;
using TShockAPI;

namespace JgransEconomySystem
{
	public class EconomyDatabase
	{
		private readonly string connectionString;
		private const int ServerBankId = 0;
		private readonly Dictionary<int, HashSet<string>> rewardedPlayers;
		private readonly Dictionary<int, Dictionary<string, DateTime>> cooldowns;

		public EconomyDatabase(string databasePath)
		{
			rewardedPlayers = new Dictionary<int, HashSet<string>>();
			cooldowns = new Dictionary<int, Dictionary<string, DateTime>>();
			connectionString = $"Data Source={databasePath}";
			_ = InitializeDatabase();
		}

		private async Task InitializeDatabase()
		{
			using var connection = new SqliteConnection(connectionString);
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

		public async Task RecordTransaction(string playerName, string reason, int amount)
		{
			string path = Path.Combine(TShock.SavePath, "EconomyTransactions.sqlite");
			using var connection = new SqliteConnection($"Data Source={path}");
			await connection.OpenAsync();

			using var command = connection.CreateCommand();
			command.CommandText = @"INSERT INTO TransactionData (PlayerName, Reason, Amount) 
							VALUES (@PlayerName, @Reason, @Amount)";
			command.Parameters.AddWithValue("@PlayerName", playerName);
			command.Parameters.AddWithValue("@Reason", reason);
			command.Parameters.AddWithValue("@Amount", amount);

			await command.ExecuteNonQueryAsync();
		}

		public async Task RecordTaxTransaction(int amount)
		{
			string path = Path.Combine(TShock.SavePath, "EconomyTransactions.sqlite");
			using var connection = new SqliteConnection($"Data Source={path}");
			await connection.OpenAsync();

			using var command = connection.CreateCommand();
			command.CommandText = @"INSERT INTO TransactionData (PlayerName, Reason, Amount) 
							VALUES (@PlayerName, @Reason, @Amount)";
			command.Parameters.AddWithValue("@PlayerName", "ServerBank");
			command.Parameters.AddWithValue("@Reason", "Tax+Sales from transactions");
			command.Parameters.AddWithValue("@Amount", amount);

			await command.ExecuteNonQueryAsync();

			var currentBalance = await GetCurrencyAmount(ServerBankId);
			var newBalance = currentBalance + amount;
			await SaveCurrencyAmount(ServerBankId, newBalance);
		}


		public class ShopItem
		{
			public int Id { get; set; }
			public int X { get; set; }
			public int Y { get; set; }
			public int Item { get; set; }
			public int Stack { get; set; }
			public string Command { get; set; }
			public int Price { get; set; }
			public string AllowedGroup { get; set; }
			public int WorldID { get; set; }
		}

		public async Task SaveShopToDatabase(int x, int y, int itemID, int stackSize, int shopPrice, string allowedGroup, int worldID)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var insertCommand = connection.CreateCommand();
				insertCommand.CommandText = @"INSERT INTO SwitchShop (X, Y, Item, Stack, Price, AllowedGroup, WorldID) 
								   VALUES (@X, @Y, @Item, @Stack, @Price, @AllowedGroup, @WorldID)";
				insertCommand.Parameters.AddWithValue("@X", x);
				insertCommand.Parameters.AddWithValue("@Y", y);
				insertCommand.Parameters.AddWithValue("@Item", itemID);
				insertCommand.Parameters.AddWithValue("@Stack", stackSize);
				insertCommand.Parameters.AddWithValue("@Price", shopPrice);
				insertCommand.Parameters.AddWithValue("@AllowedGroup", allowedGroup);
				insertCommand.Parameters.AddWithValue("@WorldID", worldID);
				await insertCommand.ExecuteNonQueryAsync();
			}
		}

		public async Task<ShopItem> GetShopFromDatabase(int switchX, int switchY, bool switchShop, bool sellCommand)
		{
			if (switchShop)
			{
				using (var connection = new SqliteConnection(connectionString))
				{
					await connection.OpenAsync();

					var selectCommand = "SELECT * FROM SwitchShop WHERE X = @X AND Y = @Y LIMIT 1";

					using (var command = new SqliteCommand(selectCommand, connection))
					{
						command.Parameters.AddWithValue("@X", switchX);
						command.Parameters.AddWithValue("@Y", switchY);

						using (var reader = await command.ExecuteReaderAsync())
						{
							if (reader.Read())
							{
								var shop = new ShopItem
								{
									Id = reader.GetInt32(0),
									X = reader.GetInt32(1),
									Y = reader.GetInt32(2),
									Item = reader.GetInt32(3),
									Stack = reader.GetInt32(4),
									Price = reader.GetInt32(5),
									AllowedGroup = reader.GetString(6),
									WorldID = reader.GetInt32(7)
								};

								return shop;
							}
						}
					}
				}
			}

			if (sellCommand)
			{
				using (var connection = new SqliteConnection(connectionString))
				{
					await connection.OpenAsync();

					var selectCommand = "SELECT * FROM CommandShop WHERE X = @X AND Y = @Y LIMIT 1";

					using (var command = new SqliteCommand(selectCommand, connection))
					{
						command.Parameters.AddWithValue("@X", switchX);
						command.Parameters.AddWithValue("@Y", switchY);

						using (var reader = await command.ExecuteReaderAsync())
						{
							if (reader.Read())
							{
								var shop = new ShopItem
								{
									Id = reader.GetInt32(0),
									X = reader.GetInt32(1),
									Y = reader.GetInt32(2),
									Command = reader.GetString(3),
									Price = reader.GetInt32(4),
									AllowedGroup = reader.IsDBNull(5) ? null : reader.GetString(5),
									WorldID = reader.GetInt32(6)
								};

								return shop;
							}
						}
					}
				}
			}
			return null;
		}

		public async Task UpdateAllowedGroup(int switchX, int switchY, string updatedAllowedGroups, bool switchShop, bool sellCommand)
		{
			if (switchShop)
			{
				using (var connection = new SqliteConnection(connectionString))
				{
					await connection.OpenAsync();

					var updateCommand = @"UPDATE SwitchShop 
							  SET AllowedGroup = @UpdatedAllowedGroups 
							  WHERE X = @SwitchX AND Y = @SwitchY";

					using (var command = new SqliteCommand(updateCommand, connection))
					{
						command.Parameters.AddWithValue("@UpdatedAllowedGroups", updatedAllowedGroups);
						command.Parameters.AddWithValue("@SwitchX", switchX);
						command.Parameters.AddWithValue("@SwitchY", switchY);
						await command.ExecuteNonQueryAsync();
					}
				}
			}

			if (sellCommand)
			{
				using (var connection = new SqliteConnection(connectionString))
				{
					await connection.OpenAsync();

					var updateCommand = @"UPDATE CommandShop 
							  SET AllowedGroup = @UpdatedAllowedGroups 
							  WHERE X = @SwitchX AND Y = @SwitchY";

					using (var command = new SqliteCommand(updateCommand, connection))
					{
						command.Parameters.AddWithValue("@UpdatedAllowedGroups", updatedAllowedGroups);
						command.Parameters.AddWithValue("@SwitchX", switchX);
						command.Parameters.AddWithValue("@SwitchY", switchY);
						await command.ExecuteNonQueryAsync();
					}
				}
			}
		}

		public async Task DeleteShopFromDatabase(int switchX, int switchY, bool switchShop, bool sellCommand)
		{
			if (switchShop)
			{
				using (var connection = new SqliteConnection(connectionString))
				{
					await connection.OpenAsync();

					var deleteCommand = "DELETE FROM SwitchShop WHERE X = @X AND Y = @Y";

					using (var command = new SqliteCommand(deleteCommand, connection))
					{
						command.Parameters.AddWithValue("@X", switchX);
						command.Parameters.AddWithValue("@Y", switchY);
						await command.ExecuteNonQueryAsync();
					}
				}
			}

			if (sellCommand)
			{
				using (var connection = new SqliteConnection(connectionString))
				{
					await connection.OpenAsync();

					var deleteCommand = "DELETE FROM CommandShop WHERE X = @X AND Y = @Y";

					using (var command = new SqliteCommand(deleteCommand, connection))
					{
						command.Parameters.AddWithValue("@X", switchX);
						command.Parameters.AddWithValue("@Y", switchY);
						await command.ExecuteNonQueryAsync();
					}
				}
			}
		}

		public async Task AddCommandToShop(int switchX, int switchY, string command, int price, string groupName, int worldID)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				connection.Open();

				var insertCommand = @"INSERT INTO CommandShop (X, Y, Command, Price, AllowedGroup, WorldID)
									VALUES (@X, @Y, @Command, @Price, @AllowedGroup, @WorldID)";

				using (var addCommand = new SqliteCommand(insertCommand, connection))
				{
					addCommand.Parameters.AddWithValue("@X", switchX);
					addCommand.Parameters.AddWithValue("@Y", switchY);
					addCommand.Parameters.AddWithValue("@Command", command);
					addCommand.Parameters.AddWithValue("@Price", price);
					addCommand.Parameters.AddWithValue("@AllowedGroup", groupName);
					addCommand.Parameters.AddWithValue("@WorldID", worldID);

					await addCommand.ExecuteNonQueryAsync();
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

		public class BuyerChest
		{
			public int ID { get; set; }
			public int X { get; set; }
			public int Y { get; set; }

			public BuyerChest(int id, int x, int y)
			{
				ID = id;
				X = x;
				Y = y;
			}
		}

		public async Task<List<BuyerChest>> GetBuyerChests(int x, int y)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var selectCommand = "SELECT * FROM BuyerChests WHERE X = @X AND Y = @Y";

				using (var command = new SqliteCommand(selectCommand, connection))
				{
					command.Parameters.AddWithValue("@X", x);
					command.Parameters.AddWithValue("@Y", y);

					using (var reader = await command.ExecuteReaderAsync())
					{
						var buyerChests = new List<BuyerChest>();

						while (reader.Read())
						{
							var id = reader.GetInt32(0);
							var xPos = reader.GetInt32(1);
							var yPos = reader.GetInt32(2);

							var buyerChest = new BuyerChest(id, xPos, yPos);
							buyerChests.Add(buyerChest);
						}

						return buyerChests;
					}
				}
			}
		}


		public async Task AddBuyerChestAsync(int x, int y)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var insertCommand = "INSERT INTO BuyerChests (X, Y) VALUES (@X, @Y)";

				using (var command = new SqliteCommand(insertCommand, connection))
				{
					command.Parameters.AddWithValue("@X", x);
					command.Parameters.AddWithValue("@Y", y);
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task DeleteBuyerChest(int chestId)
		{
			using (var connection = new SqliteConnection(connectionString))
			{
				await connection.OpenAsync();

				var deleteCommand = "DELETE FROM BuyerChests WHERE ID = @ChestId";

				using (var command = new SqliteCommand(deleteCommand, connection))
				{
					command.Parameters.AddWithValue("@ChestId", chestId);
					await command.ExecuteNonQueryAsync();
				}
			}
		}
	}
}
