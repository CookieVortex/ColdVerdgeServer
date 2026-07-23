using ColdVerdge.Domain.Characters;
using ColdVerdge.Domain.Entities;
using ColdVerdge.Domain.Items;

namespace ColdVerdge.Tests;

public sealed class CharacterRulesTests
{
    [Fact]
    public void IntuitionIsNotPartOfCombatRating()
    {
        int rating = CharacterRules.CalculateCombatRating(
            level: 1,
            maxHealth: 100,
            armor: 0,
            damage: 10,
            criticalChancePercent: 5,
            carryCapacity: 30,
            moveSpeed: 10,
            strength: 10,
            endurance: 10,
            agility: 10,
            perception: 10,
            intelligence: 10);

        Assert.Equal(1010, rating);
    }

    [Fact]
    public void RecommendedLevelNeverBlocksEquipment()
    {
        var progress = new PlayerProgress
        {
            Level = 1,
            Strength = 18,
            AssaultRifles = 25
        };
        var requirements = new ItemRequirements(
            CharacterAttribute.Strength,
            18,
            WeaponCategory.AssaultRifles,
            25,
            RecommendedLevel: 99);

        Assert.Empty(CharacterRules.ValidateEquipment(progress, requirements));
    }

    [Fact]
    public void AttributeAndWeaponSkillAreHardRequirements()
    {
        var progress = new PlayerProgress
        {
            Strength = 17,
            AssaultRifles = 24
        };
        var requirements = new ItemRequirements(
            CharacterAttribute.Strength,
            18,
            WeaponCategory.AssaultRifles,
            25);

        Assert.Equal(2, CharacterRules.ValidateEquipment(progress, requirements).Count);
    }

    [Fact]
    public void SpecializedWeaponAcceptsOnlyConfiguredProfession()
    {
        var progress = new PlayerProgress
        {
            Strength = 30,
            MachineGuns = 75,
            ProfessionId = "miner"
        };
        var requirements = new ItemRequirements(
            CharacterAttribute.Strength,
            30,
            WeaponCategory.MachineGuns,
            75,
            AllowedProfessions: new HashSet<string>(
                ["mercenary", "stormtrooper"],
                StringComparer.OrdinalIgnoreCase));

        Assert.Single(CharacterRules.ValidateEquipment(progress, requirements));
    }
}
