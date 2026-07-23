using ColdVerdge.Domain.Entities;
using ColdVerdge.Domain.Items;

namespace ColdVerdge.Domain.Characters;

public static class CharacterRules
{
    public static int CalculateCombatRating(
        int level,
        int maxHealth,
        int armor,
        int damage,
        decimal criticalChancePercent,
        decimal carryCapacity,
        decimal moveSpeed,
        int strength,
        int endurance,
        int agility,
        int perception,
        int intelligence)
    {
        decimal score =
            Math.Max(1, level) * 35m +
            Math.Max(0, strength) * 8m +
            Math.Max(0, endurance) * 8m +
            Math.Max(0, agility) * 7m +
            Math.Max(0, perception) * 7m +
            Math.Max(0, intelligence) * 5m +
            Math.Max(0, maxHealth) * 0.7m +
            Math.Max(0, armor) * 6m +
            Math.Max(0, damage) * 12m +
            Math.Max(0, criticalChancePercent) * 8m +
            Math.Max(0, carryCapacity) * 1.5m +
            Math.Max(0, moveSpeed) * 35m;

        return Math.Max(1, (int)Math.Round(score, MidpointRounding.AwayFromZero));
    }

    public static IReadOnlyList<string> ValidateEquipment(
        PlayerProgress progress,
        ItemRequirements requirements)
    {
        var failures = new List<string>();
        int attributeValue = requirements.PrimaryAttribute switch
        {
            CharacterAttribute.Strength => progress.Strength,
            CharacterAttribute.Endurance => progress.Endurance,
            CharacterAttribute.Agility => progress.Agility,
            CharacterAttribute.Perception => progress.Perception,
            CharacterAttribute.Intelligence => progress.Intelligence,
            _ => int.MaxValue
        };

        if (attributeValue < requirements.RequiredAttributeValue)
            failures.Add($"{requirements.PrimaryAttribute} {requirements.RequiredAttributeValue} required.");

        int skillValue = requirements.WeaponCategory switch
        {
            WeaponCategory.Pistols => progress.Pistols,
            WeaponCategory.SubmachineGuns => progress.SubmachineGuns,
            WeaponCategory.AssaultRifles => progress.AssaultRifles,
            WeaponCategory.Shotguns => progress.Shotguns,
            WeaponCategory.SniperRifles => progress.SniperRifles,
            WeaponCategory.MachineGuns => progress.MachineGuns,
            WeaponCategory.Throwables => progress.Throwables,
            _ => int.MaxValue
        };

        if (skillValue < requirements.RequiredWeaponSkill)
            failures.Add($"{requirements.WeaponCategory} {requirements.RequiredWeaponSkill} required.");

        if (requirements.AllowedProfessions is { Count: > 0 } &&
            !requirements.AllowedProfessions.Contains(progress.ProfessionId))
        {
            failures.Add($"Profession must be one of: {string.Join(", ", requirements.AllowedProfessions)}.");
        }

        // RecommendedLevel is deliberately not validated: it is advisory.
        return failures;
    }
}
