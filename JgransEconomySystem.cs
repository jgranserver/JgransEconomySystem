using TShockAPI;
using TerrariaApi.Server;
using Terraria;
using Microsoft.Xna.Framework;
using TShockAPI.DB;
using System.IO.Streams;
using Newtonsoft.Json;

namespace JgransEconomySystem
{
	[ApiVersion(2, 1)]
	public class JgransEconomySystem : TerrariaPlugin
	{
		private EconomyDatabase bank;
		private JgransEconomySystemConfig config;
		private string path = Path.Combine(TShock.SavePath, "JgransEconomyBanks.sqlite");
		public JgransEconomySystem(Main game) : base(game)
		{

		}

		public override string Name => "JgransEconomySystem";

		public override Version Version => new Version(2, 2);

		public override string Author => "jgranserver";

		public override string Description => "Economy system.";

		public override void Initialize()
		{
			string configPath = Path.Combine(TShock.SavePath, "JgransEconomySystemConfig.json");
			var config = JgransEconomySystemConfig.Read(configPath);
			if (!File.Exists(configPath))
			{
				config.Write(configPath);
			}
			
			bank = new EconomyDatabase(path);
			
			ServerApi.Hooks.NetSendData.Register(this, EconomyAsync);
			ServerApi.Hooks.NetGetData.Register(this, OnNetGetData);
			
			Commands.ChatCommands.Add(new Command("jgraneconomy.system", EconomyCommandsAsync, "bank"));
			Commands.ChatCommands.Add(new Command("jgranserver.admin", SetupShopCommand, "setshop"));
			Commands.ChatCommands.Add(new Command("jgranserver.admin", AddAllowedGroupCommand, "shopallow"));
			Commands.ChatCommands.Add(new Command("jgranserver.admin", DeleteItemShopCommand, "delshop"));
			Commands.ChatCommands.Add(new Command("jgranserver.admin", DeleteShopCommand, "delcommandshop"));
			Commands.ChatCommands.Add(new Command("jgranserver.admin", SetupSellCommand, "sellcommand"));

			Rank.Initialize();
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

			}
			base.Dispose(disposing);
		}

		bool spawned = false;

		private async Task Economy(SendDataEventArgs args)
		{
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
			var npc = Main.npc[npcIndex];

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

					int randomizer = Main.rand.Next(101);
					int currencyAmount = 0;
					string reason = "";

					bool isHardmode = Main.hardMode;
					if (isHardmode)
						randomizer = Main.rand.Next(86);

					if (isBoss3 && spawned && randomizer <= config.PerfectRate)
					{
						currencyAmount = Main.rand.Next(config.Boss3MaxAmount);
						reason = Transaction.ReceivedFromKillingBossNPC;
						spawned = false;
					}
					else if (isBoss2 && spawned && randomizer <= config.PerfectRate)
					{
						currencyAmount = Main.rand.Next(config.Boss2MaxAmount);
						reason = Transaction.ReceivedFromKillingBossNPC;
						spawned = false;
					}
					else if (isBoss1 && spawned && randomizer <= config.PerfectRate)
					{
						currencyAmount = Main.rand.Next(config.Boss1MaxAmount);
						reason = Transaction.ReceivedFromKillingBossNPC;
						spawned = false;
					}
					else if (isSpecial && randomizer <= config.HighRate)
					{
						currencyAmount = Main.rand.Next(config.SpecialMaxAmount);
						reason = Transaction.ReceivedFromKillingSpecialNPC;
					}
					else if (isHostile && randomizer <= config.MedRate)
					{
						currencyAmount = Main.rand.Next(config.HostileMaxAmount);
						reason = Transaction.ReceivedFromKillingHostileNPC;
					}
					else if (!(isHostile || isSpecial || isBoss1 || isBoss2 || isBoss3) && randomizer <= config.LowRate)
					{
						currencyAmount = Main.rand.Next(config.NormalMaxAmount);
						reason = Transaction.ReceivedFromKillingNormalNPC;
					}

					if (currencyAmount > 0)
					{
						if (isHardmode)
							currencyAmount = (int)(currencyAmount * 1.2);

						bool accountExists = await bank.PlayerAccountExists(player.Account.ID);
						if (!accountExists)
						{
							await bank.AddPlayerAccount(player.Account.ID, 0);
							player.SendInfoMessage($"{config.ServerName} Economy System Running!");
							player.SendInfoMessage("A new bank account has been created for your account.");
						}

						int balance = await bank.GetCurrencyAmount(player.Account.ID);
						int newBalance = balance + currencyAmount;
						await bank.RecordTransaction(player.Name, reason, currencyAmount);
						player.SendData(PacketTypes.CreateCombatTextExtended, $"{currencyAmount} {config.CurrencyName}", (int)Color.LightBlue.PackedValue, player.X, player.Y);
						await bank.SaveCurrencyAmount(player.Account.ID, newBalance);
					}

					break;

				default:
					return;
			}
		}

		public async Task EconomyCommands(CommandArgs args)
		{
			var cmd = args.Parameters;
			var player = args.Player;

			if (cmd.Count <= 0)
				return;

			switch (cmd[0])
			{
				case "bal":
					if (cmd.Count < 1)
						return;

					var bal = await bank.GetCurrencyAmount(player.Account.ID);
					player.SendMessage($"Bank Balance: [c/#00FF6E:{bal}] {config.CurrencyName}/s", Color.LightBlue);
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

					player.SendMessage($"{target.Name}'s Balance: {targetBal} {config.CurrencyName}/s", Color.LightBlue);
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
					player.SendInfoMessage($"Successfully added {addToBalance} {config.CurrencyName}/s to the account of {targetPlayer.Name}");
					await bank.SaveCurrencyAmount(targetPlayer.ID, newBalance);
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

					int receiverNewBalance = receiverBalance + payment;
					int senderNewBalance = senderBalance - payment;

					await Transaction.ProcessTransaction(receiverId, receiverAccount.Name, payment);
					await bank.SaveCurrencyAmount(senderId, senderNewBalance);

					player.SendSuccessMessage($"You have successfully paid {payment} {config.CurrencyName}/s to {receiverPlayer.Name}.");
					receiverPlayer.SendSuccessMessage($"You have received {payment} {config.CurrencyName}/s from {player.Name}.");
					break;

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

		private void SetupShopCommand(CommandArgs args)
		{
			var player = args.Player;
			var parameters = args.Parameters;

			if (parameters.Count < 4)
			{
				player.SendErrorMessage("Invalid command format. Usage: /setupshop <itemID> <stack> <price> <group>");
				return;
			}

			if (!int.TryParse(parameters[0], out int item) || !int.TryParse(parameters[1], out int stack) || !int.TryParse(parameters[2], out int price))
			{
				player.SendErrorMessage("Invalid parameter format. Item, stack, and price must be integers.");
				return;
			}

			var group = TShock.Groups.GetGroupByName(parameters[3]);
			if (group == null)
			{
				player.SendErrorMessage("Group does not exist.");
				return;
			}

			int itemID = item;
			int stackSize = stack;
			int shopPrice = price;
			string groupName = group.Name;

			player.SendSuccessMessage("Hit a switch to register the shop.");
			player.SetData("SwitchShopItemID", itemID);
			player.SetData("SwitchShopStackSize", stackSize);
			player.SetData("SwitchShopPrice", shopPrice);
			player.SetData("SwitchAllowedGroup", groupName);
			player.SetData("IsSettingUpShop", true);
		}

		private void SetupSellCommand(CommandArgs args)
		{
			var player = args.Player;
			var parameters = args.Parameters;

			if (parameters.Count < 3)
			{
				player.SendErrorMessage("Invalid command format. Usage: /sellcommand <command> <price> <group>");
				return;
			}

			string command = parameters[0];
			if (string.IsNullOrWhiteSpace(command))
			{
				player.SendErrorMessage("Invalid command value.");
				return;
			}

			if (!int.TryParse(parameters[1], out int price))
			{
				player.SendErrorMessage("Invalid parameter format. Price must be an integer.");
				return;
			}

			var group = TShock.Groups.GetGroupByName(parameters[2]);
			if (group == null)
			{
				player.SendErrorMessage("Group does not exist.");
				return;
			}

			player.SendSuccessMessage("Hit a switch to register the shop.");
			player.SetData("SwitchShopCommand", command);
			player.SetData("SwitchShopPrice", price);
			player.SetData("SwitchAllowedGroup", group.Name);
			player.SetData("IsSetupSellCommand", true);
		}

		private void DeleteItemShopCommand(CommandArgs args)
		{
			var player = args.Player;
			player.SendSuccessMessage("Hit a switch to delete the shop.");
			player.SetData("IsDeletingUpShop", true);
		}

		private void DeleteShopCommand(CommandArgs args)
		{
			var player = args.Player;
			player.SendSuccessMessage("Hit a switch to delete the shop.");
			player.SetData("IsDeletingUpCommandShop", true);
		}

		private void AddAllowedGroupCommand(CommandArgs args)
		{
			var player = args.Player;

			if (args.Parameters.Count == 0)
			{
				player.SendErrorMessage("Invalid syntax! Proper syntax: /shopallow <rank1> <rank2> <rank3> ...");
				return;
			}

			List<string> allowedGroups = new List<string>();

			foreach (var rankName in args.Parameters)
			{
				var group = TShock.Groups.GetGroupByName(rankName);
				if (group == null)
				{
					player.SendErrorMessage($"Group '{rankName}' does not exist.");
					return;
				}
				allowedGroups.Add(group.Name);
			}

			player.SendSuccessMessage("Hit a switch to add new group allowed to the shop.");

			var allowedGroupsJson = JsonConvert.SerializeObject(allowedGroups);
			player.SetData("NewAllowedGroup", allowedGroupsJson);
			player.SetData("AddAllowedGroup", true);
		}

		private void OnNetGetData(GetDataEventArgs args)
		{
			if (args.MsgID == PacketTypes.HitSwitch)
			{
				TSPlayer player = TShock.Players[args.Msg.whoAmI];
				if (player != null)
				{
					HandleHitSwitchPacket(player, args);
				}
			}

			if (args.MsgID == PacketTypes.SpawnBossorInvasion)
			{
				spawned = BossSpawned();
				TSPlayer.All.SendInfoMessage("Reward Counter Set to 1");
			}
		}

		public bool BossSpawned()
		{
			return true;
		}

		private async void HandleHitSwitchPacket(TSPlayer player, GetDataEventArgs args)
		{
			using (MemoryStream stream = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				int switchX = reader.ReadInt16();
				int switchY = reader.ReadInt16();

				if (player.GetData<bool>("IsSettingUpShop"))
				{
					int itemID = player.GetData<int>("SwitchShopItemID");
					int stackSize = player.GetData<int>("SwitchShopStackSize");
					int shopPrice = player.GetData<int>("SwitchShopPrice");
					string groupName = player.GetData<string>("SwitchAllowedGroup");
					byte switchStyle = reader.ReadByte();
					int switchWorldID = Main.worldID;

					await bank.SaveShopToDatabase(switchX, switchY, itemID, stackSize, shopPrice, groupName, switchWorldID);

					player.SendSuccessMessage("Shop successfully registered.");
					player.RemoveData("SwitchShopItemID");
					player.RemoveData("SwitchShopStackSize");
					player.RemoveData("SwitchShopPrice");
					player.RemoveData("SwitchAllowedGroup");
					player.RemoveData("IsSettingUpShop");
					return;
				}

				if (player.GetData<bool>("IsSetupSellCommand"))
				{
					string command = player.GetData<string>("SwitchShopCommand");
					int shopPrice = player.GetData<int>("SwitchShopPrice");
					string groupName = player.GetData<string>("SwitchAllowedGroup");
					byte switchStyle = reader.ReadByte();
					int switchWorldID = Main.worldID;

					await bank.AddCommandToShop(switchX, switchY, command, shopPrice, groupName, switchWorldID);

					player.SendSuccessMessage("Shop successfully registered.");
					player.RemoveData("SwitchShopCommand");
					player.RemoveData("SwitchShopPrice");
					player.RemoveData("SwitchAllowedGroup");
					player.RemoveData("IsSetupSellCommand");
					return;
				}

				if (player.GetData<bool>("AddAllowedGroup"))
				{
					var shop = await bank.GetShopFromDatabase(switchX, switchY, true, true);
					if (shop != null)
					{
						var allowedGroups = shop.AllowedGroup.Split(',');
						var newGroupsJson = player.GetData<string>("NewAllowedGroup");
						var newGroups = JsonConvert.DeserializeObject<List<string>>(newGroupsJson);

						foreach (var newGroup in newGroups)
						{
							if (!allowedGroups.Contains(newGroup))
							{
								allowedGroups = allowedGroups.Append(newGroup).ToArray();
								var updatedAllowedGroups = string.Join(",", allowedGroups);

								await bank.UpdateAllowedGroup(shop.X, shop.Y, updatedAllowedGroups, true, true);

								player.SendSuccessMessage($"Successfully added group '{newGroup}' to the shop at coordinates ({shop.X}, {shop.Y}).");
							}
							else
							{
								player.SendInfoMessage($"Group '{newGroup}' is already allowed for the shop at coordinates ({shop.X}, {shop.Y}).");
							}
						}
					}
					else
					{
						player.SendErrorMessage($"Shop not found at coordinates ({switchX}, {switchY}).");
					}

					player.RemoveData("NewAllowedGroup");
					player.RemoveData("AddAllowedGroup");
					return;
				}

				if (player.GetData<bool>("IsDeletingUpShop"))
				{
					var shop = await bank.GetShopFromDatabase(switchX, switchY, true, false);
					if (shop != null)
					{
						await bank.DeleteShopFromDatabase(switchX, switchY, true, false);
						player.SendSuccessMessage("Shop deleted successfully.");
					}
					else
					{
						player.SendErrorMessage("Shop not found at the specified coordinates.");
					}

					player.RemoveData("IsDeletingUpShop");
					return;
				}

				if (player.GetData<bool>("IsDeletingUpCommandShop"))
				{
					var shop = await bank.GetShopFromDatabase(switchX, switchY, false, true);
					if (shop != null)
					{
						await bank.DeleteShopFromDatabase(switchX, switchY, false, true);
						player.SendSuccessMessage("Shop deleted successfully.");
					}
					else
					{
						player.SendErrorMessage("Shop not found at the specified coordinates.");
					}

					player.RemoveData("IsDeletingUpCommandShop");
					return;
				}

				await Transaction.HandleSwitchTransaction(switchX, switchY, player.Account.ID, true, true);
			}
		}
	}
}