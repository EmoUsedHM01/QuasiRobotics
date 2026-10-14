using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics;

public static class Plugin
{
	public const string HarmonyId = "QuasiRobotics";

	public static readonly Logger Logger = new Logger();

	public static readonly ConfigDirectories ConfigDirectories = new ConfigDirectories();

	public static ModSettings Settings { get; private set; }

	[Hook(ModHookType.BeforeBootstrap)]
	public static void BeforeBootstrap(IModContext context)
	{
		Settings = ModSettings.LoadOrCreate(ConfigDirectories.ConfigPath);
		RobotUpgrades.SlotsPerSet = Settings.RobotSlotsPerUpgrade;
		if (!string.IsNullOrEmpty(Settings.LoadProblem))
		{
			Logger.LogWarning(Settings.LoadProblem);
		}
		try
		{
			Harmony harmony = new Harmony("QuasiRobotics");
			Type[] typesFromAssembly = AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly);
			foreach (Type type in typesFromAssembly)
			{
				try
				{
					harmony.CreateClassProcessor(type).Patch();
				}
				catch (Exception ex)
				{
					Logger.LogError("Patch class " + type.Name + " failed to apply; the feature it supports is disabled for this session.");
					Logger.LogException(ex);
				}
			}
			foreach (MethodBase patchedMethod in harmony.GetPatchedMethods())
			{
				Logger.Log("Patched " + patchedMethod.DeclaringType.Name + "." + patchedMethod.Name + ".");
			}
			Logger.Log("Armed. " + DescribeBuild() + " Config at " + ConfigDirectories.ConfigPath + ".");
		}
		catch (Exception ex2)
		{
			Logger.LogError("Harmony patching failed; QuasiRobotics is disabled for this session.");
			Logger.LogException(ex2);
		}
	}

	[Hook(ModHookType.AfterConfigsLoaded)]
	public static void AfterConfigsLoaded(IModContext context)
	{
		try
		{
			RoboticsModule.RegisterPerk(Data.MagnumPerks);
			DropPodsKeybind.Register(Data.Keybinding);
		}
		catch (Exception ex)
		{
			Logger.LogError("The Robotics room could not be registered.");
			Logger.LogException(ex);
		}
		VisibleAugmentsCompat.TryPatch();
	}

	[Hook(ModHookType.AfterBootstrap)]
	public static void AfterBootstrap(IModContext context)
	{
		try
		{
			DropPodsKeybind.EnsureKey(SingletonMonoBehaviour<InputController>.Instance);
		}
		catch (Exception ex)
		{
			Logger.LogError("The Drop Pods key could not be added; Drop Pods can still be called from Mission Control.");
			Logger.LogException(ex);
		}
		try
		{
			McmIntegration.Register();
		}
		catch (Exception ex)
		{
			Logger.LogError("Mod Configuration Menu registration failed; settings stay in config.json.");
			Logger.LogException(ex);
		}
		// Again here in case Visible Augments loaded after this mod.
		VisibleAugmentsCompat.TryPatch();
	}

	private static string DescribeBuild()
	{
		try
		{
			string location = typeof(Plugin).Assembly.Location;
			if (string.IsNullOrEmpty(location))
			{
				return "Build unknown (no assembly path).";
			}
			return "Build " + File.GetLastWriteTime(location).ToString("yyyy-MM-dd HH:mm:ss") + ".";
		}
		catch (Exception ex)
		{
			return "Build unknown (" + ex.GetType().Name + ").";
		}
	}
}
