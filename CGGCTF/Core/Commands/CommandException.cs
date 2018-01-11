using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGGCTF.Core.Commands
{
  public class CommandException : Exception
  {
    public CommandException(string message)
    {
      Message = message;
    }

    public override string Message { get; }
  }
}