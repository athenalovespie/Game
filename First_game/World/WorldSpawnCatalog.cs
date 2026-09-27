using Microsoft.Xna.Framework;

namespace First_game.World;

public static class WorldSpawnCatalog
{
	public static void RegisterRules(WorldSpawnRuleRegistry rules)
	{
		rules.Register(new WorldSpawnRule(
			"fish_spot_drop", "fish_spot", "(O)fish", chance: 1f,
			minimumCount: 1, maximumCount: 1, respawnSeconds: 45d));
		rules.Register(new WorldSpawnRule(
			"wood_pile_drop", "wood_pile", "(O)wood", chance: 1f,
			minimumCount: 1, maximumCount: 1, respawnSeconds: 60d));
		rules.Register(new WorldSpawnRule(
			"copper_vein_drop", "copper_vein", "(O)copper_ore", chance: 0.85f,
			minimumCount: 1, maximumCount: 2, respawnSeconds: 90d));
	}

	public static SpawnNodeConfig[] CreateNodes()
	{
		return new[]
		{
			new SpawnNodeConfig("fish_spot", 5, new Vector2(-1000, -1000), new Vector2(1200, 1200)),
			new SpawnNodeConfig("wood_pile", 5, new Vector2(-1000, -1000), new Vector2(1200, 1200)),
			new SpawnNodeConfig("copper_vein", 1, new Vector2(700, 1100), new Vector2(700, 1100))
		};
	}
}