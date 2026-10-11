using System.Collections.Generic;
using MGSC;

namespace QuasiRobotics.Packages;

public static class ArLoadout
{
	public static ArLoadoutComponent Find(BasePickupItem item)
	{
		if (!(item is PickupItem pickupItem))
		{
			return null;
		}
		foreach (PickupItemComponent component in pickupItem.Components)
		{
			if (component is ArLoadoutComponent result)
			{
				return result;
			}
		}
		return null;
	}

	public static ArLoadoutComponent Capture(CreatureData data)
	{
		return new ArLoadoutComponent
		{
			MobClassId = data.MobClassId,
			Inventory = data.Inventory,
			Augmentations = ((data.AugmentationMap == null) ? new Dictionary<string, string>() : new Dictionary<string, string>(data.AugmentationMap)),
			WoundSlots = ((data.WoundSlotMap == null) ? new Dictionary<string, ImplantSocketData>() : new Dictionary<string, ImplantSocketData>(data.WoundSlotMap)),
			ImplantActives = ((data.ImplantActivesMap == null) ? new Dictionary<string, Perk>() : new Dictionary<string, Perk>(data.ImplantActivesMap))
		};
	}

	public static void Apply(ArLoadoutComponent loadout, CreatureData target)
	{
		if (loadout.Inventory != null)
		{
			target.Inventory = loadout.Inventory;
			// Already initialized by capture or LoadFromJSON; do not subscribe twice.
		}
		if (loadout.Augmentations != null)
		{
			target.AugmentationMap = new Dictionary<string, string>(loadout.Augmentations);
		}
		if (loadout.WoundSlots != null && loadout.WoundSlots.Count > 0)
		{
			target.WoundSlotMap = new Dictionary<string, ImplantSocketData>(loadout.WoundSlots);
		}
		if (loadout.ImplantActives != null && loadout.ImplantActives.Count > 0)
		{
			target.ImplantActivesMap = new Dictionary<string, Perk>(loadout.ImplantActives);
		}
		AugmentationSystem.ConfigureImplicitEffects(target);
	}
}
