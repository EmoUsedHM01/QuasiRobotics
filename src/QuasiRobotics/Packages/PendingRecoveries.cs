using System.Collections.Generic;
using MGSC;

namespace QuasiRobotics.Packages;

public static class PendingRecoveries
{
	private static readonly List<BasePickupItem> Items = new List<BasePickupItem>();

	public static int Count => Items.Count;

	public static void Add(BasePickupItem item)
	{
		if (item != null)
		{
			Items.Add(item);
		}
	}

	public static List<BasePickupItem> TakeAll()
	{
		List<BasePickupItem> result = new List<BasePickupItem>(Items);
		Items.Clear();
		return result;
	}

	public static void Clear()
	{
		Items.Clear();
	}

	public static void DiscardStale(string reason)
	{
		int count = Items.Count;
		if (count != 0)
		{
			Items.Clear();
			Plugin.Logger.LogWarning("Discarded " + count + " packed AR" + ((count == 1) ? "" : "s") + " that never reached a cargo hold; " + reason + ".");
		}
	}
}
