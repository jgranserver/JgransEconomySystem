# JgransEconomySystem

JgransEconomySystem is a plugin for Terraria that provides an economy system for your server.

## Features

- Currency system: Players can earn and spend currency.
- Player accounts: Each player has their own currency balance.
- Shops: Set up shops using switch mechanisms where players can buy item.
- Command shops: Create shops using commands as the item to be sold.
- Rank-based rewards: Define ranks and permissions earned for each ranks.
- Configurable: Customize various aspects of the plugin through the configuration file.

## Building the Plugin

To build the plugin from source code, follow these steps:

1. Install [Visual Studio Code](https://code.visualstudio.com/) on your machine.
2. Clone the repository to your local machine or download the source code as a ZIP file and extract it.
3. Open Visual Studio Code and navigate to the folder containing the source code.
4. Install the C# extension for Visual Studio Code.
5. Open the terminal in Visual Studio Code (press `Ctrl+` backtick ` `).
6. Run the following command to restore the NuGet packages: ```dotnet restore```
7. Build the project by running the following command: ```dotnet build```
8. The built plugin DLL file will be located in the `bin/Debug/net6.0` or `bin/Release/net6.0` directory, depending on the build configuration.

## Configuration

The plugin uses a configuration file to customize its behavior. The configuration file is named `JgransEconomySystemConfig.json` and should be placed in the server's `tshock` folder. Modify the settings in the configuration file to suit your server's needs.

## Configuration

The plugin uses a configuration file (`JgransEconomySystemConfig.json`) to customize its behavior. The following settings can be modified in the configuration file:

- `CurrencyName` (default: "jspoints"): The name of the currency used in the economy system.
- `ServerName` (default: "Jgrans"): The name of your server.
- `LowRate` (default: 30): The currency rate for low-tier rewards.
- `MedRate` (default: 50): The currency rate for medium-tier rewards.
- `HighRate` (default: 85): The currency rate for high-tier rewards.
- `PerfectRate` (default: 100): The currency rate for perfect rewards.
- `Boss3MaxAmount` (default: 1000): The maximum currency amount obtainable from defeating the third set boss (Plantera up).
- `Boss2MaxAmount` (default: 600): The maximum currency amount obtainable from defeating the second set boss (Wall of Flesh up).
- `Boss1MaxAmount` (default: 380): The maximum currency amount obtainable from defeating the first boss (Pre-Hardmode Bosses).
- `SpecialMaxAmount` (default: 80): The maximum currency amount obtainable from special events or achievements.
- `HostileMaxAmount` (default: 50): The maximum currency amount obtainable from hostile creatures.
- `NormalMaxAmount` (default: 25): The maximum currency amount obtainable from regular gameplay.

Modify these settings to adjust the currency rates and maximum amounts according to your server's needs.

## Database

The plugin uses a SQLite database to store player account information and shop data. The database file is named `JgransEconomyBanks.sqlite` and is located in the server's `tshock` folder.

## Commands

- `/bank` - Displays the player's currency balance.
- `/setshop` - Sets up a shop at the player's current location using a switch mechanism.
- `/shopallow` - Adds a group to the allowed groups for a shop.
- `/delshop` - Deletes a shop at the player's current location.
- `/delcommandshop` - Deletes a command shop at the player's current location.
- `/sellcommand` - Sets up a command shop at the player's current location.
- `/rankadd` - Adds a new rank.
- `/rankdel` - Deletes a rank.
- `/ranknext` - Updates the next rank for a rank.
- `/rankcost` - Updates the required currency amount for a rank.
- `/rankdown` - Moves a player down to a lower rank.
- `/ranks` - Displays a list of all rank names.
- `/rankup` - Promotes a player to the next rank.

Refer to the in-game command descriptions for more information on how to use each command.

## Support

If you encounter any issues or have any questions, please create a new [Issue](https://github.com/jgranserver/JgransEconomySystem/issues) on the GitHub repository.

## Credits

- Author: jgranserver

## License

This plugin is released under the [MIT License](LICENSE).

