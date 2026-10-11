using System;
using System.IO;
using Newtonsoft.Json;

namespace QuasiRobotics;

public sealed class ModSettings
{
	public bool RobotsIgnoreAugmentationHealthPenalty;

	public bool StartWithPrintyard;

	public bool StartWithSalvageDrones;

	public bool StartWithDropPods;

	public int RobotSlotsPerUpgrade = 2;

	public bool DisableFriendlyFire = true;

	public bool UninstallMode;

	[JsonIgnore]
	public string LoadProblem { get; private set; }

	public static ModSettings LoadOrCreate(string path)
	{
		string text = null;
		try
		{
			if (File.Exists(path))
			{
				ModSettings modSettings = JsonConvert.DeserializeObject<ModSettings>(File.ReadAllText(path));
				if (modSettings != null)
				{
					return modSettings;
				}
			}
		}
		catch (Exception ex)
		{
			text = "Could not read or parse config at '" + path + "', using defaults: " + ex.Message;
			try
			{
				File.Copy(path, path + ".bak", overwrite: true);
				text += " Your file was preserved as config.json.bak.";
			}
			catch
			{
			}
		}
		ModSettings modSettings2 = new ModSettings();
		modSettings2.LoadProblem = text;
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, JsonConvert.SerializeObject(modSettings2, Formatting.Indented));
		}
		catch (Exception ex2)
		{
			modSettings2.LoadProblem = "Could not write default config to '" + path + "': " + ex2.Message;
		}
		return modSettings2;
	}

	public void Save(string path)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
	}
}
