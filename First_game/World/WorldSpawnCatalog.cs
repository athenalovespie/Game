using Microsoft.Xna.Framework;

namespace First_game.World;

public static class WorldSpawnCatalog
{
	public static void RegisterRules(WorldSpawnRuleRegistry rules)
	{
		rules.Register(new WorldSpawnRule(
            "tent_demo_drop", "tent_demo", "(F)tent", chance: 1f,
            minimumCount: 2, maximumCount: 2, respawnSeconds: 0d));
        rules.Register(new WorldSpawnRule(
			"fish_spot_drop", "fish_spot", "(O)fish", chance: 1f,
			minimumCount: 1, maximumCount: 1, respawnSeconds: 45d));
		rules.Register(new WorldSpawnRule(
			"wood_pile_drop", "wood_pile", "(O)wood", chance: 1f,
			minimumCount: 1, maximumCount: 1, respawnSeconds: 60d));
		rules.Register(new WorldSpawnRule(
			"blackberry_bush_drop", "blackberry_bush", "(O)blackberry", chance: 0.85f,
			minimumCount: 1, maximumCount: 3, respawnSeconds: 90d));
	}

	public static SpawnNodeConfig[] CreateNodes()
	{
		return new[]
		{
			new SpawnNodeConfig("tent_demo", 1, new Vector2(250, 1450), new Vector2(250, 1450)),
			new SpawnNodeConfig("wood_pile", 5, new Vector2(-1000, -1000), new Vector2(1200, 1200)),
			new SpawnNodeConfig("blackberry_bush", 5, new Vector2(-600, 400), new Vector2(1200, 1600))
		};
	}
}
