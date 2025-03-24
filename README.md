# 🎮 JgransEconomySystem

## 📝 Description
A comprehensive economy and rank progression system for Terraria TShock servers. Features currency earning, player banking, automatic leaderboard updates, and rank management.

## 🔑 Key Features

### 💰 Currency System
- Tiered NPC rewards:
  - Boss NPCs (3 tiers)
  - Special NPCs
  - Hostile NPCs
  - Normal NPCs
- Anti-farming protection
- Hardmode multipliers
- Weekend bonus events

### 🌟 Rank System
- Progressive rank hierarchy
- Automatic promotions
- Rank-based multipliers (1.5x - 10x)
- World change rank resets
- Qualification requirements

### 📊 Leaderboard
- Daily automatic updates
- Top 10 player tracking
- Historical data storage
- Position-based ranks
- Consistent update schedule

### 🏦 Banking
- Player accounts
- Inter-player transfers
- Transaction history
- Admin controls
- Balance tracking

## ⚙️ Commands

### Player Commands
```
/bank bal - Check balance
/bank pay <player> <amount> - Transfer currency
/leaderboard - View rankings
```

### Admin Commands
```
/initworld - Set server world ID
/updateboard - Force leaderboard update
/bank give <player> <amount> - Give currency
/bank giveall <amount> - Give to all
/bank resetall - Reset balances
/economyreload - Reload config
```

## 🔒 Permissions
```
jgraneconomy.system
jgraneconomy.admin
jgranserver.admin
```

## ⚡ Configuration
```json
{
    "ServerName": "string",
    "CurrencyName": "string",
    "LeaderboardUpdateHour": 0,
    "LeaderboardUpdateMinute": 0,
    "WorldResetRank": "string",
    "WeekendBonusEnabled": true,
    "WeekendBonusMultiplier": 2.0
}
```