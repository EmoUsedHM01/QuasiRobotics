using System.IO;
using System.Reflection;
using UnityEngine;

namespace QuasiRobotics;

public sealed class ConfigDirectories
{
	public string ModAssemblyName { get; private set; }

	public string AllModsConfigFolder { get; private set; }

	public string ModPersistenceFolder { get; private set; }

	public string ConfigPath { get; private set; }

	public ConfigDirectories(string configFileName = "config.json")
	{
		ModAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
		AllModsConfigFolder = Path.Combine(Application.persistentDataPath, "../Quasimorph_ModConfigs/");
		ModPersistenceFolder = Path.Combine(AllModsConfigFolder, ModAssemblyName);
		ConfigPath = Path.Combine(ModPersistenceFolder, configFileName);
	}
}
