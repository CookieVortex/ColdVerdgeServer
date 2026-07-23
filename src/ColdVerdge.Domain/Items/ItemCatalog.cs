namespace ColdVerdge.Domain.Items;

public static class ItemCatalog
{
    private static readonly IReadOnlyDictionary<string, ItemDefinition> Items =
        new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
        {
            ["greylock_p9"] = Weapon("greylock_p9", CharacterAttribute.Agility, 10, WeaponCategory.Pistols, 0, 1),
            ["blackhorn_c46"] = Weapon("blackhorn_c46", CharacterAttribute.Agility, 12, WeaponCategory.SubmachineGuns, 10, 3),
            ["kestrel_c46"] = Weapon("kestrel_c46", CharacterAttribute.Agility, 12, WeaponCategory.SubmachineGuns, 10, 3),
            ["sentinel_a556"] = Weapon("sentinel_a556", CharacterAttribute.Strength, 18, WeaponCategory.AssaultRifles, 25, 5),
            ["ranger_a556"] = Weapon("ranger_a556", CharacterAttribute.Strength, 20, WeaponCategory.AssaultRifles, 35, 7),
            ["frontier_a545"] = Weapon("frontier_a545", CharacterAttribute.Strength, 20, WeaponCategory.AssaultRifles, 35, 7),
            ["bulldog_a762"] = Weapon("bulldog_a762", CharacterAttribute.Strength, 24, WeaponCategory.AssaultRifles, 45, 8),
            ["vanguard_x762"] = Weapon("vanguard_x762", CharacterAttribute.Strength, 26, WeaponCategory.AssaultRifles, 55, 9),
            ["breach_m12"] = Weapon("breach_m12", CharacterAttribute.Strength, 18, WeaponCategory.Shotguns, 25, 5),
            ["longeye_m762"] = Weapon("longeye_m762", CharacterAttribute.Perception, 24, WeaponCategory.SniperRifles, 40, 10),
            ["longwatch_m762"] = Weapon("longwatch_m762", CharacterAttribute.Perception, 24, WeaponCategory.SniperRifles, 40, 10),
            ["ironclad_mg762"] = Weapon(
                "ironclad_mg762",
                CharacterAttribute.Strength,
                30,
                WeaponCategory.MachineGuns,
                75,
                15,
                "mercenary",
                "stormtrooper"),
            ["ak"] = Weapon("ak", CharacterAttribute.Strength, 18, WeaponCategory.AssaultRifles, 25, 6),
            ["raider_helmet"] = Equipment("raider_helmet", "head"),
            ["raider_vest"] = Equipment("raider_vest", "torso"),
            ["raider_pants"] = Equipment("raider_pants", "legs"),
            ["raider_boots"] = Equipment("raider_boots", "boots"),
            ["field_backpack"] = Equipment("field_backpack", "backpack")
        };

    public static bool TryGet(string itemId, out ItemDefinition definition) =>
        Items.TryGetValue(itemId.Trim().ToLowerInvariant(), out definition!);

    private static ItemDefinition Weapon(
        string id,
        CharacterAttribute attribute,
        int attributeValue,
        WeaponCategory category,
        int skill,
        int level,
        params string[] professions) =>
        new(
            id,
            "main_weapon",
            new ItemRequirements(
                attribute,
                attributeValue,
                category,
                skill,
                level,
                professions.Length == 0
                    ? null
                    : new HashSet<string>(professions, StringComparer.OrdinalIgnoreCase)));

    private static ItemDefinition Equipment(string id, string slot) =>
        new(id, slot, new ItemRequirements(CharacterAttribute.None, 0, WeaponCategory.None, 0));
}
