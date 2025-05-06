using System.Collections.Concurrent;
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
            connectionString = $"Data Source={databasePath};";
            InitializeDatabase().Wait();
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
                    "CREATE TABLE IF NOT EXISTS BuyerChests (ID INTEGER PRIMARY KEY, X INTEGER, Y INTEGER)",
                    @"CREATE TABLE IF NOT EXISTS LeaderboardHistory (
                        PlayerId INTEGER,
                        PlayerName TEXT,
                        CurrencyAmount INTEGER,
                        Position INTEGER,
                        UpdatedAt TEXT,
                        PRIMARY KEY (PlayerId, UpdatedAt)
                    )",
                    @"CREATE TABLE IF NOT EXISTS PlayerPreviousRanks (
                        PlayerId INTEGER,
                        WorldId TEXT,
                        Position INTEGER,
                        LastUpdated DATETIME,
                        PRIMARY KEY (PlayerId, WorldId)
                    )",
                };

                foreach (var cmdText in commands)
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = cmdText;
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task ExecuteNonQueryAsync(
            string commandText,
            params (string, object)[] parameters
        )
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

        public async Task SaveCurrencyAmount(int playerId, int currencyAmount)
        {
            var commandText =
                @"INSERT INTO EconomyData (PlayerId, CurrencyAmount)
                                VALUES (@PlayerId, @CurrencyAmount)
                                ON CONFLICT(PlayerId) DO UPDATE SET CurrencyAmount = @CurrencyAmount";
            await ExecuteNonQueryAsync(
                commandText,
                ("@PlayerId", playerId),
                ("@CurrencyAmount", currencyAmount)
            );
        }

        public async Task UpdateCurrencyAmount(int playerId, int currencyAmount)
        {
            await SaveCurrencyAmount(playerId, currencyAmount);
        }

        public async Task<int> GetCurrencyAmount(int playerId)
        {
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (
                        var command = new SqliteCommand(
                            "SELECT CurrencyAmount FROM EconomyData WHERE PlayerId = @PlayerId",
                            connection
                        )
                    )
                    {
                        command.Parameters.AddWithValue("@PlayerId", playerId);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return reader.GetInt32(0);
                            }
                            return 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error(
                    $"Error getting currency amount for player {playerId}: {ex.Message}"
                );
                return 0;
            }
        }

        public async Task AddPlayerAccount(int playerId, int initialCurrencyAmount)
        {
            var commandText =
                "INSERT INTO EconomyData (PlayerId, CurrencyAmount) VALUES (@PlayerId, @CurrencyAmount)";
            await ExecuteNonQueryAsync(
                commandText,
                ("@PlayerId", playerId),
                ("@CurrencyAmount", initialCurrencyAmount)
            );
        }

        public async Task DeletePlayerAccount(int playerId)
        {
            var commandText = "DELETE FROM EconomyData WHERE PlayerId = @PlayerId";
            await ExecuteNonQueryAsync(commandText, ("@PlayerId", playerId));
        }

        public async Task<bool> PlayerAccountExists(int playerId)
        {
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (
                        var command = new SqliteCommand(
                            "SELECT 1 FROM EconomyData WHERE PlayerId = @PlayerId",
                            connection
                        )
                    )
                    {
                        command.Parameters.AddWithValue("@PlayerId", playerId);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            bool exists = await reader.ReadAsync();
                            TShock.Log.Debug(
                                $"PlayerAccountExists check for ID {playerId}: {exists}"
                            );
                            return exists;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error checking if player {playerId} exists: {ex.Message}");
                TShock.Log.Error($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task ResetAllCurrencyAmounts()
        {
            var commandText = "UPDATE EconomyData SET CurrencyAmount = 0";
            await ExecuteNonQueryAsync(commandText);
        }

        public async Task<List<(int PlayerId, int CurrencyAmount)>> GetTopPlayersAsync(int topN)
        {
            var allPlayers = await GetAllPlayersDataAsync();
            return allPlayers.Take(topN).ToList();
        }

        public async Task<List<Rank>> GetRanks()
        {
            var ranks = new List<Rank>();
            var commandText =
                "SELECT RankName, RequiredCurrencyAmount, GroupName, NextRank FROM Ranks";

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
                                RequiredCurrencyAmount = reader.IsDBNull(1)
                                    ? 0
                                    : reader.GetInt32(1),
                                GroupName = reader.GetString(2),
                                NextRank = reader.IsDBNull(3) ? null : reader.GetString(3),
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
            var commandText =
                @"INSERT INTO Ranks (RankName, RequiredCurrencyAmount, GroupName)
                                VALUES (@RankName, @RequiredCurrencyAmount, @GroupName)";
            await ExecuteNonQueryAsync(
                commandText,
                ("@RankName", rankName),
                ("@RequiredCurrencyAmount", requiredCurrencyAmount),
                ("@GroupName", groupName)
            );
        }

        public async Task DeleteRank(string rankName)
        {
            var commandText = "DELETE FROM Ranks WHERE RankName = @RankName";
            await ExecuteNonQueryAsync(commandText, ("@RankName", rankName));
        }

        public async Task<Rank> GetRankByName(string rankName)
        {
            var commandText =
                "SELECT RankName, RequiredCurrencyAmount, GroupName, NextRank FROM Ranks WHERE RankName = @RankName";
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
                                RequiredCurrencyAmount = reader.IsDBNull(1)
                                    ? 0
                                    : reader.GetInt32(1),
                                GroupName = reader.GetString(2),
                                NextRank = reader.IsDBNull(3) ? null : reader.GetString(3),
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
            var commandText =
                @"UPDATE Ranks 
                                SET NextRank = @NextRank
                                WHERE RankName = @RankName";
            await ExecuteNonQueryAsync(
                commandText,
                ("@NextRank", nextRank),
                ("@RankName", rankName)
            );
        }

        public async Task UpdateRankRequireCurrency(string rankName, int requiredCurrency)
        {
            var commandText =
                @"UPDATE Ranks 
                                SET RequiredCurrencyAmount = @RequiredCurrencyAmount
                                WHERE RankName = @RankName";
            await ExecuteNonQueryAsync(
                commandText,
                ("@RequiredCurrencyAmount", requiredCurrency),
                ("@RankName", rankName)
            );
        }

        public async Task SaveLeaderboardData(List<LeaderboardEntry> entries)
        {
            var commandText =
                @"
                INSERT OR REPLACE INTO LeaderboardHistory 
                (PlayerId, PlayerName, CurrencyAmount, Position, UpdatedAt)
                VALUES (@PlayerId, @PlayerName, @CurrencyAmount, @Position, @UpdatedAt)";

            foreach (var entry in entries)
            {
                await ExecuteNonQueryAsync(
                    commandText,
                    ("@PlayerId", entry.PlayerId),
                    ("@PlayerName", entry.PlayerName),
                    ("@CurrencyAmount", entry.CurrencyAmount),
                    ("@Position", entry.Position),
                    ("@UpdatedAt", entry.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"))
                );
            }
        }

        public async Task<List<LeaderboardEntry>> GetLatestLeaderboardData()
        {
            var entries = new List<LeaderboardEntry>();
            var commandText =
                @"
                SELECT PlayerId, PlayerName, CurrencyAmount, Position, UpdatedAt
                FROM LeaderboardHistory
                WHERE UpdatedAt = (SELECT MAX(UpdatedAt) FROM LeaderboardHistory)
                ORDER BY Position ASC";

            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqliteCommand(commandText, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        entries.Add(
                            new LeaderboardEntry
                            {
                                PlayerId = reader.GetInt32(0),
                                PlayerName = reader.GetString(1),
                                CurrencyAmount = reader.GetInt32(2),
                                Position = reader.GetInt32(3),
                                UpdatedAt = DateTime.Parse(reader.GetString(4)),
                            }
                        );
                    }
                }
            }
            return entries;
        }

        public async Task<List<(int PlayerId, int CurrencyAmount)>> GetAllPlayersDataAsync()
        {
            var players = new List<(int PlayerId, int CurrencyAmount)>();
            var commandText =
                "SELECT PlayerId, CurrencyAmount FROM EconomyData ORDER BY CurrencyAmount DESC";

            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqliteCommand(commandText, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int playerId = reader.GetInt32(0);
                            int currencyAmount = reader.GetInt32(1);
                            players.Add((playerId, currencyAmount));
                        }
                    }
                }

                TShock.Log.Info($"Retrieved {players.Count} player records from database");
                return players;
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error retrieving player data: {ex.Message}");
                TShock.Log.Debug($"Stack trace: {ex.StackTrace}");
                return new List<(int PlayerId, int CurrencyAmount)>();
            }
        }

        public async Task SavePreviousRank(int playerId, string worldId, int position)
        {
            var commandText =
                @"
                INSERT OR REPLACE INTO PlayerPreviousRanks 
                (PlayerId, WorldId, Position, LastUpdated)
                VALUES (@PlayerId, @WorldId, @Position, @LastUpdated)";

            await ExecuteNonQueryAsync(
                commandText,
                ("@PlayerId", playerId),
                ("@WorldId", worldId),
                ("@Position", position),
                ("@LastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            );
        }

        public async Task<int> GetPreviousRank(int playerId, string worldId)
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();

            command.CommandText =
                @"
                SELECT Position 
                FROM PlayerPreviousRanks 
                WHERE PlayerId = @PlayerId AND WorldId = @WorldId";

            command.Parameters.AddWithValue("@PlayerId", playerId);
            command.Parameters.AddWithValue("@WorldId", worldId);

            var result = await command.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }
    }
}
