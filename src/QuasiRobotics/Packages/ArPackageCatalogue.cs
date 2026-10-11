using System.Collections.Generic;
using MGSC;

namespace QuasiRobotics.Packages;

public static class ArPackageCatalogue
{
	private static Dictionary<string, string> _map;

	public static Dictionary<string, string> Map
	{
		get
		{
			if (_map == null)
			{
				_map = Build();
			}
			return _map;
		}
	}

	public static bool TryGetPackageId(string mobClassId, out string itemId)
	{
		itemId = null;
		if (!string.IsNullOrEmpty(mobClassId))
		{
			return Map.TryGetValue(mobClassId, out itemId);
		}
		return false;
	}

	private static Dictionary<string, string> Build()
	{
		List<ArPackageRow> list = new List<ArPackageRow>();
		foreach (BasePickupItemRecord record in Data.Items.Records)
		{
			PlaceableDeviceRecord placeableDeviceRecord = record as PlaceableDeviceRecord;
			if (placeableDeviceRecord == null && record is CompositeItemRecord compositeItemRecord)
			{
				placeableDeviceRecord = compositeItemRecord.GetRecord<PlaceableDeviceRecord>();
			}
			if (placeableDeviceRecord != null)
			{
				list.Add(new ArPackageRow(record.Id, placeableDeviceRecord.Usage.ToString(), placeableDeviceRecord.TargetObjectId));
			}
		}
		Dictionary<string, string> dictionary = ArPackageMap.Build(list);
		if (dictionary.Count == 0)
		{
			Plugin.Logger.LogError("No recoverable AR packages found in the game's item records. Nothing will be recovered at extraction. This usually means the placeable-device records could not be read.");
			return dictionary;
		}
		Plugin.Logger.Log("Recoverable AR packages: " + dictionary.Count + " (" + string.Join(", ", new List<string>(dictionary.Values).ToArray()) + ").");
		return dictionary;
	}
}
