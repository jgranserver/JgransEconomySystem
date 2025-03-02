using Terraria.ID;

namespace JgransEconomySystem
{
	public static class NPCType
	{
		static JgransEconomySystemConfig config = new JgransEconomySystemConfig();

		public static bool IsHostile(int npcType) => config.HostileNPCs.Value.Contains(npcType);

		public static bool IsSpecial(int npcType) => config.SpecialNPCs.Value.Contains(npcType);

		public static bool IsBoss1(int npcType) => config.BossNPCs1.Value.Contains(npcType);

		public static bool IsBoss2(int npcType) => config.BossNPCs2.Value.Contains(npcType);

		public static bool IsBoss3(int npcType) => config.BossNPCs3.Value.Contains(npcType);
	}
}
