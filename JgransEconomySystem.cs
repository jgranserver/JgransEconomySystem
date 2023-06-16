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

		public override Version Version => new Version(1, 0);

		public override string Author => "jgranserver";

		public override string Description => "Economy system.";

		public override void Initialize()
		{
			bank = new EconomyDatabase(path);
			ServerApi.Hooks.NetSendData.Register(this, Economy);
			Commands.ChatCommands.Add(new Command("jgraneconomy.system", EconomyCommands, "bank"));
		}

		public void OnInit(EventArgs e)
		{

		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
		}

		private void Economy(SendDataEventArgs args)
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
					bool isBoss = NPCType.IsBoss(npc.netID);

					int randomizer = Main.rand.Next(101);
					int currencyAmount = 0;

					if (randomizer <= lowRate || !isHostile || !isHostile || !isBoss)
					{
						currencyAmount = Main.rand.Next(2);
					}
					else if (randomizer <= lowRate + medRate && isHostile)
					{
						currencyAmount = Main.rand.Next(15);
					}
					else if (randomizer <= lowRate + medRate + highRate && isSpecial)
					{
						currencyAmount = Main.rand.Next(50);
					}
					else if (randomizer <= lowRate + medRate + highRate + perfectRate && isBoss)
					{
						currencyAmount = Main.rand.Next(1000);
						TSPlayer.All.SendInfoMessage($"{player.Name} has been rewarded {currencyAmount} for the last hit blow!");
					}

					if (currencyAmount > 0)
					{
						bool accountExists = bank.PlayerAccountExists(player.Account.ID);
						if (!accountExists)
						{
							bank.AddPlayerAccount(player.Account.ID, 0);
							player.SendInfoMessage("Jgrans Economy System Running!");
							player.SendInfoMessage("A new bank account has been created for your account.");
						}

						int balance = bank.GetCurrencyAmount(player.Account.ID);
						int newBalance = balance + currencyAmount;
						bank.RecordTransaction(player.Name, Transaction.ReceivedFromKillingNormalNPC, currencyAmount);
						player.SendData(PacketTypes.CreateCombatTextExtended, $"{currencyAmount} {currencyName}", (int)Color.LightBlue.PackedValue, player.X, player.Y);
						bank.SaveCurrencyAmount(player.Account.ID, newBalance);
						return;
					}

					break;

				default:
					return;
			}
		}

		public void EconomyCommands(CommandArgs args)
		{
			var cmd = args.Parameters;
			var player = args.Player;

			if (cmd.Count == 0)
			{
				player.SendErrorMessage("Command invalid.\n/bank bal = Get account balance.");
				return;
			}

			switch (cmd[0])
			{
				case "bal":
					var bal = bank.GetCurrencyAmount(player.Account.ID);
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
						targetIdExist = bank.PlayerAccountExists(target.ID);

						if (targetIdExist)
						{
							targetBal = bank.GetCurrencyAmount(target.ID);
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

					int receiverBalance = bank.GetCurrencyAmount(receiverId);
					int senderBalance = bank.GetCurrencyAmount(senderId);

					int receiverNewBalance = receiverBalance + payment;
					int senderNewBalance = senderBalance - payment;

					Transaction.ProcessTransaction(receiverId, receiverAccount.Name, payment, 0.2);
					bank.SaveCurrencyAmount(senderId, senderNewBalance);

					player.SendSuccessMessage($"You have successfully paid {payment} {currencyName} to {receiverPlayer.Name}.");
					receiverPlayer.SendSuccessMessage($"You have received {payment} {currencyName} from {player.Name}.");
					break;
			}
		}
	}
}