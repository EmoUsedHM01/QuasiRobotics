using System;
using System.Collections.Generic;

namespace QuasiRobotics.Packages;

public static class ArPackageMap
{
	public const string AllySpawnUsage = "AllySpawn";

	public static Dictionary<string, string> Build(IEnumerable<ArPackageRow> rows)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		if (rows == null)
		{
			return dictionary;
		}
		foreach (ArPackageRow row in rows)
		{
			if (string.Equals(row.Usage, "AllySpawn", StringComparison.Ordinal) && !string.IsNullOrEmpty(row.ItemId) && !string.IsNullOrEmpty(row.TargetObjectId) && !dictionary.ContainsKey(row.TargetObjectId))
			{
				dictionary[row.TargetObjectId] = row.ItemId;
			}
		}
		return dictionary;
	}
}
