using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace CGGCTF
{
  public static class CtfConfig
  {
    private static readonly string Path = System.IO.Path.Combine(TShock.SavePath, "cggctf.json");
    private static ActualConfig _instance;

    public static int WaitTime => _instance.WaitTime;
    public static int PrepTime => _instance.PrepTime;
    public static int CombatTime => _instance.CombatTime;
    public static int SuddenDeathTime => _instance.SuddenDeathTime;
    public static int ShutdownTime => _instance.ShutdownTime;

    public static int MinPlayerToStart => _instance.MinPlayerToStart;
    public static bool AbortGameOnNoPlayer => _instance.AbortGameOnNoPlayer;
    public static bool AssignTeamIgnoreOffline => _instance.AssignTeamIgnoreOffline;
    public static bool DisallowSpectatorJoin => _instance.DisableSpectatorJoin;
    public static bool SuddenDeathDrops => _instance.SuddenDeathDrops;

    public static int FlagDistance => _instance.FlagDistance;
    public static int SpawnDistance => _instance.SpawnDistance;
    public static int WallWidth => _instance.WallWidth;

    public static int RainTimer => _instance.RainTimer;
    public static int CursedTime => _instance.CursedTime;

    public static string MoneySingularName => _instance.MoneySingularName;
    public static string MoneyPluralName => _instance.MoneyPluralName;

    public static int GainKill => _instance.GainKill;
    public static int GainDeath => _instance.GainDeath;
    public static int GainAssist => _instance.GainAssist;
    public static int GainCapture => _instance.GainCapture;
    public static int GainWin => _instance.GainWin;
    public static int GainLose => _instance.GainLose;
    public static int GainDraw => _instance.GainDraw;

    public static string ListFormatting => _instance.ListFormatting;
    public static string TextWhenNoDesc => _instance.TextWhenNoDesc;
    public static string TextIfUsable => _instance.TextIfUsable;
    public static string TextIfUnusable => _instance.TextIfUnusable;
    public static string AppendHidden => _instance.AppendHidden;

    public static int ListLineCountIngame => _instance.ListLineCountIngame;
    public static int ListLineCountOutgame => _instance.ListLineCountOutgame;

    public static void Write()
    {
      _instance.Write(Path);
    }

    public static void Read()
    {
      _instance = ActualConfig.Read(Path);
    }

    private class ActualConfig
    {
      public readonly bool AbortGameOnNoPlayer = true;
      public readonly string AppendHidden = " (Hidden)";
      public readonly bool AssignTeamIgnoreOffline = true;
      public readonly int CombatTime = 60 * 15;
      public readonly int CursedTime = 180;
      public readonly bool DisableSpectatorJoin = true;

      public readonly int FlagDistance = 225;
      public readonly int GainAssist = 5;
      public readonly int GainCapture = 50;
      public readonly int GainDeath = -5;
      public readonly int GainDraw = 10;

      public readonly int GainKill = 10;
      public readonly int GainLose = 10;
      public readonly int GainWin = 30;

      public readonly string ListFormatting = "{0} ({1}): {2} {3}{4}";

      public readonly int ListLineCountIngame = 4;
      public readonly int ListLineCountOutgame = 20;

      public readonly int MinPlayerToStart = 2;
      public readonly string MoneyPluralName = "Coins";

      public readonly string MoneySingularName = "Coin";
      public readonly int PrepTime = 60 * 5;

      public readonly int RainTimer = 10;
      public readonly int ShutdownTime = 30;
      public readonly int SpawnDistance = 300;
      public readonly bool SuddenDeathDrops = true;
      public readonly int SuddenDeathTime = 60 * 5;
      public readonly string TextIfUnusable = "(Can't use)";
      public readonly string TextIfUsable = "(Can use)";
      public readonly string TextWhenNoDesc = "No Description";
      public readonly int WaitTime = 61;
      public readonly int WallWidth = 10;

      public void Write(string path)
      {
        File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
      }

      public static ActualConfig Read(string path)
      {
        return File.Exists(path)
          ? JsonConvert.DeserializeObject<ActualConfig>(File.ReadAllText(path))
          : new ActualConfig();
      }
    }
  }
}