namespace ColdVerdge.Api.GameData;

public static class GameItemCatalog
{
    private static readonly HashSet<string> ItemIds =
        new(StringComparer.Ordinal)
        {
            "iron_ingot",
            "copper_ingot",
            "bandage",
            "ammo_9x19",
            "ammo_46x30",
            "ammo_12g",
            "ammo_545x39",
            "ammo_762x51",
            "greylock_p9",
            "kestrel_c46",
            "breach_m12",
            "frontier_a545",
            "longwatch_m762",
            "blackhorn_c46",
            "sentinel_a556",
            "ranger_a556",
            "bulldog_a762",
            "vanguard_x762",
            "longeye_m762",
            "ak",
            "raider_helmet",
            "raider_vest",
            "raider_pants",
            "raider_boots",
            "field_backpack"
        };

    private static readonly Dictionary<string, string> EquipmentSlotsByItem =
        new(StringComparer.Ordinal)
        {
            ["greylock_p9"] = "main_weapon",
            ["kestrel_c46"] = "main_weapon",
            ["breach_m12"] = "main_weapon",
            ["frontier_a545"] = "main_weapon",
            ["longwatch_m762"] = "main_weapon",
            ["blackhorn_c46"] = "main_weapon",
            ["sentinel_a556"] = "main_weapon",
            ["ranger_a556"] = "main_weapon",
            ["bulldog_a762"] = "main_weapon",
            ["vanguard_x762"] = "main_weapon",
            ["longeye_m762"] = "main_weapon",
            ["ak"] = "main_weapon",
            ["raider_helmet"] = "head",
            ["raider_vest"] = "torso",
            ["raider_pants"] = "legs",
            ["raider_boots"] = "boots",
            ["field_backpack"] = "backpack"
        };

    private static readonly Dictionary<string, (int PriceCopper, int RequiredLevel)> ShopWeapons =
        new(StringComparer.Ordinal)
        {
            ["greylock_p9"] = (35, 1),
            ["kestrel_c46"] = (85, 3),
            ["breach_m12"] = (130, 5),
            ["frontier_a545"] = (180, 7),
            ["longwatch_m762"] = (280, 10),
            ["blackhorn_c46"] = (85, 3),
            ["sentinel_a556"] = (220, 5),
            ["ranger_a556"] = (180, 7),
            ["bulldog_a762"] = (240, 8),
            ["vanguard_x762"] = (280, 9),
            ["longeye_m762"] = (360, 10),
            ["ak"] = (180, 0)
        };

    private static readonly Dictionary<string, int> FactoryPricesCopper =
        new(StringComparer.Ordinal)
        {
            ["iron_ingot"] = 100,
            ["copper_ingot"] = 180
        };

    public const int FactoryTaxBasisPoints = 1000;

    public static bool Contains(string itemId)
    {
        return ItemIds.Contains(itemId);
    }

    public static bool TryGetEquipmentSlot(
        string itemId,
        out string equipmentSlot)
    {
        return EquipmentSlotsByItem.TryGetValue(itemId, out equipmentSlot!);
    }

    public static bool IsValidEquipmentSlot(string equipmentSlot)
    {
        return EquipmentSlotsByItem.Values.Contains(
            equipmentSlot,
            StringComparer.Ordinal);
    }

    public static bool TryGetFactoryPrice(
        string itemId,
        out int unitPriceCopper)
    {
        return FactoryPricesCopper.TryGetValue(
            itemId,
            out unitPriceCopper);
    }

    public static bool TryGetShopWeapon(
        string itemId,
        out int priceCopper,
        out int requiredLevel)
    {
        if (ShopWeapons.TryGetValue(itemId, out var offer))
        {
            priceCopper = offer.PriceCopper;
            requiredLevel = offer.RequiredLevel;
            return true;
        }

        priceCopper = 0;
        requiredLevel = 0;
        return false;
    }

    public static IReadOnlyList<string> GetShopWeaponIds() =>
        ShopWeapons.Keys.OrderBy(itemId => itemId, StringComparer.Ordinal).ToArray();
}
