using ColdVerdge.Domain.Entities;

namespace ColdVerdge.Domain.Characters;

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
    MachineGuns
}

public sealed record EquipmentRequirement(
    CharacterAttribute PrimaryAttribute,
    int RequiredAttributeValue,
    WeaponCategory WeaponCategory,
    int RequiredWeaponSkill,
    int RecommendedLevel,
    IReadOnlySet<string>? AllowedProfessionIds = null);

public static class CharacterRules
{
    public const int FullSkillExperience = 15_000;
    public const long ProfessionMasterySeconds = 100L * 60L * 60L;

    public static int SkillLevel(int experience) =>
        Math.Clamp((int)Math.Floor(Math.Clamp(experience, 0, FullSkillExperience) * 100d / FullSkillExperience), 0, 100);

    public static int CombatRating(
        int level, int strength, int endurance, int agility, int perception, int intelligence,
        int maxHealth, int armor, int averageDamage, double criticalChancePercent, double carryCapacity) =>
        Math.Max(1, (int)Math.Round(
            Math.Max(1, level) * 35d +
            Math.Max(0, strength) * 8d +
            Math.Max(0, endurance) * 8d +
            Math.Max(0, agility) * 7d +
            Math.Max(0, perception) * 7d +
            Math.Max(0, intelligence) * 5d +
            Math.Max(0, maxHealth) * .7d +
            Math.Max(0, armor) * 6d +
            Math.Max(0, averageDamage) * 12d +
            Math.Max(0d, criticalChancePercent) * 8d +
            Math.Max(0d, carryCapacity) * 1.5d));

    public static IReadOnlyList<string> GetBlockingEquipmentFailures(Player player, EquipmentRequirement requirement)
    {
        var failures = new List<string>();
        int attribute = requirement.PrimaryAttribute switch
        {
            CharacterAttribute.Strength => player.Strength,
            CharacterAttribute.Endurance => player.Endurance,
            CharacterAttribute.Agility => player.Agility,
            CharacterAttribute.Perception => player.Perception,
            CharacterAttribute.Intelligence => player.Intelligence,
            _ => int.MaxValue
        };
        if (attribute < requirement.RequiredAttributeValue)
            failures.Add($"{requirement.PrimaryAttribute}: {attribute}/{requirement.RequiredAttributeValue}");

        int skillExperience = requirement.WeaponCategory switch
        {
            WeaponCategory.Pistols => player.PistolsExperience,
            WeaponCategory.SubmachineGuns => player.SubmachineGunsExperience,
            WeaponCategory.AssaultRifles => player.AssaultRiflesExperience,
            WeaponCategory.Shotguns => player.ShotgunsExperience,
            WeaponCategory.SniperRifles => player.SniperRiflesExperience,
            WeaponCategory.MachineGuns => player.MachineGunsExperience,
            _ => FullSkillExperience
        };
        int skillLevel = SkillLevel(skillExperience);
        if (skillLevel < requirement.RequiredWeaponSkill)
            failures.Add($"{requirement.WeaponCategory}: {skillLevel}/{requirement.RequiredWeaponSkill}");

        if (requirement.AllowedProfessionIds is { Count: > 0 } &&
            !requirement.AllowedProfessionIds.Contains(player.ProfessionId ?? string.Empty))
            failures.Add($"Profession: {string.Join(" or ", requirement.AllowedProfessionIds)}");

        // RecommendedLevel is deliberately not checked: it is guidance, never a hard block.
        return failures;
    }
}
