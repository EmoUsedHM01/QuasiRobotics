using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;
using TMPro;

namespace QuasiRobotics;

public static class PackageEditor
{
    public const int Command = 378732;
    private static readonly FieldInfo MercField = AccessTools.Field(typeof(ScreenWithShipCargo), "_merc");
    public static BasePickupItem Package { get; private set; }
    public static Mercenary Proxy { get; private set; }
    private static ArsenalScreen screen;
    private static Mercenary previousMerc;
    private static bool previousShuttle;

    public static bool Switching;
    public static bool LastShowShuttle;

    public static bool CanEdit(BasePickupItem item)
    {
        var saved = ArLoadout.Find(item);
        return Package == null && item?.Storage?.Source == ItemStorageSource.ShipCargo
            && item.StackCount == 1 && !item.Locked && saved?.Inventory != null
            && !string.IsNullOrEmpty(saved.MobClassId);
    }

    public static void Open(ArsenalScreen view, BasePickupItem item)
    {
        if (!CanEdit(item)) return;
        var saved = ArLoadout.Find(item);
        var mode = SingletonMonoBehaviour<SpaceGameMode>.Instance;
        var data = CreatureSystem.GenerateMonster(mode.Get<PerkFactory>(), mode.Get<Difficulty>(), saved.MobClassId, "");
        ArLoadout.Apply(saved, data);
        // This proxy is never added to the mercenary roster or serialized separately.
        var profile = Data.MercenaryProfiles.Records.First();
        var proxy = new Mercenary { CreatureData = data, ProfileId = profile.Id,
            AgentName = "Recovered AR", EnabledProgression = false, CurseData = new CurseData() };
        previousMerc = (Mercenary)MercField.GetValue(view);
        previousShuttle = LastShowShuttle;

        screen = view;
        Package = item;
        Proxy = proxy;

        try { ArEquipmentStorage.Enable(data.Inventory, view); Reconfigure(proxy, false); }
        catch { Close(); throw; }
    }

    private static void Reconfigure(Mercenary merc, bool shuttle)
    {
        Switching = true;
        try
        {
            screen.gameObject.SetActive(false);
            screen.Configure(merc, shuttle);
            screen.gameObject.SetActive(true);
        }
        finally { Switching = false; }
    }

    public static void Close()
    {
        if (Package == null) return;
        var oldScreen = screen;
        var oldMerc = previousMerc;
        var oldShuttle = previousShuttle;
        Finish();
        screen = oldScreen;
        try { Reconfigure(oldMerc, oldShuttle); }
        finally { screen = null; previousMerc = null; }
    }

    public static void Finish()
    {
        if (Package == null) return;
        // Inventory edits are already live on the saved component, including during autosaves.
        ArLoadout.Find(Package).Inventory = Proxy.CreatureData.Inventory;

        Proxy.CreatureData.Inventory.InitializeItemsOnFloor(null);
        Package = null;
        Proxy = null;
    }

    public static void Label(NoPlayerInventoryView view)
    {
        ((TextMeshProUGUI)AccessTools.Field(typeof(BaseInventoryView), "_agentName").GetValue(view)).text =
            Localization.Get("monster." + Proxy.CreatureData.LocalizationId + ".name");
        ((TextMeshProUGUI)AccessTools.Field(typeof(BaseInventoryView), "_agentSpec").GetValue(view)).text = "AR inventory";
    }
}
