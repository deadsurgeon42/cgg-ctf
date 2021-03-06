﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using TShockAPI;
using CGGCTF.Extensions;

namespace CGGCTF
{
    public class CTFController
    {
        #region Variables

        CTFCallback cb;

        Dictionary<int, CTFPlayer> players;
        public CTFPhase Phase { get; private set; }
        public bool GameIsRunning {
            get {
                return Phase != CTFPhase.Lobby && Phase != CTFPhase.Ended;
            }
        }
        public bool IsPvPPhase {
            get {
                return Phase == CTFPhase.Combat || Phase == CTFPhase.SuddenDeath;
            }
        }

        public int TotalPlayer { get; private set; } = 0;
        public int OnlinePlayer { get; private set; } = 0;
        public int RedPlayer { get; private set; } = 0;
        public int BluePlayer { get; private set; } = 0;
        public int RedOnline { get; private set; } = 0;
        public int BlueOnline { get; private set; } = 0;

        public int RedFlagHolder { get; private set; } = -1;
        public int BlueFlagHolder { get; private set; } = -1;
        public bool RedFlagHeld {
            get {
                return RedFlagHolder != -1;
            }
        }
        public bool BlueFlagHeld {
            get {
                return BlueFlagHolder != -1;
            }
        }

        public int RedScore { get; private set; } = 0;
        public int BlueScore { get; private set; } = 0;

        #endregion

        public CTFController(CTFCallback cb)
        {
            players = new Dictionary<int, CTFPlayer>();
            this.cb = cb;
        }

        #region Helper functions

        public bool PlayerExists(int id)
        {
            return players.ContainsKey(id);
        }

        public bool HasPickedClass(int id)
        {
            return players[id].PickedClass;
        }

        public CTFTeam GetPlayerTeam(int id)
        {
            if (!PlayerExists(id))
                return CTFTeam.None;
            return players[id].Team;
        }
        
        public bool PlayerDead(int id)
        {
            return players[id].Dead;
        }

        CTFTeam checkQuickEnd()
        {
            if (Math.Abs(RedScore - BlueScore) >= 2) {
                return checkWinnerByTime();
            }

            return CTFTeam.None;
        }

        CTFTeam checkWinnerByTime()
        {
            var win = CTFTeam.None;
            if (RedScore > BlueScore || BlueOnline <= 0)
                win = CTFTeam.Red;
            else if (BlueScore > RedScore || RedOnline <= 0)
                win = CTFTeam.Blue;
            else if (RedFlagHeld != BlueFlagHeld)
                win = RedFlagHeld ? CTFTeam.Blue : CTFTeam.Red;
            return win;
        }

        void assignTeam(int id)
        {
            Debug.Assert(PlayerExists(id));
            
            if (players[id].Team != CTFTeam.None)
                return;

            bool redLTblue, blueLTred;
            if (CTFConfig.AssignTeamIgnoreOffline) {
                redLTblue = RedOnline < BlueOnline;
                blueLTred = BlueOnline < RedOnline;
            } else {
                redLTblue = RedPlayer < BluePlayer;
                blueLTred = BluePlayer < RedPlayer;
            }

            if (redLTblue) {
                players[id].Team = CTFTeam.Red;
                ++RedPlayer;
            } else if (blueLTred) {
                players[id].Team = CTFTeam.Blue;
                ++BluePlayer;
            } else {
                int randnum = CTFUtils.Random(2);
                if (randnum == 0) {
                    players[id].Team = CTFTeam.Red;
                    ++RedPlayer;
                } else {
                    players[id].Team = CTFTeam.Blue;
                    ++BluePlayer;
                }
            }
        }

        void getPlayerStarted(int id)
        {
            setTeam(id);
            setPvP(id);
            tellPlayerTeam(id);
            if (!players[id].PickedClass) {
                tellPlayerSelectClass(id);
            } else {
                tellPlayerCurrentClass(id);
                setInventory(id);
            }
            warpToSpawn(id);
        }

        bool checkSufficientPlayer()
        {
            if (!CTFConfig.AbortGameOnNoPlayer)
                return true;
            if (Phase == CTFPhase.Lobby) {
                if (OnlinePlayer < 2)
                    return false;
            } else if (Phase == CTFPhase.Preparation) {
                if (RedOnline <= 0 || BlueOnline <= 0)
                    return false;
            }
            return true;
        }

        #endregion

        #region Main functions

        public void JoinGame(int id)
        {
            Debug.Assert(Phase != CTFPhase.SuddenDeath);
            Debug.Assert(!PlayerExists(id));
            players[id] = new CTFPlayer();
            players[id].Online = true;
            ++TotalPlayer;
            ++OnlinePlayer;
            if (GameIsRunning) {
                assignTeam(id);
                if (players[id].Team == CTFTeam.Red)
                    ++RedOnline;
                else
                    ++BlueOnline;
                informPlayerJoin(id);
                getPlayerStarted(id);
            } else {
                informPlayerJoin(id);
            }
        }

        public void RejoinGame(int id)
        {
            Debug.Assert(Phase != CTFPhase.SuddenDeath);
            Debug.Assert(PlayerExists(id));
            Debug.Assert(GameIsRunning);
            players[id].Online = true;
            if (!players[id].Dead) {
                if (players[id].Team == CTFTeam.Red)
                    ++RedOnline;
                if (players[id].Team == CTFTeam.Blue)
                    ++BlueOnline;
                informPlayerRejoin(id);
                getPlayerStarted(id);
            }
            ++OnlinePlayer;
        }

        public void LeaveGame(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (GameIsRunning) {
                if (Phase != CTFPhase.SuddenDeath)
                    players[id].Online = false;
                if (!players[id].Dead) {
                    if (players[id].Team == CTFTeam.Red)
                        --RedOnline;
                    else if (players[id].Team == CTFTeam.Blue)
                        --BlueOnline;
                }
                if (players[id].PickedClass)
                    saveInventory(id);
                FlagDrop(id);
                informPlayerLeave(id);
                if (Phase == CTFPhase.SuddenDeath) {
                    if (RedOnline <= 0)
                        EndGame(CTFTeam.Blue);
                    else if (BlueOnline <= 0)
                        EndGame(CTFTeam.Red);
                }
            } else {
                players.Remove(id);
                --TotalPlayer;
            }
            --OnlinePlayer;
        }

        public void PickClass(int id, CTFClass cls)
        {
            Debug.Assert(PlayerExists(id));
            Debug.Assert(GameIsRunning);
            players[id].Class = cls;
            tellPlayerCurrentClass(id);
            setInventory(id);
        }

        public void GetFlag(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Team == CTFTeam.Red) {
                if (!BlueFlagHeld) {
                    announceGetFlag(id);
                    BlueFlagHolder = id;
                    if (Phase == CTFPhase.SuddenDeath)
                        EndGame(CTFTeam.Red);
                }
            } else if (players[id].Team == CTFTeam.Blue) {
                if (!RedFlagHeld) {
                    announceGetFlag(id);
                    RedFlagHolder = id;
                    if (Phase == CTFPhase.SuddenDeath)
                        EndGame(CTFTeam.Blue);
                }
            }
        }

        public void CaptureFlag(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Team == CTFTeam.Red) {
                if (BlueFlagHolder == id) {
                    BlueFlagHolder = -1;
                    ++RedScore;
                    announceCaptureFlag(id);
                }
            } else if (players[id].Team == CTFTeam.Blue) {
                if (RedFlagHolder == id) {
                    RedFlagHolder = -1;
                    ++BlueScore;
                    announceCaptureFlag(id);
                }
            }

            var win = checkQuickEnd();
            if (win != CTFTeam.None)
                EndGame(win);
        }

        public void FlagDrop(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Team == CTFTeam.Red) {
                if (BlueFlagHolder == id) {
                    announceFlagDrop(id);
                    BlueFlagHolder = -1;
                }
            } else if (players[id].Team == CTFTeam.Blue) {
                if (RedFlagHolder == id) {
                    announceFlagDrop(id);
                    RedFlagHolder = -1;
                }
            }
        }

        public void NextPhase()
        {
            if (!checkSufficientPlayer()) {
                AbortGame("Insufficient players");
                return;
            }
            if (Phase == CTFPhase.Lobby)
                StartGame();
            else if (Phase == CTFPhase.Preparation)
                StartCombat();
            else if (Phase == CTFPhase.Combat)
                GameTimeout();
            else if (Phase == CTFPhase.SuddenDeath)
                EndGame(CTFTeam.None);
        }

        public void StartGame()
        {
            Debug.Assert(Phase == CTFPhase.Lobby);
            Phase = CTFPhase.Preparation;
            decidePositions();
            announceGameStart();
            var shuffledKeys = players.Keys.Shuffle();
            foreach (var id in shuffledKeys) {
                var player = players[id];
                Debug.Assert(player.Team == CTFTeam.None);
                assignTeam(id);
                if (players[id].Team == CTFTeam.Red)
                    ++RedOnline;
                else
                    ++BlueOnline;
                getPlayerStarted(id);
            }
        }

        public void StartCombat()
        {
            Debug.Assert(Phase == CTFPhase.Preparation);
            Phase = CTFPhase.Combat;
            announceCombatStart();
            foreach (var id in players.Keys) {
                var player = players[id];
                warpToSpawn(id);
                setPvP(id);
            }
        }

        public void GameTimeout()
        {
            Debug.Assert(Phase == CTFPhase.Combat);
            var win = checkWinnerByTime();
            if (win == CTFTeam.None)
                StartSuddenDeath();
            else
                EndGame(win);
        }

        public void StartSuddenDeath()
        {
            Debug.Assert(Phase == CTFPhase.Combat);
            Phase = CTFPhase.SuddenDeath;
            announceSuddenDeath();
            foreach (var id in players.Keys) {
                var player = players[id];
                warpToSpawn(id);
                setMediumcore(id);
            }
        }

        public void SDDeath(int id)
        {
            Debug.Assert(Phase == CTFPhase.SuddenDeath);
            Debug.Assert(PlayerExists(id));

            var plr = players[id];
            plr.Dead = true;
            if (plr.Team == CTFTeam.Red) {
                if (--RedOnline <= 0)
                    EndGame(CTFTeam.Blue);
            } else if (plr.Team == CTFTeam.Blue) {
                if (--BlueOnline <= 0)
                    EndGame(CTFTeam.Red);
            }
        }

        public void EndGame(CTFTeam winner)
        {
            Debug.Assert(GameIsRunning);
            Phase = CTFPhase.Ended;
            announceGameEnd(winner);
        }

        public void AbortGame(string reason)
        {
            Phase = CTFPhase.Ended;
            announceGameAbort(reason);
        }

        public bool SwitchTeam(int id, CTFTeam team = CTFTeam.None)
        {
            Debug.Assert(PlayerExists(id));
            Debug.Assert(players[id].Team != CTFTeam.None);

            if (players[id].Team == team)
                return false;
            
            if (players[id].Team == CTFTeam.Red) {
                players[id].Team = CTFTeam.Blue;
                --RedPlayer;
                ++BluePlayer;
            } else {
                players[id].Team = CTFTeam.Red;
                --BluePlayer;
                ++RedPlayer;
            }

            setTeam(id);
            setPvP(id);
            announcePlayerSwitchTeam(id);
            warpToSpawn(id);
            return true;
        }

        #endregion

        #region Callback managers

        void decidePositions()
        {
            cb.DecidePositions();
        }

        void setTeam(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Online)
                cb.SetTeam(id, players[id].Team);
        }

        void setPvP(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Online)
                cb.SetPvP(id, IsPvPPhase);
        }

        void setInventory(int id)
        {
            Debug.Assert(PlayerExists(id));
            Debug.Assert(players[id].PickedClass);
            Debug.Assert(players[id].Online);
            cb.SetInventory(id, players[id].Class);
        }

        void saveInventory(int id)
        {
            Debug.Assert(PlayerExists(id));
            Debug.Assert(players[id].PickedClass);
            PlayerData toSave = cb.SaveInventory(id);
            players[id].Data = toSave;
        }

        void warpToSpawn(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Online)
                cb.WarpToSpawn(id, players[id].Team);
        }

        void informPlayerJoin(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.InformPlayerJoin(id, players[id].Team);
        }

        void informPlayerRejoin(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.InformPlayerRejoin(id, players[id].Team);
        }

        void informPlayerLeave(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.InformPlayerLeave(id, players[id].Team);
        }

        void announceGetFlag(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.AnnounceGetFlag(id, players[id].Team);
        }

        void announceCaptureFlag(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.AnnounceCaptureFlag(id, players[id].Team, RedScore, BlueScore);
        }

        void announceFlagDrop(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.AnnounceFlagDrop(id, players[id].Team);
        }

        void announceGameStart()
        {
            cb.AnnounceGameStart();
        }

        void announceCombatStart()
        {
            cb.AnnounceCombatStart();
        }

        void announceSuddenDeath()
        {
            cb.AnnounceSuddenDeath();
        }

        void announceGameEnd(CTFTeam winner)
        {
            cb.AnnounceGameEnd(winner, RedScore, BlueScore);
        }

        void announceGameAbort(string reason)
        {
            cb.AnnounceGameAbort(reason);
        }

        void tellPlayerTeam(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.TellPlayerTeam(id, players[id].Team);
        }

        void tellPlayerSelectClass(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.TellPlayerSelectClass(id);
        }

        void tellPlayerCurrentClass(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.TellPlayerCurrentClass(id, players[id].Class.Name);
        }

        void announcePlayerSwitchTeam(int id)
        {
            Debug.Assert(PlayerExists(id));
            cb.AnnouncePlayerSwitchTeam(id, players[id].Team);
        }

        void setMediumcore(int id)
        {
            Debug.Assert(PlayerExists(id));
            if (players[id].Online)
                cb.SetMediumcore(id);
        }

        #endregion
    }
}
