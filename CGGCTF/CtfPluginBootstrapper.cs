using System;
using System.Threading.Tasks;
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

    private void InstantiatePlugin(EventArgs args) => InstantiatePlugin();
    private void ReinstantiatePlugin(CommandArgs args) => ReinstantiatePlugin();

    private void InstantiatePlugin()
    {
      Plugin = new CtfPlugin(this);
      Plugin.Initialize();

      Plugin.GameFinished += (sender, e) => Task.Run(() =>
      {
        WorldRegeneration.RegenerateWorld(this);
        ReinstantiatePlugin();
      });
    }

    private void ReinstantiatePlugin()
    {
      Plugin?.Dispose();

      InstantiatePlugin();
    }

    protected override void Dispose(bool disposing)
    {
      ServerApi.Hooks.GamePostInitialize.Deregister(this, InstantiatePlugin);

      Plugin?.Dispose();

      base.Dispose(disposing);
    }
  }
}