using Terraria.ID;

namespace JgransEconomySystem
{
	public static class NPCType
	{
		static JgransEconomySystemConfig config = new JgransEconomySystemConfig();

		public static bool IsHostile(int npcType)
		{
			return config.HostileNPCs.Value.Contains(npcType);
		}

		public static bool IsSpecial(int npcType)
		{
			return config.SpecialNPCs.Value.Contains(npcType);
		}

		public static bool IsBoss1(int npcType)
		{
			return config.BossNPCs1.Value.Contains(npcType);
		}

		public static bool IsBoss2(int npcType)
		{
			return config.BossNPCs2.Value.Contains(npcType);
		}
		public static bool IsBoss3(int npcType)
		{
			return config.BossNPCs3.Value.Contains(npcType);
		}
	}
}
