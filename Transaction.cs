using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TShockAPI;

namespace JgransEconomySystem
{
	public static class Transaction
	{
		private static string path = Path.Combine(TShock.SavePath, "JgransEconomyBanks.sqlite");
		private static EconomyDatabase bank = new EconomyDatabase(path);

		public const string ReceivedFromKillingNormalNPC = "Received from killing normal NPC";
		public const string ReceivedFromKillingSpecialNPC = "Received from killing special NPC";
		public const string ReceivedFromKillingHostileNPC = "Received from killing hostile NPC";
		public const string ReceivedFromPayment = "Received from payment";
		public const string ReceivedFromKillingBossNPC = "Received from killing boss NPC";
		public const string ReceivedFromVoting = "Received from voting the server";
		public const string PurchasedFromShop = "Bought an item from shop";
		

		private static int CalculateTax(int amount, double taxRate)
		{
			var taxAmount = (int)Math.Ceiling(amount * taxRate);
			return taxAmount;
		}

		public static async Task ProcessTransaction(int playerId, string playerName, int amount, double taxRate)
		{
			var taxAmount = CalculateTax(amount, taxRate);
			var netAmount = amount - taxAmount;

			var currentBalance = await bank.GetCurrencyAmount(playerId);
			var newBalance = currentBalance + netAmount;

			await bank.SaveCurrencyAmount(playerId, newBalance);
			await bank.RecordTransaction(playerName, "Received from transaction", amount);
			await bank.RecordTaxTransaction(taxAmount);
		}
		
		public static async Task HandleSwitchTransaction(int switchX, int switchY, int playerID)
		{
			// Retrieve the shop details from the database based on the switch coordinates
			var shop = await bank.GetShopFromDatabase(switchX, switchY);
			if (shop != null)
			{
				var itemID = shop.Item;
				var stackSize = shop.Stack;
				var shopPrice = shop.Price;

				// Retrieve the player's currency amount from the database
				var currencyAmount = await bank.GetCurrencyAmount(playerID);

				// Check if the player has enough currency to make the purchase
				if (currencyAmount >= shopPrice)
				{
					var player = TShock.Players.FirstOrDefault(p => p != null && p.Account != null && p.Account.ID.Equals(playerID));
					// Perform the transaction
					currencyAmount -= shopPrice;
					await bank.SaveCurrencyAmount(playerID, currencyAmount);

					// Give the player the item stacks
					player.GiveItem(itemID, stackSize);

					// Record the transaction
					await bank.RecordTransaction(TShock.Players[playerID].Name, Transaction.PurchasedFromShop, shopPrice);

					TShock.Players[playerID].SendSuccessMessage("Purchase successful.");
				}
				else
				{
					TShock.Players[playerID].SendErrorMessage("Insufficient funds to make the purchase.");
				}
			}
		}
	}
}
