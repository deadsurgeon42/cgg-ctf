using System;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace CGGCTF
{
  public sealed class SpectatorManager : IDisposable
  {
    private readonly TerrariaPlugin _registrator;

    public SpectatorManager(TerrariaPlugin registrator)
    {
      _registrator = registrator;
    }

    public void Register()
    {
      ServerApi.Hooks.NetGetData.Register(_registrator, BlockVisibleActions);
      ServerApi.Hooks.NetSendBytes.Register(_registrator, BlockSentData);

      ServerApi.Hooks.ServerLeave.Register(_registrator, RemoveLeavingPlayerFromIgnoredList);
    }

    public void Dispose()
    {
      ServerApi.Hooks.NetGetData.Deregister(_registrator, BlockVisibleActions);
      ServerApi.Hooks.NetSendBytes.Deregister(_registrator, BlockSentData);

      ServerApi.Hooks.ServerLeave.Deregister(_registrator, RemoveLeavingPlayerFromIgnoredList);
    }

    public void StartSpectating(int index)
    {
      _ignoredPlayerIndexes[index] = true;
      MakePlayerDisappear(index);
      RemoveItemsFromSight(index);
    }

    public void StopSpectating(int index)
    {
      _ignoredPlayerIndexes[index] = false;
      MakePlayerReappear(index);
    }

    public bool IsSpectating(int index) => _ignoredPlayerIndexes[index];

    private static void RemoveItemsFromSight(int index)
    {
      foreach (var item in Main.item.Where(i => i != null && i.type != 0))
        NetMessage.SendData((int) PacketTypes.UpdateItemDrop, index, number: item.whoAmI);
    }

    private void BlockVisibleActions(GetDataEventArgs args)
    {
      if (args.Index >= Main.maxPlayers || !_ignoredPlayerIndexes[args.Msg.whoAmI])
        return;

      var player = TShock.Players.ElementAtOrDefault(args.Msg.whoAmI);

      switch (args.MsgID)
      {
        case PacketTypes.PlaceObject:
        case PacketTypes.PlaceItemFrame:
        case PacketTypes.PlaceTileEntity:
        case PacketTypes.Tile:
        case PacketTypes.TileKill:
        case PacketTypes.PaintWall:
        case PacketTypes.PaintTile:

          player?.SendErrorMessage("You cannot edit tiles while spectating.");
          player?.SendTileSquare(player.TileX, player.TileY);
          args.Handled = true;
          break;

        case PacketTypes.ChestGetContents:
        case PacketTypes.ChestItem:
        case PacketTypes.ChestOpen:
        case PacketTypes.ChestName:
        case PacketTypes.ChestUnlock:

          player?.SendErrorMessage("You cannot edit chests while spectating.");
          args.Handled = true;
          break;

        case PacketTypes.ProjectileNew:
        case PacketTypes.ProjectileDestroy:
        case PacketTypes.ItemOwner:

          args.Handled = true;
          break;

        case PacketTypes.ItemDrop:
          player?.SendErrorMessage("You cannot drop items while spectating.");
          args.Handled = true;
          break;
      }
    }

    private void BlockSentData(SendBytesEventArgs args)
    {
      if (ShouldDropPacket(args.Buffer, args.Offset, args.Count, (byte) args.Socket.Id))
        args.Handled = true;
    }

    private readonly bool[] _ignoredPlayerIndexes = new bool[Main.maxPlayers];

    private void RemoveLeavingPlayerFromIgnoredList(LeaveEventArgs args)
    {
      if (_ignoredPlayerIndexes[args.Who])
        _ignoredPlayerIndexes[args.Who] = false;
    }

    // should love quake
    private bool ShouldDropPacket(byte[] buffer, int offset, int count, byte receiver)
    {
      if (count < 3) return false;
      var bOffset = offset + 3;

      switch (buffer[offset + 2])
      {
        case 4:
        case 5:
        case 12:
        case 13:
        case 14:
        case 16:
        case 30:
        case 35:
        case 36:
        case 40:
        case 41:
        case 42:
        case 43:
        case 45:
        case 50:
        case 51:
        case 55:
        case 58:
        case 62:
        case 66:
        case 80:
        case 84:
        case 96:
        case 99:
        case 102:
        case 115:
        case 117:
        case 118:
          if (count <= bOffset)
            return false;
          if (!ShouldSeeUser(buffer[bOffset], receiver))
            return true;
          break;
        case 22:
        case 24:
        case 29:
        case 70:
          if (count <= bOffset + 2)
            return false;
          if (!ShouldSeeUser(buffer[bOffset + 2], receiver))
            return true;
          break;
        case 20:
        case 61:
          if (count <= bOffset + 1)
            return false;
          if (!ShouldSeeUser((byte) BitConverter.ToUInt16(buffer, bOffset), receiver))
            return true;
          break;
        case 23:
          if (count < bOffset + 19)
            return false;
          if (!ShouldSeeUser((byte) BitConverter.ToUInt16(buffer, bOffset + 18), receiver))
            return true;
          break;
        case 27:
          if (count < bOffset + 24)
            return false;
          if (!ShouldSeeUser(buffer[bOffset + 24], receiver))
            return true;
          break;
        case 65:
          if (count < bOffset + 2)
            return false;
          BitsByte flag = buffer[bOffset];
          if (flag[0] || flag[2])
          {
            if (!ShouldSeeUser((byte) BitConverter.ToUInt16(buffer, bOffset + 1), receiver))
              return true;
          }

          break;
        case 108:
          if (count < bOffset + 14)
            return false;
          if (!ShouldSeeUser(buffer[bOffset + 14], receiver))
            return true;
          break;
        case 110:
          if (count < bOffset + 4)
            return false;
          if (!ShouldSeeUser(buffer[bOffset + 4], receiver))
            return true;
          break;
        
        case 21:
          if (_ignoredPlayerIndexes[receiver])
            return true;
          break;
      }

      return false;
    }

    private bool ShouldSeeUser(byte sender, byte receiver)
    {
      if (sender >= Main.maxPlayers || sender == receiver)
        return true;

      return !_ignoredPlayerIndexes[sender];
    }

    private static void MakePlayerDisappear(int index)
    {
      NetMessage.SendData(14, -1, index, null, index);
    }

    private static void MakePlayerReappear(int index)
    {
      NetMessage.SendData(14, -1, index, null, index, 1);
      NetMessage.SendData(4, -1, index, null, index);
      NetMessage.SendData(13, -1, index, null, index);
      NetMessage.SendData(16, -1, index, null, index);
      NetMessage.SendData(30, -1, index, null, index);
      NetMessage.SendData(45, -1, index, null, index);
      NetMessage.SendData(42, -1, index, null, index);
      NetMessage.SendData(50, -1, index, null, index);

      for (var i = 0; i < 59; ++i)
        NetMessage.SendData(5, -1, index, null, index, i, Main.player[index].inventory[i].prefix);

      for (var i = 0; i < Main.player[index].armor.Length; ++i)
        NetMessage.SendData(5, -1, index, null, index,
          59 + i, Main.player[index].armor[i].prefix);

      for (var i = 0; i < Main.player[index].dye.Length; ++i)
        NetMessage.SendData(5, -1, index, null, index,
          58 + Main.player[index].armor.Length + 1 + i, Main.player[index].dye[i].prefix);

      for (var i = 0; i < Main.player[index].miscEquips.Length; ++i)
        NetMessage.SendData(5, -1, index, null, index,
          58 + Main.player[index].armor.Length + Main.player[index].dye.Length + 1 + i,
          Main.player[index].miscEquips[i].prefix);

      for (var i = 0; i < Main.player[index].miscDyes.Length; ++i)
        NetMessage.SendData(5, -1, index, null, index,
          58 + Main.player[index].armor.Length + Main.player[index].dye.Length + Main.player[index].miscEquips.Length +
          1 + i,
          Main.player[index].miscDyes[i].prefix);
    }
  }
}