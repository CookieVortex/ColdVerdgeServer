using ColdVerdge.Domain.Characters;

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
            "frontier_a545",
            "blackhorn_c46",
            "sentinel_a556",
            "ranger_a556",
            "bulldog_a762",
            "vanguard_x762",
            "longeye_m762",
            "ironclad_mg762",
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
            ["frontier_a545"] = "main_weapon",
            ["blackhorn_c46"] = "main_weapon",
            ["sentinel_a556"] = "main_weapon",
            ["ranger_a556"] = "main_weapon",
            ["bulldog_a762"] = "main_weapon",
            ["vanguard_x762"] = "main_weapon",
            ["longeye_m762"] = "main_weapon",
            ["ironclad_mg762"] = "main_weapon",
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
            ["frontier_a545"] = (180, 7),
            ["blackhorn_c46"] = (85, 3),
            ["sentinel_a556"] = (220, 5),
            ["ranger_a556"] = (180, 7),
            ["bulldog_a762"] = (240, 8),
            ["vanguard_x762"] = (280, 9),
            ["longeye_m762"] = (360, 10),
            ["ironclad_mg762"] = (720, 15),
            ["ak"] = (180, 0)
        };

    private static readonly Dictionary<string, EquipmentRequirement> EquipmentRequirements =
        new(StringComparer.Ordinal)
        {
            ["greylock_p9"] = new(CharacterAttribute.Agility, 10, WeaponCategory.Pistols, 0, 1),
            ["blackhorn_c46"] = new(CharacterAttribute.Agility, 12, WeaponCategory.SubmachineGuns, 10, 3),
            ["sentinel_a556"] = new(CharacterAttribute.Strength, 18, WeaponCategory.AssaultRifles, 25, 5),
            ["ranger_a556"] = new(CharacterAttribute.Strength, 20, WeaponCategory.AssaultRifles, 35, 7),
            ["frontier_a545"] = new(CharacterAttribute.Strength, 20, WeaponCategory.AssaultRifles, 35, 7),
            ["bulldog_a762"] = new(CharacterAttribute.Strength, 24, WeaponCategory.AssaultRifles, 45, 8),
            ["vanguard_x762"] = new(CharacterAttribute.Strength, 26, WeaponCategory.AssaultRifles, 55, 9),
            ["longeye_m762"] = new(CharacterAttribute.Perception, 24, WeaponCategory.SniperRifles, 40, 10),
            ["ak"] = new(CharacterAttribute.Strength, 18, WeaponCategory.AssaultRifles, 25, 6),
            ["ironclad_mg762"] = new(
                CharacterAttribute.Strength,
                30,
                WeaponCategory.MachineGuns,
                75,
                15,
                new HashSet<string>(new[] { "mercenary", "stormtrooper" }, StringComparer.Ordinal))
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

    public static bool TryGetEquipmentRequirement(
        string itemId,
        out EquipmentRequirement requirement) =>
        EquipmentRequirements.TryGetValue(itemId, out requirement!);

    public static IReadOnlyList<string> GetShopWeaponIds() =>
        ShopWeapons.Keys.OrderBy(itemId => itemId, StringComparer.Ordinal).ToArray();

    public static bool TracksCondition(string itemId) =>
        EquipmentSlotsByItem.ContainsKey(itemId);

    public static string GetMarketCategory(string itemId)
    {
        if (EquipmentSlotsByItem.TryGetValue(itemId, out string? slot))
            return slot == "main_weapon" ? "weapons" : "armor";
        if (itemId.StartsWith("ammo_", StringComparison.Ordinal))
            return "ammo";
        if (itemId == "bandage")
            return "medical";
        return FactoryPricesCopper.ContainsKey(itemId) ? "resources" : string.Empty;
    }
}
