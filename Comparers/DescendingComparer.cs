using System;
using System.Collections.Generic;
using System.Text;

namespace Vac.Graphics.Dictionary
{
  public class DescendingComparer<T> : IComparer<T> where T : IComparable<T>
  {
    public int Compare(T x, T y)
    {
      return y.CompareTo(x);
    }
  }
}
