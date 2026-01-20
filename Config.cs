using Newtonsoft.Json;

#nullable enable
namespace JgransEconomySystem;

public class JgransEconomySystemConfig
{
    public JgransEconomySystemConfig.ConfigProperty<bool> ToggleEconomy { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<bool>()
        {
            Description = "Enable(true) or Disable(false)",
            Value = true,
        };

    public JgransEconomySystemConfig.ConfigProperty<string> CurrencyName { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "The name of the currency used",
            Value = "jspoints",
        };

    public JgransEconomySystemConfig.ConfigProperty<string> ServerName { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "The name of the server",
            Value = "Jgrans",
        };

    public JgransEconomySystemConfig.ConfigProperty<double> TaxRate { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<double>()
        {
            Description = "The shop and transaction tax rate",
            Value = 0.2,
        };

    public JgransEconomySystemConfig.ConfigProperty<int> LowRate { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "The rate for low conditions",
            Value = 30,
        };

    public JgransEconomySystemConfig.ConfigProperty<int> MedRate { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "The rate for medium conditions",
            Value = 50,
        };

    public JgransEconomySystemConfig.ConfigProperty<int> HighRate { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "The rate for high conditions",
            Value = 85,
        };

    public JgransEconomySystemConfig.ConfigProperty<int> PerfectRate { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "The rate for perfect conditions",
            Value = 100,
        };

    public JgransEconomySystemConfig.ConfigProperty<int> Boss3MaxAmount { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "Maximum amount of currency dropped by Boss3 NPCs",
            Value = 1000,
        };

    public JgransEconomySystemConfig.ConfigProperty<int> Boss2MaxAmount { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "Maximum amount of currency dropped by Boss2 NPCs",
            Value = 600,
        };

    public JgransEconomySystemConfig.ConfigProperty<int> Boss1MaxAmount { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "Maximum amount of currency dropped by Boss1 NPCs",
            Value = 380,
        };

    public JgransEconomySystemConfig.ConfigProperty<int> SpecialMaxAmount { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "Maximum amount of currency dropped by Special NPCs",
            Value = 80, /*0x50*/
        };

    public JgransEconomySystemConfig.ConfigProperty<int> HostileMaxAmount { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "Maximum amount of currency dropped by Hostile NPCs",
            Value = 50,
        };

    public JgransEconomySystemConfig.ConfigProperty<int> NormalMaxAmount { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "Maximum amount of currency dropped by Normal NPCs",
            Value = 25,
        };

    public JgransEconomySystemConfig.ConfigProperty<HashSet<int>> HostileNPCs { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<HashSet<int>>()
        {
            Description = "The set of hostile NPCs",
            Value = new HashSet<int>()
            {
                1,
                3,
                2,
                173,
                6,
                49,
                51,
                150,
                35,
                164,
                165,
                59,
                60,
                61,
                10,
                11,
                12,
                117,
                118,
                119,
                98,
                99,
                100,
                47,
                57,
                168,
                81,
                83,
                183,
                34,
                32 /*0x20*/
                ,
                62,
                133,
                190,
                191,
                192 /*0xC0*/
                ,
                193,
                194,
                24,
                26,
                27,
                28,
                29,
                378,
                58,
                250,
                257,
                69,
                77,
                473,
                474,
                475,
                39,
                40,
                41,
                46,
                101,
                217,
                94,
                494,
                173,
                179,
                34,
                467,
                7,
                8,
                9,
                95,
                96 /*0x60*/
                ,
                97,
                468,
                71,
                6,
                84,
                581,
                224 /*0xE0*/
                ,
                226,
                162,
                259,
                122,
                93,
                152,
                111,
                471,
                28,
                55,
                48 /*0x30*/
                ,
                60,
                174,
                42,
                150,
                147,
                268,
                137,
                51,
                236,
                -10,
                59,
                117,
                118,
                119,
                388,
                386,
                383,
                392,
                387,
                16 /*0x10*/
                ,
                258,
                252,
                148,
                213,
                215,
                214,
                491,
                492,
                75,
                262,
                140,
                244,
                156,
                172,
                269,
                270,
                271,
                541,
                542,
                99,
                98,
                100,
                5,
                65,
                21,
                110,
                293,
                291,
                302,
                187,
                56,
                145,
                185,
                143,
                184,
                204,
                221,
                405,
                406,
                407,
                408,
                411,
                409,
                410,
                403,
                402,
                404,
                292,
                53,
                514,
                513,
                515,
                141,
                546,
                44,
                167,
                86,
                158,
                61,
                164,
                165,
                82,
                3,
                161,
                223,
                320,
                321,
                331,
                319,
                332,
                338,
                254,
                23,
                133,
                190,
                191,
                192 /*0xC0*/
                ,
                193,
                194,
                555,
                552,
                566,
                561,
            },
        };

    public JgransEconomySystemConfig.ConfigProperty<HashSet<int>> SpecialNPCs { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<HashSet<int>>()
        {
            Description = "The set of special NPCs",
            Value = new HashSet<int>()
            {
                14,
                13,
                586,
                198,
                631,
                243,
                290,
                618,
                544,
                621,
                87,
                104,
                172,
                45,
                85,
                473,
                474,
                475,
                476,
                629,
                196,
                477,
                471,
                216,
                205,
                251,
                109,
                156,
                66,
                153,
                154,
                454,
                520,
                385,
                463,
                466,
                469,
                159,
                479,
                467,
                315,
                329,
                330,
                326,
                343,
                351,
                348,
                347,
                350,
                341,
                412,
                507,
                517,
                493,
                422,
                570,
                553,
                562,
                559,
                574,
                568,
                572,
            },
        };

    public JgransEconomySystemConfig.ConfigProperty<HashSet<int>> BossNPCs1 { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<HashSet<int>>()
        {
            Description = "The set of Boss NPCs (group 1)",
            Value = new HashSet<int>() { 50, 4, 15, 266, 564 },
        };

    public JgransEconomySystemConfig.ConfigProperty<HashSet<int>> BossNPCs2 { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<HashSet<int>>()
        {
            Description = "The set of Boss NPCs (group 2)",
            Value = new HashSet<int>()
            {
                222,
                35,
                113,
                551,
                576,
                577,
                565,
                134,
                (int)sbyte.MaxValue,
                125,
                126,
            },
        };

    public JgransEconomySystemConfig.ConfigProperty<HashSet<int>> BossNPCs3 { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<HashSet<int>>()
        {
            Description = "The set of Boss NPCs (group 3)",
            Value = new HashSet<int>() { 551, 262, 636, 245, 370, 439, 398 },
        };

    public Dictionary<string, RankInfo> RankInfos { get; set; } =
        new Dictionary<string, RankInfo>()
        {
            ["Page"] = new RankInfo()
            {
                Name = "Page",
                Description = "A stepping stone rank for beginners",
                Perks = new List<string>() { "Basic commands" },
                IsLeaderboardRank = false,
            },
            ["Squire"] = new RankInfo()
            {
                Name = "Squire",
                Description = "First advancement rank",
                Perks = new List<string>() { "Can use teleportation potions" },
                IsLeaderboardRank = false,
            },
            ["Knight"] = new RankInfo()
            {
                Name = "Knight",
                Description = "Basic mobility rank",
                Perks = new List<string>() { "Can teleport using pylons" },
                IsLeaderboardRank = false,
            },
            ["Templar"] = new RankInfo()
            {
                Name = "Templar",
                Description = "Combined mobility rank",
                Perks = new List<string>() { "Combines all perks from Newbie through Knight" },
                IsLeaderboardRank = false,
            },
            ["Crusader"] = new RankInfo()
            {
                Name = "Crusader",
                Description = "Boss summoning rank",
                Perks = new List<string>() { "Can spawn bosses using items" },
                IsLeaderboardRank = false,
            },
            ["Royal Knight"] = new RankInfo()
            {
                Name = "Royal Knight",
                Description = "Advanced mobility rank",
                Perks = new List<string>() { "Can teleport using Magic Conch" },
                IsLeaderboardRank = false,
            },
            ["Imperial Knight"] = new RankInfo()
            {
                Name = "Imperial Knight",
                Description = "Event starter rank",
                Perks = new List<string>() { "Can start invasions using items" },
                IsLeaderboardRank = false,
            },
            ["Warlord"] = new RankInfo()
            {
                Name = "Warlord",
                Description = "Special event rank",
                Perks = new List<string>() { "Can start Old One's Army" },
                IsLeaderboardRank = false,
            },
            ["Overlord"] = new RankInfo()
            {
                Name = "Overlord",
                Description = "Group mobility rank",
                Perks = new List<string>() { "Can use Wormhole Potions" },
                IsLeaderboardRank = false,
            },
            ["Baron"] = new RankInfo()
            {
                Name = "Baron",
                Description = "Complete basic rank",
                Perks = new List<string>()
                {
                    "Combines all permissions from Newbie through Overlord",
                },
                IsLeaderboardRank = false,
            },
            ["Viscount"] = new RankInfo()
            {
                Name = "Viscount",
                Description = "Hell mobility rank",
                Perks = new List<string>() { "Can use Demon Conch" },
                IsLeaderboardRank = false,
            },
            ["Count"] = new RankInfo()
            {
                Name = "Count",
                Description = "Full inheritance rank",
                Perks = new List<string>() { "Inherits all lower rank permissions" },
                IsLeaderboardRank = false,
            },
            ["Marquis"] = new RankInfo()
            {
                Name = "Marquis",
                Description = "Highest regular rank",
                Perks = new List<string>()
                {
                    "Can use Rod of Discord or any teleportation item/tool",
                    "Last rank eligible for leaderboard ranking",
                },
                IsLeaderboardRank = false,
            },
            ["Deity"] = new RankInfo()
            {
                Name = "Deity",
                Description = "The highest leaderboard rank (Rank 1)",
                Perks = new List<string>()
                {
                    "Inherits all rank perks",
                    "Can teleport self and NPCs",
                    "Can heal using commands",
                    "No house definition limits",
                },
                IsLeaderboardRank = true,
            },
            ["Saint"] = new RankInfo()
            {
                Name = "Saint",
                Description = "Second highest leaderboard rank (Rank 2)",
                Perks = new List<string>()
                {
                    "Can use banned items",
                    "Can rename NPCs",
                    "Can buff others using commands",
                },
                IsLeaderboardRank = true,
            },
            ["Heirophant"] = new RankInfo()
            {
                Name = "Heirophant",
                Description = "Third highest leaderboard rank (Rank 3)",
                Perks = new List<string>() { "Can move NPC housing" },
                IsLeaderboardRank = true,
            },
            ["Emperor"] = new RankInfo()
            {
                Name = "Emperor",
                Description = "Fourth highest leaderboard rank (Rank 4)",
                Perks = new List<string>() { "Can kill NPCs", "Can use warp commands" },
                IsLeaderboardRank = true,
            },
            ["Heir"] = new RankInfo()
            {
                Name = "Heir",
                Description = "Fifth and sixth leaderboard rank (Rank 5-6)",
                Perks = new List<string>() { "Can use Sundials" },
                IsLeaderboardRank = true,
            },
            ["Chancellor"] = new RankInfo()
            {
                Name = "Chancellor",
                Description = "Seventh and eighth leaderboard rank (Rank 7-8)",
                Perks = new List<string>() { "Can only use warps added to the world" },
                IsLeaderboardRank = true,
            },
            ["Duke"] = new RankInfo()
            {
                Name = "Duke",
                Description = "Ninth and tenth leaderboard rank (Rank 9-10)",
                Perks = new List<string>() { "Inherits all perks from non-leaderboard ranks" },
                IsLeaderboardRank = true,
            },
        };

    public JgransEconomySystemConfig.ConfigProperty<string> MaximumRankUpRank { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "Rank qualification for maximum rank up",
            Value = "Marquis",
        };

    public JgransEconomySystemConfig.ConfigProperty<string> Top1Rank { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "Top 1 Rank",
            Value = "Deity",
        };

    public JgransEconomySystemConfig.ConfigProperty<string> Top2Rank { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "Top 2 Rank",
            Value = "Saint",
        };

    public JgransEconomySystemConfig.ConfigProperty<string> Top3Rank { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "Top 3 Rank",
            Value = "Heirophant",
        };

    public JgransEconomySystemConfig.ConfigProperty<string> Top4Rank { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "Top 4 Rank",
            Value = "Emperor",
        };

    public JgransEconomySystemConfig.ConfigProperty<string> Top56Rank { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "Top 5-6 Rank",
            Value = "Heir",
        };

    public JgransEconomySystemConfig.ConfigProperty<string> Top78Rank { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "Top 7-8 Rank",
            Value = "Chancellor",
        };

    public JgransEconomySystemConfig.ConfigProperty<string> Top910Rank { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "Top 9-10 Rank",
            Value = "Duke",
        };

    public JgransEconomySystemConfig.ConfigProperty<string> WorldResetRank { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<string>()
        {
            Description = "The rank players will be reset to on world change",
            Value = "Overlord",
        };

    public JgransEconomySystemConfig.ConfigProperty<int> LastWorldId { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<int>()
        {
            Description = "The ID of the last world",
            Value = -1,
        };

    public JgransEconomySystemConfig.ConfigProperty<bool> WeekendBonusEnabled { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<bool>()
        {
            Description = "Enable or disable weekend bonus",
            Value = true,
        };

    public JgransEconomySystemConfig.ConfigProperty<double> WeekendBonusMultiplier { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<double>()
        {
            Description = "Multiplier for weekend bonus",
            Value = 2.0,
        };

    public JgransEconomySystemConfig.ConfigProperty<DateTime> LastLeaderboardUpdate { get; set; } =
        new JgransEconomySystemConfig.ConfigProperty<DateTime>()
        {
            Description = "The last time the leaderboard was updated",
            Value = DateTime.MinValue,
        };

    public static JgransEconomySystemConfig Read(string filePath)
    {
        return File.Exists(filePath)
            ? JsonConvert.DeserializeObject<JgransEconomySystemConfig>(File.ReadAllText(filePath)) ?? new JgransEconomySystemConfig()
            : new JgransEconomySystemConfig();
    }

    public void Write(string filePath)
    {
        string contents = JsonConvert.SerializeObject((object)this, (Formatting)1);
        File.WriteAllText(filePath, contents);
    }

    public class ConfigProperty<T>
    {
        public string Description { get; set; } = string.Empty;

        public T Value { get; set; } = default!;
    }
}
