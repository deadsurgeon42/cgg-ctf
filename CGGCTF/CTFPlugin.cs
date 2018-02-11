using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace CGGCTF
{
  public sealed class CtfPlugin : IDisposable
  {
    private const int CrownSlot = 10;
    private const int CrownNetSlot = 69;
    private const int ArmorHeadSlot = 0;
    private const int ArmorHeadNetSlot = 59;
    private const int CrownId = ItemID.GoldCrown;

    private readonly TerrariaPlugin _registrator;

    internal CtfPlugin(TerrariaPlugin registrator)
      => _registrator = registrator;

    // database
    private CtfClassManager _classes;

    // ctf game controller
    private CtfController _ctf;

    // assist list
    private readonly List<int>[] _didDamage = new List<int>[256];

    // class editing
    private readonly CtfClass[] _editingClass = new CtfClass[256];

    // time stuffs
    private Timer _gameTimer;
    private readonly bool[] _hatForce = new bool[256];

    // user stuffs
    private readonly CtfUser[] _loadedUser = new CtfUser[256];

    // player inventory
    private readonly PlayerData[] _originalChar = new PlayerData[256];
    private readonly Item[] _originalHat = new Item[256];
    private readonly CtfPvpControl _pvp = new CtfPvpControl();

    // wind and rain stuffs
    private Timer _rainTimer;
    private readonly Dictionary<int, int> _revId = new Dictionary<int, int>(); // user ID to index lookup

    // spectator
    private readonly bool[] _spectating = new bool[256];
    private SpectatorManager _spectatorManager;

    // separate stuffs for readability
    private readonly CtfTileSystem _tiles = new CtfTileSystem();
    private int _timeLeft;

    // used package list
    private readonly Dictionary<int, List<int>> _usedPackages = new Dictionary<int, List<int>>();
    private CtfUserManager _users;


    private CtfClass BlankClass => new CtfClass();
    private CtfClass TemplateClass => _classes.GetClass("_template") ?? BlankClass;
    private CtfClass SpectateClass => _classes.GetClass("_spectate") ?? BlankClass;
    private int WaitTime => CtfConfig.WaitTime;
    private int PrepTime => CtfConfig.PrepTime;
    private int CombatTime => CtfConfig.CombatTime;
    private int SdTime => CtfConfig.SuddenDeathTime;
    private bool SdDrops => CtfConfig.SuddenDeathDrops;
    private int ShutdownTime => CtfConfig.ShutdownTime;
    private int MinPlayerToStart => CtfConfig.MinPlayerToStart;

    // money
    private string Singular => CtfConfig.MoneySingularName;
    private string Plural => CtfConfig.MoneyPluralName;

    #region Initialization

    public void Initialize()
    {
      GeneralHooks.ReloadEvent += OnReload;
      ServerApi.Hooks.GameUpdate.Register(_registrator, OnUpdate);

      ServerApi.Hooks.ServerJoin.Register(_registrator, OnJoin);
      PlayerHooks.PlayerPostLogin += OnLogin;
      PlayerHooks.PlayerLogout += OnLogout;
      ServerApi.Hooks.ServerLeave.Register(_registrator, OnLeave);

      GetDataHandlers.PlayerUpdate += OnPlayerUpdate;
      GetDataHandlers.KillMe += OnDeath;
      GetDataHandlers.PlayerSpawn += OnSpawn;
      ServerApi.Hooks.NetSendData.Register(_registrator, OnSendData);
      GetDataHandlers.PlayerSlot += OnSlot;
      ServerApi.Hooks.NetGetData.Register(_registrator, OnGetData);

      ServerApi.Hooks.NpcSpawn.Register(_registrator, OnNpcSpawn);

      GetDataHandlers.TileEdit += OnTileEdit;
      GetDataHandlers.ChestOpen += OnChestOpen;
      GetDataHandlers.ItemDrop += OnItemDrop;
      GetDataHandlers.PlayerTeam += _pvp.PlayerTeamHook;
      GetDataHandlers.TogglePvp += _pvp.TogglePvPHook;

      CtfConfig.Read();
      CtfConfig.Write();

      #region Database stuffs

      _classes = new CtfClassManager();
      _users = new CtfUserManager();

      #endregion

      #region CTF stuffs

      _ctf = new CtfController(GetCallback());
      _spectatorManager = new SpectatorManager(_registrator);
      _spectatorManager.Register();

      #endregion

      #region Time stuffs

      _gameTimer = new Timer(1000);
      _gameTimer.Start();
      _gameTimer.Elapsed += OnGameTimerElapsed;

      #endregion

      #region Wind and rain stuffs

      _rainTimer = new Timer(CtfConfig.RainTimer * 1000);
      _rainTimer.Start();
      _rainTimer.Elapsed += delegate
      {
        Main.StopRain();
        Main.StopSlimeRain();
        Main.windSpeed = 0F;
        Main.windSpeedSet = 0F;
        Main.windSpeedSpeed = 0F;
        TSPlayer.All.SendData(PacketTypes.WorldInfo);
      };

      #endregion

      #region Commands

      void Add(Command c)
      {
        Commands.ChatCommands.RemoveAll(c2 => c2.Names.Exists(s2 => c.Names.Contains(s2)));
        Commands.ChatCommands.Add(c);
      }

      Add(new Command(Permissions.spawn, CmdSpawn, "spawn", "home"));
      Add(new Command(CtfPermissions.Play, CmdJoin, "join"));
      Add(new Command(CtfPermissions.Play, CmdClass, "class"));
      Add(new Command(CtfPermissions.PackageUse, CmdPackage, "pkg", "package"));
      Add(new Command(CtfPermissions.Skip, CmdSkip, "skip"));
      Add(new Command(CtfPermissions.Extend, CmdExtend, "extend"));
      Add(new Command(CtfPermissions.SwitchTeam, CmdTeam, "team"));
      Add(new Command(CtfPermissions.Spectate, CmdSpectate, "spectate"));
      Add(new Command(CtfPermissions.BalCheck, CmdBalance, "balance", "bal"));
      Add(new Command(CtfPermissions.StatsSelf, CmdStats, "stats"));

      #endregion

      foreach (var player in TShock.Players.Where(p => p != null && p.Active))
      {
        InitializePlayer(player.Index);
        if (player.IsLoggedIn)
          InitializeLoggedInPlayer(player);
      }
    }

    public void Dispose()
    {
      GeneralHooks.ReloadEvent -= OnReload;
      ServerApi.Hooks.GameUpdate.Deregister(_registrator, OnUpdate);

      ServerApi.Hooks.ServerJoin.Deregister(_registrator, OnJoin);
      PlayerHooks.PlayerPostLogin -= OnLogin;
      PlayerHooks.PlayerLogout -= OnLogout;
      ServerApi.Hooks.ServerLeave.Deregister(_registrator, OnLeave);

      GetDataHandlers.PlayerUpdate -= OnPlayerUpdate;
      GetDataHandlers.KillMe -= OnDeath;
      GetDataHandlers.PlayerSpawn -= OnSpawn;
      ServerApi.Hooks.NetSendData.Deregister(_registrator, OnSendData);
      GetDataHandlers.PlayerSlot -= OnSlot;
      ServerApi.Hooks.NetGetData.Deregister(_registrator, OnGetData);

      ServerApi.Hooks.NpcSpawn.Deregister(_registrator, OnNpcSpawn);

      GetDataHandlers.TileEdit -= OnTileEdit;
      GetDataHandlers.ChestOpen -= OnChestOpen;
      GetDataHandlers.ItemDrop -= OnItemDrop;
      GetDataHandlers.PlayerTeam -= _pvp.PlayerTeamHook;
      GetDataHandlers.TogglePvp -= _pvp.TogglePvPHook;

      _gameTimer.Dispose();
      _rainTimer.Dispose();

      _spectatorManager.Dispose();
    }

    private void OnReload(ReloadEventArgs args)
    {
      CtfConfig.Read();
      CtfConfig.Write();
    }

    #endregion

    #region Basic Hooks

    private bool _worldLoaded;

    private void OnUpdate(EventArgs args)
    {
      if (!_worldLoaded)
      {
        _worldLoaded = true;
        OnWorldLoad(args);
      }
    }

    private void OnWorldLoad(EventArgs args)
    {
      _tiles.RemoveBadStuffs();
      RemoveTownNpcs();

      foreach (var client in Netplay.Clients)
        client.ResetSections();
    }

    private void OnNpcSpawn(NpcSpawnEventArgs args)
    {
      RemoveTownNpcs();
    }

    private static void RemoveTownNpcs()
    {
      foreach (var npc in Main.npc.Where(n => n != null && n.townNPC))
      {
        Main.npc[npc.whoAmI] = new NPC();
        TSPlayer.All.SendData(PacketTypes.NpcUpdate, number: npc.whoAmI);
      }
    }

    private void OnJoin(JoinEventArgs args)
    {
      InitializePlayer(args.Who);
    }

    private void InitializePlayer(int playerIndex)
    {
      var ix = playerIndex;
      var tplr = TShock.Players[ix];

      _pvp.SetTeam(playerIndex, TeamColor.White);
      _pvp.SetPvP(playerIndex, false);

      SetDifficulty(tplr, 0);

      if (!tplr.IsLoggedIn)
      {
        if (tplr.PlayerData == null)
          tplr.PlayerData = new PlayerData(tplr);
        SetPlayerClass(tplr, BlankClass);
      }
    }

    private void OnLogin(PlayerPostLoginEventArgs args)
    {
      InitializeLoggedInPlayer(args.Player);
    }

    private void InitializeLoggedInPlayer(TSPlayer player)
    {
      var tplr = player;
      var ix = tplr.Index;
      var id = tplr.User.ID;

      _loadedUser[ix] = _users.GetUser(id);

      _revId[id] = ix;

      _originalChar[ix] = new PlayerData(tplr);
      _originalChar[ix].CopyCharacter(tplr);

      SetPlayerClass(tplr, BlankClass);

      // TODO - make joining player sees the message for auto-login
      if (_ctf.GameIsRunning)
      {
        if (_ctf.PlayerExists(id))
          _ctf.RejoinGame(id);
        else if (tplr.HasPermission(CtfPermissions.Play)
                 && tplr.HasPermission(CtfPermissions.Spectate))
          tplr.SendInfoMessage("{0}join to join the game. {0}spectate to watch the game.",
            Commands.Specifier);
        else if (tplr.HasPermission(CtfPermissions.Play))
          tplr.SendInfoMessage("{0}join to join the game.", Commands.Specifier);
        else if (tplr.HasPermission(CtfPermissions.Spectate))
          tplr.SendInfoMessage("{0}spectate to watch the game.", Commands.Specifier);
      }
      else if (tplr.HasPermission(CtfPermissions.Play))
      {
        tplr.SendInfoMessage("{0}join to join the game.", Commands.Specifier);
      }
    }

    private void OnLogout(PlayerLogoutEventArgs args)
    {
      var tplr = args.Player;
      var ix = tplr.Index;
      var id = tplr.User.ID;

      _users.SaveUser(_loadedUser[ix]);
      _loadedUser[ix] = null;

      if (_ctf.PlayerExists(id))
        _ctf.LeaveGame(id);

      tplr.PlayerData = _originalChar[ix];
      TShock.CharacterDB.InsertPlayerData(tplr);

      _hatForce[ix] = false;
      _editingClass[ix] = null;
      tplr.IsLoggedIn = false;
      _spectating[ix] = false;
      _didDamage[ix] = null;

      SetPlayerClass(tplr, BlankClass);
      _revId.Remove(id);
    }


    private void OnLeave(LeaveEventArgs args)
    {
      // i don't even know why
      var tplr = TShock.Players[args.Who];
      if (tplr != null && tplr.IsLoggedIn)
        OnLogout(new PlayerLogoutEventArgs(tplr));
    }

    #endregion

    #region Data hooks

    private void OnPlayerUpdate(object sender, GetDataHandlers.PlayerUpdateEventArgs args)
    {
      var ix = args.PlayerId;
      var tplr = TShock.Players[ix];
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

      var x = (int) Math.Round(args.Position.X / 16);
      var y = (int) Math.Round(args.Position.Y / 16);

      if (!_ctf.GameIsRunning || !_ctf.PlayerExists(id) || _ctf.PlayerDead(id))
        return;

      if (_ctf.GetPlayerTeam(id) == CtfTeam.Red)
      {
        if (_tiles.InRedFlag(x, y))
          _ctf.CaptureFlag(id);
        else if (_tiles.InBlueFlag(x, y))
          _ctf.GetFlag(id);
      }
      else if (_ctf.GetPlayerTeam(id) == CtfTeam.Blue)
      {
        if (_tiles.InBlueFlag(x, y))
          _ctf.CaptureFlag(id);
        else if (_tiles.InRedFlag(x, y))
          _ctf.GetFlag(id);
      }
    }

    private void OnDeath(object sender, GetDataHandlers.KillMeEventArgs args)
    {
      var ix = args.PlayerId;
      var tplr = TShock.Players[ix];
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

      if (!_ctf.PlayerExists(id) || _ctf.PlayerDead(id))
        return;

      if (_ctf.GameIsRunning)
      {
        _ctf.FlagDrop(id);

        if (args.Pvp)
        {
          var item = TShock.Utils.GetItemById(ItemID.RestorationPotion);
          tplr.GiveItem(item.type, item.Name, item.width, item.height, 1);
        }

        if (_ctf.Phase == CtfPhase.SuddenDeath)
        {
          _ctf.SdDeath(id);
          tplr.Dead = true;
          tplr.RespawnTimer = 1;
          args.Handled = true;
        }
      }
    }

    private void OnSpawn(object sender, GetDataHandlers.SpawnEventArgs args)
    {
      if (args.Handled)
        return;

      var tplr = TShock.Players[args.Player];
      if (!tplr.Active || !tplr.RealPlayer || !tplr.IsLoggedIn)
        return;

      var id = tplr.User.ID;
      if (!_ctf.PlayerExists(id))
        return;
      if (_ctf.PlayerDead(id))
      {
        GiveSpectate(tplr);
        tplr.Teleport(tplr.TileX * 16, tplr.TileY * 16);
        return;
      }

      if (!_ctf.GameIsRunning)
        return;

      SpawnPlayer(id, _ctf.GetPlayerTeam(id));
    }

    private void OnSendData(SendDataEventArgs args)
    {
      if (args.MsgId == PacketTypes.Status
          && !args.text.ToString().EndsWith("ctf"))
        args.Handled = true;
    }

    private void OnTileEdit(object sender, GetDataHandlers.TileEditEventArgs args)
    {
      // we have to bear with code mess sometimes

      if (args.Handled)
        return;

      var tplr = args.Player;
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

      Action sendTile = () =>
      {
        TSPlayer.All.SendTileSquare(args.X, args.Y, 1);
        args.Handled = true;
      };
      if (!tplr.HasPermission(CtfPermissions.IgnoreInteract)
          && (!_ctf.PlayerExists(id)
              || _ctf.PlayerDead(id)
              || _ctf.Phase == CtfPhase.Lobby))
      {
        sendTile();
        return;
      }

      var team = _ctf.GetPlayerTeam(id);

      if (_tiles.InvalidPlace(team, args.X, args.Y, _ctf.Phase == CtfPhase.Preparation))
      {
        args.Player.SetBuff(BuffID.Cursed, CtfConfig.CursedTime, true);
        sendTile();
      }
      else if (args.Action == GetDataHandlers.EditAction.PlaceTile)
      {
        if (args.EditData == _tiles.GrayBlock)
        {
          if (team == CtfTeam.Red && _tiles.InRedSide(args.X))
          {
            _tiles.SetTile(args.X, args.Y, _tiles.RedBlock);
            sendTile();
          }
          else if (team == CtfTeam.Blue && _tiles.InBlueSide(args.X))
          {
            _tiles.SetTile(args.X, args.Y, _tiles.BlueBlock);
            sendTile();
          }
        }
        else if (args.EditData == _tiles.RedBlock && (!_tiles.InRedSide(args.X) || team != CtfTeam.Red)
                 || args.EditData == _tiles.BlueBlock && (!_tiles.InBlueSide(args.X) || team != CtfTeam.Blue))
        {
          _tiles.SetTile(args.X, args.Y, _tiles.GrayBlock);
          sendTile();
        }
      }
    }

    private void OnChestOpen(object sender, GetDataHandlers.ChestOpenEventArgs args)
    {
      if (args.Handled)
        return;

      var tplr = args.Player;
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

      if (!tplr.HasPermission(CtfPermissions.IgnoreInteract)
          && (!_ctf.PlayerExists(id)
              || _ctf.PlayerDead(id)
              || _ctf.Phase == CtfPhase.Lobby))
        args.Handled = true;
    }

    private void OnSlot(object sender, GetDataHandlers.PlayerSlotEventArgs args)
    {
      int ix = args.PlayerId;
      var tplr = TShock.Players[ix];

      if (!tplr.Active || !tplr.RealPlayer)
        return;

      if (args.Slot == CrownNetSlot)
      {
        if (_hatForce[ix])
        {
          SendHeadVanity(tplr);
          args.Handled = true;
        }
        else if (args.Type == CrownId)
        {
          SendHeadVanity(tplr);
          args.Handled = true;
        }
      }
      else if (args.Slot == ArmorHeadNetSlot)
      {
        if (args.Type == CrownId) SendArmorHead(tplr);
      }
    }

    private void OnItemDrop(object sender, GetDataHandlers.ItemDropEventArgs args)
    {
      var tplr = args.Player;
      var ix = tplr.Index;
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

      if (_spectating[ix]) args.Handled = true;
    }

    private void OnGetData(GetDataEventArgs args)
    {
      if (args.Handled)
        return;

      if (args.MsgID == PacketTypes.PlayerHurtV2)
        using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
        {
          var ix = reader.ReadByte();
          var deathReason = reader.ReadByte();
          if ((deathReason & 1) == 0)
            return;
          var kix = reader.ReadInt16();

          var tplr = TShock.Players[ix];
          var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
          var cusr = _loadedUser[ix];

          var ktplr = TShock.Players[kix];
          var kid = ktplr.IsLoggedIn ? ktplr.User.ID : -1;
          var kcuser = _loadedUser[kix];

          if (!_ctf.GameIsRunning || !_ctf.PlayerExists(id) || !_ctf.PlayerExists(kid))
            return;

          if (_didDamage[ix] == null)
            _didDamage[ix] = new List<int>(1);
          if (!_didDamage[ix].Contains(kix))
            _didDamage[ix].Add(kix);
        }
      else if (args.MsgID == PacketTypes.PlayerDeathV2)
        using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
        {
          var ix = reader.ReadByte();
          var deathReason = reader.ReadByte();

          var tplr = TShock.Players[ix];
          var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
          var cusr = _loadedUser[ix];

          if (!_ctf.GameIsRunning || !_ctf.PlayerExists(id) || _ctf.PlayerDead(id))
            return;

          var kix = -1;
          if ((deathReason & 1) != 0)
          {
            kix = reader.ReadInt16();
            var ktplr = TShock.Players[kix];
            var kid = ktplr.IsLoggedIn ? ktplr.User.ID : -1;
            var kcuser = _loadedUser[kix];
            if (_ctf.PlayerExists(kid))
            {
              ++kcuser.Kills;
              GiveCoins(ktplr, CtfConfig.GainKill);
            }
          }

          if (_didDamage[ix] != null)
          {
            foreach (var aix in _didDamage[ix])
            {
              if (aix == kix)
                continue;
              var atplr = TShock.Players[aix];
              var acusr = _loadedUser[aix];
              ++acusr.Assists;
              GiveCoins(atplr, CtfConfig.GainAssist);
            }
          }

          ++cusr.Deaths;
          GiveCoins(tplr, CtfConfig.GainDeath);
          _didDamage[ix] = null;
        }
    }

    #endregion

    #region Timer Display

    private string _lastDisplay;

    private void DisplayMessage(string s)
    {
      var ss = new StringBuilder(s);
      DisplayMessage(ss);
    }

    private void DisplayMessage(StringBuilder ss)
    {
      for (var i = 0; i < 50; ++i)
        ss.Append("\n");
      ss.Append("a");
      for (var i = 0; i < 28; ++i)
        ss.Append(" ");
      ss.Append("\nctf");
      TSPlayer.All.SendData(PacketTypes.Status, ss.ToString(), 0);
    }

    private void DisplayTime(string phase = null)
    {
      if (phase == null)
        if (_lastDisplay != null)
          phase = _lastDisplay;
        else
          return;
      var ss = new StringBuilder();
      ss.Append(phase);
      ss.Append("\nTime left - {0}:{1:d2}".SFormat(_timeLeft / 60, _timeLeft % 60));
      ss.Append("\n");
      ss.Append("\nRed | {0} - {1} | Blue".SFormat(_ctf.RedScore, _ctf.BlueScore));
      ss.Append("\n");
      if (_ctf.BlueFlagHeld)
        ss.Append("\n{0} has blue flag.".SFormat(TShock.Players[_revId[_ctf.BlueFlagHolder]].Name));
      if (_ctf.RedFlagHeld)
        ss.Append("\n{0} has red flag.".SFormat(TShock.Players[_revId[_ctf.RedFlagHolder]].Name));
      DisplayMessage(ss);
      _lastDisplay = phase;
    }

    private void DisplayBlank()
    {
      DisplayMessage("");
    }

    private void OnGameTimerElapsed(object sender, ElapsedEventArgs args)
    {
      if (_timeLeft > 0)
      {
        --_timeLeft;

        if (_timeLeft == 0)
          NextPhase();

        switch (_ctf.Phase)
        {
          case CtfPhase.Lobby:
            if (_timeLeft == 60 || _timeLeft == 30)
              AnnounceWarning("Game will start in {0}.", CtfUtils.TimeToString(_timeLeft));
            break;
          case CtfPhase.Preparation:
            DisplayTime("Preparation Phase");
            if (_timeLeft == 60)
              AnnounceWarning("{0} left for preparation phase.", CtfUtils.TimeToString(_timeLeft));
            break;
          case CtfPhase.Combat:
            DisplayTime("Combat Phase");
            if (_timeLeft == 60 * 5 || _timeLeft == 60)
              AnnounceWarning("{0} left for combat phase.", CtfUtils.TimeToString(_timeLeft));
            break;
          case CtfPhase.SuddenDeath:
            DisplayTime("Sudden Death");
            if (_timeLeft == 60)
              AnnounceWarning("{0} left for sudden death.", CtfUtils.TimeToString(_timeLeft));
            break;
          case CtfPhase.Ended:
            if (_timeLeft == 20 || _timeLeft == 10)
              AnnounceWarning("The map will regenerate in {0}.", CtfUtils.TimeToString(_timeLeft));
            break;
        }
      }
    }

    #endregion

    #region Commands

    private void CmdSpawn(CommandArgs args)
    {
      var tplr = args.Player;
      var ix = tplr.Index;
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
      if (tplr == TSPlayer.Server || !tplr.Active || !tplr.RealPlayer)
      {
        tplr.SendErrorMessage("You must be in-game to use this command.");
        return;
      }

      if (!_ctf.GameIsRunning || !_ctf.PlayerExists(id))
      {
        tplr.Teleport(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
        tplr.SendSuccessMessage("Warped to spawn point.");
      }
      else if (_ctf.IsPvPPhase)
      {
        tplr.SendErrorMessage("You can't warp to spawn now!");
      }
      else
      {
        SpawnPlayer(id, _ctf.GetPlayerTeam(id));
        tplr.SendSuccessMessage("Warped to spawn point.");
      }
    }

    private void CmdJoin(CommandArgs args)
    {
      var tplr = args.Player;
      var ix = tplr.Index;
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
      if (tplr == TSPlayer.Server || !tplr.Active || !tplr.RealPlayer)
      {
        tplr.SendErrorMessage("You must be in-game to use this command.");
        return;
      }

      if (_ctf.Phase == CtfPhase.Ended)
      {
        tplr.SendErrorMessage("There is no game to join.");
      }
      else if (_ctf.Phase == CtfPhase.SuddenDeath)
      {
        tplr.SendErrorMessage("Can't join in sudden death phase.");
      }
      else if (_ctf.PlayerExists(id))
      {
        tplr.SendErrorMessage("You are already in the game.");
      }
      else if (CtfConfig.DisallowSpectatorJoin && _spectating[ix]
                                               && tplr.HasPermission(CtfPermissions.IgnoreSpecJoin))
      {
        tplr.SendErrorMessage("You are currently spectating the game.");
      }
      else
      {
        tplr.GodMode = false;
        _spectating[ix] = false;
        SetPlayerClass(tplr, BlankClass);
        _ctf.JoinGame(id);
      }
    }

    private void CmdClass(CommandArgs args)
    {
      var tplr = args.Player;
      var ix = tplr.Index;
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
      var validPlayer = tplr != TSPlayer.Server && tplr.Active && tplr.RealPlayer;

      if (args.Parameters.Count == 0)
        if (validPlayer)
        {
          tplr.SendErrorMessage("Usage: {0}class <name/list>", Commands.Specifier);
          return;
        }

      var outgameSub = new[]
      {
        "list", "check", "allow", "disallow", "refund"
      };
      if (!validPlayer
          && (args.Parameters.Count == 0
              || !outgameSub.Contains(args.Parameters[0].ToLower())))
      {
        tplr.SendErrorMessage("You must be in-game to use this command.");
        return;
      }

      switch (args.Parameters[0].ToLower())
      {
        #region /class list

        case "list":
        {
          int pageNumber;
          if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
            return;

          var classList = GenerateList(tplr, false);
          var lineCount = validPlayer
            ? CtfConfig.ListLineCountIngame
            : CtfConfig.ListLineCountOutgame;

          PaginationTools.SendPage(
            tplr,
            pageNumber,
            classList,
            new PaginationTools.Settings
            {
              HeaderFormat = "Classes ({0}/{1})",
              FooterFormat = string.Format(
                "Type {0}class list {{0}} for more.",
                Commands.Specifier),
              NothingToDisplayString = "There are no available classes.",
              MaxLinesPerPage = lineCount
            });
        }
          break;

        #endregion

        #region /class edit <name>

        case "edit":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_ctf.Phase != CtfPhase.Lobby)
          {
            tplr.SendErrorMessage("You can only edit classes before game starts.");
            return;
          }

          if (_editingClass[ix] != null)
          {
            tplr.SendErrorMessage("You are editing class {0} right now.", _editingClass[ix].Name);
            tplr.SendErrorMessage("{0}class save or {0}class discard.", Commands.Specifier);
            return;
          }

          if (args.Parameters.Count < 2)
          {
            tplr.SendErrorMessage("Usage: {0}class edit <name>", Commands.Specifier);
            return;
          }

          var className = string.Join(" ", args.Parameters.Skip(1));
          var cls = _classes.GetClass(className);
          if (cls == null)
          {
            cls = TemplateClass;
            cls.Name = className;
            tplr.SendSuccessMessage("You are adding new class {0}.", cls.Name);
          }
          else
          {
            tplr.SendSuccessMessage("You may now start editing class {0}.", cls.Name);
          }

          tplr.SendInfoMessage("{0}class save when you're done.", Commands.Specifier);
          tplr.SendInfoMessage("{0}class cancel to cancel.", Commands.Specifier);
          tplr.SendInfoMessage("Also try: {0}class hp/mana/desc/name", Commands.Specifier);

          _timeLeft = -1;
          SetPlayerClass(tplr, cls);
          _editingClass[ix] = cls;
        }
          break;

        #endregion

        #region /class save

        case "save":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_editingClass[ix] == null)
          {
            tplr.SendErrorMessage("You are not editing any classes right now.");
            return;
          }

          tplr.PlayerData.CopyCharacter(tplr);
          _editingClass[ix].CopyFromPlayerData(tplr.PlayerData);
          _classes.SaveClass(_editingClass[ix]);

          SetPlayerClass(tplr, BlankClass);

          tplr.SendSuccessMessage("Edited class {0}.", _editingClass[ix].Name);
          _editingClass[ix] = null;
          _timeLeft = _ctf.OnlinePlayer >= CtfConfig.MinPlayerToStart ? WaitTime : 0;
        }
          break;

        #endregion

        #region /class cancel

        case "discard":
        case "cancel":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_editingClass[ix] == null)
          {
            tplr.SendErrorMessage("You are not editing any classes right now.");
            return;
          }

          SetPlayerClass(tplr, BlankClass);

          tplr.SendInfoMessage("Canceled editing class {0}.", _editingClass[ix].Name);
          _editingClass[ix] = null;
          _timeLeft = _ctf.OnlinePlayer >= CtfConfig.MinPlayerToStart ? WaitTime : 0;
        }
          break;

        #endregion

        #region /class hp <amount>

        case "hp":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_editingClass[ix] == null)
          {
            tplr.SendErrorMessage("You are not editing any classes right now.");
            return;
          }

          if (args.Parameters.Count != 2)
          {
            tplr.SendErrorMessage("Usage: {0}class hp <amount>", Commands.Specifier);
            return;
          }

          int amount;
          if (!int.TryParse(args.Parameters[1], out amount))
          {
            tplr.SendErrorMessage("Invalid HP amount.");
            return;
          }

          tplr.TPlayer.statLife = amount;
          tplr.TPlayer.statLifeMax = amount;
          tplr.SendSuccessMessage("Changed your HP to {0}.", amount);
          TSPlayer.All.SendData(PacketTypes.PlayerHp, "", ix, amount, amount);
        }
          break;

        #endregion

        #region /class mana <amount>

        case "mp":
        case "mana":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_editingClass[ix] == null)
          {
            tplr.SendErrorMessage("You are not editing any classes right now.");
            return;
          }

          if (args.Parameters.Count != 2)
          {
            tplr.SendErrorMessage("Usage: {0}class mana <amount>", Commands.Specifier);
            return;
          }

          int amount;
          if (!int.TryParse(args.Parameters[1], out amount))
          {
            tplr.SendErrorMessage("Invalid mana amount.");
            return;
          }

          tplr.TPlayer.statMana = amount;
          tplr.TPlayer.statManaMax = amount;
          tplr.SendSuccessMessage("Changed your mana to {0}.", amount);
          TSPlayer.All.SendData(PacketTypes.PlayerMana, "", ix, amount, amount);
        }
          break;

        #endregion

        #region /class desc <text>

        case "desc":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_editingClass[ix] == null)
          {
            tplr.SendErrorMessage("You are not editing any classes right now.");
            return;
          }

          if (args.Parameters.Count < 2)
          {
            tplr.SendErrorMessage("Usage: {0}class desc <text>", Commands.Specifier);
            return;
          }

          var text = string.Join(" ", args.Parameters.Skip(1));
          _editingClass[ix].Description = text;
          tplr.SendSuccessMessage("Changed {0} description to:", _editingClass[ix].Name);
          tplr.SendInfoMessage(text);
        }
          break;

        #endregion

        #region /class name <text>

        case "name":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_editingClass[ix] == null)
          {
            tplr.SendErrorMessage("You are not editing any classes right now.");
            return;
          }

          if (args.Parameters.Count < 2)
          {
            tplr.SendErrorMessage("Usage: {0}class name <text>", Commands.Specifier);
            return;
          }

          var text = string.Join(" ", args.Parameters.Skip(1));
          tplr.SendSuccessMessage("Changed name of {0} to {1}.",
            _editingClass[ix].Name, text);
          _editingClass[ix].Name = text;
        }
          break;

        #endregion

        #region /class price <amount>

        case "price":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_editingClass[ix] == null)
          {
            tplr.SendErrorMessage("You are not editing any classes right now.");
            return;
          }

          if (args.Parameters.Count != 2)
          {
            tplr.SendErrorMessage("Usage: {0}class price <amount>", Commands.Specifier);
            return;
          }

          int amount;
          if (!int.TryParse(args.Parameters[1], out amount))
          {
            tplr.SendErrorMessage("Invalid price.");
            return;
          }

          _editingClass[ix].Price = amount;
          tplr.SendSuccessMessage("Changed {0} price to {1}.",
            _editingClass[ix].Name,
            CtfUtils.Pluralize(_editingClass[ix].Price, Singular, Plural));
        }
          break;

        #endregion

        #region /class visible

        case "visible":
        case "hidden":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_editingClass[ix] == null)
          {
            tplr.SendErrorMessage("You are not editing any classes right now.");
            return;
          }

          _editingClass[ix].Hidden = !_editingClass[ix].Hidden;
          if (_editingClass[ix].Hidden)
            tplr.SendSuccessMessage("{0} is now hidden.", _editingClass[ix].Name);
          else
            tplr.SendSuccessMessage("{0} is now visible.", _editingClass[ix].Name);
        }
          break;

        #endregion

        #region /class lock

        case "lock":
        case "sell":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_editingClass[ix] == null)
          {
            tplr.SendErrorMessage("You are not editing any classes right now.");
            return;
          }

          _editingClass[ix].Sell = !_editingClass[ix].Sell;
          if (_editingClass[ix].Sell)
            tplr.SendSuccessMessage("{0} can be bought now.", _editingClass[ix].Name);
          else
            tplr.SendSuccessMessage("{0} can't be bought now.", _editingClass[ix].Name);
        }
          break;

        #endregion

        #region /class delete <name>

        case "delete":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_ctf.Phase != CtfPhase.Lobby)
          {
            tplr.SendErrorMessage("You can only edit classes before game starts.");
            return;
          }

          if (_editingClass[ix] != null)
          {
            tplr.SendErrorMessage("You are editing class {0} right now.", _editingClass[ix].Name);
            tplr.SendErrorMessage("{0}class save or {0}class discard.", Commands.Specifier);
            return;
          }

          if (args.Parameters.Count < 2)
          {
            tplr.SendErrorMessage("Usage: {0}class delete <name>", Commands.Specifier);
            return;
          }

          var className = string.Join(" ", args.Parameters.Skip(1));
          var cls = _classes.GetClass(className);
          if (cls == null)
          {
            tplr.SendErrorMessage("Class {0} doesn't exist.", className);
            return;
          }

          tplr.SendSuccessMessage("Class {0} has been removed.", cls.Name);
          _classes.DeleteClass(cls.Id);
        }
          break;

        #endregion

        #region /class clone <old> <new>

        case "clone":
        case "copy":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (_ctf.Phase != CtfPhase.Lobby)
          {
            tplr.SendErrorMessage("You can only edit classes before game starts.");
            return;
          }

          if (_editingClass[ix] != null)
          {
            tplr.SendErrorMessage("You are editing class {0} right now.", _editingClass[ix].Name);
            tplr.SendErrorMessage("{0}class save or {0}class discard.", Commands.Specifier);
            return;
          }

          if (args.Parameters.Count != 3)
          {
            tplr.SendErrorMessage("Usage: {0}class clone <old> <new>", Commands.Specifier);
            return;
          }

          var cls1 = _classes.GetClass(args.Parameters[1]);
          if (cls1 == null)
          {
            tplr.SendErrorMessage("Class {0} doesn't exist.", args.Parameters[1]);
            return;
          }

          var cls2 = _classes.GetClass(args.Parameters[2]);
          if (cls2 != null)
          {
            tplr.SendErrorMessage("Class {0} already exists.", cls2.Name);
            return;
          }

          cls1.Id = -1;
          cls1.Name = args.Parameters[2];
          _classes.SaveClass(cls1);
          tplr.SendSuccessMessage("Cloned {0} into {1}.", cls1.Name, args.Parameters[2]);
        }
          return;

        #endregion

        #region /class check <player> <class>

        case "check":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (args.Parameters.Count != 3)
          {
            tplr.SendErrorMessage("Usage: {0}class check <player> <class>", Commands.Specifier);
            return;
          }

          var name = args.Parameters[1];
          TSPlayer ttplr;
          User tusr;
          CtfUser cusr;
          if (!FindUser(name, out ttplr, out tusr, out cusr))
          {
            tplr.SendErrorMessage("User {0} doesn't exist.", name);
            return;
          }

          name = tusr.Name;

          var clsName = args.Parameters[2];
          var cls = _classes.GetClass(clsName);
          if (cls == null)
          {
            tplr.SendErrorMessage("Class {0} doesn't exist.", clsName);
            return;
          }

          clsName = cls.Name;

          if (cusr.HasClass(cls.Id))
            tplr.SendInfoMessage("{0} has class {1}.", name, clsName);
          else
            tplr.SendInfoMessage("{0} doesn't have class {1}.", name, clsName);
        }
          break;

        #endregion

        #region /class allow <player> <class>

        case "allow":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (args.Parameters.Count != 3)
          {
            tplr.SendErrorMessage("Usage: {0}class allow <player> <class>", Commands.Specifier);
            return;
          }

          var name = args.Parameters[1];
          TSPlayer ttplr;
          User tusr;
          CtfUser cusr;
          if (!FindUser(name, out ttplr, out tusr, out cusr))
          {
            tplr.SendErrorMessage("User {0} doesn't exist.", name);
            return;
          }

          name = tusr.Name;

          var clsName = args.Parameters[2];
          var cls = _classes.GetClass(clsName);
          if (cls == null)
          {
            tplr.SendErrorMessage("Class {0} doesn't exist.", clsName);
            return;
          }

          clsName = cls.Name;

          cusr.AddClass(cls.Id);
          SaveUser(cusr);
          tplr.SendSuccessMessage("Allowed {0} to use {1}.", name, clsName);
        }
          break;

        #endregion

        #region /class disallow <player> <class>

        case "disallow":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (args.Parameters.Count != 3)
          {
            tplr.SendErrorMessage("Usage: {0}class disallow <player> <class>", Commands.Specifier);
            return;
          }

          var name = args.Parameters[1];
          TSPlayer ttplr = null;
          User tusr = null;
          CtfUser cusr = null;
          if (name != "*")
          {
            if (!FindUser(name, out ttplr, out tusr, out cusr))
            {
              tplr.SendErrorMessage("User {0} doesn't exist.", name);
              return;
            }

            name = tusr.Name;
          }

          var clsName = args.Parameters[2];
          var cls = _classes.GetClass(clsName);
          if (cls == null)
          {
            tplr.SendErrorMessage("Class {0} doesn't exist.", clsName);
            return;
          }

          clsName = cls.Name;

          if (name == "*")
          {
            var usrs = _users.GetUsers();
            foreach (var usr in usrs)
              if (usr.HasClass(cls.Id))
              {
                usr.RemoveClass(cls.Id);
                SaveUser(usr);
              }

            tplr.SendSuccessMessage("Disallowed everybody from using {0}.", clsName);
          }
          else
          {
            cusr.RemoveClass(cls.Id);
            SaveUser(cusr);
            tplr.SendSuccessMessage("Disallowed {0} from using {1}.", name, clsName);
          }
        }
          break;

        #endregion

        #region /class refund <name> <amount>

        case "refund":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (args.Parameters.Count != 3)
          {
            tplr.SendErrorMessage("Usage: {0}class refund <name> <amount>", Commands.Specifier);
            return;
          }

          int amount;
          if (!int.TryParse(args.Parameters[2], out amount))
          {
            tplr.SendErrorMessage("Invalid amount.");
            return;
          }

          var clsName = args.Parameters[1];
          var cls = _classes.GetClass(clsName);
          if (cls == null)
          {
            tplr.SendErrorMessage("Class {0} doesn't exist.", clsName);
            return;
          }

          clsName = cls.Name;

          var usrs = _users.GetUsers();
          var count = 0;
          foreach (var usr in usrs)
            if (usr.HasClass(cls.Id))
            {
              ++count;
              usr.Coins += amount;
              SaveUser(usr);
            }

          tplr.SendSuccessMessage("Gave {0} to {1} players.",
            CtfUtils.Pluralize(amount, Singular, Plural), count);
        }
          break;

        #endregion

        #region /class buy <name>

        case "buy":
        {
          if (!tplr.HasPermission(CtfPermissions.ClassBuy))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (args.Parameters.Count < 2)
          {
            tplr.SendErrorMessage("Usage: {0}class buy <name>", Commands.Specifier);
            return;
          }

          var className = string.Join(" ", args.Parameters.Skip(1));
          var cls = _classes.GetClass(className);
          if (cls == null || !CanSeeClass(tplr, cls) && !CanUseClass(tplr, cls))
          {
            tplr.SendErrorMessage("Class {0} doesn't exist. Try {1}class list.",
              className, Commands.Specifier);
            return;
          }

          var cusr = _loadedUser[ix];

          if (CanUseClass(tplr, cls))
          {
            tplr.SendErrorMessage("You already have class {0}.", cls.Name);
            return;
          }

          if (!tplr.HasPermission(CtfPermissions.ClassBuyAll) && !cls.Sell)
          {
            tplr.SendErrorMessage("You may not buy this class.");
            return;
          }

          if (cusr.Coins < cls.Price)
          {
            tplr.SendErrorMessage("You don't have enough {0} to buy class {1}.",
              Plural, cls.Name);
            return;
          }

          cusr.Coins -= cls.Price;
          cusr.AddClass(cls.Id);
          tplr.SendSuccessMessage("You bought class {0}.", cls.Name);
          SaveUser(cusr);
        }
          break;

        #endregion

        #region /class <name>

        default:
        {
          var className = string.Join(" ", args.Parameters);
          if (!_ctf.GameIsRunning)
          {
            tplr.SendErrorMessage("The game hasn't started yet!");
            return;
          }

          if (!_ctf.PlayerExists(id))
          {
            tplr.SendErrorMessage("You are not in the game!");
            return;
          }

          if (_ctf.HasPickedClass(id))
          {
            tplr.SendErrorMessage("You already picked a class!");
            return;
          }

          var cls = _classes.GetClass(className);
          if (cls == null || !CanSeeClass(tplr, cls) && !CanUseClass(tplr, cls)
                          || cls.Name.StartsWith("*") || cls.Name.StartsWith("_"))
          {
            tplr.SendErrorMessage("Class {0} doesn't exist. Try {1}class list.",
              className, Commands.Specifier);
            return;
          }

          if (!CanUseClass(tplr, cls))
          {
            if (cls.Sell || tplr.HasPermission(CtfPermissions.ClassBuyAll))
            {
              tplr.SendErrorMessage("You do not have {0}. Type {1}class buy {0}.",
                cls.Name, Commands.Specifier);
              tplr.SendErrorMessage("Price: {0}. You have {1}.",
                CtfUtils.Pluralize(cls.Price, Singular, Plural),
                CtfUtils.Pluralize(_loadedUser[ix].Coins, Singular, Plural));
            }
            else
            {
              tplr.SendErrorMessage("You do not have {0}.", cls.Name);
            }

            return;
          }

          _ctf.PickClass(id, cls);
        }
          break;

        #endregion
      }
    }

    private void CmdPackage(CommandArgs args)
    {
      var tplr = args.Player;
      var ix = tplr.Index;
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
      var validPlayer = tplr != TSPlayer.Server && tplr.Active && tplr.RealPlayer;

      if (args.Parameters.Count == 0)
        if (validPlayer)
        {
          tplr.SendErrorMessage("Usage: {0}pkg <name/list>", Commands.Specifier);
          return;
        }

      if (!validPlayer
          && (args.Parameters.Count == 0
              || args.Parameters[0] != "list"))
      {
        tplr.SendErrorMessage("You must be in-game to use this command.");
        return;
      }

      switch (args.Parameters[0].ToLower())
      {
        #region Editing stuffs

        case "edit":
        case "save":
        case "cancel":
        case "discard":
        case "delete":
        case "name":
        case "desc":
        case "price":
        case "hidden":
        case "visible":
        case "lock":
        case "sell":
        case "clone":
        case "copy":
        case "check":
        case "allow":
        case "disallow":
        case "refund":
        {
          tplr.SendInfoMessage("To edit package, use {0}class edit *<name>.",
            Commands.Specifier);
        }
          break;

        #endregion

        #region /pkg list

        case "list":
        {
          int pageNumber;
          if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
            return;

          var pkgList = GenerateList(tplr, true);
          var lineCount = validPlayer
            ? CtfConfig.ListLineCountIngame
            : CtfConfig.ListLineCountOutgame;

          PaginationTools.SendPage(
            tplr,
            pageNumber,
            pkgList,
            new PaginationTools.Settings
            {
              HeaderFormat = "Packages ({0}/{1})",
              FooterFormat = string.Format(
                "Type {0}pkg list {{0}} for more.",
                Commands.Specifier),
              NothingToDisplayString = "There are no available packages.",
              MaxLinesPerPage = lineCount
            });
        }
          break;

        #endregion

        #region /pkg <name>

        default:
        {
          var pkgName = string.Join(" ", args.Parameters);
          if (!_ctf.GameIsRunning)
          {
            tplr.SendErrorMessage("The game hasn't started yet!");
            return;
          }

          if (!_ctf.PlayerExists(id))
          {
            tplr.SendErrorMessage("You are not in the game!");
            return;
          }

          if (!_ctf.HasPickedClass(id))
          {
            tplr.SendErrorMessage("You must pick a class first!");
            return;
          }

          var cls = _classes.GetClass("*" + pkgName);
          if (cls == null || !CanSeeClass(tplr, cls) && !CanUseClass(tplr, cls))
          {
            tplr.SendErrorMessage("Package {0} doesn't exist. Try {1}pkg list.",
              pkgName, Commands.Specifier);
            return;
          }

          pkgName = cls.Name.Remove(0, 1);
          if (!CanUseClass(tplr, cls))
          {
            if (cls.Sell || tplr.HasPermission(CtfPermissions.PackageBuyAll))
            {
              tplr.SendErrorMessage("You do not have {0}. Type {1}pkg buy {0}.",
                pkgName, Commands.Specifier);
              tplr.SendErrorMessage("Price: {0}. You have {1}.",
                CtfUtils.Pluralize(cls.Price, Singular, Plural),
                CtfUtils.Pluralize(_loadedUser[ix].Coins, Singular, Plural));
            }
            else
            {
              tplr.SendErrorMessage("You do not have {0}.", pkgName);
            }

            return;
          }

          if (!_usedPackages.ContainsKey(id))
            _usedPackages[id] = new List<int>(1);
          if (_usedPackages[id].Contains(cls.Id))
          {
            tplr.SendErrorMessage("You have already used {0}.", pkgName);
            return;
          }

          _usedPackages[id].Add(cls.Id);

          for (var i = 0; i < NetItem.MaxInventory; ++i)
          {
            var item = TShock.Utils.GetItemById(cls.Inventory[i].NetId);
            if (item != null)
            {
              item.stack = cls.Inventory[i].Stack;
              item.Prefix(cls.Inventory[i].PrefixId);
              tplr.GiveItem(item.type, item.Name, item.width, item.height, 1);
            }
          }

          tplr.SendSuccessMessage("You used {0}.", pkgName);
        }
          break;

        #endregion

        #region /pkg buy <name>

        case "buy":
        {
          if (!tplr.HasPermission(CtfPermissions.PackageBuy))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (args.Parameters.Count < 2)
          {
            tplr.SendErrorMessage("Usage: {0}pkg buy <name>", Commands.Specifier);
            return;
          }

          var pkgName = string.Join(" ", args.Parameters.Skip(1));
          var className = "*" + pkgName;
          var cls = _classes.GetClass(className);
          if (cls == null || !CanSeeClass(tplr, cls) && !CanUseClass(tplr, cls))
          {
            tplr.SendErrorMessage("Package {0} doesn't exist. Try {1}pkg list.",
              pkgName, Commands.Specifier);
            return;
          }

          className = cls.Name;
          pkgName = className.Remove(0, 1);

          var cusr = _loadedUser[ix];

          if (CanUseClass(tplr, cls))
          {
            tplr.SendErrorMessage("You already have package {0}.", pkgName);
            return;
          }

          if (!tplr.HasPermission(CtfPermissions.PackageBuyAll) && !cls.Sell)
          {
            tplr.SendErrorMessage("You may not buy this package.");
            return;
          }

          if (cusr.Coins < cls.Price)
          {
            tplr.SendErrorMessage("You don't have enough {0} to buy package {1}.",
              Plural, pkgName);
            return;
          }

          cusr.Coins -= cls.Price;
          cusr.AddClass(cls.Id);
          tplr.SendSuccessMessage("You bought package {0}.", pkgName);
          SaveUser(cusr);
        }
          break;

        #endregion
      }
    }

    private void CmdSkip(CommandArgs args)
    {
      NextPhase();
    }

    private void CmdExtend(CommandArgs args)
    {
      var tplr = args.Player;

      if (args.Parameters.Count != 1)
      {
        tplr.SendErrorMessage("Usage: {0}extend <time>", Commands.Specifier);
        return;
      }

      var time = 0;
      if (!TShock.Utils.TryParseTime(args.Parameters[0], out time))
      {
        tplr.SendErrorMessage("Invalid time string! Proper format: _d_h_m_s, with at least one time specifier.");
        tplr.SendErrorMessage("For example, 1d and 10h-30m+2m are both valid time strings, but 2 is not.");
        return;
      }

      _timeLeft += time;
      tplr.SendSuccessMessage("Extended time of current phase.");
    }

    private void CmdTeam(CommandArgs args)
    {
      var tplr = args.Player;

      if (args.Parameters.Count != 2)
      {
        tplr.SendErrorMessage("Usage: {0}team <player> <color>", Commands.Specifier);
        return;
      }

      var color = args.Parameters[1].ToLower();
      if (color != "red" && color != "blue")
      {
        tplr.SendErrorMessage("Invalid team color.");
        return;
      }

      var team = color == "red" ? CtfTeam.Red : CtfTeam.Blue;

      var matches = TShock.Utils.FindPlayer(args.Parameters[0]);
      if (matches.Count < 0)
      {
        tplr.SendErrorMessage("Invalid player!");
        return;
      }

      if (matches.Count > 1)
      {
        TShock.Utils.SendMultipleMatchError(tplr, matches.Select(m => m.Name));
        return;
      }

      var target = matches[0];

      if (!target.IsLoggedIn)
      {
        tplr.SendErrorMessage("{0} isn't logged in.", target.Name);
        return;
      }

      if (!_ctf.PlayerExists(target.User.ID)) tplr.SendErrorMessage("{0} hasn't joined the game.", target.Name);

      if (!_ctf.SwitchTeam(target.User.ID, team))
        tplr.SendErrorMessage("{0} was already on {1} team.",
          target.Name, color);
    }

    private void CmdSpectate(CommandArgs args)
    {
      var tplr = args.Player;
      var ix = tplr.Index;
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

      if (!_ctf.GameIsRunning)
      {
        tplr.SendErrorMessage("There is no game to spectate.");
        return;
      }

      if (_spectating[ix])
      {
        tplr.SendErrorMessage("You are already spectating.");
        return;
      }

      if (_ctf.PlayerExists(id))
      {
        tplr.SendErrorMessage("You are currently in-game.");
        return;
      }

      GiveSpectate(tplr);
      if (!tplr.HasPermission(CtfPermissions.IgnoreTempgroup))
        tplr.tempGroup = TShock.Groups.GetGroupByName("spectate");
      tplr.SendSuccessMessage("You are now spectating the game.");
    }

    private void CmdBalance(CommandArgs args)
    {
      var tplr = args.Player;
      var ix = tplr.Index;

      if (args.Parameters.Count > 0)
      {
        if (args.Parameters[0].ToLower() == "add")
        {
          if (!tplr.HasPermission(CtfPermissions.BalEdit))
          {
            tplr.SendErrorMessage("You don't have access to this command.");
            return;
          }

          if (args.Parameters.Count < 3)
          {
            tplr.SendErrorMessage("Usage: {0}balance add <name> <amount>",
              Commands.Specifier);
            return;
          }

          int amount;
          if (!int.TryParse(args.Parameters[args.Parameters.Count - 1], out amount))
          {
            tplr.SendErrorMessage("Invalid amount.");
            return;
          }

          var prms = new List<string>(args.Parameters.Count - 2);
          for (var i = 1; i < args.Parameters.Count - 1; ++i)
            prms.Add(args.Parameters[i]);
          var name = string.Join(" ", prms);

          TSPlayer ttplr;
          User ttusr;
          CtfUser tcusr;
          if (!FindUser(name, out ttplr, out ttusr, out tcusr))
          {
            tplr.SendErrorMessage("User {0} doesn't exist.", name);
            return;
          }

          tcusr.Coins += amount;
          SaveUser(tcusr);
          tplr.SendSuccessMessage("Gave {0} {1}.",
            ttplr?.Name ?? ttusr.Name,
            CtfUtils.Pluralize(amount, Singular, Plural));
        }
        else
        {
          if (!tplr.HasPermission(CtfPermissions.BalCheckOther))
          {
            tplr.SendErrorMessage("You can only check your balance.");
            return;
          }

          var name = string.Join(" ", args.Parameters);
          TSPlayer ttplr;
          User ttusr;
          CtfUser tcusr;
          if (!FindUser(name, out ttplr, out ttusr, out tcusr))
          {
            tplr.SendErrorMessage("User {0} doesn't exist.", name);
            return;
          }

          tplr.SendInfoMessage("{0} has {1}.",
            ttplr?.Name ?? ttusr.Name,
            CtfUtils.Pluralize(tcusr.Coins, Singular, Plural));
        }

        return;
      }

      TSPlayer xtplr;
      User xtusr;
      CtfUser cusr;
      if (!FindUser(tplr.Name, out xtplr, out xtusr, out cusr))
      {
        tplr.SendErrorMessage("You must be logged in to use this command.");
        return;
      }

      tplr.SendInfoMessage("You have {0}.", CtfUtils.Pluralize(cusr.Coins, Singular, Plural));
    }

    private void CmdStats(CommandArgs args)
    {
      var tplr = args.Player;
      var ix = tplr.Index;
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;

      var name = tplr.Name;

      if (args.Parameters.Count > 0)
      {
        if (!tplr.HasPermission(CtfPermissions.StatsOther))
        {
          tplr.SendErrorMessage("You may only check your own stats.");
          return;
        }

        name = string.Join(" ", args.Parameters);
      }

      TSPlayer plr;
      User user;
      CtfUser cusr;
      if (!FindUser(name, out plr, out user, out cusr))
      {
        if (name == tplr.Name)
          tplr.SendErrorMessage("You must be logged in to use this command.");
        else
          tplr.SendErrorMessage("User {0} doesn't exist.", name);
        return;
      }

      tplr.SendSuccessMessage("Statistics for {0}", plr?.Name ?? user.Name);
      tplr.SendInfoMessage("Wins: {0} | Loses: {1} | Draws: {2}",
        cusr.Wins, cusr.Loses, cusr.Draws);
      tplr.SendInfoMessage("Kills: {0} | Deaths: {1} | Assists: {2}",
        cusr.Kills, cusr.Deaths, cusr.Assists);
      tplr.SendInfoMessage("Total Games: {0} | K/D Ratio: {1:f3}",
        cusr.TotalGames, cusr.KdRatio);
    }

    #endregion

    #region Messages

    private void SendRedMessage(TSPlayer tplr, string msg, params object[] args)
    {
      tplr.SendMessage(msg.SFormat(args), 255, 102, 102);
    }

    private void AnnounceRedMessage(string msg, params object[] args)
    {
      SendRedMessage(TSPlayer.All, msg, args);
    }

    private void SendBlueMessage(TSPlayer tplr, string msg, params object[] args)
    {
      tplr.SendMessage(msg.SFormat(args), 102, 178, 255);
    }

    private void AnnounceBlueMessage(string msg, params object[] args)
    {
      SendBlueMessage(TSPlayer.All, msg, args);
    }

    private void AnnounceMessage(string msg, params object[] args)
    {
      TSPlayer.All.SendInfoMessage(msg, args);
    }

    private void AnnounceWarning(string msg, params object[] args)
    {
      TSPlayer.All.SendWarningMessage(msg, args);
    }

    private void AnnounceScore(int red, int blue)
    {
      AnnounceMessage("Current Score | Red {0} - {1} Blue", red, blue);
    }

    #endregion

    #region Utils

    private bool SpawnPlayer(int id, CtfTeam team)
    {
      var tplr = TShock.Players[_revId[id]];
      if (team == CtfTeam.Red)
        return tplr.Teleport(_tiles.RedSpawn.X * 16, _tiles.RedSpawn.Y * 16);
      if (team == CtfTeam.Blue)
        return tplr.Teleport(_tiles.BlueSpawn.X * 16, _tiles.BlueSpawn.Y * 16);
      return false;
    }

    private CtfCallback GetCallback()
    {
      var cb = new CtfCallback();
      cb.DecidePositions = delegate { _tiles.DecidePositions(); };
      cb.SetTeam = delegate(int id, CtfTeam team)
      {
        var color = TeamColor.White;
        if (team == CtfTeam.Red)
          color = TeamColor.Red;
        else if (team == CtfTeam.Blue)
          color = TeamColor.Blue;
        _pvp.SetTeam(_revId[id], color);
      };
      cb.SetPvP = delegate(int id, bool pv) { _pvp.SetPvP(_revId[id], pv); };
      cb.SetInventory = delegate(int id, CtfClass cls)
      {
        var tplr = TShock.Players[_revId[id]];
        SetPlayerClass(tplr, cls);
      };
      cb.SaveInventory = delegate(int id)
      {
        var tplr = TShock.Players[_revId[id]];
        var data = new PlayerData(tplr);
        data.CopyCharacter(tplr);
        return data;
      };
      cb.WarpToSpawn = delegate(int id, CtfTeam team) { SpawnPlayer(id, team); };
      cb.InformPlayerJoin = delegate(int id, CtfTeam team)
      {
        var tplr = TShock.Players[_revId[id]];
        if (team == CtfTeam.Red)
          AnnounceRedMessage("{0} joined the red team!", tplr.Name);
        else if (team == CtfTeam.Blue)
          AnnounceBlueMessage("{0} joined the blue team!", tplr.Name);
        else
          AnnounceMessage("{0} joined the game.", tplr.Name);
        if (_ctf.Phase == CtfPhase.Lobby && _timeLeft == 0 && _ctf.OnlinePlayer >= MinPlayerToStart)
          _timeLeft = WaitTime;
      };
      cb.InformPlayerRejoin = delegate(int id, CtfTeam team)
      {
        Debug.Assert(team != CtfTeam.None);
        var tplr = TShock.Players[_revId[id]];
        if (team == CtfTeam.Red)
          AnnounceRedMessage("{0} rejoined the red team.", tplr.Name);
        else
          AnnounceBlueMessage("{0} rejoined the blue team.", tplr.Name);
      };
      cb.InformPlayerLeave = delegate(int id, CtfTeam team)
      {
        Debug.Assert(team != CtfTeam.None);
        var tplr = TShock.Players[_revId[id]];
        if (team == CtfTeam.Red)
          AnnounceRedMessage("{0} left the red team.", tplr.Name);
        else
          AnnounceBlueMessage("{0} left the blue team.", tplr.Name);
      };
      cb.AnnounceGetFlag = delegate(int id, CtfTeam team)
      {
        Debug.Assert(team != CtfTeam.None);
        var tplr = TShock.Players[_revId[id]];
        AddCrown(tplr);
        DisplayTime();
        if (team == CtfTeam.Red)
        {
          _tiles.RemoveBlueFlag();
          AnnounceRedMessage("{0} is taking blue team's flag!", tplr.Name);
        }
        else
        {
          _tiles.RemoveRedFlag();
          AnnounceBlueMessage("{0} is taking red team's flag!", tplr.Name);
        }
      };
      cb.AnnounceCaptureFlag = delegate(int id, CtfTeam team, int redScore, int blueScore)
      {
        Debug.Assert(team != CtfTeam.None);
        var tplr = TShock.Players[_revId[id]];
        GiveCoins(tplr, CtfConfig.GainCapture);
        RemoveCrown(tplr);
        DisplayTime();
        if (team == CtfTeam.Red)
        {
          _tiles.AddBlueFlag();
          AnnounceRedMessage("{0} captured blue team's flag and scored a point!", tplr.Name);
        }
        else
        {
          _tiles.AddRedFlag();
          AnnounceBlueMessage("{0} captured red team's flag and scored a point!", tplr.Name);
        }

        AnnounceScore(redScore, blueScore);
      };
      cb.AnnounceFlagDrop = delegate(int id, CtfTeam team)
      {
        Debug.Assert(team != CtfTeam.None);
        var tplr = TShock.Players[_revId[id]];
        RemoveCrown(tplr);
        DisplayTime();
        if (team == CtfTeam.Red)
        {
          _tiles.AddBlueFlag();
          AnnounceRedMessage("{0} dropped blue team's flag.", tplr.Name);
        }
        else
        {
          _tiles.AddRedFlag();
          AnnounceBlueMessage("{0} dropped red team's flag.", tplr.Name);
        }
      };
      cb.AnnounceGameStart = delegate
      {
        AnnounceMessage("The game has started! You have {0} to prepare your base!",
          CtfUtils.TimeToString(PrepTime, false));
        _tiles.AddSpawns();
        _tiles.AddFlags();
        _tiles.AddMiddleBlock();
        _timeLeft = PrepTime;

        foreach (var player in TShock.Players.Where(p => p != null && !_spectating[p.Index]
                                                                   && !_ctf.PlayerExists(p.User?.ID ?? -1)))
          GiveSpectate(player);
      };
      cb.AnnounceCombatStart = delegate
      {
        AnnounceMessage("Preparation phase has ended! Capture the other team's flag!");
        AnnounceMessage("First team to get 2 points more than the other team wins!");
        _tiles.RemoveMiddleBlock();
        _timeLeft = CombatTime;
      };
      cb.AnnounceSuddenDeath = delegate
      {
        AnnounceMessage("Sudden Death has started! Deaths are permanent!");
        AnnounceMessage("First team to touch other team's flag wins!");
        _timeLeft = SdTime;
      };
      cb.SetMediumcore = delegate(int id)
      {
        var tplr = TShock.Players[_revId[id]];
        if (SdDrops)
          SetDifficulty(tplr, 1);
      };
      cb.AnnounceGameEnd = delegate(CtfTeam winner, int redScore, int blueScore)
      {
        DisplayBlank();
        _timeLeft = ShutdownTime;
        AnnounceMessage("The game has ended with score of {0} - {1}.", redScore, blueScore);
        if (winner == CtfTeam.Red)
          AnnounceRedMessage("Congratulations to red team!");
        else if (winner == CtfTeam.Blue)
          AnnounceBlueMessage("Congratulations to blue team!");
        else
          AnnounceMessage("Game ended in a draw.");
        foreach (var tplr in TShock.Players)
        {
          if (tplr == null)
            continue;
          var ix = tplr.Index;
          var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
          var cusr = _loadedUser[ix];

          TakeSpectate(tplr);

          if (!_ctf.PlayerExists(id))
            continue;

          if (_ctf.GetPlayerTeam(id) == winner)
          {
            ++cusr.Wins;
            GiveCoins(tplr, CtfConfig.GainWin);
          }
          else if (winner != CtfTeam.None)
          {
            ++cusr.Loses;
            GiveCoins(tplr, CtfConfig.GainLose);
          }
          else
          {
            ++cusr.Draws;
            GiveCoins(tplr, CtfConfig.GainDraw);
          }
        }

        _pvp.Enforced = false;
      };
      cb.AnnounceGameAbort = delegate(string reason)
      {
        DisplayBlank();
        _timeLeft = ShutdownTime;
        AnnounceMessage("The game has been aborted.{0}",
          string.IsNullOrWhiteSpace(reason)
            ? ""
            : string.Format(" ({0})", reason));
        _pvp.Enforced = false;
      };
      cb.TellPlayerTeam = delegate(int id, CtfTeam team)
      {
        Debug.Assert(team != CtfTeam.None);
        var tplr = TShock.Players[_revId[id]];
        if (team == CtfTeam.Red)
          SendRedMessage(tplr, "You are on the red team. Your opponent is to the {0}.",
            _tiles.LeftTeam == CtfTeam.Red ? "right" : "left");
        else
          SendBlueMessage(tplr, "You are on the blue team. Your opponent is to the {0}.",
            _tiles.LeftTeam == CtfTeam.Blue ? "right" : "left");
      };
      cb.TellPlayerSelectClass = delegate(int id)
      {
        var tplr = TShock.Players[_revId[id]];
        tplr.SendInfoMessage("Select your class with {0}class.", Commands.Specifier);
      };
      cb.TellPlayerCurrentClass = delegate(int id, string cls)
      {
        var tplr = TShock.Players[_revId[id]];
        tplr.SendInfoMessage("Your class is {0}.", cls);
      };
      cb.AnnouncePlayerSwitchTeam = delegate(int id, CtfTeam team)
      {
        Debug.Assert(team != CtfTeam.None);
        var tplr = TShock.Players[_revId[id]];
        if (team == CtfTeam.Red)
          AnnounceRedMessage("{0} switched to red team.", tplr.Name);
        else
          AnnounceBlueMessage("{0} switched to blue team.", tplr.Name);
      };
      return cb;
    }

    public event EventHandler GameFinished;

    private void NextPhase()
    {
      if (_ctf.Phase == CtfPhase.Ended)
        GameFinished?.Invoke(this, EventArgs.Empty);
      else
        _ctf.NextPhase();
    }

    private void AddCrown(TSPlayer tplr)
    {
      var ix = tplr.Index;

      _hatForce[ix] = true;
      _originalHat[ix] = tplr.TPlayer.armor[CrownSlot];

      var crown = TShock.Utils.GetItemById(CrownId);
      tplr.TPlayer.armor[CrownSlot] = crown;
      SendHeadVanity(tplr);
    }

    private void RemoveCrown(TSPlayer tplr)
    {
      var ix = tplr.Index;
      _hatForce[ix] = false;
      tplr.TPlayer.armor[CrownSlot] = _originalHat[ix];
      SendHeadVanity(tplr);
    }

    private void SendHeadVanity(TSPlayer tplr)
    {
      var ix = tplr.Index;
      var item = tplr.TPlayer.armor[CrownSlot];
      TSPlayer.All.SendData(PacketTypes.PlayerSlot, "", ix, CrownNetSlot, item.prefix, item.stack, item.netID);
    }

    private void SendArmorHead(TSPlayer tplr)
    {
      var ix = tplr.Index;
      var item = tplr.TPlayer.armor[ArmorHeadSlot];
      TSPlayer.All.SendData(PacketTypes.PlayerSlot, "", ix, ArmorHeadNetSlot, item.prefix, item.stack, item.netID);
    }

    private bool CanUseClass(TSPlayer tplr, CtfClass cls)
    {
      bool hasPerm;
      if (cls.Name.StartsWith("*"))
        hasPerm = tplr.HasPermission(CtfPermissions.PackageUseAll);
      else
        hasPerm = tplr.HasPermission(CtfPermissions.ClassUseAll);

      if (hasPerm || cls.Price == 0 && cls.Sell)
        return true;
      return GetUser(tplr)?.HasClass(cls.Id) ?? false;
    }

    private bool CanSeeClass(TSPlayer tplr, CtfClass cls)
    {
      bool hasPerm;
      if (cls.Name.StartsWith("*"))
        hasPerm = tplr.HasPermission(CtfPermissions.PackageSeeAll);
      else
        hasPerm = tplr.HasPermission(CtfPermissions.ClassSeeAll);

      if (cls.Hidden && !hasPerm)
        return false;
      return true;
    }

    private void SetDifficulty(TSPlayer tplr, int diff)
    {
      tplr.Difficulty = diff;
      tplr.TPlayer.difficulty = (byte) diff;
      TSPlayer.All.SendData(PacketTypes.PlayerInfo, "", tplr.Index);
    }

    private void SetPlayerClass(TSPlayer tplr, CtfClass cls)
    {
      cls.CopyToPlayerData(tplr.PlayerData);
      tplr.PlayerData.RestoreCharacter(tplr);
    }

    private void GiveSpectate(TSPlayer tplr)
    {
      var ix = tplr.Index;
      _spectating[ix] = true;
      tplr.GodMode = true;
      _pvp.SetPvP(ix, false);
      SetPlayerClass(tplr, SpectateClass);
      _spectatorManager.StartSpectating(ix);
    }

    private void TakeSpectate(TSPlayer tplr)
    {
      var ix = tplr.Index;
      _spectating[ix] = false;
      tplr.GodMode = false;

      _spectatorManager.StopSpectating(ix);
    }

    private void SaveUser(CtfUser cusr)
    {
      if (_revId.ContainsKey(cusr.Id))
        _loadedUser[_revId[cusr.Id]] = cusr;
      _users.SaveUser(cusr);
    }

    private List<Tuple<string, Color>> GenerateList(TSPlayer tplr, bool forPackage)
    {
      var have = new List<Tuple<string, Color>>();
      var dontHave = new List<Tuple<string, Color>>();

      var allClasses = _classes.GetClasses();

      foreach (var cls in allClasses)
      {
        // skip template classes
        if (cls.Name.StartsWith("_"))
          continue;
        // skip package if generating class list and vice versa
        if (forPackage != cls.Name.StartsWith("*"))
          continue;

        // assign list to add to according to player's permission
        List<Tuple<string, Color>> addTo;
        Color color;
        if (!CanSeeClass(tplr, cls))
          continue;

        if (CanUseClass(tplr, cls))
        {
          addTo = have;
          color = Color.Yellow;
        }
        else
        {
          addTo = dontHave;
          color = Color.OrangeRed;
        }

        // format class text and add to the list
        addTo.Add(new Tuple<string, Color>(
          string.Format(
            CtfConfig.ListFormatting, // 0
            forPackage ? cls.Name.Remove(0, 1) : cls.Name, // 1
            cls.Price == 0
              ? (cls.Sell ? "Free" : "Locked")
              : CtfUtils.Pluralize(cls.Price, Singular, Plural), // 2
            string.IsNullOrWhiteSpace(cls.Description)
              ? CtfConfig.TextWhenNoDesc
              : cls.Description,
            CanUseClass(tplr, cls)
              ? CtfConfig.TextIfUsable
              : CtfConfig.TextIfUnusable, // 3
            cls.Hidden ? CtfConfig.AppendHidden : "" // 4
          ),
          color
        ));
      }

      have.AddRange(dontHave);
      return have;
    }

    private void GiveCoins(TSPlayer tplr, int amount, bool alert = true)
    {
      if (!tplr.HasPermission(CtfPermissions.BalGain))
        return;

      var ix = tplr.Index;
      var id = tplr.IsLoggedIn ? tplr.User.ID : -1;
      var cusr = _loadedUser[ix];

      var old = cusr.Coins;
      cusr.Coins += amount;
      if (cusr.Coins < 0)
        cusr.Coins = 0;
      var diff = cusr.Coins - old;

      SaveUser(cusr);
      if (alert && diff != 0)
        tplr.SendInfoMessage("You {0} {1}!",
          diff < 0 ? "lost" : "got",
          CtfUtils.Pluralize(Math.Abs(diff), Singular, Plural));
    }

    private bool FindUser(string name, out TSPlayer tplr, out User tusr, out CtfUser cusr)
    {
      var plrMatches = TShock.Utils.FindPlayer(name);
      if (plrMatches.Count == 1)
      {
        tplr = plrMatches[0];
        if (tplr.IsLoggedIn)
        {
          tusr = tplr.User;
          cusr = _loadedUser[tplr.Index];
          return true;
        }
      }

      var usr = TShock.Users.GetUserByName(name);
      if (usr == null)
      {
        tplr = null;
        tusr = null;
        cusr = null;
        return false;
      }

      if (_revId.ContainsKey(usr.ID))
      {
        tplr = TShock.Players[_revId[usr.ID]];
        tusr = tplr.User;
        cusr = _loadedUser[tplr.Index];
        return true;
      }

      tplr = null;
      tusr = usr;
      cusr = _users.GetUser(tusr.ID);
      return true;
    }

    private CtfUser GetUser(TSPlayer plr)
    {
      TSPlayer tplr;
      User tusr;
      CtfUser cusr;
      if (!plr.IsLoggedIn
          || !FindUser(plr.User.Name, out tplr, out tusr, out cusr))
        return null;
      return cusr;
    }

    #endregion
  }
}