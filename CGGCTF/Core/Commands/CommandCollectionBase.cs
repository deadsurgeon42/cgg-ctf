using System;
using System.Collections.Generic;
using System.Linq;
using TShockAPI;

namespace CGGCTF.Core.Commands
{
  public abstract class CommandCollectionBase : CommandBase
  {
    private Dictionary<string, Command> _subcommands;

    private IEnumerable<Command> GetSubcommands()
    {
      return GetType().GetNestedTypes()
        .Where(p => p.IsClass && !p.IsAbstract && p.IsAssignableFrom(typeof(CommandBase)))
        .Select(t => ((CommandBase) Activator.CreateInstance(t)).ToCommand());
    }

    public override Command ToCommand()
    {
      _subcommands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
      
      foreach (var cmd in GetSubcommands())
      foreach (var cmdName in cmd.Names)
        _subcommands[cmdName] = cmd;

      return base.ToCommand();
    }

    protected override void Execute(CommandArgs args)
    {
      if (args.Parameters.Count < 1 || string.IsNullOrWhiteSpace(args.Parameters[0]))
      {
        args.Player.SendErrorMessage("No subcommand specified. Subcommands:");
        SendSubcommandList();
        return;
      }

      if (!_subcommands.TryGetValue(args.Parameters[0].ToLowerInvariant(), out var cmd))
      {
        args.Player.SendErrorMessage("Invalid subcommand specified. Subcommands:");
        SendSubcommandList();
        return;
      }
      
      cmd.Run(args.Message, args.Silent, args.Player, args.Parameters.Skip(1).ToList());

      void SendSubcommandList()
      {
        args.Player.SendInfoMessage("{0}{1} {2}", TShockAPI.Commands.Specifier ?? "/", Name,
          string.Join(", ", _subcommands.Values.Where(sc => sc.Permissions.Any(args.Player.HasPermission))
            .Select(sc => sc.Name.Substring(1))));
      }
    }
  }
}