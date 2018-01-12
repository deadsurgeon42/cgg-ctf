using System.Collections.Generic;
using System.Linq;

namespace CGGCTF.Extensions
{
  public static class EnumerableExtensions
  {
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> list)
    {
      var li = list.ToList();
      for (var i = li.Count - 1; i >= 0; --i)
      {
        var j = CtfUtils.Random(i + 1);
        var temp = li[i];
        li[i] = li[j];
        li[j] = temp;
      }

      return li;
    }
  }
}