using Terraria;
using Terraria.Localization;
using TShockAPI;

namespace CGGCTF
{
  public class CtfPvpControl
  {
    public bool Enforced = true;
    private readonly TeamColor[] _playerColor = new TeamColor[256];
    private readonly bool[] _playerPvP = new bool[256];

    public void SetTeam(int index, TeamColor color)
    {
      var tplr = TShock.Players[index];
      _playerColor[index] = color;
      Main.player[index].team = (int) _playerColor[index];
      if (!tplr.HasPermission(CtfPermissions.IgnoreTempgroup))
        if (color == TeamColor.Red)
          tplr.tempGroup = TShock.Groups.GetGroupByName("red");
        else if (color == TeamColor.Blue)
          tplr.tempGroup = TShock.Groups.GetGroupByName("blue");
      NetMessage.SendData((int) PacketTypes.PlayerTeam, -1, -1, NetworkText.Empty, index);
    }

    public void PlayerTeamHook(object sender, GetDataHandlers.PlayerTeamEventArgs e)
    {
      if (!Enforced)
        return;
      e.Handled = true;
      var index = e.PlayerId;
      Main.player[index].team = (int) _playerColor[index];
      NetMessage.SendData((int) PacketTypes.PlayerTeam, -1, -1, NetworkText.Empty, index);
    }

    public void SetPvP(int index, bool pvp)
    {
      _playerPvP[index] = pvp;
      Main.player[index].hostile = _playerPvP[index];
      NetMessage.SendData((int) PacketTypes.TogglePvp, -1, -1, NetworkText.Empty, index);
    }

    public void TogglePvPHook(object sender, GetDataHandlers.TogglePvpEventArgs e)
    {
      if (!Enforced)
        return;
      e.Handled = true;
      var index = e.PlayerId;
      Main.player[index].hostile = _playerPvP[index];
      NetMessage.SendData((int) PacketTypes.TogglePvp, -1, -1, NetworkText.Empty, index);
    }
  }
}