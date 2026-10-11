using System.Collections.Generic;
using MGSC;

namespace QuasiRobotics.Packages;

public class ArLoadoutComponent : PickupItemComponent
{
	[Save]
	public string MobClassId;

	[Save]
	public Inventory Inventory;

	[Save]
	public Dictionary<string, string> Augmentations = new Dictionary<string, string>();

	[Save]
	public Dictionary<string, ImplantSocketData> WoundSlots = new Dictionary<string, ImplantSocketData>();

	[Save]
	public Dictionary<string, Perk> ImplantActives = new Dictionary<string, Perk>();
}
