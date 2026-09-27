namespace First_game.Inventory;

public sealed class InventorySaveData
{
	public int Version { get; set; }
	public int Capacity { get; set; }
	public ItemInstance[] Slots { get; set; }
}