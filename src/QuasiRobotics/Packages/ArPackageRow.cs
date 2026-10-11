namespace QuasiRobotics.Packages;

public struct ArPackageRow(string itemId, string usage, string targetObjectId)
{
	public readonly string ItemId = itemId;

	public readonly string Usage = usage;

	public readonly string TargetObjectId = targetObjectId;
}
