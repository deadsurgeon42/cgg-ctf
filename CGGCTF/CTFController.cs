using System;
using System.Collections.Generic;
using System.Diagnostics;
using CGGCTF.Extensions;

namespace CGGCTF
{
  public class CtfController
  {
    public CtfController(CtfCallback cb)
    {
      _players = new Dictionary<int, CtfPlayer>();
      this._cb = cb;
    }

    #region Variables

    private readonly CtfCallback _cb;

    private readonly Dictionary<int, CtfPlayer> _players;
    public CtfPhase Phase { get; private set; }

    public bool GameIsRunning => Phase != CtfPhase.Lobby && Phase != CtfPhase.Ended;

    public bool IsPvPPhase => Phase == CtfPhase.Combat || Phase == CtfPhase.SuddenDeath;

    public int TotalPlayer { get; private set; }
    public int OnlinePlayer { get; private set; }
    public int RedPlayer { get; private set; }
    public int BluePlayer { get; private set; }
    public int RedOnline { get; private set; }
    public int BlueOnline { get; private set; }

    public int RedFlagHolder { get; private set; } = -1;
    public int BlueFlagHolder { get; private set; } = -1;

    public bool RedFlagHeld => RedFlagHolder != -1;

    public bool BlueFlagHeld => BlueFlagHolder != -1;

    public int RedScore { get; private set; }
    public int BlueScore { get; private set; }

    #endregion

    #region Helper functions

    public bool PlayerExists(int id)
    {
      return _players.ContainsKey(id);
    }

    public bool HasPickedClass(int id)
    {
      return _players[id].PickedClass;
    }

    public CtfTeam GetPlayerTeam(int id)
    {
      if (!PlayerExists(id))
        return CtfTeam.None;
      return _players[id].Team;
    }

    public bool PlayerDead(int id)
    {
      return _players[id].Dead;
    }

    private CtfTeam CheckQuickEnd()
    {
      if (Math.Abs(RedScore - BlueScore) >= 2) return CheckWinnerByTime();

      return CtfTeam.None;
    }

    private CtfTeam CheckWinnerByTime()
    {
      var win = CtfTeam.None;
      if (RedScore > BlueScore || BlueOnline <= 0)
        win = CtfTeam.Red;
      else if (BlueScore > RedScore || RedOnline <= 0)
        win = CtfTeam.Blue;
      else if (RedFlagHeld != BlueFlagHeld)
        win = RedFlagHeld ? CtfTeam.Blue : CtfTeam.Red;
      return win;
    }

    private void AssignTeam(int id)
    {
      Debug.Assert(PlayerExists(id));

      if (_players[id].Team != CtfTeam.None)
        return;

      bool redLTblue, blueLTred;
      if (CtfConfig.AssignTeamIgnoreOffline)
      {
        redLTblue = RedOnline < BlueOnline;
        blueLTred = BlueOnline < RedOnline;
      }
      else
      {
        redLTblue = RedPlayer < BluePlayer;
        blueLTred = BluePlayer < RedPlayer;
      }

      if (redLTblue)
      {
        _players[id].Team = CtfTeam.Red;
        ++RedPlayer;
      }
      else if (blueLTred)
      {
        _players[id].Team = CtfTeam.Blue;
        ++BluePlayer;
      }
      else
      {
        var randnum = CtfUtils.Random(2);
        if (randnum == 0)
        {
          _players[id].Team = CtfTeam.Red;
          ++RedPlayer;
        }
        else
        {
          _players[id].Team = CtfTeam.Blue;
          ++BluePlayer;
        }
      }
    }

    private void GetPlayerStarted(int id)
    {
      SetTeam(id);
      SetPvP(id);
      TellPlayerTeam(id);
      if (!_players[id].PickedClass)
      {
        TellPlayerSelectClass(id);
      }
      else
      {
        TellPlayerCurrentClass(id);
        SetInventory(id);
      }

      WarpToSpawn(id);
    }

    private bool CheckSufficientPlayer()
    {
      if (!CtfConfig.AbortGameOnNoPlayer)
        return true;
      if (Phase == CtfPhase.Lobby)
      {
        if (OnlinePlayer < 2)
          return false;
      }
      else if (Phase == CtfPhase.Preparation)
      {
        if (RedOnline <= 0 || BlueOnline <= 0)
          return false;
      }

      return true;
    }

    #endregion

    #region Main functions

    public void JoinGame(int id)
    {
      Debug.Assert(Phase != CtfPhase.SuddenDeath);
      Debug.Assert(!PlayerExists(id));
      _players[id] = new CtfPlayer();
      _players[id].Online = true;
      ++TotalPlayer;
      ++OnlinePlayer;
      if (GameIsRunning)
      {
        AssignTeam(id);
        if (_players[id].Team == CtfTeam.Red)
          ++RedOnline;
        else
          ++BlueOnline;
        InformPlayerJoin(id);
        GetPlayerStarted(id);
      }
      else
      {
        InformPlayerJoin(id);
      }
    }

    public void RejoinGame(int id)
    {
      Debug.Assert(Phase != CtfPhase.SuddenDeath);
      Debug.Assert(PlayerExists(id));
      Debug.Assert(GameIsRunning);
      _players[id].Online = true;
      if (!_players[id].Dead)
      {
        if (_players[id].Team == CtfTeam.Red)
          ++RedOnline;
        if (_players[id].Team == CtfTeam.Blue)
          ++BlueOnline;
        InformPlayerRejoin(id);
        GetPlayerStarted(id);
      }

      ++OnlinePlayer;
    }

    public void LeaveGame(int id)
    {
      Debug.Assert(PlayerExists(id));
      if (GameIsRunning)
      {
        if (Phase != CtfPhase.SuddenDeath)
          _players[id].Online = false;
        if (!_players[id].Dead)
          if (_players[id].Team == CtfTeam.Red)
            --RedOnline;
          else if (_players[id].Team == CtfTeam.Blue)
            --BlueOnline;
        if (_players[id].PickedClass)
          SaveInventory(id);
        FlagDrop(id);
        InformPlayerLeave(id);
        if (Phase == CtfPhase.SuddenDeath)
          if (RedOnline <= 0)
            EndGame(CtfTeam.Blue);
          else if (BlueOnline <= 0)
            EndGame(CtfTeam.Red);
      }
      else
      {
        _players.Remove(id);
        --TotalPlayer;
      }

      --OnlinePlayer;
    }

    public void PickClass(int id, CtfClass cls)
    {
      Debug.Assert(PlayerExists(id));
      Debug.Assert(GameIsRunning);
      _players[id].Class = cls;
      TellPlayerCurrentClass(id);
      SetInventory(id);
    }

    public void GetFlag(int id)
    {
      Debug.Assert(PlayerExists(id));
      if (_players[id].Team == CtfTeam.Red)
      {
        if (!BlueFlagHeld)
        {
          AnnounceGetFlag(id);
          BlueFlagHolder = id;
          if (Phase == CtfPhase.SuddenDeath)
            EndGame(CtfTeam.Red);
        }
      }
      else if (_players[id].Team == CtfTeam.Blue)
      {
        if (!RedFlagHeld)
        {
          AnnounceGetFlag(id);
          RedFlagHolder = id;
          if (Phase == CtfPhase.SuddenDeath)
            EndGame(CtfTeam.Blue);
        }
      }
    }

    public void CaptureFlag(int id)
    {
      Debug.Assert(PlayerExists(id));
      if (_players[id].Team == CtfTeam.Red)
      {
        if (BlueFlagHolder == id)
        {
          BlueFlagHolder = -1;
          ++RedScore;
          AnnounceCaptureFlag(id);
        }
      }
      else if (_players[id].Team == CtfTeam.Blue)
      {
        if (RedFlagHolder == id)
        {
          RedFlagHolder = -1;
          ++BlueScore;
          AnnounceCaptureFlag(id);
        }
      }

      var win = CheckQuickEnd();
      if (win != CtfTeam.None)
        EndGame(win);
    }

    public void FlagDrop(int id)
    {
      Debug.Assert(PlayerExists(id));
      if (_players[id].Team == CtfTeam.Red)
      {
        if (BlueFlagHolder == id)
        {
          AnnounceFlagDrop(id);
          BlueFlagHolder = -1;
        }
      }
      else if (_players[id].Team == CtfTeam.Blue)
      {
        if (RedFlagHolder == id)
        {
          AnnounceFlagDrop(id);
          RedFlagHolder = -1;
        }
      }
    }

    public void NextPhase()
    {
      if (!CheckSufficientPlayer())
      {
        AbortGame("Insufficient players");
        return;
      }

      if (Phase == CtfPhase.Lobby)
        StartGame();
      else if (Phase == CtfPhase.Preparation)
        StartCombat();
      else if (Phase == CtfPhase.Combat)
        GameTimeout();
      else if (Phase == CtfPhase.SuddenDeath)
        EndGame(CtfTeam.None);
    }

    public void StartGame()
    {
      Debug.Assert(Phase == CtfPhase.Lobby);
      Phase = CtfPhase.Preparation;
      DecidePositions();
      AnnounceGameStart();
      var shuffledKeys = _players.Keys.Shuffle();
      foreach (var id in shuffledKeys)
      {
        var player = _players[id];
        Debug.Assert(player.Team == CtfTeam.None);
        AssignTeam(id);
        if (_players[id].Team == CtfTeam.Red)
          ++RedOnline;
        else
          ++BlueOnline;
        GetPlayerStarted(id);
      }
    }

    public void StartCombat()
    {
      Debug.Assert(Phase == CtfPhase.Preparation);
      Phase = CtfPhase.Combat;
      AnnounceCombatStart();
      foreach (var id in _players.Keys)
      {
        var player = _players[id];
        WarpToSpawn(id);
        SetPvP(id);
      }
    }

    public void GameTimeout()
    {
      Debug.Assert(Phase == CtfPhase.Combat);
      var win = CheckWinnerByTime();
      if (win == CtfTeam.None)
        StartSuddenDeath();
      else
        EndGame(win);
    }

    public void StartSuddenDeath()
    {
      Debug.Assert(Phase == CtfPhase.Combat);
      Phase = CtfPhase.SuddenDeath;
      AnnounceSuddenDeath();
      foreach (var id in _players.Keys)
      {
        var player = _players[id];
        WarpToSpawn(id);
        SetMediumcore(id);
      }
    }

    public void SdDeath(int id)
    {
      Debug.Assert(Phase == CtfPhase.SuddenDeath);
      Debug.Assert(PlayerExists(id));

      var plr = _players[id];
      plr.Dead = true;
      if (plr.Team == CtfTeam.Red)
      {
        if (--RedOnline <= 0)
          EndGame(CtfTeam.Blue);
      }
      else if (plr.Team == CtfTeam.Blue)
      {
        if (--BlueOnline <= 0)
          EndGame(CtfTeam.Red);
      }
    }

    public void EndGame(CtfTeam winner)
    {
      Debug.Assert(GameIsRunning);
      Phase = CtfPhase.Ended;
      AnnounceGameEnd(winner);
    }

    public void AbortGame(string reason)
    {
      Phase = CtfPhase.Ended;
      AnnounceGameAbort(reason);
    }

    public bool SwitchTeam(int id, CtfTeam team = CtfTeam.None)
    {
      Debug.Assert(PlayerExists(id));
      Debug.Assert(_players[id].Team != CtfTeam.None);

      if (_players[id].Team == team)
        return false;

      if (_players[id].Team == CtfTeam.Red)
      {
        _players[id].Team = CtfTeam.Blue;
        --RedPlayer;
        ++BluePlayer;
      }
      else
      {
        _players[id].Team = CtfTeam.Red;
        --BluePlayer;
        ++RedPlayer;
      }

      SetTeam(id);
      SetPvP(id);
      AnnouncePlayerSwitchTeam(id);
      WarpToSpawn(id);
      return true;
    }

    #endregion

    #region Callback managers

    private void DecidePositions()
    {
      _cb.DecidePositions();
    }

    private void SetTeam(int id)
    {
      Debug.Assert(PlayerExists(id));
      if (_players[id].Online)
        _cb.SetTeam(id, _players[id].Team);
    }

    private void SetPvP(int id)
    {
      Debug.Assert(PlayerExists(id));
      if (_players[id].Online)
        _cb.SetPvP(id, IsPvPPhase);
    }

    private void SetInventory(int id)
    {
      Debug.Assert(PlayerExists(id));
      Debug.Assert(_players[id].PickedClass);
      Debug.Assert(_players[id].Online);
      _cb.SetInventory(id, _players[id].Class);
    }

    private void SaveInventory(int id)
    {
      Debug.Assert(PlayerExists(id));
      Debug.Assert(_players[id].PickedClass);
      var toSave = _cb.SaveInventory(id);
      _players[id].Data = toSave;
    }

    private void WarpToSpawn(int id)
    {
      Debug.Assert(PlayerExists(id));
      if (_players[id].Online)
        _cb.WarpToSpawn(id, _players[id].Team);
    }

    private void InformPlayerJoin(int id)
    {
      Debug.Assert(PlayerExists(id));
      _cb.InformPlayerJoin(id, _players[id].Team);
    }

    private void InformPlayerRejoin(int id)
    {
      Debug.Assert(PlayerExists(id));
      _cb.InformPlayerRejoin(id, _players[id].Team);
    }

    private void InformPlayerLeave(int id)
    {
      Debug.Assert(PlayerExists(id));
      _cb.InformPlayerLeave(id, _players[id].Team);
    }

    private void AnnounceGetFlag(int id)
    {
      Debug.Assert(PlayerExists(id));
      _cb.AnnounceGetFlag(id, _players[id].Team);
    }

    private void AnnounceCaptureFlag(int id)
    {
      Debug.Assert(PlayerExists(id));
      _cb.AnnounceCaptureFlag(id, _players[id].Team, RedScore, BlueScore);
    }

    private void AnnounceFlagDrop(int id)
    {
      Debug.Assert(PlayerExists(id));
      _cb.AnnounceFlagDrop(id, _players[id].Team);
    }

    private void AnnounceGameStart()
    {
      _cb.AnnounceGameStart();
    }

    private void AnnounceCombatStart()
    {
      _cb.AnnounceCombatStart();
    }

    private void AnnounceSuddenDeath()
    {
      _cb.AnnounceSuddenDeath();
    }

    private void AnnounceGameEnd(CtfTeam winner)
    {
      _cb.AnnounceGameEnd(winner, RedScore, BlueScore);
    }

    private void AnnounceGameAbort(string reason)
    {
      _cb.AnnounceGameAbort(reason);
    }

    private void TellPlayerTeam(int id)
    {
      Debug.Assert(PlayerExists(id));
      _cb.TellPlayerTeam(id, _players[id].Team);
    }

    private void TellPlayerSelectClass(int id)
    {
      Debug.Assert(PlayerExists(id));
      _cb.TellPlayerSelectClass(id);
    }

    private void TellPlayerCurrentClass(int id)
    {
      Debug.Assert(PlayerExists(id));
      _cb.TellPlayerCurrentClass(id, _players[id].Class.Name);
    }

    private void AnnouncePlayerSwitchTeam(int id)
    {
      Debug.Assert(PlayerExists(id));
      _cb.AnnouncePlayerSwitchTeam(id, _players[id].Team);
    }

    private void SetMediumcore(int id)
    {
      Debug.Assert(PlayerExists(id));
      if (_players[id].Online)
        _cb.SetMediumcore(id);
    }

    #endregion
  }
}