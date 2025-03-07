using Microsoft.Data.Sqlite;
using Microsoft.Xna.Framework;
using TShockAPI;
using TShockAPI.Configuration;
using TShockAPI.DB;

namespace JgransEconomySystem
{
	public class Rank
	{
		private static string path = Path.Combine(TShock.SavePath, "JgransEconomyBanks.sqlite");
		private static string tshockPath = Path.Combine(TShock.SavePath, "tshock.sqlite");
		private static JgransEconomySystemConfig? config;

		private static EconomyDatabase bank = new EconomyDatabase(path);

		public string Name { get; set; }
		public int RequiredCurrencyAmount { get; set; }
		public string GroupName { get; set; }
		public string NextRank { get; set; }

		// Add any additional properties or methods you need

		public static void Initialize(JgransEconomySystemConfig systemConfig)
		{
			if (systemConfig == null)
			{
				throw new ArgumentNullException(nameof(systemConfig), "Configuration cannot be null");
			}

			config = systemConfig;
			TShock.Log.Info("Rank system initialized with configuration");

			var adminCommands = new (string, CommandDelegate)[]
			{
				("rankadd", AddRankCommand),
				("rankdel", DeleteRankCommand),
				("ranknext", RankUpdateNextRankCommand),
				("rankcost", UpdateRankRequireCurrencyCommand),
				("rankdown", RankDownCommand),
				("rankdownall", RankDownAllCommand),
				("updateboard", UpdateLeaderboardCommand)
			};

			foreach (var (name, cmd) in adminCommands)
			{
				Commands.ChatCommands.Add(new Command("jgranserver.admin", cmd, name));
			}

			var playerCommands = new (string, CommandDelegate)[]
			{
				("ranks", ShowRankNames),
				("rankup", RankUpCommand)
			};

			foreach (var (name, cmd) in playerCommands)
			{
				Commands.ChatCommands.Add(new Command("jgranserver.player", cmd, name));
			 }

			// Start the timer after config is initialized
			StartLeaderboardUpdateTimer();
		}

		// Add config initialization
		public static void SetConfig(JgransEconomySystemConfig systemConfig)
		{
			config = systemConfig;
		}

		private static void StartLeaderboardUpdateTimer()
		{
			try
			{
				if (config == null)
				{
					throw new InvalidOperationException("Configuration is not initialized");
				}

				var now = DateTime.Now;
				var scheduledTime = new DateTime(
					now.Year, 
					now.Month, 
					now.Day, 
					config.LeaderboardUpdateHour.Value, 
					config.LeaderboardUpdateMinute.Value, 
					0
				);

				// If scheduled time is in the past, add one day
				if (scheduledTime <= now)
				{
					scheduledTime = scheduledTime.AddDays(1);
				}

				var timeUntilFirstRun = scheduledTime - now;
				TShock.Log.Info($"Next leaderboard update scheduled for: {scheduledTime:yyyy-MM-dd HH:mm:ss}");

				var timer = new Timer(async _ =>
				{
					try
					{
						await UpdateLeaderboardRanks();
						TShock.Log.Info($"Next update scheduled for: {DateTime.Now.AddDays(1):yyyy-MM-dd HH:mm:ss}");
					}
					catch (Exception ex)
					{
						TShock.Log.Error($"Timer callback failed: {ex.Message}");
					}
				}, null, timeUntilFirstRun, TimeSpan.FromDays(1));

				TShock.Log.Info($"Leaderboard ranks will update daily at {config.LeaderboardUpdateHour.Value:D2}:{config.LeaderboardUpdateMinute.Value:D2}");
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Failed to start leaderboard update timer: {ex.Message}");
				TShock.Log.Debug($"Stack trace: {ex.StackTrace}");
			}
		}

		private static async void AddRankCommand(CommandArgs args)
		{
			var player = args.Player;

			if (args.Parameters.Count < 3)
			{
				player.SendErrorMessage("Invalid syntax! Proper syntax: /rankadd <rankName> <requiredCurrencyAmount> <groupName>");
				return;
			}

			string rankName = args.Parameters[0];
			var rankNameExist = await bank.GetRankByName(rankName);

			if (rankNameExist != null)
			{
				player.SendErrorMessage($"{rankName} is already added to the rank list.");
				return;
			}

			if (int.TryParse(args.Parameters[1], out int requiredCurrencyAmount))
			{
				string groupName = args.Parameters[2];

				var group = TShock.Groups.GetGroupByName(groupName);
				if (group != null)
				{
					await bank.AddRank(rankName, requiredCurrencyAmount, group.Name);
					player.SendSuccessMessage("Rank added successfully.");
				}
				else
				{
					player.SendErrorMessage($"Group '{groupName}' does not exist.");
				}
			}
			else
			{
				player.SendErrorMessage("Invalid required currency amount! Please provide a valid integer.");
			}
		}

		private static async void RankUpCommand(CommandArgs args)
		{
			var player = args.Player;

			try
			{
				// Check if player has a leaderboard rank
				if (player.Group.Name == config.Top1Rank.Value ||
					player.Group.Name == config.Top2Rank.Value ||
					player.Group.Name == config.Top3Rank.Value ||
					player.Group.Name == config.Top4Rank.Value ||
					player.Group.Name == config.Top56Rank.Value ||
					player.Group.Name == config.Top78Rank.Value ||
					player.Group.Name == config.Top910Rank.Value)
				{
					player.SendErrorMessage("You cannot use /rankup with a leaderboard rank.");
					player.SendInfoMessage("Your rank is determined by your position on the leaderboard.");
					return;
				}

				TShock.Log.Info($"[RankUp] Starting rankup for player {player.Name} (ID: {player.Account?.ID})");

				if (config == null)
				{
					TShock.Log.Error("[RankUp] Config is null!");
					player.SendErrorMessage("System configuration error. Please contact an administrator.");
					return;
				}

				var currentCurrencyAmount = await bank.GetCurrencyAmount(player.Account.ID);
				var ranks = await bank.GetRanks();
				
				// Get current rank
				var currentRank = ranks.FirstOrDefault(r => r.GroupName == player.Group.Name);
				if (currentRank == null)
				{
					TShock.Log.Error($"[RankUp] No valid rank found for group {player.Group.Name}");
					player.SendErrorMessage("You don't have a valid rank. Please contact an administrator.");
					return;
				}

				// Get next rank by checking NextRank property
				var nextRank = ranks.FirstOrDefault(r => r.Name == currentRank.NextRank);
				if (nextRank == null)
				{
					player.SendInfoMessage("You have reached the highest rank available.");
					return;
				}

				// Check if this rank is beyond the maximum allowed rankup rank
				if (currentRank.Name == config.MaximumRankUpRank.Value)
				{
					player.SendInfoMessage($"You have reached the maximum rank available through rankup.");
					player.SendInfoMessage("Higher ranks are reserved for top players on the leaderboard.");
					return;
				}

				// Calculate costs including tax
				var tax = nextRank.RequiredCurrencyAmount * config.TaxRate.Value;
				var totalCost = nextRank.RequiredCurrencyAmount + tax;

				if (currentCurrencyAmount < totalCost)
				{
					var needed = totalCost - currentCurrencyAmount;
					player.SendInfoMessage($"Current rank: {currentRank.Name}");
					player.SendInfoMessage($"You need {needed} more {config.CurrencyName.Value} to rank up to {nextRank.Name}");
					return;
				}

				// Process the rank up
				var newBalance = currentCurrencyAmount - totalCost;
				
				// Update currency first
				await bank.UpdateCurrencyAmount(player.Account.ID, (int)newBalance);
				
				// Record the transaction
				await Transaction.RecordTransaction(player.Name, $"Rank up to {nextRank.Name}", (int)totalCost);

				// Update the player's group
				var userAccount = TShock.UserAccounts.GetUserAccountByName(player.Name);
				if (userAccount != null)
				{
					TShock.UserAccounts.SetUserGroup(userAccount, nextRank.GroupName);
					player.SendSuccessMessage($"Congratulations! You have been promoted to {nextRank.Name}!");
					player.SendMessage($"New balance: {newBalance} {config.CurrencyName.Value}", Color.LightBlue);
					
					if (tax > 0)
					{
						player.SendMessage($"Tax paid: {tax} {config.CurrencyName.Value}", Color.LightBlue);
					}
				}
				else
				{
					throw new Exception($"Could not find user account for {player.Name}");
				}
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"[RankUp] Error during rank up for {player.Name}: {ex.Message}");
				TShock.Log.Error($"[RankUp] Stack trace: {ex.StackTrace}");
				player.SendErrorMessage("An error occurred during rank up. Please contact an administrator.");
			}
		}

		private static async void RankUpdateNextRankCommand(CommandArgs args)
		{
			var player = args.Player;

			if (args.Parameters.Count < 2)
			{
				player.SendErrorMessage("Invalid syntax! Proper syntax: /ranknext <rankName> <nextRank>");
				return;
			}

			string rankName = args.Parameters[0];
			string nextRankName = args.Parameters[1];

			var rank = await bank.GetRankByName(rankName);
			if (rank == null)
			{
				player.SendErrorMessage($"Rank '{rankName}' does not exist.");
				return;
			}

			var nextRank = await bank.GetRankByName(nextRankName);
			if (nextRank == null)
			{
				player.SendErrorMessage($"Next rank '{nextRankName}' does not exist.");
				return;
			}

			rank.NextRank = nextRankName;
			await bank.UpdateRankNextRank(rankName, nextRankName);

			player.SendSuccessMessage($"Next rank for '{rankName}' has been updated to '{nextRankName}'.");
		}

		private static async void UpdateRankRequireCurrencyCommand(CommandArgs args)
		{
			var player = args.Player;

			if (args.Parameters.Count < 2)
			{
				player.SendErrorMessage("Invalid syntax! Proper syntax: /rankcost <rankName> <requiredCurrency>");
				return;
			}

			string rankName = args.Parameters[0];
			if (int.TryParse(args.Parameters[1], out int requiredCurrency))
			{
				var rank = await bank.GetRankByName(rankName);
				if (rank != null)
				{
					await bank.UpdateRankRequireCurrency(rankName, requiredCurrency);
					player.SendSuccessMessage($"Successfully updated the required currency for rank '{rankName}' to {requiredCurrency}.");
				}
				else
				{
					player.SendErrorMessage($"Rank '{rankName}' does not exist.");
				}
			}
			else
			{
				player.SendErrorMessage("Invalid required currency amount! Please provide a valid integer.");
			}
		}

		private static async void DeleteRankCommand(CommandArgs args)
		{
			var player = args.Player;

			if (args.Parameters.Count < 1)
			{
				player.SendErrorMessage("Invalid syntax! Proper syntax: /rankdel <rankName>");
				return;
			}

			string rankName = args.Parameters[0];

			var rank = await bank.GetRankByName(rankName);
			if (rank != null)
			{
				await bank.DeleteRank(rankName);
				player.SendSuccessMessage($"Rank '{rankName}' deleted successfully.");
			}
			else
			{
				player.SendErrorMessage($"Rank '{rankName}' does not exist.");
			}
		}

		private static void ShowRankNames(CommandArgs args)
		{
			var player = args.Player;

			// Retrieve all RankNames
			var rankNames = bank.GetAllRankNames().Result;

			if (rankNames.Count > 0)
			{
				player.SendInfoMessage("Available Ranks:");
				foreach (var rankName in rankNames)
				{
					player.SendInfoMessage(rankName);
				}
			}
			else
			{
				player.SendInfoMessage("No ranks available.");
			}
		}

		private static void RankDownCommand(CommandArgs args)
		{
			var cmd = args.Parameters;
			var datas = new UserAccountManager(new SqliteConnection(string.Format($"Data Source={tshockPath}")));

			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /rankdown <rankName> <newRank>");
				return;
			}

			string groupName = cmd[0];
			List<string> playersInGroup = new List<string>();

			// Get the list of players in the desired group
			foreach (var player in datas.GetUserAccounts())
			{
				if (player.Group == groupName)
				{
					playersInGroup.Add(player.Name);
				}
			}

			if (playersInGroup.Count == 0)
			{
				args.Player.SendErrorMessage("No players have been found in this group.");
				return;
			}

			// Assign the players to a new group
			Console.WriteLine($"Players in group: {playersInGroup.Count}");
			string newGroupName = cmd[1];
			var newGroup = TShock.Groups.GetGroupByName(newGroupName);
			Console.WriteLine($"New group: {newGroup?.Name}");

			if (newGroup != null)
			{
				foreach (var player in playersInGroup)
				{
					var ply = TShock.UserAccounts.GetUserAccountByName(player);
					if (ply != null)
					{
						TShock.UserAccounts.SetUserGroup(ply, newGroup.ToString());
					}
				}
				playersInGroup.Clear();
				args.Player.SendSuccessMessage($"Successfully updated the ranks!");
			}
			else
			{
				args.Player.SendErrorMessage($"The group '{newGroupName}' does not exist.");
			}
		}

		private static async void RankDownAllCommand(CommandArgs args)
		{
			var player = args.Player;
			var ranks = await bank.GetRanks();
			var userAccounts = new UserAccountManager(new SqliteConnection($"Data Source={tshockPath}")).GetUserAccounts();

			foreach (var user in userAccounts)
			{
				var currentRank = ranks.FirstOrDefault(r => r.Name == user.Group);
				if (currentRank != null)
				{
					var previousRank = ranks.FirstOrDefault(r => r.Name == currentRank.NextRank);
					if (previousRank != null)
					{
						var twoRanksDown = ranks.FirstOrDefault(r => r.Name == previousRank.NextRank);
						if (twoRanksDown != null)
						{
							TShock.UserAccounts.SetUserGroup(user, twoRanksDown.GroupName);
							continue;
						}
						else
						{
							TShock.UserAccounts.SetUserGroup(user, previousRank.GroupName);
							continue;
						}
					}
					else
					{
						player.SendInfoMessage($"Player {user.Name} is already at the lowest rank.");
					}
				}
			}

			player.SendSuccessMessage("All players have been demoted by two ranks where possible.");
		}

		public static async Task UpdateLeaderboardRanks()
		{
			try
			{
				TShock.Log.Info("Starting leaderboard rank update...");
				var allPlayers = await bank.GetTopPlayersAsync(100); // Get more players to filter from
				var qualifiedPlayers = new List<(int PlayerId, int CurrencyAmount)>();
				var ranks = await bank.GetRanks();

				// First, filter qualified players
				foreach (var (playerId, currency) in allPlayers)
				{
					var account = TShock.UserAccounts.GetUserAccountByID(playerId);
					if (account == null) continue;

					// Check if player is qualified (at or above MaximumRankUpRank)
					if (IsQualifiedForLeaderboard(account.Group))
					{
						qualifiedPlayers.Add((playerId, currency));
						TShock.Log.Info($"{account.Name} is qualified for leaderboard with {currency} currency");
					}
					else
					{
						TShock.Log.Info($"{account.Name} is not qualified (current rank: {account.Group})");
					}
				}

				// Take top 10 from qualified players only
				var topPlayers = qualifiedPlayers
					.OrderByDescending(p => p.CurrencyAmount)
					.Take(10)
					.ToList();

				// Reset players who are no longer in top 10
				foreach (var userAccount in TShock.UserAccounts.GetUserAccounts())
				{
					if (IsLeaderboardRank(userAccount.Group) && 
						!topPlayers.Any(p => p.PlayerId == userAccount.ID))
					{
						TShock.Log.Info($"Resetting {userAccount.Name} to {config.MaximumRankUpRank.Value}");
						TShock.UserAccounts.SetUserGroup(userAccount, config.MaximumRankUpRank.Value);

						var player = TShock.Players.FirstOrDefault(p => p?.Account?.ID == userAccount.ID);
						if (player != null)
						{
							player.SendInfoMessage($"You are no longer in the top 10. Your rank has been reset to {config.MaximumRankUpRank.Value}.");
						}
					}
				}

				// Update ranks for top players
				for (int i = 0; i < topPlayers.Count; i++)
				{
					var (playerId, currency) = topPlayers[i];
					var position = i + 1;

					var userAccount = TShock.UserAccounts.GetUserAccountByID(playerId);
					if (userAccount == null) continue;

					string newRank = position switch
					{
						1 => config.Top1Rank.Value,
						2 => config.Top2Rank.Value,
						3 => config.Top3Rank.Value,
						4 => config.Top4Rank.Value,
						5 or 6 => config.Top56Rank.Value,
						7 or 8 => config.Top78Rank.Value,
						9 or 10 => config.Top910Rank.Value,
						_ => config.MaximumRankUpRank.Value
					};

					if (newRank != userAccount.Group)
					{
						string oldRank = userAccount.Group;
						TShock.UserAccounts.SetUserGroup(userAccount, newRank);
						TShock.Log.Info($"Updated {userAccount.Name} from {oldRank} to {newRank} (Position: {position})");

						// Broadcast the change
						TSPlayer.All.SendInfoMessage($"{userAccount.Name} is now rank {position} on the leaderboard!");

						// Additional notification for the affected player
						var player = TShock.Players.FirstOrDefault(p => p?.Account?.ID == playerId);
						if (player != null)
						{
							player.SendSuccessMessage($"Your rank has been updated to {newRank} (Position: {position})!");
							player.SendInfoMessage($"Current balance: {currency:N0} {config.CurrencyName.Value}");
						}
					}
				}

				// Save leaderboard history
				var leaderboardEntries = topPlayers.Select((p, index) => new LeaderboardEntry
				{
					PlayerId = p.PlayerId,
					PlayerName = TShock.UserAccounts.GetUserAccountByID(p.PlayerId)?.Name ?? "Unknown",
					CurrencyAmount = p.CurrencyAmount,
					Position = index + 1,
					UpdatedAt = DateTime.Now
				}).ToList();

				await bank.SaveLeaderboardData(leaderboardEntries);
				TShock.Log.Info($"Leaderboard update completed. {topPlayers.Count} players ranked.");
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Error updating leaderboard ranks: {ex.Message}");
				TShock.Log.Error($"Stack trace: {ex.StackTrace}");
			}
		}

		private static bool IsLeaderboardRank(string rank)
		{
			return rank == config.Top1Rank.Value ||
				   rank == config.Top2Rank.Value ||
				   rank == config.Top3Rank.Value ||
				   rank == config.Top4Rank.Value ||
				   rank == config.Top56Rank.Value ||
				   rank == config.Top78Rank.Value ||
				   rank == config.Top910Rank.Value;
		}

		private static int GetPreviousPosition(string rank)
		{
			if (rank == config.Top1Rank.Value) return 1;
			if (rank == config.Top2Rank.Value) return 2;
			if (rank == config.Top3Rank.Value) return 3;
			if (rank == config.Top4Rank.Value) return 4;
			if (rank == config.Top56Rank.Value) return 6;
			if (rank == config.Top78Rank.Value) return 8;
			if (rank == config.Top910Rank.Value) return 10;
			return 99; // Default for non-leaderboard ranks
		}

		private static bool IsQualifiedForLeaderboard(string currentRank)
		{
			try
			{
				if (IsLeaderboardRank(currentRank))
					return true;

				var ranks = bank.GetRanks().Result;
				var currentRankObj = ranks.FirstOrDefault(r => r.GroupName == currentRank);
				if (currentRankObj == null)
					return false;

				// Start with MaximumRankUpRank and traverse up using NextRank
				var maxRankObj = ranks.FirstOrDefault(r => r.Name == config.MaximumRankUpRank.Value);
				if (maxRankObj == null)
					return false;

				// Create a list of qualified ranks starting from MaximumRankUpRank
				var qualifiedRanks = new HashSet<string> { maxRankObj.Name };
				
				// Follow NextRank chain to get all higher ranks
				string nextRankName = maxRankObj.NextRank;
				while (!string.IsNullOrEmpty(nextRankName))
				{
					qualifiedRanks.Add(nextRankName);
					var nextRank = ranks.FirstOrDefault(r => r.Name == nextRankName);
					if (nextRank == null)
						break;
					nextRankName = nextRank.NextRank;
				}

				// Check if current rank is in the qualified ranks list
				return qualifiedRanks.Contains(currentRankObj.Name);
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Error in IsQualifiedForLeaderboard: {ex.Message}");
				return false;
			}
		}

		// Add this new command method
		private static async void UpdateLeaderboardCommand(CommandArgs args)
		{
			var player = args.Player;
			int countdown = 10;

			try
			{
				// Announce start of update
				TSPlayer.All.SendInfoMessage($"Leaderboard rankings will update in {countdown} seconds!");

				// Start countdown
				for (int i = countdown; i > 0; i--)
				{
					if (i <= 5)
					{
						TSPlayer.All.SendInfoMessage($"Updating in {i}...");
					}
					await Task.Delay(1000); // Wait 1 second
				}

				TSPlayer.All.SendInfoMessage("Updating leaderboard rankings now...");
				
				// Perform the update
				await UpdateLeaderboardRanks();
				
				TSPlayer.All.SendSuccessMessage("Leaderboard rankings have been updated!");

				// Log the manual update
				TShock.Log.Info($"Leaderboard rankings manually updated by {player.Name}");
			 }
			catch (Exception ex)
			{
				TShock.Log.Error($"Error in manual leaderboard update: {ex.Message}");
				player.SendErrorMessage("An error occurred while updating the leaderboard rankings.");
			}
		}
	}
}
