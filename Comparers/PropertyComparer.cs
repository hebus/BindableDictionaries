using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.ComponentModel;
using System.Collections;

namespace Vac.Graphics.Dictionary
{
  public class PropertyComparer<TKey, TValue> : IComparer<KeyValuePair<TKey, TValue>>
  {
    private PropertyInfo property;
    private ListSortDirection sortDirection;

    public PropertyComparer(string sortProperty, ListSortDirection sortDirection)
    {
      property = typeof(TValue).GetProperty(sortProperty);
      this.sortDirection = sortDirection;
    }

    #region IComparer<KeyValuePair<TKey,TValue>> Membres

    public int Compare(KeyValuePair<TKey, TValue> x, KeyValuePair<TKey, TValue> y)
    {
      object valueX = property.GetValue(x.Value, null);
      object valueY = property.GetValue(y.Value, null);

      // 10.11.2011 - s'assurer que les objets sont bien identiques avant de faire la comparaison
      // sinon, on utilise le type string pour comparer
      if (valueX.GetType().Equals(valueY.GetType()))
      {
        if (sortDirection == ListSortDirection.Ascending)
        {
          return Comparer.Default.Compare(valueX, valueY);
        }
        else
        {
          return Comparer.Default.Compare(valueY, valueX);
        }
      }
      else
      {
        if (sortDirection == ListSortDirection.Ascending)
        {
          return Comparer.Default.Compare(valueX.ToString(), valueY.ToString());
        }
        else
        {
          return Comparer.Default.Compare(valueY.ToString(), valueX.ToString());
        }
      }
    }

    #endregion
  }
}
