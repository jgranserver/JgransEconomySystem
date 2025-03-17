using TShockAPI;
using TerrariaApi.Server;
using Terraria;
using Microsoft.Xna.Framework;
using TShockAPI.DB;
using System.IO.Streams;
using Newtonsoft.Json;
using Terraria.ID;
using Terraria.GameContent.UI;
using SQLitePCL;
using System.Text;
using Terraria.GameContent.Drawing;

namespace JgransEconomySystem
{
	[ApiVersion(2, 1)]
	public class JgransEconomySystem : TerrariaPlugin
	{
		private EconomyDatabase bank;
		private JgransEconomySystemConfig config;
		private string path = Path.Combine(TShock.SavePath, "JgransEconomyBanks.sqlite");
		private string configPath = Path.Combine(TShock.SavePath, "JgransEconomySystemConfig.json");
		private bool EconomyOnline;
		private int lastWorldId = -1;

		public JgransEconomySystem(Main game) : base(game)
		{

		}

		public override string Name => "JgransEconomySystem";

		public override Version Version => new Version(5, 6);

		public override string Author => "jgranserver";

		public override string Description => "Economy system.";

		public override void Initialize()
		{
			try 
			{
				// Load or create config first
				if (!File.Exists(configPath))
				{
					config = new JgransEconomySystemConfig();
					config.Write(configPath);
				}
				else
				{
					config = JgransEconomySystemConfig.Read(configPath);
				}

				// Initialize database
				bank = new EconomyDatabase(path);

				// Initialize Rank system with config
				Rank.Initialize(config);

				// Register hooks
				ServerApi.Hooks.NetSendData.Register(this, EconomyAsync);
				ServerApi.Hooks.NetGetData.Register(this, OnNetGetData);
				ServerApi.Hooks.ServerChat.Register(this, OnServerChat);
				ServerApi.Hooks.ServerJoin.Register(this, OnPlayerJoin);
				GetDataHandlers.TileEdit += OnTileEdit;
				ServerApi.Hooks.GamePostInitialize.Register(this, OnWorldLoad);

				// Register commands
				Commands.ChatCommands.Add(new Command("jgraneconomy.system", EconomyCommandsAsync, "bank"));
				Commands.ChatCommands.Add(new Command("jgraneconomy.admin", ReloadConfigCommand, "economyreload", "er"));
				Commands.ChatCommands.Add(new Command("jgraneconomy.system", LeaderboardCommandAsync, "leaderboard"));
				Commands.ChatCommands.Add(new Command("jgranserver.admin", InitializeWorldCommand, "initworld"));

				// Initialize transaction system
				Transaction.InitializeTransactionDataAsync();
				UpdateEconomyStatus();

				// Start the leaderboard update timer with proper delay
				var now = DateTime.Now;
				var nextUpdate = new DateTime(now.Year, now.Month, now.Day, 
					config.LeaderboardUpdateHour.Value, 
					config.LeaderboardUpdateMinute.Value, 0);

				if (nextUpdate < now)
				{
					nextUpdate = nextUpdate.AddDays(1);
				}

				var delay = nextUpdate - now;
				TShock.Log.Info($"Next leaderboard update scheduled for {nextUpdate:yyyy-MM-dd HH:mm:ss}");

				Timer leaderboardTimer = new Timer(async _ =>
				{
					try
					{
						await Rank.UpdateLeaderboardRanks();
					}
					catch (Exception ex)
					{
						TShock.Log.Error($"Leaderboard update failed: {ex.Message}");
						TShock.Log.Debug($"Stack trace: {ex.StackTrace}");
					}
				}, null, delay, TimeSpan.FromDays(1));

				TShock.Log.Info("JgransEconomySystem initialized successfully");
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Failed to initialize JgransEconomySystem: {ex.Message}");
				TShock.Log.Debug($"Stack trace: {ex.StackTrace}");
			}
		}

		private void UpdateEconomyStatus()
		{
			EconomyOnline = config.ToggleEconomy.Value;
		}

		private async void EconomyCommandsAsync(CommandArgs args)
		{
			await EconomyCommands(args);
		}

		private async void EconomyAsync(SendDataEventArgs args)
		{
			await Economy(args);
		}


		public void OnInit(EventArgs e)
		{

		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.NetSendData.Deregister(this, EconomyAsync);
				ServerApi.Hooks.NetGetData.Deregister(this, OnNetGetData);
				ServerApi.Hooks.ServerChat.Deregister(this, OnServerChat);
				ServerApi.Hooks.ServerJoin.Deregister(this, OnPlayerJoin);
				ServerApi.Hooks.GamePostInitialize.Deregister(this, OnWorldLoad);

				GetDataHandlers.TileEdit -= OnTileEdit;
			}
			base.Dispose(disposing);
		}

		private void ReloadConfigCommand(CommandArgs args)
		{
			// Read the updated config file
			string json = File.ReadAllText(configPath);

			// Deserialize the config file into a new instance
			JgransEconomySystemConfig newConfig = JsonConvert.DeserializeObject<JgransEconomySystemConfig>(json);

			// Assign the new config instance to the config variable
			config = newConfig;

			// If necessary, update config references in other components or classes

			// Inform server admins or log the config reload
			TShock.Log.ConsoleInfo("JgransEconomySystemConfig has been reloaded.");

			// Update the EconomyOnline flag
			UpdateEconomyStatus();
		}

		public static bool spawned = false;

		private Dictionary<int, DateTime> lastNpcStrikeTime = new Dictionary<int, DateTime>();

		private async Task Economy(SendDataEventArgs args)
		{
			config = new JgransEconomySystemConfig();
			if (!EconomyOnline)
			{
				return;
			}

			var data = args.MsgId;
			var npcIndex = args.number;

			if (args.ignoreClient == -1)
				return;

			var players = TSPlayer.FindByNameOrID(args.ignoreClient.ToString());
			if (players.Count == 0)
				return;

			if (npcIndex >= Main.npc.Length || npcIndex < 0)
				return;

			var player = players[0];
			var unsuccessfulAttempts = player.GetData<int>("unsuccessfulAttempts");
			var npc = Main.npc[npcIndex];
			
			if(npc.SpawnedFromStatue)
			{
				return;
			}

			bool isHostile = NPCType.IsHostile(npc.netID);
			bool isSpecial = NPCType.IsSpecial(npc.netID);
			bool isBoss1 = NPCType.IsBoss1(npc.netID);
			bool isBoss2 = NPCType.IsBoss2(npc.netID);
			bool isBoss3 = NPCType.IsBoss3(npc.netID);

			switch (data)
			{
				case PacketTypes.NpcStrike:
					if (npc == null || npc.life > 0)
						return;

					// Check for delay or lag
					if (lastNpcStrikeTime.ContainsKey(player.Index))
					{
						var lastStrike = lastNpcStrikeTime[player.Index];
						if ((DateTime.Now - lastStrike).TotalMilliseconds < 500)
						{
							// If the last strike was less than 500ms ago, ignore this strike
							return;
						}
					}
					lastNpcStrikeTime[player.Index] = DateTime.Now;

					int randomizer = Main.rand.Next(101);
					int currencyAmount = 0;
					string reason = "";

					bool isHardmode = Main.hardMode;
					if (isHardmode)
						randomizer = Main.rand.Next(86);

					// Check if the randomizer value is below 20
					if (randomizer < 20)
					{
						// Reset the counter if the randomizer value is below 20
						unsuccessfulAttempts = 0;
						player.SetData("unsuccessfulAttempts", unsuccessfulAttempts);
					}
					else
					{
						// Increment the counter if the randomizer value is not below 20
						unsuccessfulAttempts++;
						player.SetData("unsuccessfulAttempts", unsuccessfulAttempts);

						// Check if the counter reaches the limit (5)
						if (unsuccessfulAttempts >= 5)
						{
							// Set the randomizer value to 1
							randomizer = 1;
							unsuccessfulAttempts = 0; // Reset the counter
							player.SetData("unsuccessfulAttempts", unsuccessfulAttempts);
						}
					}

					if (isBoss3 && spawned && randomizer <= config.PerfectRate.Value)
					{
						if (randomizer != 1)
						{
							currencyAmount = Main.rand.Next(config.Boss3MaxAmount.Value);
						}
						else
						{
							currencyAmount = config.Boss3MaxAmount.Value;
						}

						reason = Transaction.ReceivedFromKillingBossNPC;
						TSPlayer.All.SendMessage($"{player.Name} recieved {currencyAmount} {config.CurrencyName.Value} from killing {npc.TypeName}.", Color.LightCyan);

						spawned = false;
					}
					else if (isBoss2 && spawned && randomizer <= config.PerfectRate.Value)
					{
						if (randomizer != 1)
						{
							currencyAmount = Main.rand.Next(config.Boss2MaxAmount.Value);
						}
						else
						{
							currencyAmount = config.Boss2MaxAmount.Value;
						}
						reason = Transaction.ReceivedFromKillingBossNPC;
						TSPlayer.All.SendMessage($"{player.Name} recieved {currencyAmount} {config.CurrencyName.Value} from killing {npc.TypeName}.", Color.LightCyan);

						spawned = false;
					}
					else if (isBoss1 && spawned && randomizer <= config.PerfectRate.Value)
					{
						if (randomizer != 1)
						{
							currencyAmount = Main.rand.Next(config.Boss1MaxAmount.Value);
						}
						else
						{
							currencyAmount = config.Boss1MaxAmount.Value;
						}
						reason = Transaction.ReceivedFromKillingBossNPC;
						TSPlayer.All.SendMessage($"{player.Name} recieved {currencyAmount} {config.CurrencyName.Value} from killing {npc.TypeName}.", Color.LightCyan);

						spawned = false;
					}
					else if (isSpecial && randomizer <= config.HighRate.Value)
					{
						if (randomizer != 1)
						{
							currencyAmount = Main.rand.Next(config.SpecialMaxAmount.Value);
						}
						else
						{
							currencyAmount = config.SpecialMaxAmount.Value;
						}
						reason = Transaction.ReceivedFromKillingSpecialNPC;
					}
					else if (isHostile && randomizer <= config.MedRate.Value)
					{
						if (randomizer != 1)
						{
							currencyAmount = Main.rand.Next(config.HostileMaxAmount.Value);
						}
						else
						{
							currencyAmount = config.HostileMaxAmount.Value;
						}
						reason = Transaction.ReceivedFromKillingHostileNPC;
					}
					else if (!(isHostile || isSpecial || isBoss1 || isBoss2 || isBoss3) && randomizer <= config.LowRate.Value)
					{
						if (randomizer != 1)
						{
							currencyAmount = Main.rand.Next(config.NormalMaxAmount.Value);
						}
						else
						{
							currencyAmount = config.NormalMaxAmount.Value;
						}
						reason = Transaction.ReceivedFromKillingNormalNPC;
					}

					if (currencyAmount > 0)
					{
						try
						{
							// Apply hardmode multiplier first
							if (isHardmode)
								currencyAmount = (int)(currencyAmount * 1.2);

							// Apply rank multiplier
							double rankMultiplier = GetRankMultiplier(player.Group.Name);
							int originalAmount = currencyAmount;
							currencyAmount = (int)(currencyAmount * rankMultiplier);

							// Get current balance and update
							int balance = await bank.GetCurrencyAmount(player.Account.ID);
							int newBalance = balance + currencyAmount;
							await bank.UpdateCurrencyAmount(player.Account.ID, newBalance);
							await Transaction.RecordTransaction(player.Name, reason, currencyAmount);
							
							// Calculate the position above the player
							Vector2 displayPosition = player.TPlayer.Center + new Vector2(0, -100);
							
							// Send combat text with multiplier info if applicable
							player.SendData(PacketTypes.CreateCombatTextExtended, 
								$"{currencyAmount} {config.CurrencyName.Value}", 
								(int)Color.LightBlue.PackedValue, 
								displayPosition.X,
								displayPosition.Y + 1);

							// Spawn Lucky Coin particle effect
							ParticleOrchestraSettings settings = new ParticleOrchestraSettings
							{
								IndexOfPlayerWhoInvokedThis = (byte)player.Index,
								PositionInWorld = displayPosition,
								MovementVector = Vector2.Zero,
								UniqueInfoPiece = ItemID.LuckyCoin
							};

							// Broadcast the particle effect
							ParticleOrchestrator.BroadcastParticleSpawn(ParticleOrchestraType.ItemTransfer, settings);
						}
						catch (Exception ex)
						{
							TShock.Log.Error($"Error updating currency in Economy: {ex.Message}");
						}
					}
					break;

				default:
					return;
			}
		}

		public void OnTileEdit(object sender, GetDataHandlers.TileEditEventArgs e)
		{
			if (e.Handled || e.Player == null || e.Action != GetDataHandlers.EditAction.KillTile)
				return;

			// Check if the edited tile is a Plantera bulb
			if (Main.tile[e.X, e.Y].type == TileID.PlanteraBulb)
			{
				// Plantera bulb was broken by a player
				// Perform your desired actions
				spawned = true;
				TSPlayer.All.SendInfoMessage("Boss Spawned! The one who gets the last hit gets the jspoints!");
			}
		}


		public async Task EconomyCommands(CommandArgs args)
		{
			var cmd = args.Parameters;
			var player = args.Player;

			if (cmd.Count == 0)
			{
				ShowHelpCommands(player);
				return;
			}

			switch (cmd[0].ToLower())
			{
				case "bal":
					await HandleBalanceCommand(player);
					break;

				case "check":
					await HandleCheckCommand(player, cmd);
					break;

				case "give":
					await HandleGiveCommand(player, cmd);
					break;

				case "giveall":
					await HandleGiveAllCommand(player, cmd);
					break;

				case "pay":
					await HandlePayCommand(player, cmd);
					break;

				case "resetall":
					await HandleResetAllCommand(player);
					break;

				case "help":
				default:
					ShowHelpCommands(player);
					break;
			}
		}

		private void ShowHelpCommands(TSPlayer player)
		{
			player.SendMessage("Bank commands:", Color.LightBlue);
			player.SendMessage("/bank bal - Check your balance", Color.LightBlue);
			player.SendMessage("/bank pay <player> <amount> - Pay another player", Color.LightBlue);
			if (player.Group.HasPermission("jgranserver.admin"))
			{
				player.SendMessage("/bank resetall - Reset all balances", Color.LightBlue);
				player.SendMessage("/bank check <player> - Check player's balance", Color.LightBlue);
				player.SendMessage("/bank give <player> <amount> - Give currency to player", Color.LightBlue);
				player.SendMessage("/bank giveall <amount> - Give currency to all players", Color.LightBlue);
			}
		}

		private async Task HandleBalanceCommand(TSPlayer player)
		{
			try
			{
				var exists = await bank.PlayerAccountExists(player.Account.ID);
				if (!exists)
				{
					await bank.UpdateCurrencyAmount(player.Account.ID, 0);
					player.SendInfoMessage("A new bank account has been created for you.");
				}

				var bal = await bank.GetCurrencyAmount(player.Account.ID);
				player.SendMessage($"Bank Balance: [c/#00FF6E:{bal}] {config.CurrencyName.Value}/s", Color.LightBlue);
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Error in HandleBalanceCommand: {ex.Message}");
				player.SendErrorMessage("An error occurred while checking your balance.");
			}
		}

		private async Task HandleCheckCommand(TSPlayer player, List<string> cmd)
		{
			if (!player.Group.HasPermission("jgranserver.admin"))
			{
				player.SendErrorMessage("You don't have permission to check other players' bank accounts.");
				return;
			}

			if (cmd.Count < 2)
			{
				player.SendErrorMessage("Usage: /bank check <playername>");
				return;
			}

			try
			{
				var targetName = cmd[1];
				var target = TShock.UserAccounts.GetUserAccountByName(targetName);
				if (target == null)
				{
					player.SendErrorMessage("Player does not exist.");
					return;
				}

				// Log the check attempt
				TShock.Log.Debug($"Checking balance for player ID: {target.ID}, Name: {target.Name}");

				bool exists = await bank.PlayerAccountExists(target.ID);
				TShock.Log.Debug($"Account exists check result: {exists}");

				if (!exists)
				{
					player.SendErrorMessage("Player does not have a bank account.");
					return;
				}

				int targetBal = await bank.GetCurrencyAmount(target.ID);
				TShock.Log.Debug($"Retrieved balance: {targetBal}");

				player.SendMessage($"{target.Name}'s Balance: {targetBal} {config.CurrencyName.Value}/s", Color.LightBlue);
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Error in HandleCheckCommand: {ex.Message}");
				player.SendErrorMessage("An error occurred while checking the player's balance.");
			}
		}

		private async Task HandlePayCommand(TSPlayer player, List<string> cmd)
		{
			if (cmd.Count < 3 || !int.TryParse(cmd[2], out int payment) || payment <= 0)
			{
				player.SendErrorMessage("Usage: /bank pay <playername> <amount>");
				return;
			}

			var receiverAccount = TShock.UserAccounts.GetUserAccountByName(cmd[1]);
			if (receiverAccount == null)
			{
				player.SendErrorMessage("Player does not exist.");
				return;
			}

			var receiverPlayer = TShock.Players.FirstOrDefault(p => 
				p?.Account?.Name.Equals(cmd[1], StringComparison.CurrentCulture) == true);
			if (receiverPlayer == null)
			{
				player.SendErrorMessage("Player is not online.");
				return;
			}

			int senderBalance = await bank.GetCurrencyAmount(player.Account.ID);
			if (senderBalance < payment)
			{
				player.SendErrorMessage("Insufficient funds for this payment.");
				return;
			}

			int receiverBalance = await bank.GetCurrencyAmount(receiverAccount.ID);
			await bank.UpdateCurrencyAmount(player.Account.ID, senderBalance - payment);
			await bank.UpdateCurrencyAmount(receiverAccount.ID, receiverBalance + payment);
			await Transaction.RecordTransaction(receiverAccount.Name, Transaction.ReceivedFromPayment + player.Account.Name, payment);

			player.SendSuccessMessage($"Paid {payment} {config.CurrencyName.Value}/s to {receiverPlayer.Name}.");
			receiverPlayer.SendSuccessMessage($"Received {payment} {config.CurrencyName.Value}/s from {player.Name}.");
		}

		private async Task HandleGiveCommand(TSPlayer player, List<string> cmd)
		{
			if (!player.Group.HasPermission("jgranserver.admin"))
			{
				player.SendErrorMessage("You don't have permission to give currency.");
				return;
			}

			if (cmd.Count < 3 || !int.TryParse(cmd[2], out int amount))
			{
				player.SendErrorMessage("Usage: /bank give <playername> <amount>");
				return;
			}

			var target = TShock.UserAccounts.GetUserAccountByName(cmd[1]);
			if (target == null)
			{
				player.SendErrorMessage("Player does not exist.");
				return;
			}

			try
			{
				// Check if account exists and create it if it doesn't
				if (!await bank.PlayerAccountExists(target.ID))
				{
					await bank.UpdateCurrencyAmount(target.ID, 0);
				}

				// Get current balance and add the amount
				var currentBalance = await bank.GetCurrencyAmount(target.ID);
				await bank.UpdateCurrencyAmount(target.ID, currentBalance + amount);

				// Record the transaction
				await Transaction.RecordTransaction(target.Name, $"Given by {player.Account.Name}", amount);

				player.SendSuccessMessage($"Added {amount} {config.CurrencyName.Value}/s to {target.Name}'s account.");
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Error in HandleGiveCommand: {ex.Message}");
				player.SendErrorMessage("An error occurred while processing the command.");
			}
		}

		private async Task HandleGiveAllCommand(TSPlayer player, List<string> cmd)
		{
			if (!player.HasPermission("jgranserver.admin"))
			{
				player.SendErrorMessage("You don't have permission to give currency to all players.");
				return;
			}

			if (cmd.Count < 2 || !int.TryParse(cmd[1], out int amount) || amount == 0)
			{
				player.SendErrorMessage("Usage: /bank giveall <amount>");
				return;
			}

			foreach (var account in TShock.UserAccounts.GetUserAccounts())
			{
				if (await bank.PlayerAccountExists(account.ID))
				{
					var balance = await bank.GetCurrencyAmount(account.ID);
					await bank.UpdateCurrencyAmount(account.ID, balance + amount);

					var onlinePlayer = TShock.Players.FirstOrDefault(p => p?.Account?.ID == account.ID);
					onlinePlayer?.SendSuccessMessage($"Received {amount} {config.CurrencyName.Value}/s.");
				}
			}

			player.SendSuccessMessage($"Added {amount} {config.CurrencyName.Value}/s to all player accounts.");
		}

		private async Task HandleResetAllCommand(TSPlayer player)
		{
			if (!player.Group.HasPermission("jgranserver.admin"))
			{
				player.SendErrorMessage("You don't have permission to reset all accounts.");
				return;
			}

			await bank.ResetAllCurrencyAmounts();
			player.SendMessage("All bank accounts have been reset.", Color.LightBlue);
		}

		private void OnNetGetData(GetDataEventArgs args)
		{
			TSPlayer player = TShock.Players[args.Msg.whoAmI];

			if (args.MsgID == PacketTypes.SpawnBossorInvasion)
			{
				spawned = true;
				TSPlayer.All.SendInfoMessage("Boss Spawned! The one who gets the last hit gets the jspoints!");
			}
		}

		private void OnServerChat(ServerChatEventArgs args)
		{
			string message = args.Text;

			// Check if the chat message starts with the command prefix (e.g., "/")
			if (message.StartsWith("/") || message.StartsWith("."))
			{
				// Extract the command name from the chat message
				string commandName = message.Substring(1).Split(' ')[0];

				// Check if the command name corresponds to your desired command
				if (commandName.Equals("spawnboss", StringComparison.OrdinalIgnoreCase) || commandName.Equals("sb", StringComparison.OrdinalIgnoreCase))
				{
					// The "/spawnboss" command was executed by a player

					// Execute the command
					bool commandExecuted = TShockAPI.Commands.HandleCommand(TSPlayer.Server, message);

					// Check if the command was successfully executed
					if (commandExecuted)
					{
						// The command was executed successfully

						// Perform your desired actions
						spawned = true;
						TSPlayer.All.SendInfoMessage("Boss Spawned! The one who gets the last hit gets the jspoints!");

						// Access the player who executed the command
						var player = TShock.Players[args.Who];
						if (player != null)
						{
							// You can perform additional actions specific to the player here
						}
					}
					else
					{
						// The command execution failed

						// Perform any necessary error handling or notification
					}
				}
			}
		}

		private async void OnPlayerJoin(JoinEventArgs args)
		{
			var player = TShock.Players[args.Who];
			if (player == null || !player.IsLoggedIn)
				return;

			try
			{
				bool accountExists = await bank.PlayerAccountExists(player.Account.ID);
				if (!accountExists)
				{
					await bank.UpdateCurrencyAmount(player.Account.ID, 0);
					player.SendInfoMessage($"{config.ServerName.Value} Economy System Running!");
					player.SendInfoMessage("A new bank account has been created for your account.");
				}
				else
				{
					int balance = await bank.GetCurrencyAmount(player.Account.ID);
					player.SendInfoMessage($"{config.ServerName.Value} Economy System Running!");
					player.SendInfoMessage($"Your current bank balance is {balance} {config.CurrencyName.Value}.");
				}
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Error in OnPlayerJoin: {ex.Message}");
			}
		}

		private async void LeaderboardCommandAsync(CommandArgs args)
		{
			try
			{
				var leaderboardData = await bank.GetLatestLeaderboardData();
				
				var sb = new StringBuilder();
				sb.AppendLine($"=== Top {config.CurrencyName.Value} Leaderboard ===");
				
				if (leaderboardData.Count == 0)
				{
					sb.AppendLine("No leaderboard data available.");
					sb.AppendLine($"Next update scheduled for: {GetNextUpdateTime():HH:mm:ss}");
				}
				else
				{
					sb.AppendLine($"Last updated: {leaderboardData[0].UpdatedAt:yyyy-MM-dd HH:mm:ss}");
					sb.AppendLine($"Next update in: {GetTimeUntilNextUpdate().ToString(@"hh\:mm\:ss")}");
					sb.AppendLine("----------------------------------------");

					foreach (var entry in leaderboardData)
					{
						string rankDisplay = GetLeaderboardRankDisplay(entry.Position);
						sb.AppendLine($"{rankDisplay} {entry.PlayerName}: {entry.CurrencyAmount:N0} {config.CurrencyName.Value}");
					}
				}

				args.Player.SendMessage(sb.ToString(), Color.LightGoldenrodYellow);
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Error in LeaderboardCommandAsync: {ex.Message}");
				args.Player.SendErrorMessage("An error occurred while retrieving the leaderboard.");
			}
		}

		private DateTime GetNextUpdateTime()
		{
			var now = DateTime.Now;
			var next = new DateTime(now.Year, now.Month, now.Day, 
				config.LeaderboardUpdateHour.Value, 
				config.LeaderboardUpdateMinute.Value, 0);
			
			if (next <= now)
				next = next.AddDays(1);
			
			return next;
		}

		private TimeSpan GetTimeUntilNextUpdate()
		{
			return GetNextUpdateTime() - DateTime.Now;
		}

		private string GetLeaderboardRankDisplay(int position)
		{
			return position switch
			{
				1 => "[c/FFD700:#1]",  // Gold
				2 => "[c/C0C0C0:#2]",  // Silver
				3 => "[c/CD7F32:#3]",  // Bronze
				_ => $"#{position}"
			};
		}

		private async void OnWorldLoad(EventArgs args)
		{
			try
			{
				if (config.LastWorldId.Value != -1 && config.LastWorldId.Value != Main.worldID)
				{
					// World has changed, reset ranks
					await ResetRanksOnWorldChange();
				}

				// Update the stored world ID
				config.LastWorldId.Value = Main.worldID;
				config.Write(configPath);
				
				TShock.Log.Info($"World ID updated to: {Main.worldID}");
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Error in OnWorldLoad: {ex.Message}");
			}
		}

		private async Task ResetRanksOnWorldChange()
		{
			try
			{
				TShock.Log.Info("World change detected. Starting rank reset process...");
				var resetRank = config.WorldResetRank.Value;
				var affectedPlayers = new List<string>();

				foreach (var account in TShock.UserAccounts.GetUserAccounts())
				{
					// Check if player's rank is higher than the reset rank
					var ranks = await bank.GetRanks();
					var currentRank = ranks.FirstOrDefault(r => r.GroupName == account.Group);
					var resetRankObj = ranks.FirstOrDefault(r => r.GroupName == resetRank);

					if (currentRank == null || resetRankObj == null) continue;

					// Skip if player is already at or below the reset rank
					if (currentRank.RequiredCurrencyAmount <= resetRankObj.RequiredCurrencyAmount)
						continue;

					// Reset player's rank
					TShock.UserAccounts.SetUserGroup(account, resetRank);
					affectedPlayers.Add(account.Name);

					// Notify online player
					var player = TShock.Players.FirstOrDefault(p => p?.Account?.ID == account.ID);
					if (player != null)
					{
						player.SendMessage($"Due to world change, your rank has been reset to {resetRank}.", Color.Orange);
					}
				}

				// Log the reset
				if (affectedPlayers.Count > 0)
				{
					TShock.Log.Info($"Reset {affectedPlayers.Count} players to {resetRank} rank:");
					foreach (var name in affectedPlayers)
					{
						TShock.Log.Info($"- {name}");
					}
					TSPlayer.All.SendInfoMessage($"World change detected! {affectedPlayers.Count} players' ranks have been reset.");
				}
			}
			catch (Exception ex)
			{
				TShock.Log.Error($"Error resetting ranks on world change: {ex.Message}");
				TShock.Log.Error($"Stack trace: {ex.StackTrace}");
			}
		}

		private void InitializeWorldCommand(CommandArgs args)
		{
			if (!args.Player.HasPermission("jgranserver.admin"))
			{
				args.Player.SendErrorMessage("You don't have permission to use this command.");
				return;
			}

			config.LastWorldId.Value = Main.worldID;
			config.Write(configPath);
			
			args.Player.SendSuccessMessage($"World ID initialized to: {Main.worldID}");
			TShock.Log.Info($"World ID manually initialized to {Main.worldID} by {args.Player.Name}");
		}

		private double GetRankMultiplier(string rankName)
		{
			if (string.IsNullOrEmpty(rankName))
				return 1.0;

			return rankName switch
			{
				var r when r == config.Top1Rank.Value => 10.0,    // 10x multiplier
				var r when r == config.Top2Rank.Value => 8.0,     // 8x multiplier
				var r when r == config.Top3Rank.Value => 6.0,     // 6x multiplier
				var r when r == config.Top4Rank.Value => 4.0,     // 4x multiplier
				var r when r == config.Top56Rank.Value => 3.0,    // 3x multiplier
				var r when r == config.Top78Rank.Value => 2.0,    // 2x multiplier
				var r when r == config.Top910Rank.Value => 1.5,   // 1.5x multiplier
				_ => 1.0                                          // Default multiplier
			};
		}
	}
}
