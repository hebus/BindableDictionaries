using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;

namespace Vac.Graphics.Dictionary
{
  public struct SingleFilterInfo
  {
    internal string PropName;
    internal PropertyDescriptor PropDesc;
    internal Object CompareValue;
    internal FilterOperator OperatorValue;
  }
  // Enum to hold filter operators. The chars 
  // are converted to their integer values.
  public enum FilterOperator
  {
    EqualTo = '=',
    LessThan = '<',
    GreaterThan = '>',
    NotEqualTo = '!',
    GreaterOrEqualThan = '$',
    LessOrEqualThan = '£',
    None = ' '
  }

}
