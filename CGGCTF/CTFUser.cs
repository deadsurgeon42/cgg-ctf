using System.Collections.Generic;

namespace CGGCTF
{
  public class CtfUser
  {
    public int Assists = 0;
    public List<int> Classes = new List<int>();
    public int Coins = 0;
    public int Deaths = 0;
    public int Draws = 0;
    public int Id = -1;
    public int Kills = 0;
    public int Loses = 0;
    public int Wins = 0;
    public double KdRatio => Deaths == 0 ? Kills : (double) Kills / Deaths;
    public double WlRatio => Loses == 0 ? Wins : (double) Wins / Loses;
    public int TotalGames => Wins + Loses + Draws;

    public bool HasClass(int cls)
    {
      return Classes.Contains(cls);
    }

    public void AddClass(int cls)
    {
      if (!HasClass(cls))
        Classes.Add(cls);
    }

    public void RemoveClass(int cls)
    {
      if (HasClass(cls))
        Classes.Remove(cls);
    }
  }
}