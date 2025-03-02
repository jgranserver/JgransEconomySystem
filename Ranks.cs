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

		public static void Initialize()
		{
			var adminCommands = new (string, CommandDelegate)[]
			{
				("rankadd", AddRankCommand),
				("rankdel", DeleteRankCommand),
				("ranknext", RankUpdateNextRankCommand),
				("rankcost", UpdateRankRequireCurrencyCommand),
				("rankdown", RankDownCommand),
				("rankdownall", RankDownAllCommand)
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
				var currentCurrencyAmount = await bank.GetCurrencyAmount(player.Account.ID);
				var ranks = await bank.GetRanks();
				var currentRank = ranks.FirstOrDefault(r => r.Name == player.Group.Name);
				var nextRank = ranks.FirstOrDefault(r => r.Name == currentRank?.NextRank);

				if (nextRank != null)
				{
					var tax = nextRank.RequiredCurrencyAmount * (config?.TaxRate.Value ?? 0); // Ensure config is not null
					var requiredCurrency = nextRank.RequiredCurrencyAmount + tax - currentCurrencyAmount;
					bool ableRankUp = requiredCurrency <= 0;

					if (ableRankUp)
					{
						// Process the transaction
						var newBalance = currentCurrencyAmount - nextRank.RequiredCurrencyAmount;
						await Transaction.ProcessTransaction(player.Account.ID, player.Name, nextRank.RequiredCurrencyAmount);
						await bank.SaveCurrencyAmount(player.Account.ID, newBalance);

						var group = TShock.Groups.GetGroupByName(nextRank.GroupName);
						if (group != null)
						{
							var ply = TShock.UserAccounts.GetUserAccountByName(player.Name);
							if (ply != null)
							{
								TShock.UserAccounts.SetUserGroup(ply, group.ToString());
							}
							player.SendSuccessMessage($"Congratulations! You have been promoted to the {group.Name} rank.");
							player.SendMessage($"Balance after ranking up: {newBalance}", Color.LightBlue);
							return;
						}
						else
						{
							TShock.Log.Error($"Group '{nextRank.GroupName}' not found for rank promotion.");
						}
					}
					else
					{
						player.SendInfoMessage($"Current rank: {currentRank?.Name}.");
						player.SendInfoMessage($"You need {requiredCurrency} more currency to rank up to {nextRank.Name}.");
					}
				}
				else
				{
					player.SendInfoMessage("You have reached the highest rank.");
				}
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Error during rank up: {ex.Message}");
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
	}
}
