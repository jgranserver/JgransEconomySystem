using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace JgransEconomySystem
{
    [ApiVersion(2, 1)]
    public class JgransEconomySystem : TerrariaPlugin
    {
        private EconomyDatabase bank;
        private JgransEconomySystemConfig config;
        private string path = Path.Combine(TShock.SavePath, "JgransEconomyBanks.sqlite");
        private string configPath = Path.Combine(TShock.SavePath, "JgransEconomySystemConfig.json");
        public static bool spawned = false;
        private Dictionary<int, DateTime> lastNpcStrikeTime = new Dictionary<int, DateTime>();
        private Timer weekendBonusTimer;
        private bool isWeekendBonus = false;
        private Timer leaderboardTimer;
        private Dictionary<int, PaymentConfirmation> pendingPayments =
            new Dictionary<int, PaymentConfirmation>();

        private class PaymentConfirmation
        {
            public List<UserAccount> MatchingAccounts { get; set; }
            public int Amount { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public JgransEconomySystem(Main game)
            : base(game) { }

        public override string Name => "JgransEconomySystem";

        public override Version Version => new Version(5, 6, 3);

        public override string Author => "jgranserver";

        public override string Description => "Economy system.";

        public override async void Initialize()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    config = new JgransEconomySystemConfig();
                    config.Write(configPath);
                }
                else
                {
                    config = JgransEconomySystemConfig.Read(configPath);
                }

                bank = new EconomyDatabase(path);
                Rank.Initialize(config);

                ServerApi.Hooks.NetSendData.Register(this, EconomyAsync);
                ServerApi.Hooks.NetGetData.Register(this, OnNetGetData);
                ServerApi.Hooks.ServerChat.Register(this, OnServerChat);
                ServerApi.Hooks.ServerJoin.Register(this, OnPlayerJoin);
                GetDataHandlers.TileEdit += OnTileEdit;
                ServerApi.Hooks.GamePostInitialize.Register(this, OnWorldLoad);
                ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

                Commands.ChatCommands.Add(
                    new Command("jgraneconomy.system", EconomyCommandsAsync, "bank")
                );
                Commands.ChatCommands.Add(
                    new Command("jgraneconomy.admin", ReloadConfigCommand, "economyreload", "er")
                );
                Commands.ChatCommands.Add(
                    new Command("jgraneconomy.system", LeaderboardCommandAsync, "leaderboard")
                );
                Commands.ChatCommands.Add(
                    new Command("jgranserver.admin", InitializeWorldCommand, "initworld")
                );

                await Transaction.InitializeTransactionDataAsync();
                InitializeWeekendBonus();

                TShock.Log.Info("JgransEconomySystem initialized successfully");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Failed to initialize JgransEconomySystem: {ex.Message}");
                TShock.Log.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        private async void EconomyCommandsAsync(CommandArgs args)
        {
            await EconomyCommands(args);
        }

        private async void EconomyAsync(SendDataEventArgs args)
        {
            await Economy(args);
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
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);

                GetDataHandlers.TileEdit -= OnTileEdit;
                leaderboardTimer?.Dispose();
                weekendBonusTimer?.Dispose();
                Transaction.batchProcessingTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ReloadConfigCommand(CommandArgs args)
        {
            string json = File.ReadAllText(configPath);
            JgransEconomySystemConfig newConfig =
                JsonConvert.DeserializeObject<JgransEconomySystemConfig>(json);
            config = newConfig;

            TShock.Log.ConsoleInfo("JgransEconomySystemConfig has been reloaded.");
        }

        private async Task Economy(SendDataEventArgs args)
        {
            if (!config.ToggleEconomy.Value)
                return;

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

            if (npc.SpawnedFromStatue)
                return;

            bool isHardmode = Main.hardMode;

            if (data != PacketTypes.NpcStrike || npc == null || npc.life > 0)
                return;

            // Anti-farming check
            if (lastNpcStrikeTime.ContainsKey(player.Index))
            {
                var lastStrike = lastNpcStrikeTime[player.Index];
                if ((DateTime.Now - lastStrike).TotalMilliseconds < 500)
                    return;
            }
            lastNpcStrikeTime[player.Index] = DateTime.Now;

            // Calculate drop chance and currency
            double dropChance = CalculateDropChance(npc, isHardmode);
            double roll = Main.rand.NextDouble() * 100;
            int currencyAmount = 0;
            string reason = "";

            if (roll <= dropChance)
            {
                currencyAmount = CalculateCurrencyAmount(npc);
                reason = GetDropReason(npc);

                if (IsBossNPC(npc) && spawned)
                {
                    TSPlayer.All.SendMessage(
                        $"{player.Name} received {currencyAmount:N0} {config.CurrencyName.Value} from killing {npc.TypeName}!",
                        Color.LightCyan
                    );
                    spawned = false;
                }
            }

            if (currencyAmount > 0)
            {
                try
                {
                    // Apply hardmode multiplier
                    if (isHardmode)
                        currencyAmount = (int)(currencyAmount * 1.2);

                    // Apply rank multiplier
                    double rankMultiplier = GetRankMultiplier(player.Group.Name);
                    bool isRegularRank = rankMultiplier == 1.0; // Regular ranks have no multiplier
                    int originalAmount = currencyAmount;
                    currencyAmount = (int)(currencyAmount * rankMultiplier);

                    // Show rank multiplier notification if applicable
                    if (rankMultiplier > 1)
                    {
                        Vector2 multiplierPosition = player.TPlayer.Center + new Vector2(0, -120);
                        player.SendData(
                            PacketTypes.CreateCombatTextExtended,
                            $"x{rankMultiplier:F1} Rank Bonus!",
                            (int)Color.Gold.PackedValue,
                            multiplierPosition.X,
                            multiplierPosition.Y
                        );
                    }

                    // Apply weekend bonus only for regular ranks
                    if (isWeekendBonus && config.WeekendBonusEnabled.Value && isRegularRank)
                    {
                        int preWeekendBonus = currencyAmount;
                        currencyAmount = (int)(
                            currencyAmount * config.WeekendBonusMultiplier.Value
                        );

                        // Show weekend bonus notification
                        Vector2 bonusPosition = player.TPlayer.Center + new Vector2(0, -140);
                        player.SendData(
                            PacketTypes.CreateCombatTextExtended,
                            $"x{config.WeekendBonusMultiplier.Value:F1} Weekend Bonus!",
                            (int)Color.LightGreen.PackedValue,
                            bonusPosition.X,
                            bonusPosition.Y
                        );
                    }

                    // Update player's balance
                    int balance = await bank.GetCurrencyAmount(player.Account.ID);
                    int newBalance = balance + currencyAmount;
                    await bank.UpdateCurrencyAmount(player.Account.ID, newBalance);
                    Transaction.QueueTransaction(player.Name, reason, currencyAmount);

                    // Display currency gain
                    Vector2 displayPosition = player.TPlayer.Center + new Vector2(0, -100);
                    player.SendData(
                        PacketTypes.CreateCombatTextExtended,
                        $"{currencyAmount:N0} {config.CurrencyName.Value}",
                        (int)Color.LightBlue.PackedValue,
                        displayPosition.X,
                        displayPosition.Y + 1
                    );

                    // Show particle effect
                    ParticleOrchestraSettings settings = new ParticleOrchestraSettings
                    {
                        IndexOfPlayerWhoInvokedThis = (byte)player.Index,
                        PositionInWorld = displayPosition,
                        MovementVector = Vector2.Zero,
                        UniqueInfoPiece = ItemID.LuckyCoin,
                    };

                    ParticleOrchestrator.BroadcastParticleSpawn(
                        ParticleOrchestraType.ItemTransfer,
                        settings
                    );
                }
                catch (Exception ex)
                {
                    TShock.Log.Error($"Error updating currency in Economy: {ex.Message}");
                }
            }
        }

        private double CalculateHostileNPCChance(NPC npc)
        {
            return Math.Min(
                35.0
                    + (double)npc.lifeMax / 100.0 * 0.5
                    + (double)npc.damage / 20.0 * 0.5
                    + (double)npc.defense / 10.0 * 0.5
                    + (double)npc.value / 100.0 * 0.2,
                75.0
            );
        }

        private double CalculateDropChance(NPC npc, bool isHardmode)
        {
            // Calculate base chance based on NPC type
            double baseChance = npc switch
            {
                var n when NPCType.IsBoss3(n.netID) => 100.0, // Tier 3 bosses
                var n when NPCType.IsBoss2(n.netID) => 100.0, // Tier 2 bosses
                var n when NPCType.IsBoss1(n.netID) => 100.0, // Tier 1 bosses
                var n when NPCType.IsSpecial(n.netID) => 65.0, // Special NPCs
                var n when NPCType.IsHostile(n.netID) => CalculateHostileNPCChance(npc),
                _ => 20.0, // Normal NPCs
            };

            // Apply multipliers based on conditions
            double finalChance = baseChance;

            // Hardmode multiplier
            if (isHardmode)
                finalChance *= 1.2; // 20% increase in hardmode

            // Night time bonus
            if (!Main.dayTime)
                finalChance *= 1.15; // 15% increase at night

            // Underground bonus
            if (npc.position.Y > Main.worldSurface * 16.0)
                finalChance *= 1.1; // 10% increase underground

            // Cap at 100%
            return Math.Min(finalChance, 100.0);
        }

        private int CalculateCurrencyAmount(NPC npc)
        {
            int baseAmount = npc switch
            {
                var n when NPCType.IsBoss3(n.netID) => Main.rand.Next(
                    config.Boss3MaxAmount.Value / 2,
                    config.Boss3MaxAmount.Value
                ),
                var n when NPCType.IsBoss2(n.netID) => Main.rand.Next(
                    config.Boss2MaxAmount.Value / 2,
                    config.Boss2MaxAmount.Value
                ),
                var n when NPCType.IsBoss1(n.netID) => Main.rand.Next(
                    config.Boss1MaxAmount.Value / 2,
                    config.Boss1MaxAmount.Value
                ),
                var n when NPCType.IsSpecial(n.netID) => Main.rand.Next(
                    config.SpecialMaxAmount.Value / 2,
                    config.SpecialMaxAmount.Value
                ),
                var n when NPCType.IsHostile(n.netID) => Main.rand.Next(
                    config.HostileMaxAmount.Value / 2,
                    config.HostileMaxAmount.Value
                ),
                _ => Main.rand.Next(config.NormalMaxAmount.Value / 2, config.NormalMaxAmount.Value),
            };

            return baseAmount;
        }

        private string GetDropReason(NPC npc)
        {
            return npc switch
            {
                var n
                    when NPCType.IsBoss3(n.netID)
                        || NPCType.IsBoss2(n.netID)
                        || NPCType.IsBoss1(n.netID) => Transaction.ReceivedFromKillingBossNPC,
                var n when NPCType.IsSpecial(n.netID) => Transaction.ReceivedFromKillingSpecialNPC,
                var n when NPCType.IsHostile(n.netID) => Transaction.ReceivedFromKillingHostileNPC,
                _ => Transaction.ReceivedFromKillingNormalNPC,
            };
        }

        private bool IsBossNPC(NPC npc)
        {
            return NPCType.IsBoss1(npc.netID)
                || NPCType.IsBoss2(npc.netID)
                || NPCType.IsBoss3(npc.netID);
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
                TSPlayer.All.SendInfoMessage(
                    "Boss Spawned! The one who gets the last hit gets the jspoints!"
                );
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
                player.SendMessage(
                    "/bank check <player> - Check player's balance",
                    Color.LightBlue
                );
                player.SendMessage(
                    "/bank give <player> <amount> - Give currency to player",
                    Color.LightBlue
                );
                player.SendMessage(
                    "/bank giveall <amount> - Give currency to all players",
                    Color.LightBlue
                );
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
                player.SendMessage(
                    $"Bank Balance: [c/#00FF6E:{bal:N0}] {config.CurrencyName.Value}/s",
                    Color.LightBlue
                );
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
                player.SendErrorMessage(
                    "You don't have permission to check other players' bank accounts."
                );
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
                TShock.Log.Debug(
                    $"Checking balance for player ID: {target.ID}, Name: {target.Name}"
                );

                bool exists = await bank.PlayerAccountExists(target.ID);
                TShock.Log.Debug($"Account exists check result: {exists}");

                if (!exists)
                {
                    player.SendErrorMessage("Player does not have a bank account.");
                    return;
                }

                int targetBal = await bank.GetCurrencyAmount(target.ID);
                TShock.Log.Debug($"Retrieved balance: {targetBal}");

                player.SendMessage(
                    $"{target.Name}'s Balance: {targetBal:N0} {config.CurrencyName.Value}/s",
                    Color.LightBlue
                );
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error in HandleCheckCommand: {ex.Message}");
                player.SendErrorMessage("An error occurred while checking the player's balance.");
            }
        }

        private async Task HandlePayCommand(TSPlayer player, List<string> cmd)
        {
            try
            {
                // Check basic command format
                if (cmd.Count < 3 || !int.TryParse(cmd[2], out int payment) || payment <= 0)
                {
                    player.SendErrorMessage("Usage: /bank pay <playername> <amount>");
                    return;
                }

                // If player has pending confirmation, handle selection
                if (pendingPayments.TryGetValue(player.Index, out var pending))
                {
                    if ((DateTime.Now - pending.CreatedAt).TotalMinutes > 1)
                    {
                        pendingPayments.Remove(player.Index);
                        player.SendErrorMessage("Payment selection expired. Please try again.");
                        return;
                    }

                    if (
                        int.TryParse(cmd[1], out int selection)
                        && selection > 0
                        && selection <= pending.MatchingAccounts.Count
                    )
                    {
                        var selectedAccount = pending.MatchingAccounts[selection - 1];
                        await ProcessPayment(player, selectedAccount, pending.Amount);
                        pendingPayments.Remove(player.Index);
                        return;
                    }
                    else
                    {
                        player.SendErrorMessage("Invalid selection. Payment cancelled.");
                        pendingPayments.Remove(player.Index);
                        return;
                    }
                }

                // Get target name pattern
                string namePattern = cmd[1];

                // Find matching accounts using regex
                var matchingAccounts = TShock
                    .UserAccounts.GetUserAccounts()
                    .Where(acc => Regex.IsMatch(acc.Name, namePattern, RegexOptions.IgnoreCase))
                    .ToList();

                // Handle no matches
                if (matchingAccounts.Count == 0)
                {
                    player.SendErrorMessage($"No players found matching pattern: {namePattern}");
                    return;
                }

                // Handle single match
                if (matchingAccounts.Count == 1)
                {
                    await ProcessPayment(player, matchingAccounts[0], payment);
                    return;
                }

                // Handle multiple matches
                var sb = new StringBuilder();
                sb.AppendLine("Multiple matches found. Please select a player by number:");
                for (int i = 0; i < matchingAccounts.Count; i++)
                {
                    sb.AppendLine($"{i + 1}. {matchingAccounts[i].Name}");
                }
                sb.AppendLine("Type '/bank pay <number> <amount>' to confirm payment.");

                player.SendInfoMessage(sb.ToString());
                pendingPayments[player.Index] = new PaymentConfirmation
                {
                    MatchingAccounts = matchingAccounts,
                    Amount = payment,
                    CreatedAt = DateTime.Now,
                };
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error in HandlePayCommand: {ex.Message}");
                player.SendErrorMessage("An error occurred while processing the payment.");
            }
        }

        private async Task ProcessPayment(TSPlayer sender, UserAccount receiver, int amount)
        {
            try
            {
                // Check if receiver is online
                var receiverPlayer = TShock.Players.FirstOrDefault(p =>
                    p?.Account?.ID == receiver.ID
                );
                if (receiverPlayer == null)
                {
                    sender.SendErrorMessage($"Player {receiver.Name} is not online.");
                    return;
                }

                // Check sender's balance
                int senderBalance = await bank.GetCurrencyAmount(sender.Account.ID);
                if (senderBalance < amount)
                {
                    sender.SendErrorMessage(
                        $"Insufficient funds. Your balance: {senderBalance:N0} {config.CurrencyName.Value}"
                    );
                    return;
                }

                // Process the payment
                int receiverBalance = await bank.GetCurrencyAmount(receiver.ID);
                await bank.UpdateCurrencyAmount(sender.Account.ID, senderBalance - amount);
                await bank.UpdateCurrencyAmount(receiver.ID, receiverBalance + amount);

                // Record transaction
                await Transaction.RecordTransaction(
                    receiver.Name,
                    Transaction.ReceivedFromPayment + sender.Name,
                    amount
                );

                // Notify both parties
                sender.SendSuccessMessage(
                    $"Paid {amount:N0} {config.CurrencyName.Value} to {receiver.Name}"
                );
                receiverPlayer.SendSuccessMessage(
                    $"Received {amount:N0} {config.CurrencyName.Value} from {sender.Name}"
                );

                // Log the transaction
                TShock.Log.Info(
                    $"Payment: {sender.Name} -> {receiver.Name}: {amount:N0} {config.CurrencyName.Value}"
                );
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error in ProcessPayment: {ex.Message}");
                sender.SendErrorMessage("An error occurred while processing the payment.");
            }
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
                // Get server bank balance
                int serverBalance = await bank.GetCurrencyAmount(0); // 0 is ServerBankId

                // Check if server has enough funds
                if (serverBalance < amount)
                {
                    player.SendErrorMessage(
                        $"Insufficient server bank funds. Server Balance: {serverBalance:N0} {config.CurrencyName.Value}"
                    );
                    return;
                }

                // Check if target account exists and create it if it doesn't
                if (!await bank.PlayerAccountExists(target.ID))
                {
                    await bank.UpdateCurrencyAmount(target.ID, 0);
                }

                // Start transaction
                var targetBalance = await bank.GetCurrencyAmount(target.ID);
                await bank.UpdateCurrencyAmount(target.ID, targetBalance + amount); // Give to player
                await bank.UpdateCurrencyAmount(0, serverBalance - amount); // Deduct from server bank

                // Record transactions
                await Transaction.RecordTransaction(
                    target.Name,
                    $"Given by admin {player.Account.Name}",
                    amount
                );
                await Transaction.RecordTransaction(
                    "ServerBank",
                    $"Given to {target.Name} by admin {player.Account.Name}",
                    -amount
                );

                // Send notifications
                player.SendSuccessMessage(
                    $"Added {amount:N0} {config.CurrencyName.Value} to {target.Name}'s account."
                );
                player.SendInfoMessage(
                    $"Server bank new balance: {serverBalance - amount:N0} {config.CurrencyName.Value}"
                );

                // Notify target if online
                var targetPlayer = TShock.Players.FirstOrDefault(p => p?.Account?.ID == target.ID);
                if (targetPlayer != null)
                {
                    targetPlayer.SendSuccessMessage(
                        $"Received {amount:N0} {config.CurrencyName.Value} from server bank."
                    );
                }

                // Log the transaction
                TShock.Log.Info(
                    $"Admin {player.Name} gave {amount:N0} {config.CurrencyName.Value} to {target.Name} from server bank."
                );
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
                player.SendErrorMessage(
                    "You don't have permission to give currency to all players."
                );
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

                    var onlinePlayer = TShock.Players.FirstOrDefault(p =>
                        p?.Account?.ID == account.ID
                    );
                    onlinePlayer?.SendSuccessMessage(
                        $"Received {amount:N0} {config.CurrencyName.Value}/s."
                    );
                }
            }

            player.SendSuccessMessage(
                $"Added {amount:N0} {config.CurrencyName.Value}/s to all player accounts."
            );
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
                TSPlayer.All.SendInfoMessage(
                    "Boss Spawned! The one who gets the last hit gets the jspoints!"
                );
            }
        }

        private async void OnServerChat(ServerChatEventArgs args)
        {
            if (!args.Text.StartsWith("/") && !args.Text.StartsWith("."))
                return;

            var player = TShock.Players[args.Who];
            if (player == null || !player.IsLoggedIn)
                return;

            string message = args.Text;
            string[] cmdParts = message.Substring(1).Split(' ');
            string command = cmdParts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "spawnboss":
                    case "sb":
                        await HandleBossSpawnCommand(player, message);
                        break;

                    case "gbuff":
                        await HandleBuffCommand(player, cmdParts);
                        break;

                    case "warp":
                        await HandleWarpCommand(player, cmdParts, message);
                        break;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error processing {command} command: {ex.Message}");
                TShock.Log.Debug($"Stack trace: {ex.StackTrace}");
                player.SendErrorMessage("An error occurred while processing the command.");
            }
        }

        private async Task HandleBossSpawnCommand(TSPlayer player, string message)
        {
            if (!player.Group.HasPermission("tshock.npc.summonboss"))
            {
                player.SendErrorMessage("You don't have permission to spawn bosses.");
                return;
            }

            if (Commands.HandleCommand(TSPlayer.Server, message))
            {
                spawned = true;
                TSPlayer.All.SendInfoMessage(
                    "Boss Spawned! The one who gets the last hit gets the jspoints!"
                );
            }
        }

        private async Task HandleBuffCommand(TSPlayer player, string[] cmdParts)
        {
            if (cmdParts.Length < 3)
            {
                player.SendErrorMessage("Usage: /gbuff <playername> <buffid/buffname> [duration]");
                player.SendInfoMessage(
                    "Note: Duration is optional (default: 5000 points). Negative duration costs 10000 points for permanent buff"
                );
                return;
            }

            string targetName = cmdParts[1];
            string buffInput = cmdParts[2];
            int duration = 0;
            int cost = 5000;

            if (cmdParts.Length >= 4 && !int.TryParse(cmdParts[3], out duration))
            {
                player.SendErrorMessage("Duration must be a valid number.");
                return;
            }

            cost = duration < 0 ? 10000 : (duration == 0 ? 5000 : duration);
            bool canBypassCost = player.HasPermission("jgraneconomy.bypassbuffcost");

            if (!canBypassCost)
            {
                int balance = await bank.GetCurrencyAmount(player.Account.ID);
                if (balance < cost)
                {
                    player.SendErrorMessage(
                        $"Insufficient funds. Cost: {cost:N0} {config.CurrencyName.Value}, Your balance: {balance:N0} {config.CurrencyName.Value}"
                    );
                    return;
                }
                player.SendInfoMessage($"Buff will cost {cost:N0} {config.CurrencyName.Value}");
            }

            if (Commands.HandleCommand(player, string.Join(" ", cmdParts)))
            {
                if (!canBypassCost)
                {
                    int balance = await bank.GetCurrencyAmount(player.Account.ID);
                    await bank.UpdateCurrencyAmount(player.Account.ID, balance - cost);

                    string durationText = duration switch
                    {
                        < 0 => "permanent",
                        0 => "default",
                        _ => $"{duration}s",
                    };

                    await Transaction.RecordTransaction(
                        player.Name,
                        $"Buff command ({buffInput} for {durationText}) on {targetName}",
                        -cost
                    );
                    player.SendSuccessMessage(
                        $"Paid {cost:N0} {config.CurrencyName.Value} for buff command"
                    );
                    player.SendInfoMessage(
                        $"Your new balance: {balance - cost:N0} {config.CurrencyName.Value}"
                    );
                }
                else
                {
                    player.SendSuccessMessage(
                        $"Buff command executed (Cost bypassed: {cost:N0} {config.CurrencyName.Value})"
                    );
                }
            }
            else
            {
                player.SendErrorMessage("Failed to execute buff command.");
            }
        }

        private async Task HandleWarpCommand(TSPlayer player, string[] cmdParts, string message)
        {
            if (cmdParts.Length < 2)
            {
                player.SendErrorMessage("Usage: /warp <warpname> or /warp add <name>");
                return;
            }

            string subCommand = cmdParts[1].ToLower();
            bool canBypassCost = player.HasPermission("jgraneconomy.bypasswarpcost");

            switch (subCommand)
            {
                case "list":
                    Commands.HandleCommand(player, message);
                    return;

                case "add":
                    await HandleWarpAddCommand(player, cmdParts, message, canBypassCost);
                    break;

                default:
                    await HandleWarpTeleportCommand(player, cmdParts, message, canBypassCost);
                    break;
            }
        }

        private async Task HandleWarpAddCommand(
            TSPlayer player,
            string[] cmdParts,
            string message,
            bool canBypassCost
        )
        {
            const int WARP_CREATE_COST = 50000;

            if (!canBypassCost)
            {
                int balance = await bank.GetCurrencyAmount(player.Account.ID);
                if (balance < WARP_CREATE_COST)
                {
                    player.SendErrorMessage(
                        $"Insufficient funds. Creating a warp costs {WARP_CREATE_COST:N0} {config.CurrencyName.Value}"
                    );
                    return;
                }
            }

            if (Commands.HandleCommand(player, message))
            {
                if (!canBypassCost)
                {
                    int balance = await bank.GetCurrencyAmount(player.Account.ID);
                    await bank.UpdateCurrencyAmount(player.Account.ID, balance - WARP_CREATE_COST);
                    await Transaction.RecordTransaction(
                        player.Name,
                        $"Created warp: {cmdParts[2]}",
                        -WARP_CREATE_COST
                    );
                    player.SendSuccessMessage(
                        $"Paid {WARP_CREATE_COST:N0} {config.CurrencyName.Value} to create warp point."
                    );
                }
            }
        }

        private async Task HandleWarpTeleportCommand(
            TSPlayer player,
            string[] cmdParts,
            string message,
            bool canBypassCost
        )
        {
            const int WARP_COST = 5000;

            if (!canBypassCost)
            {
                int balance = await bank.GetCurrencyAmount(player.Account.ID);
                if (balance < WARP_COST)
                {
                    player.SendErrorMessage(
                        $"Insufficient funds. Cost: {WARP_COST:N0} {config.CurrencyName.Value}"
                    );
                    return;
                }
            }

            if (Commands.HandleCommand(player, message) && !canBypassCost)
            {
                int balance = await bank.GetCurrencyAmount(player.Account.ID);
                await bank.UpdateCurrencyAmount(player.Account.ID, balance - WARP_COST);
                await Transaction.RecordTransaction(
                    player.Name,
                    $"Warp to {cmdParts[1]}",
                    -WARP_COST
                );
                player.SendSuccessMessage(
                    $"Paid {WARP_COST:N0} {config.CurrencyName.Value} for warping."
                );
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
                    player.SendInfoMessage(
                        $"Your current bank balance is {balance:N0} {config.CurrencyName.Value}."
                    );
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error in OnPlayerJoin: {ex.Message}");
            }
        }

        private async void LeaderboardCommandAsync(CommandArgs args)
        {
            LeaderboardCheckUpdate();
        }

        private DateTime lastCheckTime = DateTime.MinValue;
        private const int CHECK_INTERVAL_SECONDS = 600;

        private async void OnGameUpdate(EventArgs args)
        {
            try
            {
                var now = DateTime.Now;

                // Only check every 10 minutes (600 seconds)
                if ((now - lastCheckTime).TotalSeconds >= CHECK_INTERVAL_SECONDS)
                {
                    lastCheckTime = now;

                    // Perform leaderboard update
                    await Rank.UpdateLeaderboardRanks();

                    // Update config with last update time
                    config.LastLeaderboardUpdate.Value = now;
                    config.Write(configPath);

                    // Calculate next update time
                    var nextUpdate = now.AddSeconds(CHECK_INTERVAL_SECONDS);

                    // Send notifications
                    TSPlayer.All.SendSuccessMessage("Leaderboard rankings have been updated!");
                    TSPlayer.All.SendInfoMessage(
                        $"Next update in {TimeSpan.FromSeconds(CHECK_INTERVAL_SECONDS).TotalMinutes:0} minutes"
                    );

                    // Display current leaderboard
                    LeaderboardCheckUpdate();

                    // Log update
                    TShock.Log.Info($"Leaderboard auto-updated at {now:yyyy-MM-dd HH:mm:ss}");
                    TShock.Log.Info($"Next update scheduled for: {nextUpdate:yyyy-MM-dd HH:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error in OnGameUpdate: {ex.Message}");
                TShock.Log.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        private async void LeaderboardCheckUpdate()
        {
            try
            {
                var leaderboardData = await bank.GetLatestLeaderboardData();
                var lastUpdate = config.LastLeaderboardUpdate.Value;
                var nextUpdate = lastUpdate.AddSeconds(CHECK_INTERVAL_SECONDS);

                var sb = new StringBuilder();
                sb.AppendLine($"=== Top {config.CurrencyName.Value} Leaderboard ===");

                if (leaderboardData.Count == 0)
                {
                    sb.AppendLine("No leaderboard data available.");
                    if (lastUpdate == DateTime.MinValue)
                    {
                        sb.AppendLine("First update scheduled.");
                    }
                }
                else
                {
                    // Show last update time from config
                    sb.AppendLine($"Last updated: {lastUpdate:yyyy-MM-dd HH:mm:ss}");

                    // Calculate and show time until next update
                    var timeUntilNext = nextUpdate - DateTime.Now;
                    if (timeUntilNext.TotalSeconds > 0)
                    {
                        sb.AppendLine(
                            $"Next update in: {timeUntilNext.Minutes:D2}m {timeUntilNext.Seconds:D2}s"
                        );
                    }
                    else
                    {
                        sb.AppendLine("Update pending...");
                    }
                    sb.AppendLine("----------------------------------------");

                    // Display leaderboard entries
                    foreach (var entry in leaderboardData)
                    {
                        string rankDisplay = GetLeaderboardRankDisplay(entry.Position);
                        string rankMultiplier =
                            GetRankMultiplier(entry.Position.ToString()) > 1
                                ? $" (x{GetRankMultiplier(entry.Position.ToString()):F1})"
                                : "";

                        sb.AppendLine(
                            $"{rankDisplay} {entry.PlayerName}: {entry.CurrencyAmount:N0} {config.CurrencyName.Value}{rankMultiplier}"
                        );
                    }
                }

                // Send to all online players
                foreach (var player in TShock.Players.Where(p => p?.Active == true))
                {
                    player.SendMessage(sb.ToString(), Color.LightGoldenrodYellow);
                }

                // Also log to console
                TShock.Log.ConsoleInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error in LeaderboardCheckUpdate: {ex.Message}");
                TShock.Log.ConsoleError("An error occurred while retrieving the leaderboard.");
                TSPlayer.All.SendErrorMessage(
                    "An error occurred while updating the leaderboard display."
                );
            }
        }

        private string GetLeaderboardRankDisplay(int position)
        {
            return position switch
            {
                1 => "[c/FFD700:#1]", // Gold
                2 => "[c/C0C0C0:#2]", // Silver
                3 => "[c/CD7F32:#3]", // Bronze
                _ => $"#{position}",
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

        private bool IsLeaderboardRank(string groupName)
        {
            // Define the logic to determine if a group is a leaderboard rank
            return groupName == config.Top1Rank.Value
                || groupName == config.Top2Rank.Value
                || groupName == config.Top3Rank.Value
                || groupName == config.Top4Rank.Value
                || groupName == config.Top56Rank.Value
                || groupName == config.Top78Rank.Value
                || groupName == config.Top910Rank.Value;
        }

        private async Task ResetRanksOnWorldChange()
        {
            try
            {
                TShock.Log.Info("World change detected. Starting rank reset process...");
                var resetRank = config.WorldResetRank.Value;
                var maxResetRank = config.MaximumRankUpRank.Value;
                var affectedPlayers = new List<string>();
                var ranks = await bank.GetRanks();

                foreach (var account in TShock.UserAccounts.GetUserAccounts())
                {
                    // Check if player has a rank that should be reset
                    bool shouldReset = false;

                    // Check for leaderboard ranks
                    if (IsLeaderboardRank(account.Group))
                    {
                        shouldReset = true;
                        TShock.Log.Info($"Resetting leaderboard rank for {account.Name}");
                    }
                    // Check for maximum rankup rank
                    else if (account.Group == config.MaximumRankUpRank.Value)
                    {
                        shouldReset = true;
                        TShock.Log.Info($"Resetting maximum rankup rank for {account.Name}");
                    }

                    if (shouldReset)
                    {
                        // Save previous rank before resetting
                        var currentRank = ranks.FirstOrDefault(r => r.GroupName == account.Group);
                        if (currentRank != null)
                        {
                            var position =
                                ranks
                                    .OrderBy(r => r.RequiredCurrencyAmount)
                                    .ToList()
                                    .FindIndex(r => r.Name == currentRank.Name) + 1;
                            await bank.SavePreviousRank(
                                account.ID,
                                Main.worldID.ToString(),
                                position
                            );
                        }

                        // Reset player's rank
                        TShock.UserAccounts.SetUserGroup(account, resetRank);
                        affectedPlayers.Add(account.Name);

                        // Notify online player
                        var player = TShock.Players.FirstOrDefault(p =>
                            p?.Account?.ID == account.ID
                        );
                        if (player != null)
                        {
                            player.SendMessage(
                                $"Due to world change, your rank has been reset to {resetRank}.",
                                Color.Orange
                            );
                        }
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
                    TSPlayer.All.SendInfoMessage(
                        $"World change detected! {affectedPlayers.Count} players' ranks have been reset."
                    );
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
            TShock.Log.Info(
                $"World ID manually initialized to {Main.worldID} by {args.Player.Name}"
            );
        }

        private double GetRankMultiplier(string groupName)
        {
            try
            {
                if (string.IsNullOrEmpty(groupName))
                    return 1.0;

                // Get ranks from database
                var ranks = bank.GetRanks().Result;

                // Find the rank object that matches the player's group
                var rank = ranks.FirstOrDefault(r =>
                    r.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase)
                );
                if (rank == null)
                    return 1.0;

                // Return multiplier based on rank name
                return rank.Name switch
                {
                    var r when r == config.Top1Rank.Value => 10.0,
                    var r when r == config.Top2Rank.Value => 8.0,
                    var r when r == config.Top3Rank.Value => 6.0,
                    var r when r == config.Top4Rank.Value => 5.0,
                    var r when r == config.Top56Rank.Value => 4.0,
                    var r when r == config.Top78Rank.Value => 3.0,
                    var r when r == config.Top910Rank.Value => 2.5,
                    _ => 1.0, // Default multiplier
                };
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error in GetRankMultiplier: {ex.Message}");
                return 1.0;
            }
        }

        private void InitializeWeekendBonus()
        {
            // Check current status
            UpdateWeekendBonusStatus();

            // Start timer for hourly checks
            weekendBonusTimer = new Timer(
                _ =>
                {
                    CheckAndUpdateWeekendBonus();
                },
                null,
                TimeSpan.Zero,
                TimeSpan.FromHours(1)
            );
        }

        private void CheckAndUpdateWeekendBonus()
        {
            try
            {
                bool wasWeekendBonus = isWeekendBonus;
                UpdateWeekendBonusStatus();

                var now = DateTime.Now;
                if (isWeekendBonus)
                {
                    if (!wasWeekendBonus)
                    {
                        // Weekend bonus just started
                        TSPlayer.All.SendMessage(
                            "Weekend Bonus has started! All currency gains are doubled!",
                            Color.LightGreen
                        );
                    }
                    // Regular reminder during weekend
                    TSPlayer.All.SendMessage(
                        $"Weekend Bonus is active! (x{config.WeekendBonusMultiplier.Value} currency)",
                        Color.Yellow
                    );
                }
                else
                {
                    // Not weekend, check time until next weekend
                    var nextWeekend = GetNextWeekendStart();
                    var timeUntil = nextWeekend - now;
                    TSPlayer.All.SendMessage(
                        $"Weekend Bonus starts in {timeUntil.Days}d {timeUntil.Hours}h {timeUntil.Minutes}m",
                        Color.Gray
                    );
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error in CheckAndUpdateWeekendBonus: {ex.Message}");
            }
        }

        private void UpdateWeekendBonusStatus()
        {
            if (!config.WeekendBonusEnabled.Value)
            {
                isWeekendBonus = false;
                return;
            }

            var now = DateTime.Now;
            isWeekendBonus =
                now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;
        }

        private DateTime GetNextWeekendStart()
        {
            var now = DateTime.Now;
            int daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilSaturday == 0 && now.Hour >= 0)
                daysUntilSaturday = 7;
            return now.Date.AddDays(daysUntilSaturday);
        }
    }
}
