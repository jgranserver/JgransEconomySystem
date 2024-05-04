using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;

namespace JgransEconomySystem
{
	public static class Transaction
	{
		private static readonly string path = Path.Combine(TShock.SavePath, "JgransEconomyBanks.sqlite");
		private static readonly string transaction = Path.Combine(TShock.SavePath, "EconomyTransactions.sqlite");

		private static readonly EconomyDatabase bank = new(path, transaction);
		static readonly JgransEconomySystemConfig config = new();
		private static Dictionary<int, DateTime> cooldowns = new();


		private static double TaxRate => config.TaxRate.Value;

		public const string ReceivedFromKillingNormalNPC = "Received from killing normal NPC";
		public const string ReceivedFromKillingSpecialNPC = "Received from killing special NPC";
		public const string ReceivedFromKillingHostileNPC = "Received from killing hostile NPC";
		public const string ReceivedFromPayment = "Received from payment";
		public const string ReceivedFromKillingBossNPC = "Received from killing boss NPC";
		public const string ReceivedFromVoting = "Received from voting the server";
		public const string PurchasedFromShop = "Bought an item from shop";
		public const string SoldItemToShop = "Sold an item from shop";



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
			await bank?.RecordTransaction(playerName, "Received from transaction", amount);
			await bank.RecordTaxTransaction(taxAmount);
		}

		public static async Task HandleSwitchTransaction(int switchX, int switchY, int playerID, bool itemShop, bool sellCommand)
		{
			// Check if the player is on cooldown
			if (cooldowns.TryGetValue(playerID, out DateTime lastExecutionTime) && (itemShop == true || sellCommand == true))
			{
				TimeSpan cooldownDuration = TimeSpan.FromSeconds(5); // Adjust the cooldown duration as needed
				TimeSpan timeSinceLastExecution = DateTime.Now - lastExecutionTime;

				if (timeSinceLastExecution < cooldownDuration)
				{
					// Player is on cooldown, return or display a message
					var player = TShock.Players.FirstOrDefault(p => p?.Account?.ID == playerID);
					player?.SendErrorMessage("You are on cooldown. Please wait before performing another transaction.");
					return;
				}
			}

			// Retrieve the shop details from the database based on the switch coordinates
			if (itemShop)
			{
				var itemShops = await bank?.GetShopFromDatabase(switchX, switchY, true, false);
				var player = TShock.Players.FirstOrDefault(p => p?.Account?.ID == playerID);

				if (itemShops != null && player != null)
				{
					var allowedGroups = itemShops.AllowedGroup.Split(',');

					if (!allowedGroups.Contains(player.Group?.Name))
					{
						player.SendErrorMessage("Your rank is not allowed to purchase this item.");
						player.SendErrorMessage("Allowed ranks: " + string.Join(", ", allowedGroups));
						return;
					}

					var itemID = itemShops.Item;
					var stackSize = itemShops.Stack;
					var shopPrice = itemShops.Price;

					var item = TShock.Utils.GetItemById(itemID);

					// Retrieve the player's currency amount from the database
					var currencyAmount = await bank?.GetCurrencyAmount(playerID);

					// Check if the player has enough currency to make the purchase
					if (currencyAmount >= shopPrice)
					{
						var taxAmount = CalculateTax(shopPrice);
						var newBalance = currencyAmount - shopPrice - taxAmount;

						// Check if the resulting currency amount would be negative
						if (newBalance >= 0)
						{
							// Perform the transaction
							await bank?.SaveCurrencyAmount(playerID, newBalance);

							// Give the player the item stacks
							player.GiveItem(itemID, stackSize);

							// Record the transaction
							await bank?.RecordTransaction(player.Name, Transaction.PurchasedFromShop, shopPrice + taxAmount);
							await bank?.RecordTaxTransaction(shopPrice + taxAmount);

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
				CleanupCooldowns();
			}

			if (sellCommand)
			{
				var commandShops = await bank?.GetShopFromDatabase(switchX, switchY, false, true);
				var player = TShock.Players.FirstOrDefault(p => p?.Account?.ID == playerID);

				if (commandShops != null && player != null)
				{
					var allowedGroups = commandShops.AllowedGroup.Split(',');

					if (!allowedGroups.Contains(player.Group?.Name))
					{
						player.SendErrorMessage("Your rank is not allowed to purchase this item.");
						player.SendErrorMessage("Allowed ranks: " + string.Join(", ", allowedGroups));
						return;
					}

					var command = commandShops.Command;
					var price = commandShops.Price;

					var currencyAmount = await bank?.GetCurrencyAmount(playerID);

					if (currencyAmount >= price)
					{
						var taxAmount = CalculateTax(price);
						var newBalance = currencyAmount - price - taxAmount;

						// Check if the resulting currency amount would be negative
						if (newBalance >= 0)
						{
							// Perform the transaction
							await bank?.SaveCurrencyAmount(playerID, newBalance);

							var originalGroup = player.Group?.ToString();

							// Temporarily set the player's group to a group with the necessary permissions
							TShock.UserAccounts.SetUserGroup(player.Account, "superadmin");

							try
							{
								// Execute the command
								bool commandExecuted = Commands.HandleCommand(player, command);

								if (command.StartsWith("/") || command.StartsWith("."))
								{
									string commandName = command.Substring(1).Split(' ')[0];

									if (commandName.Equals("spawnboss", StringComparison.OrdinalIgnoreCase) || commandName.Equals("sb", StringComparison.OrdinalIgnoreCase))
									{
										// The "/spawnboss" command was executed by a player

										// Check if the command was successfully executed
										if (commandExecuted)
										{
											// The command was executed successfully
											JgransEconomySystem.spawned = true;
											// Perform your desired actions
											TSPlayer.All.SendInfoMessage("Reward Counter Set to 1");
										}
										else
										{
											// The command execution failed

											// Perform any necessary error handling or notification
										}
									}
								}
							}
							finally
							{
								// Restore the player's original group
								TShock.UserAccounts.SetUserGroup(player.Account, originalGroup);
							}

							// Record the transaction
							await bank?.RecordTransaction(player.Name, Transaction.PurchasedFromShop, price + taxAmount);
							await bank?.RecordTaxTransaction(price + taxAmount);

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
				CleanupCooldowns();
			}
			else
			{
				// Shop not found or player not found or not initialized correctly
				TShock.Log.Error("Shop not found or player not found or not initialized correctly for switch transaction.");
			}

			// Update the last execution time for the player
			cooldowns[playerID] = DateTime.Now;
		}

		public static async void HandleItemSelling(TSPlayer player, Chest chest)
		{
			// Check if the player's active chest is valid
			if (player.ActiveChest == -1 || player.ActiveChest > Main.chest.Length - 1)
				return;

			// Get the selected item from the chest
			Item selectedItem = chest.item[player.SelectedItem.netID];

			// Check if the selected item exists and is valid for selling
			if (selectedItem == null || selectedItem.IsAir)
				return;

			// Calculate the price payment based on item rarity
			int price = GetItemPrice(selectedItem);
			var reason = Transaction.SoldItemToShop;

			// Update the player's currency
			// Replace "bank" with your currency management system
			int balance = await bank.GetCurrencyAmount(player.Account.ID);
			int newBalance = balance + price;
			await bank.RecordTransaction(player.Name, reason, price);
			await bank.SaveCurrencyAmount(player.Account.ID, newBalance);

			// Remove the sold item from the chest
			chest.item[player.SelectedItem.netID].SetDefaults();

			// Inform the player about the transaction
			player.SendSuccessMessage($"You sold {selectedItem.Name} for {price} currency.");
		}


		private static int GetItemPrice(Item soldItem)
		{

            return soldItem.rare switch
            {
                // White
                0 => 10,
                // Green
                1 => 20,
                // Blue
                2 => 30,
                // Orange
                3 => 40,
                // LightRed
                4 => 50,
                // Pink
                5 => 60,
                // LightPurple
                6 => 70,
                // Lime
                7 => 10,
                // Yellow
                8 => 20,
                // Cyan
                9 => 30,
                // Red
                10 => 40,
                // Rainbow
                11 => 50,
                _ => 0,
            };
        }

		private static void CleanupCooldowns()
		{
			TimeSpan cooldownDuration = TimeSpan.FromSeconds(5); // Adjust the cooldown duration as needed
			DateTime expirationTime = DateTime.Now - cooldownDuration;

			// Remove expired cooldown entries
			cooldowns = cooldowns.Where(kvp => kvp.Value > expirationTime).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
		}
	}
}
