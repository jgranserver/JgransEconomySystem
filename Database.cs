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

        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
				CREATE TABLE IF NOT EXISTS EconomyData (
					PlayerId INTEGER PRIMARY KEY,
					CurrencyAmount INTEGER
				)";
                command.ExecuteNonQuery();

                using var transactionCommand = connection.CreateCommand();
                transactionCommand.CommandText = @"CREATE TABLE IF NOT EXISTS TransactionData (
													Id INTEGER PRIMARY KEY,
													PlayerName TEXT,
													Reason TEXT,
													Amount INTEGER,
													Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
												)";
                transactionCommand.ExecuteNonQuery();
            }
        }

        public void SaveCurrencyAmount(int playerId, int currencyAmount)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var saveCommand = @"
				INSERT INTO EconomyData (PlayerId, CurrencyAmount)
				VALUES (@PlayerId, @CurrencyAmount)
				ON CONFLICT(PlayerId) DO UPDATE SET CurrencyAmount = @CurrencyAmount";

                using (var command = new SqliteCommand(saveCommand, connection))
                {
                    command.Parameters.AddWithValue("@PlayerId", playerId);
                    command.Parameters.AddWithValue("@CurrencyAmount", currencyAmount);
                    command.ExecuteNonQuery();
                }
            }
        }

        public int GetCurrencyAmount(int playerId)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var selectCommand = "SELECT CurrencyAmount FROM EconomyData WHERE PlayerId = @PlayerId";

                using (var command = new SqliteCommand(selectCommand, connection))
                {
                    command.Parameters.AddWithValue("@PlayerId", playerId);
                    var result = command.ExecuteScalar();
                    if (result != null && int.TryParse(result.ToString(), out int currencyAmount))
                    {
                        return currencyAmount;
                    }
                }
            }

            return 0;
        }

        public void AddPlayerAccount(int playerId, int initialCurrencyAmount)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var insertCommand = @"
				INSERT INTO EconomyData (PlayerId, CurrencyAmount)
				VALUES (@PlayerId, @CurrencyAmount)";

                using (var command = new SqliteCommand(insertCommand, connection))
                {
                    command.Parameters.AddWithValue("@PlayerId", playerId);
                    command.Parameters.AddWithValue("@CurrencyAmount", initialCurrencyAmount);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void DeletePlayerAccount(int playerId)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var deleteCommand = "DELETE FROM EconomyData WHERE PlayerId = @PlayerId";

                using (var command = new SqliteCommand(deleteCommand, connection))
                {
                    command.Parameters.AddWithValue("@PlayerId", playerId);
                    command.ExecuteNonQuery();
                }
            }
        }

        public bool PlayerAccountExists(int playerId)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var selectCommand = "SELECT COUNT(*) FROM EconomyData WHERE PlayerId = @PlayerId";

                using (var command = new SqliteCommand(selectCommand, connection))
                {
                    command.Parameters.AddWithValue("@PlayerId", playerId);
                    var result = command.ExecuteScalar();
                    if (result != null && int.TryParse(result.ToString(), out int count) && count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void ResetAllCurrencyAmounts()
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var updateCommand = "UPDATE EconomyData SET CurrencyAmount = 0";

                using (var command = new SqliteCommand(updateCommand, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }


        public void RecordTransaction(string playerName, string reason, int amount)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO TransactionData (PlayerName, Reason, Amount) 
									VALUES (@PlayerName, @Reason, @Amount)";
            command.Parameters.AddWithValue("@PlayerName", playerName);
            command.Parameters.AddWithValue("@Reason", reason);
            command.Parameters.AddWithValue("@Amount", amount);

            command.ExecuteNonQuery();
        }

        public void RecordTaxTransaction(int amount)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO TransactionData (PlayerName, Reason, Amount) 
									VALUES (@PlayerName, @Reason, @Amount)";
            command.Parameters.AddWithValue("@PlayerName", "ServerBank");
            command.Parameters.AddWithValue("@Reason", "Tax from transactions");
            command.Parameters.AddWithValue("@Amount", amount);

            command.ExecuteNonQuery();

            // Update ServerBank account's currency amount
            var currentBalance = GetCurrencyAmount(ServerBankId);
            var newBalance = currentBalance + amount;
            SaveCurrencyAmount(ServerBankId, newBalance);
        }
    }
}