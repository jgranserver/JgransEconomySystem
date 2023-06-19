using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using TShockAPI;

namespace JgransEconomySystem
{
    public static class Transaction
    {
        private static string path = Path.Combine(TShock.SavePath, "JgransEconomyBanks.sqlite");
        private static EconomyDatabase bank = new EconomyDatabase(path);

        private static double TaxRate => 0.2;

        public const string ReceivedFromKillingNormalNPC = "Received from killing normal NPC";
        public const string ReceivedFromKillingSpecialNPC = "Received from killing special NPC";
        public const string ReceivedFromKillingHostileNPC = "Received from killing hostile NPC";
        public const string ReceivedFromPayment = "Received from payment";
        public const string ReceivedFromKillingBossNPC = "Received from killing boss NPC";
        public const string ReceivedFromVoting = "Received from voting the server";
        public const string PurchasedFromShop = "Bought an item from shop";


        private static int CalculateTax(int amount)
        {
            var taxAmount = (int)Math.Ceiling(amount * TaxRate);
            return taxAmount;
        }

        public static async Task ProcessTransaction(int playerId, string playerName, int amount)
        {
            var taxAmount = CalculateTax(amount);
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
            var player = TShock.Players.FirstOrDefault(p => p?.Account?.ID == playerID);

            if (shop != null && player != null)
            {
                var allowedGroups = shop.AllowedGroup.Split(',');

                if (!allowedGroups.Contains(player.Group.Name))
                {
                    player.SendErrorMessage("Your rank is not allowed to purchase this item.");
                    player.SendErrorMessage("Allowed ranks: " + string.Join(", ", allowedGroups));
                    return;
                }

                var itemID = shop.Item;
                var stackSize = shop.Stack;
                var shopPrice = shop.Price;

                var item = TShock.Utils.GetItemById(itemID);

                // Retrieve the player's currency amount from the database
                var currencyAmount = await bank.GetCurrencyAmount(playerID);

                // Check if the player has enough currency to make the purchase
                if (currencyAmount >= shopPrice)
                {
                    var taxAmount = CalculateTax(shopPrice);
                    var newBalance = currencyAmount - shopPrice - taxAmount;

                    // Check if the resulting currency amount would be negative
                    if (newBalance >= 0)
                    {
                        // Perform the transaction
                        await bank.SaveCurrencyAmount(playerID, newBalance);

                        // Give the player the item stacks
                        player.GiveItem(itemID, stackSize);

                        // Record the transaction
                        await bank.RecordTransaction(player.Name, Transaction.PurchasedFromShop, shopPrice + taxAmount);
                        await bank.RecordTaxTransaction(shopPrice + taxAmount);

                        player.SendSuccessMessage($"Successfully purchased {item.Name} x {stackSize} for {shopPrice + taxAmount}.");
                        player.SendSuccessMessage("Tax transaction applied: 20% to maintain economy balance.");
                        player.SendSuccessMessage($"Updated Balance: {newBalance}");
                    }
                    else
                    {
                        player.SendErrorMessage("Insufficient funds to make the purchase.");
                        player.SendMessage($"Balance: {currencyAmount}", Color.LightBlue);
                    }
                }
                else
                {
                    player.SendErrorMessage("Insufficient funds to make the purchase.");
                    player.SendMessage($"Balance: {currencyAmount}", Color.LightBlue);
                }
            }
            else
            {
                // Shop not found or player not found or not initialized correctly
                TShock.Log.Error("Shop not found or player not found or not initialized correctly for switch transaction.");
            }
        }
    }
}
