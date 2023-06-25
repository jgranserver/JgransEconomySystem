using Microsoft.Data.Sqlite;
using Microsoft.Xna.Framework;
using TShockAPI;
using TShockAPI.DB;

namespace JgransEconomySystem
{
    public class Rank
	{
		private static string path = Path.Combine(TShock.SavePath, "JgransEconomyBanks.sqlite");
		private static string tshockPath = Path.Combine(TShock.SavePath, "tshock.sqlite");

		private static EconomyDatabase bank = new EconomyDatabase(path);

		public string Name { get; set; }
		public int RequiredCurrencyAmount { get; set; }
		public string GroupName { get; set; }
		public string NextRank { get; set; }

		// Add any additional properties or methods you need

		public static void Initialize()
		{
			Commands.ChatCommands.Add(new Command("jgranserver.admin", AddRankCommand, "rankadd"));
			Commands.ChatCommands.Add(new Command("jgranserver.admin", DeleteRankCommand, "rankdel"));
			Commands.ChatCommands.Add(new Command("jgranserver.admin", RankUpdateNextRankCommand, "ranknext"));
			Commands.ChatCommands.Add(new Command("jgranserver.admin", UpdateRankRequireCurrencyCommand, "rankcost"));
			Commands.ChatCommands.Add(new Command("jgranserver.admin", RankDownCommand, "rankdown"));
			Commands.ChatCommands.Add(new Command("jgraneconomy.system", ShowRankNames, "ranks"));
			Commands.ChatCommands.Add(new Command("jgraneconomy.system", RankUpCommand, "rankup"));
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
			var currentCurrencyAmount = await bank.GetCurrencyAmount(player.Account.ID);
			var ranks = await bank.GetRanks();
			var currentRank = ranks.FirstOrDefault(r => r.Name == player.Group.Name);
			var nextRank = ranks.FirstOrDefault(r => r.Name == currentRank?.NextRank);

			if (nextRank != null)
			{
				var requiredCurrency = nextRank.RequiredCurrencyAmount - currentCurrencyAmount;
				bool ableRankUp = requiredCurrency <= 0;

				if (ableRankUp)
				{
					// Process the transaction
					var newBalance = currentCurrencyAmount - nextRank.RequiredCurrencyAmount;
					player.SendMessage($"Balance after ranking up: {newBalance}", Color.LightBlue);

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
						return;
					}
					else
					{
						TShock.Log.Error($"Group '{nextRank.GroupName}' not found for rank promotion.");
						return;
					}
				}
				else
				{
					if (requiredCurrency > 0)
					{
						player.SendInfoMessage($"Current rank: {currentRank?.Name}.");
						player.SendInfoMessage($"You need {requiredCurrency} more currency to rank up to {nextRank.Name}.");
						return;
					}
				}
			}
			else
			{
				player.SendInfoMessage("You have reached the highest rank.");
				return;
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
	}
}
