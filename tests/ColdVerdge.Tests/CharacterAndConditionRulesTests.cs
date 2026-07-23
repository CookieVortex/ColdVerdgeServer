using ColdVerdge.Domain.Characters;
using ColdVerdge.Domain.Entities;

namespace ColdVerdge.Tests;

public sealed class CharacterAndConditionRulesTests
{
    [Fact]
    public void RecommendedLevel_DoesNotBlockEquipment()
    {
        var player = PlayerWithAttributes();
        player.Level = 1;
        player.Strength = 30;
        player.AssaultRiflesExperience = CharacterRules.FullSkillExperience;
        var requirement = new EquipmentRequirement(
            CharacterAttribute.Strength,
            18,
            WeaponCategory.AssaultRifles,
            25,
            50);

        IReadOnlyList<string> failures =
            CharacterRules.GetBlockingEquipmentFailures(player, requirement);

        Assert.Empty(failures);
    }

    [Fact]
    public void PerceptionAndSniperSkill_AreBothServerAuthoritative()
    {
        var player = PlayerWithAttributes();
        player.Perception = 23;
        player.SniperRiflesExperience = 5_850;
        var requirement = new EquipmentRequirement(
            CharacterAttribute.Perception,
            24,
            WeaponCategory.SniperRifles,
            40,
            10);

        IReadOnlyList<string> failures =
            CharacterRules.GetBlockingEquipmentFailures(player, requirement);

        Assert.Contains(failures, failure => failure.StartsWith("Perception:", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.StartsWith("SniperRifles:", StringComparison.Ordinal));
    }

    [Fact]
    public void SpecializedWeapon_RequiresAllowedProfession()
    {
        var player = PlayerWithAttributes();
        player.Strength = 30;
        player.MachineGunsExperience = CharacterRules.FullSkillExperience;
        player.ProfessionId = "miner";
        var requirement = new EquipmentRequirement(
            CharacterAttribute.Strength,
            30,
            WeaponCategory.MachineGuns,
            75,
            15,
            new HashSet<string>(new[] { "mercenary", "stormtrooper" }, StringComparer.Ordinal));

        IReadOnlyList<string> failures =
            CharacterRules.GetBlockingEquipmentFailures(player, requirement);

        Assert.Single(failures);
        Assert.StartsWith("Profession:", failures[0]);
    }

    [Fact]
    public void ItemInstance_DefaultsToFullCondition()
    {
        var instance = new PlayerItemInstance();

        Assert.Equal(100, instance.ConditionPercent);
    }

    private static Player PlayerWithAttributes() => new()
    {
        Level = 1,
        Strength = 10,
        Endurance = 10,
        Agility = 10,
        Perception = 10,
        Intelligence = 10
    };
}
