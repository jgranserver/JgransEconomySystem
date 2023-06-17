using System;
using System.Collections.Generic;
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
		
		private static int CalculateTax(int amount, double taxRate)
		{
			var taxAmount = (int)Math.Ceiling(amount * taxRate);
			return taxAmount;
		}

		public static void ProcessTransaction(int playerId, string playerName, int amount, double taxRate)
		{
			var taxAmount = CalculateTax(amount, taxRate);
			var netAmount = amount - taxAmount;

			var currentBalance = bank.GetCurrencyAmount(playerId);
			var newBalance = currentBalance + netAmount;

			bank.SaveCurrencyAmount(playerId, newBalance);
			bank.RecordTransaction(playerName, "Received from transaction", amount);
			bank.RecordTaxTransaction(taxAmount);
		}
	}
}