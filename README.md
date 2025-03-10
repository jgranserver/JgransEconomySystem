# 🎮 JgransEconomySystem - Terraria TShock Plugin

## 📝 Description
A comprehensive economy and ranking system for Terraria servers using TShock. Features currency drops from NPCs, player banking, automatic leaderboard updates, and a dynamic rank progression system.

## ⚙️ Features

### 💰 Economy System
- Currency drops from different NPC types
- Configurable drop rates and amounts
- Visual effects with Lucky Coin particles
- Hardmode bonus multipliers
- Anti-farming protection

### 🏦 Banking System
- Personal player accounts
- Secure transactions
- Player-to-player payments
- Transaction history
- Admin controls

### 📊 Leaderboard System
- Daily automatic updates
- Historical data tracking
- Top 10 player rankings
- Special formatting for top positions
- Countdown to next update

### ⭐ Rank System
- Progressive rank hierarchy
- Automatic promotions
- World change rank resets
- Qualification requirements
- Leaderboard-based ranks

## 🛠️ Installation

1. Place `JgransEconomySystem.dll` in your server's `ServerPlugins` folder
2. Start the server to generate configuration files
3. Configure settings in `JgransEconomySystemConfig.json`
4. Use `/initworld` command to initialize the world ID

## 📋 Commands

### Player Commands
- `/bank bal` - Check your balance
- `/bank pay <player> <amount>` - Pay another player
- `/leaderboard` - View current rankings
- `/ranks` - Show available ranks
- `/rankup` - Attempt to rank up

### Admin Commands
- `/initworld` - Set server world ID
- `/updateboard` - Force leaderboard update
- `/bank give <player> <amount>` - Give currency
- `/bank giveall <amount>` - Give to all players
- `/bank resetall` - Reset all balances
- `/economyreload` or `/er` - Reload config

## ⚡ Permissions
```
jgraneconomy.system
jgraneconomy.admin
jgranserver.admin
```

## 🔧 Configuration
```json
{
    "ServerName": "YourServer",
    "CurrencyName": "Points",
    "TaxRate": 0.1,
    "ToggleEconomy": true,
    "LeaderboardUpdateHour": 0,
    "LeaderboardUpdateMinute": 0,
    "WorldResetRank": "default",
    "MaximumRankUpRank": "Elite",
    "Top1Rank": "Champion",
    // ...other settings
}
```

## 🎯 Features in Detail

### NPC Currency Drops
- **Boss NPCs**: Highest rewards, requires last hit
- **Special NPCs**: Medium-high rewards
- **Hostile NPCs**: Medium rewards
- **Normal NPCs**: Low rewards

### Visual Effects
- Lucky Coin particle effects
- Colored combat text
- Rank-up notifications
- Leaderboard formatting

### World Change System
- Automatic rank resets
- Configurable reset rank
- Player notifications
- Progress preservation options

## 🤝 Support
Create an issue on GitHub for:
- Bug reports
- Feature requests
- Configuration help
- General support
