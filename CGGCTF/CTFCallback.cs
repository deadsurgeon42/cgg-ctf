using TShockAPI;

namespace CGGCTF
{
  public class CtfCallback
  {
    public delegate void AnnounceCaptureFlagD(int id, CtfTeam team, int redScore, int blueScore);

    public delegate void AnnounceCombatStartD();

    public delegate void AnnounceFlagDropD(int id, CtfTeam team);

    public delegate void AnnounceGameAbortD(string reason);

    public delegate void AnnounceGameEndD(CtfTeam winner, int redScore, int blueScore);

    public delegate void AnnounceGameStartD();

    public delegate void AnnounceGetFlagD(int id, CtfTeam team);

    public delegate void AnnouncePlayerSwitchTeamD(int id, CtfTeam team);

    public delegate void AnnounceSuddenDeathD();

    public delegate void DecidePositionsD();

    public delegate void InformPlayerJoinD(int id, CtfTeam team);

    public delegate void InformPlayerLeaveD(int id, CtfTeam team);

    public delegate void InformPlayerRejoinD(int id, CtfTeam team);

    public delegate PlayerData SaveInventoryD(int id);

    public delegate void SetInventoryD(int id, CtfClass cls);

    public delegate void SetMediumcoreD(int id);

    public delegate void SetPvPd(int id, bool pvp);

    public delegate void SetTeamD(int id, CtfTeam team);

    public delegate void TellPlayerCurrentClassD(int id, string name);

    public delegate void TellPlayerSelectClassD(int id);

    public delegate void TellPlayerTeamD(int id, CtfTeam team);

    public delegate void WarpToSpawnD(int id, CtfTeam team);

    public AnnounceCaptureFlagD AnnounceCaptureFlag;
    public AnnounceCombatStartD AnnounceCombatStart;
    public AnnounceFlagDropD AnnounceFlagDrop;
    public AnnounceGameAbortD AnnounceGameAbort;
    public AnnounceGameEndD AnnounceGameEnd;
    public AnnounceGameStartD AnnounceGameStart;
    public AnnounceGetFlagD AnnounceGetFlag;
    public AnnouncePlayerSwitchTeamD AnnouncePlayerSwitchTeam;
    public AnnounceSuddenDeathD AnnounceSuddenDeath;

    public DecidePositionsD DecidePositions;
    public InformPlayerJoinD InformPlayerJoin;
    public InformPlayerLeaveD InformPlayerLeave;
    public InformPlayerRejoinD InformPlayerRejoin;
    public SaveInventoryD SaveInventory;
    public SetInventoryD SetInventory;
    public SetMediumcoreD SetMediumcore;
    public SetPvPd SetPvP;
    public SetTeamD SetTeam;
    public TellPlayerCurrentClassD TellPlayerCurrentClass;
    public TellPlayerSelectClassD TellPlayerSelectClass;
    public TellPlayerTeamD TellPlayerTeam;
    public WarpToSpawnD WarpToSpawn;
  }
}