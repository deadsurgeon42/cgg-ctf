using System;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace CGGCTF
{
  [ApiVersion(2, 1)]
  public sealed class CtfPluginBootstrapper : TerrariaPlugin
  {
    public override string Name => "CGGCTF";
    public override string Description => "Automated CTF game for CatGiveGames Server";
    public override string Author => "AquaBlitz11";
    public override Version Version => typeof(CtfPluginBootstrapper).Assembly.GetName().Version;

    public static CtfPlugin Plugin { get; private set; }

    public CtfPluginBootstrapper(Main game) : base(game)
    {
    }

    public override void Initialize()
    {
      ServerApi.Hooks.GamePostInitialize.Register(this, InstantiatePlugin);

      Commands.ChatCommands.Add(new Command(CtfPermissions.Reload, ReinstantiatePlugin, "ctfreload"));
    }

    private void InstantiatePlugin(EventArgs args)
    {
      Plugin = new CtfPlugin(this);
      Plugin.Initialize();
    }

    private void ReinstantiatePlugin(CommandArgs args)
    {
      Plugin?.Dispose();

      Plugin = new CtfPlugin(this);
      Plugin.Initialize();
    }

    protected override void Dispose(bool disposing)
    {
      ServerApi.Hooks.GamePostInitialize.Deregister(this, InstantiatePlugin);

      Plugin?.Dispose();

      base.Dispose(disposing);
    }
  }
}