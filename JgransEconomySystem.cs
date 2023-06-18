using System.Reflection.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TShockAPI;
using static TShockAPI.GetDataHandlers;
using TerrariaApi.Server;
using Terraria;
using Terraria.ID;
using TShockAPI.Hooks;
using Microsoft.Xna.Framework;
using Terraria.Localization;
using TShockAPI.DB;
using System.Diagnostics;

namespace JgransEconomySystem
{
	[ApiVersion(2, 1)]
	public class JgransEconomySystem : TerrariaPlugin
	{
		private EconomyDatabase bank;
		private string currencyName => "jspoints";
		private string path = Path.Combine(TShock.SavePath, "JgransEconomyBanks.sqlite");
		public JgransEconomySystem(Main game) : base(game)
		{

		}

		public override string Name => "JgransEconomySystem";

		public override Version Version => new Version(2, 1);

		public override string Author => "jgranserver";

		public override string Description => "Economy system.";

		public override void Initialize()
		{
			bank = new EconomyDatabase(path);
			ServerApi.Hooks.NetSendData.Register(this, EconomyAsync);
			ServerApi.Hooks.NetGetData.Register(this, OnNetGetData);
			Commands.ChatCommands.Add(new Command("jgraneconomy.system", EconomyCommandsAsync, "bank"));
			Commands.ChatCommands.Add(new Command("jgranserver.admin", SetupShopCommand, "setshop"));
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
			base.Dispose(disposing);
		}

		private async Task Economy(SendDataEventArgs args)
		{
			var data = args.MsgId;
			var npcIndex = args.number;

			if (args.ignoreClient == -1)
			{
				return;
			}

			var players = TSPlayer.FindByNameOrID(args.ignoreClient.ToString());
			if (players.Count == 0)
			{
				// No players found with the specified name or ID
				return;
			}

			var player = players[0];

			switch (data)
			{
				case PacketTypes.NpcStrike:
					if (npcIndex >= Main.npc.Length || npcIndex < 0)
					{
						return;
					}

					var npc = Main.npc[npcIndex];
					if (npc == null || npc.life > 0)
					{
						return;
					}

					int lowRate = 30;
					int medRate = 20;
					int highRate = 35;
					int perfectRate = 15;

					bool isHostile = NPCType.IsHostile(npc.netID);
					bool isSpecial = NPCType.IsSpecial(npc.netID);
					bool isBoss1 = NPCType.IsBoss1(npc.netID);
					bool isBoss2 = NPCType.IsBoss2(npc.netID);
					bool isBoss3 = NPCType.IsBoss3(npc.netID);

					int randomizer = Main.rand.Next(101);
					int currencyAmount = 0;
					string reason = "";

					switch (npc.netID)
					{
						case int id when isBoss3 && randomizer <= lowRate + medRate + highRate + perfectRate:
							currencyAmount = Main.rand.Next(1000);
							reason = Transaction.ReceivedFromKillingBossNPC;
							break;

						case int id when isBoss2 && randomizer <= lowRate + medRate + highRate + perfectRate:
							currencyAmount = Main.rand.Next(600);
							reason = Transaction.ReceivedFromKillingBossNPC;
							break;

						case int id when isBoss1 && randomizer <= lowRate + medRate + highRate + perfectRate:
							currencyAmount = Main.rand.Next(380);
							reason = Transaction.ReceivedFromKillingBossNPC;
							break;

						case int id when isSpecial && randomizer <= lowRate + medRate + highRate:
							currencyAmount = Main.rand.Next(50);
							reason = Transaction.ReceivedFromKillingSpecialNPC;
							break;

						case int id when isHostile && randomizer <= lowRate + medRate:
							currencyAmount = Main.rand.Next(15);
							reason = Transaction.ReceivedFromKillingHostileNPC;
							break;

						case int id when !(isHostile || isSpecial || isBoss1 || isBoss2 || isBoss3) && randomizer <= lowRate:
							currencyAmount = Main.rand.Next(3);
							reason = Transaction.ReceivedFromKillingNormalNPC;
							break;
					}

					if (currencyAmount > 0)
					{
						bool accountExists = await bank.PlayerAccountExists(player.Account.ID);
						if (!accountExists)
						{
							await bank.AddPlayerAccount(player.Account.ID, 0);
							player.SendInfoMessage("Jgrans Economy System Running!");
							player.SendInfoMessage("A new bank account has been created for your account.");
						}

						if (isBoss1 || isBoss2 || isBoss3)
						{
							TSPlayer.All.SendMessage($"{player.Name} has been rewarded {currencyAmount} {currencyName} for the last hit blow!", Color.LightBlue);
						}

						int balance = await bank.GetCurrencyAmount(player.Account.ID);
						int newBalance = balance + currencyAmount;
						await bank.RecordTransaction(player.Name, reason, currencyAmount);
						player.SendData(PacketTypes.CreateCombatTextExtended, $"{currencyAmount} {currencyName}", (int)Color.LightBlue.PackedValue, player.X, player.Y);
						await bank.SaveCurrencyAmount(player.Account.ID, newBalance);

						return;
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

			switch (cmd[0])
			{
				case "bal":
					if (cmd.Count < 1)
					{
						return;
					}
					var bal = await bank.GetCurrencyAmount(player.Account.ID);
					player.SendMessage($"Bank Balance: [c/#00FF6E:{bal}]", Color.LightBlue);
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
						{
							targetBal = await bank.GetCurrencyAmount(target.ID);
						}
					}
					catch (NullReferenceException)
					{
						player.SendErrorMessage("Player does not have a bank account or does not exist.");
						return;
					}

					player.SendMessage($"{target.Name}'s Balance: {targetBal}", Color.LightBlue);
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

					await Transaction.ProcessTransaction(receiverId, receiverAccount.Name, payment, 0.2);
					await bank.SaveCurrencyAmount(senderId, senderNewBalance);

					player.SendSuccessMessage($"You have successfully paid {payment} {currencyName} to {receiverPlayer.Name}.");
					receiverPlayer.SendSuccessMessage($"You have received {payment} {currencyName} from {player.Name}.");
					break;

				case "resetall":
					if (!player.Group.HasPermission("jgranserver.admin"))
					{
						player.SendErrorMessage("You dont have the right use this command.");
						return;
					}
					if (cmd.Count == 0)
					{
						player.SendErrorMessage("Invalid command format. Usage: /bank resetall");
						return;
					}
					player.SendMessage($"All bank accounts has been reset.", Color.LightBlue);
					await bank.ResetAllCurrencyAmounts();
					break;

				case "help":
				default:
					player.SendMessage("Bank commands:", Color.LightBlue);
					player.SendMessage("/bank bal", Color.LightBlue);
					player.SendMessage("/bank pay", Color.LightBlue);
					if (player.Group.HasPermission("jgranserver.admin"))
					{
						player.SendMessage("/bank resetall", Color.LightBlue);
						player.SendMessage("/bank check", Color.LightBlue);
					}
					break;
			}
		}

		private void SetupShopCommand(CommandArgs args)
		{
			var player = args.Player;
			var parameters = args.Parameters;

			if (parameters.Count < 3)
			{
				player.SendErrorMessage("Invalid command format. Usage: /setupshop <itemID> <stack> <price>");
				return;
			}

			if (!int.TryParse(parameters[0], out int item) ||
				!int.TryParse(parameters[1], out int stack) ||
				!int.TryParse(parameters[2], out int price))
			{
				player.SendErrorMessage("Invalid parameter format. Item, stack, and price must be integers.");
				return;
			}

			// Store the item, stack, and price values in variables for later use
			int itemID = item;
			int stackSize = stack;
			int shopPrice = price;

			player.SendSuccessMessage("Hit a switch to register the shop.");
			player.SetData("SwitchShopItemID", itemID);
			player.SetData("SwitchShopStackSize", stackSize);
			player.SetData("SwitchShopPrice", shopPrice);
			player.SetData("IsSettingUpShop", true);
		}

		private void OnNetGetData(GetDataEventArgs args)
		{
			// Check if the packet type is HitSwitch
			if (args.MsgID == PacketTypes.HitSwitch)
			{
				TSPlayer player = TShock.Players[args.Msg.whoAmI];
				if (player != null)
				{
					// Handle the HitSwitch packet
					HandleHitSwitchPacket(player, args);
				}
			}
		}

		private async void HandleHitSwitchPacket(TSPlayer player, GetDataEventArgs args)
		{
			using (MemoryStream stream = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
			{
				using (BinaryReader reader = new BinaryReader(stream))
				{
					// Read switch coordinates
					int switchX = reader.ReadInt16();
					int switchY = reader.ReadInt16();

					// Check if the player is setting up a shop
					if (player.GetData<bool>("IsSettingUpShop"))
					{
						// Read other shop information
						int itemID = player.GetData<int>("SwitchShopItemID");
						int stackSize = player.GetData<int>("SwitchShopStackSize");
						int shopPrice = player.GetData<int>("SwitchShopPrice");
						byte switchStyle = reader.ReadByte();
						int switchWorldID = Main.worldID;

						// Save the switch coordinates and other information to the database
						await bank.SaveShopToDatabase(switchX, switchY, itemID, stackSize, shopPrice, switchWorldID);

						player.SendSuccessMessage("Shop successfully registered.");
						player.RemoveData("SwitchShopItemID");
						player.RemoveData("SwitchShopStackSize");
						player.RemoveData("SwitchShopPrice");
						player.RemoveData("IsSettingUpShop");
					}
					else
					{
						// Handle the switch transaction
						await Transaction.HandleSwitchTransaction(switchX, switchY, player.Account.ID);
					}
				}
			}
		}
	}
}