using TShockAPI;

namespace CGGCTF
{
  public class CtfClass
  {
    public CtfClass()
    {
      Id = -1;
      Name = null;
      Description = null;
      Hp = 100;
      Mana = 20;
      Inventory = new NetItem[NetItem.MaxInventory];
      for (var i = 0; i < NetItem.MaxInventory; ++i)
        Inventory[i] = new NetItem(0, 0, 0);
      Price = 0;
      Hidden = true;
      Sell = false;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int Hp { get; set; }
    public int Mana { get; set; }
    public NetItem[] Inventory { get; set; }
    public int Price { get; set; }
    public bool Hidden { get; set; }
    public bool Sell { get; set; }

    public void CopyToPlayerData(PlayerData pd)
    {
      pd.health = Hp;
      pd.maxHealth = Hp;
      pd.mana = Mana;
      pd.maxMana = Mana;
      pd.inventory = Inventory;
    }

    public void CopyFromPlayerData(PlayerData pd)
    {
      Hp = pd.maxHealth;
      Mana = pd.maxMana;
      Inventory = pd.inventory;
    }
  }
}