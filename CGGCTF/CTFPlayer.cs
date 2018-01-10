using TShockAPI;

namespace CGGCTF
{
  public class CtfPlayer
  {
    public CtfClass Class;
    public PlayerData Data;
    public bool Dead;
    public bool Online;
    public CtfTeam Team;

    public CtfPlayer()
    {
      Team = CtfTeam.None;
      Class = null;
      Online = true;
      Dead = false;
      Data = new PlayerData(null);
    }

    public bool PickedClass => Class != null;
  }
}