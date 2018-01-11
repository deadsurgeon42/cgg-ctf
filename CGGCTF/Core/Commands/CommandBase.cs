using System;
using System.Collections.Generic;
using System.Linq;
using TShockAPI;

namespace CGGCTF.Core.Commands
{
  public abstract class CommandBase
  {
    public abstract string Name { get; }
    public virtual IEnumerable<string> Aliases { get; } = new string[0];
    public virtual IEnumerable<string> Permissions { get; } = new List<string>(0);

    public virtual string Summary { get; } = "";
    public virtual IEnumerable<string> Description { get; } = new string[0];

    public virtual bool AllowServer { get; } = true;
    public virtual bool DoLog { get; } = true;

    protected abstract void Execute(CommandArgs args);

    private void WrapExecute(CommandArgs args)
    {
      try
      {
        Execute(args);
      }
      catch (CommandException e)
      {
        args.Player.SendErrorMessage(e.Message);
      }
    }

    public virtual Command ToCommand() =>
      new Command(Permissions.ToList(), WrapExecute, new[] {Name}.Concat(Aliases).ToArray())
      {
        AllowServer = AllowServer,
        DoLog = DoLog,
        HelpText = Summary,
        HelpDesc = Description.ToArray()
      };

    public override string ToString()
      => (TShockAPI.Commands.Specifier ?? "/") + Name + " (" + string.Join(", ", Aliases) + ")";
  }
}