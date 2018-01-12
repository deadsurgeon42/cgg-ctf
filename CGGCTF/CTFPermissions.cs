using System.Collections.Generic;

namespace CGGCTF
{
  public static class CtfPermissions
  {
    public const string Play = "ctf.game.play";
    public const string Spectate = "ctf.game.spectate";
    public const string Skip = "ctf.game.skip";
    public const string Extend = "ctf.game.extend";
    public const string SwitchTeam = "ctf.game.switchteam";

    public const string ClassBuy = "ctf.class.buy";
    public const string ClassSeeAll = "ctf.class.seeall";
    public const string ClassBuyAll = "ctf.class.buyall";
    public const string ClassUseAll = "ctf.class.useall";
    public const string ClassEdit = "ctf.class.edit";

    public const string PackageUse = "ctf.pkg.use";
    public const string PackageBuy = "ctf.pkg.buy";
    public const string PackageSeeAll = "ctf.pkg.seeall";
    public const string PackageBuyAll = "ctf.pkg.buyall";
    public const string PackageUseAll = "ctf.pkg.useall";
    public const string PackageEdit = "ctf.pkg.edit";

    public const string BalCheck = "ctf.bal.check.self";
    public const string BalCheckOther = "ctf.bal.check.others";
    public const string BalEdit = "ctf.bal.edit";
    public const string BalGain = "ctf.bal.gain";

    public const string StatsSelf = "ctf.stats.self";
    public const string StatsOther = "ctf.stats.others";

    public const string IgnoreInteract = "ctf.ignore.interact";
    public const string IgnoreTempgroup = "ctf.ignore.tempgroup";
    public const string IgnoreSpecJoin = "ctf.ignore.specjoin";

    public const string Reload = "ctf.reload";
  }
}