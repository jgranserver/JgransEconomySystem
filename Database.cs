using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TShockAPI;

namespace JgransEconomySystem
{
    public class EconomyDatabase
    {
        private readonly string connectionString;
        private const int ServerBankId = 0; // ServerBank account ID
        private Dictionary<int, HashSet<string>> rewardedPlayers;
        private Dictionary<int, Dictionary<string, DateTime>> cooldowns;

        public EconomyDatabase(string databasePath)
        {
            rewardedPlayers = new Dictionary<int, HashSet<string>>();
            cooldowns = new Dictionary<int, Dictionary<string, DateTime>>();
            connectionString = $"Data Source={databasePath}";
            InitializeDatabase();
        }

        private async Task InitializeDatabase()
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
				CREATE TABLE IF NOT EXISTS EconomyData (
					PlayerId INTEGER PRIMARY KEY,
					CurrencyAmount INTEGER
				)";
                await command.ExecuteNonQueryAsync();

                using var transactionCommand = connection.CreateCommand();
                transactionCommand.CommandText = @"CREATE TABLE IF NOT EXISTS TransactionData (
													Id INTEGER PRIMARY KEY,
													PlayerName TEXT,
													Reason TEXT,
													Amount INTEGER,
													Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
												)";
                await transactionCommand.ExecuteNonQueryAsync();

                using var shopCommand = connection.CreateCommand();
                shopCommand.CommandText = @"CREATE TABLE IF NOT EXISTS SwitchShop (
												Id INTEGER PRIMARY KEY,
												X INTEGER,
												Y INTEGER,
												Item INTEGER,
												Stack INTEGER,
												Price INTEGER,
												WorldID INTEGER
											)";
                await shopCommand.ExecuteNonQueryAsync();
            }
        }

        public async Task SaveCurrencyAmount(int playerId, int currencyAmount)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();

                var saveCommand = @"
				INSERT INTO EconomyData (PlayerId, CurrencyAmount)
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

                var insertCommand = @"
				INSERT INTO EconomyData (PlayerId, CurrencyAmount)
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
            using var connection = new SqliteConnection(connectionString);
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
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO TransactionData (PlayerName, Reason, Amount) 
									VALUES (@PlayerName, @Reason, @Amount)";
            command.Parameters.AddWithValue("@PlayerName", "ServerBank");
            command.Parameters.AddWithValue("@Reason", "Tax+Sales from transactions");
            command.Parameters.AddWithValue("@Amount", amount);

            await command.ExecuteNonQueryAsync();

            // Update ServerBank account's currency amount
            var currentBalance = await GetCurrencyAmount(ServerBankId);
            var newBalance = currentBalance + amount;
            await SaveCurrencyAmount(ServerBankId, newBalance);
        }

        public async Task SaveShopToDatabase(int x, int y, int itemID, int stackSize, int shopPrice, int worldID)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"INSERT INTO SwitchShop (X, Y, Item, Stack, Price, WorldID) 
									   VALUES (@X, @Y, @Item, @Stack, @Price, @WorldID)";
                insertCommand.Parameters.AddWithValue("@X", x);
                insertCommand.Parameters.AddWithValue("@Y", y);
                insertCommand.Parameters.AddWithValue("@Item", itemID);
                insertCommand.Parameters.AddWithValue("@Stack", stackSize);
                insertCommand.Parameters.AddWithValue("@Price", shopPrice);
                insertCommand.Parameters.AddWithValue("@WorldID", worldID);
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        public class ShopItem
        {
            public int Id { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Item { get; set; }
            public int Stack { get; set; }
            public int Price { get; set; }
            public int WorldID { get; set; }
        }

        public async Task<ShopItem> GetShopFromDatabase(int switchX, int switchY)
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
                                WorldID = reader.GetInt32(6)
                            };

                            return shop;
                        }
                    }
                }
            }

            return null;
        }

        public async Task DeleteShopFromDatabase(int switchX, int switchY)
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
    }
}
