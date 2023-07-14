using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace JgransEconomySystem
{
	public class JgransEconomySystemConfig
	{
		public string CurrencyName { get; set; } = "jspoints";
		public string ServerName { get; set; } = "Jgrans";
		public int LowRate { get; set; } = 30;
		public int MedRate { get; set; } = 50;
		public int HighRate { get; set; } = 85;
		public int PerfectRate { get; set; } = 100;
		public int Boss3MaxAmount { get; set; } = 1000;
		public int Boss2MaxAmount { get; set; } = 600;
		public int Boss1MaxAmount { get; set; } = 380;
		public int SpecialMaxAmount { get; set; } = 80;
		public int HostileMaxAmount { get; set; } = 50;
		public int NormalMaxAmount { get; set; } = 25;

		// Add other configurable properties here

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
	}
}