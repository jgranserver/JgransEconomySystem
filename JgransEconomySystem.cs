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

		public JgransEconomySystem(Main game) : base(game)
		{

		}

		public override string Name => "JgransEconomySystem";

		public override Version Version => new Version(5, 0);

		public override string Author => "jgranserver";

		public override string Description => "Economy system.";

		public override void Initialize()
		{
			config = JgransEconomySystemConfig.Read(configPath);
			if (!File.Exists(configPath))
			{
				config.Write(configPath);
			}

			bank = new EconomyDatabase(path);

			ServerApi.Hooks.NetSendData.Register(this, EconomyAsync);
			ServerApi.Hooks.NetGetData.Register(this, OnNetGetData);
			ServerApi.Hooks.ServerChat.Register(this, OnServerChat);
			ServerApi.Hooks.ServerJoin.Register(this, OnPlayerJoin);

			GetDataHandlers.TileEdit += OnTileEdit;

			Commands.ChatCommands.Add(new Command("jgraneconomy.system", EconomyCommandsAsync, "bank"));
			Commands.ChatCommands.Add(new Command("jgraneconomy.admin", ReloadConfigCommand, "economyreload", "er"));

			Rank.Initialize();
			Transaction.InitializeTransactionDataAsync();
			UpdateEconomyStatus();
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
						if (isHardmode)
							currencyAmount = (int)(currencyAmount * 1.2);

						int balance = await bank.GetCurrencyAmount(player.Account.ID);
						int newBalance = balance + currencyAmount;
						await Transaction.RecordTransaction(player.Name, reason, currencyAmount);
						player.SendData(PacketTypes.CreateCombatTextExtended, $"{currencyAmount} {config.CurrencyName.Value}", (int)Color.LightBlue.PackedValue, player.X, player.Y);
						await bank.SaveCurrencyAmount(player.Account.ID, newBalance);
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
				player.SendMessage("Bank commands:", Color.LightBlue);
				player.SendMessage("/bank bal", Color.LightBlue);
				player.SendMessage("/bank pay", Color.LightBlue);
				if (player.Group.HasPermission("jgranserver.admin"))
				{
					player.SendMessage("/bank resetall", Color.LightBlue);
					player.SendMessage("/bank check", Color.LightBlue);
				}
				return;
			}

			switch (cmd[0])
			{
				case "bal":
					var bal = await bank.GetCurrencyAmount(player.Account.ID);
					player.SendMessage($"Bank Balance: [c/#00FF6E:{bal}] {config.CurrencyName.Value}/s", Color.LightBlue);
					break;

				case "check":
					var target = TShock.UserAccounts.GetUserAccountByName(cmd[1]);
					int targetBal = 0;
					bool targetIdExist;

					if (!player.Group.HasPermission("jgranserver.admin"))
					{
						player.SendErrorMessage("You dont have the right to check other player bank accounts.");
						return;
					}

					if (cmd.Count < 2)
					{
						player.SendErrorMessage("Command invalid.\n/bank check <playername>");
						return;
					}

					try
					{
						targetIdExist = await bank.PlayerAccountExists(target.ID);

						if (targetIdExist)
							targetBal = await bank.GetCurrencyAmount(target.ID);
					}
					catch (NullReferenceException)
					{
						player.SendErrorMessage("Player does not have a bank account or does not exist.");
						return;
					}

					player.SendMessage($"{target.Name}'s Balance: {targetBal} {config.CurrencyName.Value}/s", Color.LightBlue);
					break;

				case "give":
					if (cmd.Count < 3)
					{
						player.SendErrorMessage("Command invalid.\n/bank give <playername> <amount>");
						return;
					}

					if (!player.Group.HasPermission("jgranserver.admin"))
					{
						player.SendErrorMessage("You don't have the right to add balance to other players' bank accounts.");
						return;
					}

					var targetPlayerName = cmd[1];
					var addToBalance = Convert.ToInt32(cmd[2]);

					var targetPlayer = TShock.UserAccounts.GetUserAccountByName(targetPlayerName);
					if (targetPlayer == null)
					{
						player.SendErrorMessage("Player does not exist.");
						return;
					}

					var targetIdExistGive = await bank.PlayerAccountExists(targetPlayer.ID);
					if (!targetIdExistGive)
						await bank.AddPlayerAccount(targetPlayer.ID, 0);

					var targetBalGive = await bank.GetCurrencyAmount(targetPlayer.ID);
					var newBalance = targetBalGive + addToBalance;
					player.SendInfoMessage($"Successfully added {addToBalance} {config.CurrencyName.Value}/s to the account of {targetPlayer.Name}");
					await bank.SaveCurrencyAmount(targetPlayer.ID, newBalance);
					break;

				case "giveall":
					if (!player.HasPermission("jgranserver.admin"))
					{
						return;
					}

					if (int.TryParse(cmd[1], out int amount))
					{
						if (amount != 0)
						{
							foreach (var p in TShock.UserAccounts.GetUserAccounts())
							{
								var exist = await bank.PlayerAccountExists(p.ID);
								var _p = TSPlayer.All;

								if (exist)
								{
									var playerBalance = await bank.GetCurrencyAmount(p.ID);
									newBalance = playerBalance + amount;

									await bank.SaveCurrencyAmount(p.ID, newBalance);

									if (_p.IsLoggedIn)
									{
										_p.SendSuccessMessage("Received {0} jspoints as reward.", amount);
									}
								}
							}
							player.SendSuccessMessage("Successfully added {0} jspoints for each players bank.", amount);
						}
					}
					else
					{
						player.SendErrorMessage("Command failed to execute. Please contact the admin.");
					}
					break;

				case "pay":
					if (cmd.Count < 3 || !int.TryParse(cmd[2], out int payment) || payment <= 0)
					{
						player.SendErrorMessage("Invalid command format. Usage: /bank pay <playername> <amount>");
						return;
					}

					string targetName = cmd[1];
					UserAccount receiverAccount = TShock.UserAccounts.GetUserAccountByName(targetName);
					if (receiverAccount == null)
					{
						player.SendErrorMessage("The specified player does not have a bank account.");
						return;
					}

					var receiverPlayer = TShock.Players.FirstOrDefault(p => p != null && p.Account != null && p.Account.Name.Equals(targetName, StringComparison.CurrentCulture));
					if (receiverPlayer == null)
					{
						player.SendErrorMessage("The specified player is not online.");
						return;
					}

					int receiverId = receiverAccount.ID;
					int senderId = player.Account.ID;

					int receiverBalance = await bank.GetCurrencyAmount(receiverId);
					int senderBalance = await bank.GetCurrencyAmount(senderId);

					if (senderBalance >= payment)
					{
						int receiverNewBalance = receiverBalance + payment;
						int senderNewBalance = senderBalance - payment;

						await Transaction.ProcessTransaction(receiverId, receiverAccount.Name, payment);
						await bank.SaveCurrencyAmount(senderId, senderNewBalance);

						player.SendSuccessMessage($"You have successfully paid {payment} {config.CurrencyName.Value}/s to {receiverPlayer.Name}.");
						receiverPlayer.SendSuccessMessage($"You have received {payment} {config.CurrencyName.Value}/s from {player.Name}.");
						break;
					}

					player.SendErrorMessage("You dont have enough amount for this payment!");
					return;

				case "resetall":
					if (!player.Group.HasPermission("jgranserver.admin"))
					{
						player.SendErrorMessage("You dont have the right use this command.");
						return;
					}

					player.SendMessage($"All bank accounts has been reset.", Color.LightBlue);
					await bank.ResetAllCurrencyAmounts();
					break;

				case "help":
				default:
					if (cmd.Count == 0)
					{
						player.SendMessage("Bank commands:", Color.LightBlue);
						player.SendMessage("/bank bal", Color.LightBlue);
						player.SendMessage("/bank pay", Color.LightBlue);
						if (player.Group.HasPermission("jgranserver.admin"))
						{
							player.SendMessage("/bank resetall", Color.LightBlue);
							player.SendMessage("/bank check", Color.LightBlue);
						}
					}
					break;
			}
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

			bool accountExists = await bank.PlayerAccountExists(player.Account.ID);
			if (!accountExists)
			{
				await bank.AddPlayerAccount(player.Account.ID, 0);
				player.SendInfoMessage($"{config.ServerName.Value} Economy System Running!");
				player.SendInfoMessage("A new bank account has been created for your account.");
			}
		}
	}
}