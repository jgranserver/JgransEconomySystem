using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Terraria.ID;

namespace JgransEconomySystem
{
    public class JgransEconomySystemConfig
    {
        public ConfigProperty<bool> ToggleEconomy { get; set; } =
            new ConfigProperty<bool>
            {
                Description = "Enable(true) or Disable(false)",
                Value = true,
            };
        public ConfigProperty<string> CurrencyName { get; set; } =
            new ConfigProperty<string>
            {
                Description = "The name of the currency used",
                Value = "jspoints",
            };

        public ConfigProperty<string> ServerName { get; set; } =
            new ConfigProperty<string> { Description = "The name of the server", Value = "Jgrans" };

        public ConfigProperty<double> TaxRate { get; set; } =
            new ConfigProperty<double>
            {
                Description = "The shop and transaction tax rate",
                Value = 0.2,
            };

        public ConfigProperty<int> LowRate { get; set; } =
            new ConfigProperty<int> { Description = "The rate for low conditions", Value = 30 };

        public ConfigProperty<int> MedRate { get; set; } =
            new ConfigProperty<int> { Description = "The rate for medium conditions", Value = 50 };

        public ConfigProperty<int> HighRate { get; set; } =
            new ConfigProperty<int> { Description = "The rate for high conditions", Value = 85 };

        public ConfigProperty<int> PerfectRate { get; set; } =
            new ConfigProperty<int>
            {
                Description = "The rate for perfect conditions",
                Value = 100,
            };

        public ConfigProperty<int> Boss3MaxAmount { get; set; } =
            new ConfigProperty<int>
            {
                Description = "Maximum amount of currency dropped by Boss3 NPCs",
                Value = 1000,
            };

        public ConfigProperty<int> Boss2MaxAmount { get; set; } =
            new ConfigProperty<int>
            {
                Description = "Maximum amount of currency dropped by Boss2 NPCs",
                Value = 600,
            };

        public ConfigProperty<int> Boss1MaxAmount { get; set; } =
            new ConfigProperty<int>
            {
                Description = "Maximum amount of currency dropped by Boss1 NPCs",
                Value = 380,
            };

        public ConfigProperty<int> SpecialMaxAmount { get; set; } =
            new ConfigProperty<int>
            {
                Description = "Maximum amount of currency dropped by Special NPCs",
                Value = 80,
            };

        public ConfigProperty<int> HostileMaxAmount { get; set; } =
            new ConfigProperty<int>
            {
                Description = "Maximum amount of currency dropped by Hostile NPCs",
                Value = 50,
            };

        public ConfigProperty<int> NormalMaxAmount { get; set; } =
            new ConfigProperty<int>
            {
                Description = "Maximum amount of currency dropped by Normal NPCs",
                Value = 25,
            };

        public ConfigProperty<HashSet<int>> HostileNPCs { get; set; } =
            new ConfigProperty<HashSet<int>>
            {
                Description = "The set of hostile NPCs",
                Value = new HashSet<int>
                {
                    NPCID.BlueSlime,
                    NPCID.Zombie,
                    NPCID.DemonEye,
                    NPCID.Crimera,
                    NPCID.EaterofSouls,
                    NPCID.CaveBat,
                    NPCID.JungleBat,
                    NPCID.IceBat,
                    NPCID.SkeletronHead,
                    NPCID.WallCreeper,
                    NPCID.WallCreeperWall,
                    NPCID.LavaSlime,
                    NPCID.Hellbat,
                    NPCID.Vulture,
                    NPCID.GiantWormHead,
                    NPCID.GiantWormBody,
                    NPCID.GiantWormTail,
                    NPCID.LeechHead,
                    NPCID.LeechBody,
                    NPCID.LeechTail,
                    NPCID.SeekerHead,
                    NPCID.SeekerBody,
                    NPCID.SeekerTail,
                    NPCID.CorruptBunny,
                    NPCID.CorruptGoldfish,
                    NPCID.CorruptPenguin,
                    NPCID.CorruptSlime,
                    NPCID.CursedHammer,
                    NPCID.Crimslime,
                    NPCID.CursedSkull,
                    NPCID.DarkCaster,
                    NPCID.Demon,
                    NPCID.WanderingEye,
                    NPCID.CataractEye,
                    NPCID.SleepyEye,
                    NPCID.DialatedEye,
                    NPCID.GreenEye,
                    NPCID.PurpleEye,
                    NPCID.FireImp,
                    NPCID.GoblinPeon,
                    NPCID.GoblinThief,
                    NPCID.GoblinWarrior,
                    NPCID.GoblinSorcerer,
                    NPCID.ChatteringTeethBomb,
                    NPCID.Piranha,
                    NPCID.AngryNimbus,
                    NPCID.AnomuraFungus,
                    NPCID.Antlion,
                    NPCID.ArmoredSkeleton,
                    NPCID.BigMimicCorruption,
                    NPCID.BigMimicCrimson,
                    NPCID.BigMimicHallow,
                    NPCID.BoneSerpentHead,
                    NPCID.BoneSerpentBody,
                    NPCID.BoneSerpentTail,
                    NPCID.Bunny,
                    NPCID.Clinger,
                    NPCID.CochinealBeetle,
                    NPCID.Corruptor,
                    NPCID.Crawdad,
                    NPCID.Crimera,
                    NPCID.CrimsonAxe,
                    NPCID.CursedSkull,
                    NPCID.DeadlySphere,
                    NPCID.DevourerHead,
                    NPCID.DevourerBody,
                    NPCID.DevourerTail,
                    NPCID.DiggerHead,
                    NPCID.DiggerBody,
                    NPCID.DiggerTail,
                    NPCID.DrManFly,
                    NPCID.DungeonSlime,
                    NPCID.EaterofSouls,
                    NPCID.EnchantedSword,
                    NPCID.FlyingAntlion,
                    NPCID.FlyingFish,
                    NPCID.FlyingSnake,
                    NPCID.Frankenstein,
                    NPCID.FungiBulb,
                    NPCID.Gastropod,
                    NPCID.GiantBat,
                    NPCID.GiantFlyingFox,
                    NPCID.GoblinArcher,
                    NPCID.GoblinSummoner,
                    NPCID.GoblinWarrior,
                    NPCID.Goldfish,
                    NPCID.Harpy,
                    NPCID.Hellbat,
                    NPCID.Herpling,
                    NPCID.Hornet,
                    NPCID.IceBat,
                    NPCID.IceSlime,
                    NPCID.IchorSticker,
                    NPCID.IlluminantBat,
                    NPCID.JungleBat,
                    NPCID.JungleCreeper,
                    NPCID.JungleSlime,
                    NPCID.LavaSlime,
                    NPCID.LeechHead,
                    NPCID.LeechBody,
                    NPCID.LeechTail,
                    NPCID.MartianDrone,
                    NPCID.MartianEngineer,
                    NPCID.MartianOfficer,
                    NPCID.MartianSaucer,
                    NPCID.MartianTurret,
                    NPCID.MotherSlime,
                    NPCID.MushiLadybug,
                    NPCID.Parrot,
                    NPCID.Penguin,
                    NPCID.PirateCorsair,
                    NPCID.PirateCrossbower,
                    NPCID.PirateDeadeye,
                    NPCID.PirateShip,
                    NPCID.PirateShipCannon,
                    NPCID.Pixie,
                    NPCID.Plantera,
                    NPCID.PossessedArmor,
                    NPCID.RainbowSlime,
                    NPCID.RedDevil,
                    NPCID.RuneWizard,
                    NPCID.RustyArmoredBonesAxe,
                    NPCID.RustyArmoredBonesFlail,
                    NPCID.RustyArmoredBonesSword,
                    NPCID.SandElemental,
                    NPCID.SandShark,
                    NPCID.SeekerBody,
                    NPCID.SeekerHead,
                    NPCID.SeekerTail,
                    NPCID.ServantofCthulhu,
                    NPCID.Shark,
                    NPCID.Skeleton,
                    NPCID.SkeletonArcher,
                    NPCID.SkeletonCommando,
                    NPCID.SkeletonSniper,
                    NPCID.SlimeMasked,
                    NPCID.SlimedZombie,
                    NPCID.Snatcher,
                    NPCID.SnowBalla,
                    NPCID.SnowFlinx,
                    NPCID.SnowmanGangsta,
                    NPCID.SpikedIceSlime,
                    NPCID.SpikedJungleSlime,
                    NPCID.Squid,
                    NPCID.StardustCellBig,
                    NPCID.StardustCellSmall,
                    NPCID.StardustJellyfishBig,
                    NPCID.StardustJellyfishSmall,
                    NPCID.StardustSoldier,
                    NPCID.StardustSpiderBig,
                    NPCID.StardustSpiderSmall,
                    NPCID.StardustWormBody,
                    NPCID.StardustWormHead,
                    NPCID.StardustWormTail,
                    NPCID.TacticalSkeleton,
                    NPCID.TheGroom,
                    NPCID.TombCrawlerBody,
                    NPCID.TombCrawlerHead,
                    NPCID.TombCrawlerTail,
                    NPCID.ToxicSludge,
                    NPCID.Tumbleweed,
                    NPCID.UndeadMiner,
                    NPCID.UndeadViking,
                    NPCID.Unicorn,
                    NPCID.VampireBat,
                    NPCID.Vulture,
                    NPCID.WallCreeper,
                    NPCID.WallCreeperWall,
                    NPCID.Wraith,
                    NPCID.Zombie,
                    NPCID.ZombieEskimo,
                    NPCID.ZombieRaincoat,
                    NPCID.ZombieSuperman,
                    NPCID.ZombiePixie,
                    NPCID.ZombieXmas,
                    NPCID.ZombieDoctor,
                    NPCID.ZombieSweater,
                    NPCID.ZombieElf,
                    NPCID.ZombieMushroom,
                    NPCID.MeteorHead,
                    NPCID.WanderingEye,
                    NPCID.CataractEye,
                    NPCID.SleepyEye,
                    NPCID.DialatedEye,
                    NPCID.GreenEye,
                    NPCID.PurpleEye,
                    NPCID.DD2GoblinBomberT1,
                    NPCID.DD2GoblinT1,
                    NPCID.DD2SkeletonT1,
                    NPCID.DD2JavelinstT1,
                    // Add more hostile NPCs as needed
                },
            };

        public ConfigProperty<HashSet<int>> SpecialNPCs { get; set; } =
            new ConfigProperty<HashSet<int>>
            {
                Description = "The set of special NPCs",
                Value = new HashSet<int>
                {
                    NPCID.EaterofWorldsBody,
                    NPCID.EaterofWorldsHead,
                    NPCID.ZombieMerman,
                    NPCID.Lihzahrd,
                    NPCID.RockGolem,
                    NPCID.IceGolem,
                    NPCID.Paladin,
                    NPCID.BloodNautilus,
                    NPCID.SandsharkCrimson,
                    NPCID.BloodEelHead,
                    NPCID.WyvernHead,
                    NPCID.Werewolf,
                    NPCID.RuneWizard,
                    NPCID.Tim,
                    NPCID.Mimic,
                    NPCID.BigMimicCorruption,
                    NPCID.BigMimicCrimson,
                    NPCID.BigMimicHallow,
                    NPCID.BigMimicJungle,
                    NPCID.IceMimic,
                    NPCID.Nymph,
                    NPCID.Mothron,
                    NPCID.GoblinSummoner,
                    NPCID.PirateCaptain,
                    NPCID.Moth,
                    NPCID.Eyezor,
                    NPCID.Clown,
                    NPCID.RedDevil,
                    NPCID.VoodooDemon,
                    NPCID.GiantTortoise,
                    NPCID.IceTortoise,
                    NPCID.CultistDragonHead,
                    NPCID.MartianWalker,
                    NPCID.GrayGrunt,
                    NPCID.Nailhead,
                    NPCID.Psycho,
                    NPCID.ThePossessed,
                    NPCID.Vampire,
                    NPCID.MothronSpawn,
                    NPCID.DeadlySphere,
                    NPCID.HeadlessHorseman,
                    NPCID.Hellhound,
                    NPCID.Poltergeist,
                    NPCID.Splinterling,
                    NPCID.Yeti,
                    NPCID.Krampus,
                    NPCID.Nutcracker,
                    NPCID.ElfCopter,
                    NPCID.ElfArcher,
                    NPCID.PresentMimic,
                    NPCID.SolarCrawltipedeHead,
                    NPCID.LunarTowerNebula,
                    NPCID.LunarTowerSolar,
                    NPCID.LunarTowerStardust,
                    NPCID.LunarTowerVortex,
                    NPCID.DD2DrakinT2,
                    NPCID.DD2GoblinT2,
                    NPCID.DD2JavelinstT2,
                    NPCID.DD2WyvernT2,
                    NPCID.DD2KoboldFlyerT2,
                    NPCID.DD2WitherBeastT2,
                    NPCID.DD2KoboldWalkerT2,
                },
            };

        public ConfigProperty<HashSet<int>> BossNPCs1 { get; set; } =
            new ConfigProperty<HashSet<int>>
            {
                Description = "The set of Boss NPCs (group 1)",
                Value = new HashSet<int>
                {
                    NPCID.KingSlime,
                    NPCID.EyeofCthulhu,
                    NPCID.EaterofWorldsTail,
                    NPCID.BrainofCthulhu,
                    NPCID.DD2DarkMageT1,
                    // Add more boss NPCs as needed
                },
            };

        public ConfigProperty<HashSet<int>> BossNPCs2 { get; set; } =
            new ConfigProperty<HashSet<int>>
            {
                Description = "The set of Boss NPCs (group 2)",
                Value = new HashSet<int>
                {
                    NPCID.QueenBee,
                    NPCID.SkeletronHead,
                    NPCID.WallofFlesh,
                    NPCID.DD2Betsy,
                    NPCID.DD2OgreT2,
                    NPCID.DD2OgreT3,
                    NPCID.DD2DarkMageT3,
                    NPCID.TheDestroyer,
                    NPCID.SkeletronPrime,
                    NPCID.Retinazer,
                    NPCID.Spazmatism,
                    // Add more boss NPCs as needed
                },
            };

        public ConfigProperty<HashSet<int>> BossNPCs3 { get; set; } =
            new ConfigProperty<HashSet<int>>
            {
                Description = "The set of Boss NPCs (group 3)",
                Value = new HashSet<int>
                {
                    NPCID.DD2Betsy,
                    NPCID.Plantera,
                    636,
                    NPCID.Golem,
                    NPCID.DukeFishron,
                    NPCID.CultistBoss,
                    NPCID.MoonLordCore,
                    // Add more boss NPCs as needed
                },
            };

        public ConfigProperty<string> MaximumRankUpRank { get; set; } =
            new ConfigProperty<string>
            {
                Description = "Rank qualification for maximum rank up",
                Value = "Marquis",
            };
        public ConfigProperty<string> Top1Rank { get; set; } =
            new ConfigProperty<string> { Description = "Top 1 Rank", Value = "Deity" };
        public ConfigProperty<string> Top2Rank { get; set; } =
            new ConfigProperty<string> { Description = "Top 2 Rank", Value = "Saint" };
        public ConfigProperty<string> Top3Rank { get; set; } =
            new ConfigProperty<string> { Description = "Top 3 Rank", Value = "Heirophant" };
        public ConfigProperty<string> Top4Rank { get; set; } =
            new ConfigProperty<string> { Description = "Top 4 Rank", Value = "Emperor" };
        public ConfigProperty<string> Top56Rank { get; set; } =
            new ConfigProperty<string> { Description = "Top 5-6 Rank", Value = "Heir" };
        public ConfigProperty<string> Top78Rank { get; set; } =
            new ConfigProperty<string> { Description = "Top 7-8 Rank", Value = "Chancellor" };
        public ConfigProperty<string> Top910Rank { get; set; } =
            new ConfigProperty<string> { Description = "Top 9-10 Rank", Value = "Duke" };

        public ConfigProperty<string> WorldResetRank { get; set; } =
            new ConfigProperty<string>
            {
                Description = "The rank players will be reset to on world change",
                Value = "Overlord",
            };

        public ConfigProperty<int> LastWorldId { get; set; } =
            new ConfigProperty<int> { Description = "The ID of the last world", Value = -1 };

        public ConfigProperty<bool> WeekendBonusEnabled { get; set; } =
            new ConfigProperty<bool>
            {
                Description = "Enable or disable weekend bonus",
                Value = true,
            };

        public ConfigProperty<double> WeekendBonusMultiplier { get; set; } =
            new ConfigProperty<double>
            {
                Description = "Multiplier for weekend bonus",
                Value = 2.0,
            };

        public ConfigProperty<DateTime> LastLeaderboardUpdate { get; set; } =
            new ConfigProperty<DateTime>
            {
                Description = "The last time the leaderboard was updated",
                Value = DateTime.MinValue,
            };

        public static JgransEconomySystemConfig Read(string filePath)
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<JgransEconomySystemConfig>(json);
            }
            else
            {
                return new JgransEconomySystemConfig();
            }
        }

        public void Write(string filePath)
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public class ConfigProperty<T>
        {
            public string Description { get; set; }
            public T Value { get; set; }
        }
    }
}
