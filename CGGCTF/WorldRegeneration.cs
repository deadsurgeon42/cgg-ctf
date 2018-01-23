using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.World.Generation;
using TerrariaApi.Server;
using TShockAPI;

namespace CGGCTF
{
  public static class WorldRegeneration
  {
    private static volatile bool _working;

    public static void RegenerateWorldInBackground(TerrariaPlugin plugin, bool sendStatusUpdates = true)
      => Task.Run(() => RegenerateWorld(plugin, sendStatusUpdates));

    public static void RegenerateWorld(TerrariaPlugin plugin, bool sendStatusUpdates = true)
    {
      if (_working)
        throw new InvalidOperationException("The world is already being generated.");

      try
      {
        _working = true;

        TSPlayer.All.SendMessage("Starting world regeneration.\n" +
                                 "The world will be unresponsive during the process.\n" +
                                 "Moving around is not recommended.", Color.Orange);

        ServerApi.Hooks.NetGetData.Register(plugin, DiscardWorldModificationPackets);
        ServerApi.Hooks.NetSendData.Register(plugin, BlockIncompletePackets);
        ServerApi.Hooks.WorldSave.Register(plugin, PreventWorldSave);

        for (var i = 20; i < Main.maxTilesX - 20; ++i)
        for (var j = 20; j < Main.maxTilesY - 20; ++j)
          Main.tile[i, j] = new Tile();

        var progress = new GenerationProgress();

        if (sendStatusUpdates)
          Task.Run(() => StatusMessageSender(progress));

        WorldGen.generateWorld(new Random().Next(), progress);

        TSPlayer.All.SendMessage("The world has been regenerated successfully.\n" +
                                 "All players will be teleported to spawn.", Color.LimeGreen);
      }
      catch (Exception e)
      {
        TSPlayer.All.SendErrorMessage("There has been an error during world regeneration.\n" +
                                      "The world is in a corrupted state.\n" +
                                      "All players will be teleported to spawn.");

        TShock.Log.ConsoleError(e.ToString());
      }
      finally
      {
        ServerApi.Hooks.NetSendData.Deregister(plugin, BlockIncompletePackets);
        ServerApi.Hooks.WorldSave.Deregister(plugin, PreventWorldSave);

        TShock.Utils.SaveWorld();

        foreach (var client in Netplay.Clients)
          client.ResetSections();

        TSPlayer.All.Teleport(Main.spawnTileX * 16, (Main.spawnTileY * 16) - 48);

        _working = false;
      }
    }

    private static void DiscardWorldModificationPackets(GetDataEventArgs args)
    {
      if (args.MsgID == PacketTypes.Tile || args.MsgID == PacketTypes.TileKill ||
          args.MsgID == PacketTypes.PlaceItemFrame || args.MsgID == PacketTypes.PlaceItemFrame ||
          args.MsgID == PacketTypes.PlaceTileEntity || args.MsgID == PacketTypes.PlaceObject ||
          args.MsgID == PacketTypes.Teleport || args.MsgID == PacketTypes.TeleportationPotion ||
          args.MsgID == PacketTypes.NpcTeleportPortal || args.MsgID == PacketTypes.PlayerTeleportPortal ||
          args.MsgID == PacketTypes.ChestGetContents || args.MsgID == PacketTypes.ChestItem ||
          args.MsgID == PacketTypes.ChestOpen || args.MsgID == PacketTypes.ChestName ||
          args.MsgID == PacketTypes.ChestUnlock || args.MsgID == PacketTypes.ForceItemIntoNearestChest)
        args.Handled = true;
    }

    private static void PreventWorldSave(WorldSaveEventArgs args)
    {
      args.Handled = true;
    }

    private static void BlockIncompletePackets(SendDataEventArgs args)
    {
      if (args.MsgId == PacketTypes.TileSendSection ||
          args.MsgId == PacketTypes.TileSendSquare ||
          args.MsgId == PacketTypes.WorldInfo)
        args.Handled = true;
    }

    private static void StatusMessageSender(GenerationProgress progress)
    {
      try
      {
        var currentMsg = progress.Message;
        while (_working)
        {
          if (progress.Message != currentMsg)
          {
            currentMsg = progress.Message;

            TSPlayer.All.SendData(PacketTypes.Status,
              new StringBuilder().Append('\n', 11)
                .Append(currentMsg)
                .AppendFormat("\n{0:F0}%", progress.TotalProgress * 100)
                .Append('\n', 40).ToString());
          }
        }

        TSPlayer.All.SendData(PacketTypes.Status);
      }
      catch (Exception e)
      {
        TShock.Log.ConsoleError(e.ToString());
      }
    }
  }
}