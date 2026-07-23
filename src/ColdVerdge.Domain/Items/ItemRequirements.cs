namespace ColdVerdge.Domain.Items;

public enum CharacterAttribute
{
    None,
    Strength,
    Endurance,
    Agility,
    Perception,
    Intelligence
}

public enum WeaponCategory
{
    None,
    Pistols,
    SubmachineGuns,
    AssaultRifles,
    Shotguns,
    SniperRifles,
    MachineGuns,
    Throwables
}

public sealed record ItemRequirements(
    CharacterAttribute PrimaryAttribute,
    int RequiredAttributeValue,
    WeaponCategory WeaponCategory,
    int RequiredWeaponSkill,
    int RecommendedLevel = 0,
    IReadOnlySet<string>? AllowedProfessions = null);

public sealed record ItemDefinition(
    string ItemId,
    string EquipmentSlot,
    ItemRequirements Requirements);
