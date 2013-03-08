using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Threading;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Vac.Graphics.Dictionary
{
  public class FilteredBindableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IBindingListView where TValue : new()
  {
    // Permet d'identifier le dictionnaire
    public string Name { get { return _defaultview.Name; } set { _defaultview.Name = value; } }

    protected BindableDictionary<TKey, TValue> _defaultview = new BindableDictionary<TKey, TValue>();
    public BindableDictionary<TKey, TValue> DefaultView { get { return _defaultview; } }

    SynchronizationContext ctx = SynchronizationContext.Current;
    public SynchronizationContext Context
    {
      set
      {
        ctx = value;
        _defaultview.Context = ctx;
      }
    }

    List<TKey> indexTable = new List<TKey>();
    bool issorted = false;
    ListSortDirection sortdirection;
    PropertyDescriptor sortproperty;


    #region IBindingList
    void IBindingList.AddIndex(PropertyDescriptor property) { }
    object IBindingList.AddNew() { throw new NotImplementedException(); }
    bool IBindingList.AllowEdit { get { return true; } }
    bool IBindingList.AllowNew { get { return false; } }
    bool IBindingList.AllowRemove { get { return false; } }
    void IBindingList.ApplySort(PropertyDescriptor property, ListSortDirection direction)
    {
      List<KeyValuePair<TKey, TValue>> liste = new List<KeyValuePair<TKey, TValue>>(this);
      liste.Sort(new PropertyComparer<TKey, TValue>(property.Name, direction));

      base.Clear();
      indexTable.Clear();
      foreach (KeyValuePair<TKey, TValue> item in liste)
      {
        base.Add(item.Key, item.Value);
        indexTable.Add(item.Key);
      }

      sortproperty = property;
      sortdirection = direction;
      issorted = true;

      OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }
    int IBindingList.Find(PropertyDescriptor property, object key) { throw new NotImplementedException(); }
    bool IBindingList.IsSorted { get { return issorted; } }
    void IBindingList.RemoveIndex(PropertyDescriptor property) { }
    void IBindingList.RemoveSort()
    {
      issorted = false;
      sortdirection = ListSortDirection.Ascending;
      sortproperty = null;
    }
    ListSortDirection IBindingList.SortDirection { get { return sortdirection; } }
    PropertyDescriptor IBindingList.SortProperty { get { return sortproperty; } }
    bool IBindingList.SupportsChangeNotification { get { return true; } }
    bool IBindingList.SupportsSearching { get { return false; } }
    bool IBindingList.SupportsSorting { get { return true; } }
    #endregion

    #region IList
    object System.Collections.IList.this[int index]
    {
      get
      {
        TKey key = KeyByIndex(index);

        if (key != null)
        {
          if (this.ContainsKey(key))
            return this[key];
        }
        IEnumerator ie = this.Keys.GetEnumerator();
        ie.MoveNext();
        return base[(TKey)ie.Current];
      }
      set { throw new NotImplementedException(); }
    }
    bool System.Collections.IList.Contains(object value)
    {
      if (value is TKey)
      {
        return base.ContainsKey((TKey)value);
      }
      else if (value is TValue)
      {
        return base.ContainsValue((TValue)value);
      }
      return false;
    }
    int System.Collections.IList.Add(object value) { throw new NotImplementedException(); }
    int System.Collections.IList.IndexOf(object value) { return -1; }
    void System.Collections.IList.Insert(int index, object value) { throw new NotImplementedException(); }
    void System.Collections.IList.Remove(object value) { if (value is TKey) { base.Remove((TKey)value); } }
    void System.Collections.IList.RemoveAt(int index) { throw new NotImplementedException(); }
    bool System.Collections.IList.IsFixedSize { get { return false; } }
    bool System.Collections.IList.IsReadOnly { get { return true; } }
    #endregion

    public void ItemChanged(TKey key)
    {
      int index = IndexOf(key);
      TValue value = base[key];
      if (_defaultview.ContainsKey(key))
      {
        // TODO : vérifier l'éligibilité de la ligne!!! sinon ne rien faire!!
        if (!string.IsNullOrEmpty(filterValue) && !CheckFiltersElligibility(value))
        {
          _defaultview.Remove(key);
        }
        else
        {
          _defaultview[key] = value;
        }
      }
      OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, index));
    }

    new public virtual void Add(TKey key, TValue value)
    {
      if (!base.ContainsKey(key))
      {
        lock (((ICollection)this).SyncRoot)
        {
          base.Add(key, value);
          indexTable.Add(key);
          HookPropertyChanged(value);
        }
        OnListChanged(new ListChangedEventArgs(ListChangedType.ItemAdded, base.Count));
      }

      if (string.IsNullOrEmpty(filterValue) || CheckFiltersElligibility(value))
      {
        _defaultview.Add(key, value);
      }
    }
    new public virtual bool Remove(TKey key)
    {
      // 22.07.2011 - exception élément non présent dans le dictionnaire...     
      bool result = false;
      int index = IndexOf(key);

      // il faut que l'élément existe pour qu'il soit supprimé!
      if (index != -1)
      {
        TValue item = base[key];
        UnhookPropertyChanged(item);

        // contient le résultat de la tentative de suppression
        result = base.Remove(key);
        indexTable.Remove(key);
        if (result)
        {
          // avertir que la suppression a réussie
          if (index != -1)
            OnListChanged(new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
          //else
          //  OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        // mise à jour de la vue dépendante si nécessaire
        if (_defaultview.ContainsKey(key))
        {
          _defaultview.Remove(key);
        }
      }

      // retourner le résultat de la suppression
      return result;
    }
    new public void Clear()
    {
      base.Clear();
      indexTable.Clear();
      OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }

    new public virtual TValue this[TKey key]
    {
      get
      {
        if (base.ContainsKey(key)) return base[key];
        else return new TValue();
      }
      set
      {
        int index = -1;
        lock (((ICollection)this).SyncRoot)
        {
          index = IndexOf(key);
          UnhookPropertyChanged(base[key]);
          base[key] = value;
          HookPropertyChanged(value);
        }
        ItemChanged(index);

        if (_defaultview.ContainsKey(key))
        {
          // TODO : vérifier l'éligibilité de la ligne!!! sinon ne rien faire!!
          if (!string.IsNullOrEmpty(filterValue) && !CheckFiltersElligibility(value))
          {
            _defaultview.Remove(key);
          }
          else
          {
            _defaultview[key] = value;
          }
        }
        else
        {
          // http://jira/jira/browse/DSIAVEN-1217
          // si pour une raison quelconque, la ligne a été supprimé du filtre, il faut qu'elle
          // puisse y être de nouveau ajouté si ses critères le permettent
          if (string.IsNullOrEmpty(filterValue) || CheckFiltersElligibility(value))
          {
            _defaultview.Add(key, value);
          }
        }
      }
    }
    public virtual TValue this[int index]
    {
      get { return this[KeyByIndex(index)]; }
      set { this[KeyByIndex(index)] = value; }
    }
    protected TKey KeyByIndex(int index)
    {
      return indexTable[index];
      //// 07.03.2011 - beaucoup plus efficace que l'ancienne méthode
      //TKey[] keys;
      //lock (((ICollection)base.Keys).SyncRoot)
      //{
      //  keys = new TKey[base.Keys.Count];
      //  base.Keys.CopyTo(keys, 0);
      //}
      //return keys[index];
    }
    protected int IndexOf(TKey key)
    {
      return indexTable.IndexOf(key);
      //TKey[] keys;
      //lock (((ICollection)base.Keys).SyncRoot)
      //{
      //  keys = new TKey[base.Keys.Count];
      //  base.Keys.CopyTo(keys, 0);
      //}

      //for (int i = 0; i < keys.Length; i++)
      //{
      //  if (key.Equals(keys[i]))
      //    return i;
      //}
      //return -1;
    }



    protected ListChangedEventHandler listChanged;

    event ListChangedEventHandler IBindingList.ListChanged
    {
      add { listChanged += value; }
      remove { listChanged -= value; }
    }

    public virtual void ItemChanged(int index)
    {
      TKey key = KeyByIndex(index);
      TValue value = base[key];
      if (_defaultview.ContainsKey(key))
      {
        // TODO : vérifier l'éligibilité de la ligne!!! sinon ne rien faire!!
        if (!string.IsNullOrEmpty(filterValue) && !CheckFiltersElligibility(value))
        {
          _defaultview.Remove(key);
        }
        else
        {
          _defaultview[key] = value;
        }
      }
      OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, index));
    }

    protected void OnListChanged(ListChangedEventArgs e)
    {
      ListChangedEventHandler evt = listChanged;

      if (evt != null)
      {
        if (ctx == null)
        {
          if (raiseListChangeEvents)
            evt(this, e);
        }
        else
          ctx.Post(delegate { if (raiseListChangeEvents) evt(this, e); }, null);
      }
    }


    #region IBindingListView Membres

    public void ApplySort(ListSortDescriptionCollection sorts)
    {
      throw new NotSupportedException();
    }

    public void RemoveFilter()
    {
      if (Filter != null) Filter = null;
    }

    public ListSortDescriptionCollection SortDescriptions
    {
      get { return null; }
    }

    public bool SupportsAdvancedSorting
    {
      get { return false; }
    }

    public bool SupportsFiltering
    {
      get { return true; }
    }

    #endregion

    #region Sorting
    bool isSortedValue;
    ArrayList sortedList;
    FilteredBindableDictionary<TKey, TValue> unsortedItems;
    ListSortDirection sortDirectionValue;
    PropertyDescriptor sortPropertyValue;

    protected bool SupportsSortingCore
    {
      get { return true; }
    }

    protected bool IsSortedCore
    {
      get { return isSortedValue; }
    }

    protected PropertyDescriptor SortPropertyCore
    {
      get { return sortPropertyValue; }
    }

    protected ListSortDirection SortDirectionCore
    {
      get { return sortDirectionValue; }
    }


    public void ApplySort(string propertyName, ListSortDirection direction)
    {
      // Check the properties for a property with the specified name.
      PropertyDescriptor prop = TypeDescriptor.GetProperties(typeof(TValue))[propertyName];

      // If there is not a match, return -1 otherwise pass search to
      // FindCore method.
      if (prop == null)
        throw new ArgumentException(propertyName +
          " is not a valid property for type:" + typeof(TValue).Name);
      else
        ApplySortCore(prop, direction);
    }

    protected void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
    {

      sortedList = new ArrayList();

      // Check to see if the property type we are sorting by implements
      // the IComparable interface.
      Type interfaceType = prop.PropertyType.GetInterface("IComparable");

      if (interfaceType != null)
      {
        // If so, set the SortPropertyValue and SortDirectionValue.
        sortPropertyValue = prop;
        sortDirectionValue = direction;

        unsortedItems = new FilteredBindableDictionary<TKey, TValue>();

        if (sortPropertyValue != null)
        {
          // Loop through each item, adding it the the sortedItems ArrayList.
          foreach (KeyValuePair<TKey, TValue> item in this)
          {
            unsortedItems.Add((TKey)item.Key, (TValue)item.Value);
            sortedList.Add(prop.GetValue(item.Value));
          }
        }
        // Call Sort on the ArrayList.
        sortedList.Sort();
        TValue temp;

        // Check the sort direction and then copy the sorted items
        // back into the list.
        if (direction == ListSortDirection.Descending)
          sortedList.Reverse();

        for (int i = 0; i < this.Count; i++)
        {
          int position = Find(prop.Name, sortedList[i]);
          if (position != i && position > 0)
          {
            temp = this[i];
            this[i] = this[position];
            this[position] = temp;
          }
        }

        isSortedValue = true;

        // If the list does not have a filter applied, 
        // raise the ListChanged event so bound controls refresh their
        // values. Pass -1 for the index since this is a Reset.
        if (String.IsNullOrEmpty(Filter))
          listChanged(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
      }
      else
        // If the property type does not implement IComparable, let the user
        // know.
        throw new InvalidOperationException("Cannot sort by "
          + prop.Name + ". This" + prop.PropertyType.ToString() +
          " does not implement IComparable");
    }

    protected void RemoveSortCore()
    {
      //this.RaiseListChangedEvents = false;
      // Ensure the list has been sorted.
      if (unsortedItems != null && base.Count > 0)
      {
        base.Clear();
        indexTable.Clear();
        if (Filter != null)
        {
          unsortedItems.Filter = this.Filter;
          foreach (KeyValuePair<TKey, TValue> item in unsortedItems)
          {
            base.Add(item.Key, item.Value);
            indexTable.Add(item.Key);
          }
        }
        else
        {
          foreach (KeyValuePair<TKey, TValue> item in this)
          {
            base.Add(item.Key, item.Value);
            indexTable.Add(item.Key);
          }
        }
        isSortedValue = false;
        //this.RaiseListChangedEvents = true;
        // Raise the list changed event, indicating a reset, and index
        // of -1.
        listChanged(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
      }
    }

    public void RemoveSort()
    {
      RemoveSortCore();
    }


    public void EndNew(int itemIndex)
    {
      // Check to see if the item is added to the end of the list,
      // and if so, re-sort the list.
      if (IsSortedCore && itemIndex > 0
        && itemIndex == this.Count - 1)
      {
        ApplySortCore(this.sortPropertyValue,
          this.sortDirectionValue);
        //base.EndNew(itemIndex);
      }
    }

    #endregion Sorting

    #region Find
    protected int FindCore(PropertyDescriptor prop, object key)
    {
      // Get the property info for the specified property.
      PropertyInfo propInfo = typeof(TValue).GetProperty(prop.Name);
      TValue item;

      if (key != null)
      {
        // Loop through the items to see if the key
        // value matches the property value.
        for (int i = 0; i < this.Count; ++i)
        {
          item = (TValue)this[i];
          if (propInfo.GetValue(item, null).Equals(key))
            return i;
        }
      }
      return -1;
    }

    public int Find(string property, object key)
    {
      // Check the properties for a property with the specified name.
      PropertyDescriptorCollection properties =
        TypeDescriptor.GetProperties(typeof(TValue));
      PropertyDescriptor prop = properties.Find(property, true);

      // If there is not a match, return -1 otherwise pass search to
      // FindCore method.
      if (prop == null)
        return -1;
      else
        return FindCore(prop, key);
    }
    #endregion

    #region Filter
    private string filterValue = null;

    BindableDictionary<TKey, TValue> tmp = new BindableDictionary<TKey, TValue>();
    public string Filter
    {
      get
      {
        return filterValue;
      }
      set
      {
        if (filterValue == value) return;

        // If the value is not null or empty, but doesn't
        // match expected format, throw an exception.
        if (!string.IsNullOrEmpty(value) && !Regex.IsMatch(value, BuildRegExForFilterFormat(), RegexOptions.Singleline))
          throw new ArgumentException("Filter is not in " + "the format: propName[<>=]'value'.");

        //Turn off list-changed events.
        //_defaultview.RaiseListChangeEvents = false;

        // If the value is null or empty, reset list.
        if (string.IsNullOrEmpty(value))
        {
          _defaultview.RaiseListChangeEvents = false;
          ResetList();
          _defaultview.RaiseListChangeEvents = true;
        }
        else
        {
          int count = 0;
          string[] matches = value.Split(new string[] { " AND " },
            StringSplitOptions.RemoveEmptyEntries);

          // Check to see if the filter was set previously.
          // Also, check if current filter is a subset of 
          // the previous filter.
          //if (!String.IsNullOrEmpty(filterValue) && !value.Equals(filterValue))
          //{
          tmp.Clear();
          List<KeyValuePair<TKey, TValue>> liste = new List<KeyValuePair<TKey, TValue>>(this);
          foreach (KeyValuePair<TKey, TValue> t in liste)
            tmp.Add(t.Key, t.Value);

          //ResetList();
          //}
          _defaultview.Clear();

          while (count < matches.Length)
          {
            string filterPart = matches[count].ToString();

            // Parse and apply the filter.
            SingleFilterInfo filterInfo = ParseFilter(filterPart);
            ApplyFilter(filterInfo);
            count++;
          }

          _defaultview.RaiseListChangeEvents = false;

          foreach (KeyValuePair<TKey, TValue> itemFound in tmp)
            _defaultview.Add(itemFound.Key, itemFound.Value);

          _defaultview.RaiseListChangeEvents = true;
        }
        // Set the filter value and turn on list changed events.
        filterValue = value;
      }
    }

    // Build a regular expression to determine if 
    // filter is in correct format.
    public static string BuildRegExForFilterFormat()
    {
      StringBuilder regex = new StringBuilder();

      // Look for optional literal brackets, 
      // followed by word characters or space.
      regex.Append(@"\[?[\w\s]+\]?\s?");

      // Add the operators: > < or =.
      regex.Append(@"[><=@]");

      //Add optional space followed by optional quote and
      // any character followed by the optional quote.
      regex.Append(@"\s?'?.+'?");

      return regex.ToString();
    }

    private void ResetList()
    {
      _defaultview.Clear();

      List<KeyValuePair<TKey, TValue>> liste = new List<KeyValuePair<TKey, TValue>>(this);
      foreach (KeyValuePair<TKey, TValue> t in liste)
        _defaultview.Add(t.Key, t.Value);

      if (IsSortedCore)
        ApplySortCore(SortPropertyCore, SortDirectionCore);
    }

    internal void ApplyFilter(SingleFilterInfo filterParts)
    {
      BindableDictionary<TKey, TValue> results = new BindableDictionary<TKey, TValue>();

      // Check to see if the property type we are filtering by implements
      // the IComparable interface.

      Type interfaceType =
        TypeDescriptor.GetProperties(typeof(TValue))[filterParts.PropName]
        .PropertyType.GetInterface("IComparable");

      if (interfaceType == null)
        throw new InvalidOperationException("Filtered property" +
        " must implement IComparable.");

      // Check each value and add to the results list.
      foreach (KeyValuePair<TKey, TValue> item in tmp/*_defaultview*/)
      {
        if (isEligible(filterParts, item.Value))
          results.Add(item.Key, item.Value);
        //if (filterParts.PropDesc.GetValue(item.Value) != null)
        //{
        //  IComparable compareValue = filterParts.PropDesc.GetValue(item.Value) as IComparable;

        //  // 04.01.2011 - si CompareValue contiens un séparateur ','
        //  // il faut comparer les différentes valeurs
        //  int result = 0;
        //  if (filterParts.CompareValue is string)
        //  {
        //    // seul le paramètre "equal to" est possible
        //    string[] values = filterParts.CompareValue.ToString().Split(',');
        //    for (int i = 0; i < values.Length; i++)
        //    {
        //      result = compareValue.CompareTo(values[i]);
        //      if (result == 0)
        //        break;
        //    }
        //  }
        //  else if (filterParts.CompareValue is DateTime)
        //  {
        //    if (
        //      (((DateTime)compareValue).Date == DateTime.MinValue.Date) &&
        //      (filterParts.OperatorValue != FilterOperator.EqualTo && filterParts.OperatorValue != FilterOperator.NotEqualTo)
        //      )
        //      result = 0;
        //    else
        //      result = ((DateTime)compareValue).Date.CompareTo(((DateTime)filterParts.CompareValue).Date);
        //  }
        //  else
        //  {
        //    result = compareValue.CompareTo(filterParts.CompareValue);
        //  }

        //  if (filterParts.OperatorValue ==
        //    FilterOperator.EqualTo && result == 0)
        //    results.Add(item.Key, item.Value);
        //  if (filterParts.OperatorValue ==
        //    FilterOperator.GreaterThan && result > 0)
        //    results.Add(item.Key, item.Value);
        //  if (filterParts.OperatorValue ==
        //    FilterOperator.LessThan && result < 0)
        //    results.Add(item.Key, item.Value);
        //  if (filterParts.OperatorValue == FilterOperator.NotEqualTo
        //    && (result > 0 || result < 0))
        //    results.Add(item.Key, item.Value);
        //  if (filterParts.OperatorValue == FilterOperator.GreaterOrEqualThan
        //    && (result > 0 || result == 0))
        //    results.Add(item.Key, item.Value);
        //  if (filterParts.OperatorValue == FilterOperator.LessOrEqualThan
        //    && (result < 0 || result == 0))
        //    results.Add(item.Key, item.Value);
        //}
      }

      //_defaultview.Clear();
      //_defaultview.RaiseListChangeEvents = false;

      tmp.Clear();
      foreach (KeyValuePair<TKey, TValue> itemFound in results)
        tmp.Add(itemFound.Key, itemFound.Value);

      //_defaultview.RaiseListChangeEvents = true;
    }

    internal SingleFilterInfo ParseFilter(string filterPart)
    {
      SingleFilterInfo filterInfo = new SingleFilterInfo();
      filterInfo.OperatorValue = DetermineFilterOperator(filterPart);

      string[] filterStringParts = null;
      switch (filterInfo.OperatorValue)
      {
        case FilterOperator.EqualTo:
        case FilterOperator.LessThan:
        case FilterOperator.GreaterThan:
        case FilterOperator.None:
          filterStringParts = filterPart.Split(new char[] { (char)filterInfo.OperatorValue });
          break;
        case FilterOperator.NotEqualTo:
          filterStringParts = filterPart.Split(new string[] { "<>" }, StringSplitOptions.None);
          break;
        case FilterOperator.GreaterOrEqualThan:
          filterStringParts = filterPart.Split(new string[] { ">=" }, StringSplitOptions.None);
          break;
        case FilterOperator.LessOrEqualThan:
          filterStringParts = filterPart.Split(new string[] { "<=" }, StringSplitOptions.None);
          break;
      }

      filterInfo.PropName =
        filterStringParts[0].Replace("[", "").
        Replace("]", "").Replace(" AND ", "").Trim();

      // Get the property descriptor for the filter property name.
      PropertyDescriptor filterPropDesc =
        TypeDescriptor.GetProperties(typeof(TValue))[filterInfo.PropName];

      // Convert the filter compare value to the property type.
      if (filterPropDesc == null)
        throw new InvalidOperationException("Specified property to " +
          "filter " + filterInfo.PropName +
          " on does not exist on type: " + typeof(TValue).Name);

      filterInfo.PropDesc = filterPropDesc;

      string comparePartNoQuotes = StripOffQuotes(filterStringParts[1]);
      try
      {
        TypeConverter converter =
          TypeDescriptor.GetConverter(filterPropDesc.PropertyType);
        filterInfo.CompareValue = converter.ConvertFromString(comparePartNoQuotes);
      }
      catch (NotSupportedException)
      {
        throw new InvalidOperationException("Specified filter" +
          "value " + comparePartNoQuotes + " can not be converted" +
          "from string. Implement a type converter for " +
          filterPropDesc.PropertyType.ToString());
      }
      return filterInfo;
    }

    internal FilterOperator DetermineFilterOperator(string filterPart)
    {
      // Determine the filter's operator.
      if (Regex.IsMatch(filterPart, "[^>^<]="))
        return FilterOperator.EqualTo;
      else if (Regex.IsMatch(filterPart, "<[^>^=]"))
        return FilterOperator.LessThan;
      else if (Regex.IsMatch(filterPart, "[^<]>[^=]"))
        return FilterOperator.GreaterThan;
      else if (Regex.IsMatch(filterPart, "<>"))
        return FilterOperator.NotEqualTo;
      else if (Regex.IsMatch(filterPart, ">="))
        return FilterOperator.GreaterOrEqualThan;
      else if (Regex.IsMatch(filterPart, "<="))
        return FilterOperator.LessOrEqualThan;
      else
        return FilterOperator.None;
    }

    internal static string StripOffQuotes(string filterPart)
    {
      // Strip off quotes in compare value if they are present.
      if (Regex.IsMatch(filterPart, "'.+'"))
      {
        int quote = filterPart.IndexOf('\'');
        filterPart = filterPart.Remove(quote, 1);
        quote = filterPart.LastIndexOf('\'');
        filterPart = filterPart.Remove(quote, 1);
        filterPart = filterPart.Trim();
      }
      return filterPart;
    }

    internal bool CheckFiltersElligibility(TValue item)
    {
      int count = 0;
      string[] matches = filterValue.Split(new string[] { " AND " },
        StringSplitOptions.RemoveEmptyEntries);


      bool result = true;
      while (count < matches.Length)
      {
        string filterPart = matches[count].ToString();

        // Parse and apply the filter.
        SingleFilterInfo filterInfo = ParseFilter(filterPart);

        // si checkEligibility retourne false, alors on arrête le test et on retourne false
        if (!checkEligibility(filterInfo, item))
        {
          result = false;
          break;
        }

        count++;
      }
      return result;
    }
    internal bool checkEligibility(SingleFilterInfo filterParts, TValue item)
    {
      // Check to see if the property type we are filtering by implements
      // the IComparable interface.

      Type interfaceType =
        TypeDescriptor.GetProperties(typeof(TValue))[filterParts.PropName]
        .PropertyType.GetInterface("IComparable");

      if (interfaceType == null)
        throw new InvalidOperationException("Filtered property" +
        " must implement IComparable.");

      return isEligible(filterParts, item);

      // Check each value and add to the results list.
    }
    internal bool isEligible(SingleFilterInfo filterParts, TValue item)
    {
      if (filterParts.PropDesc.GetValue(item) != null)
      {
        IComparable compareValue = filterParts.PropDesc.GetValue(item) as IComparable;

        // 04.01.2011 - si CompareValue contiens un séparateur ','
        // il faut comparer les différentes valeurs
        int result = 0;
        if (filterParts.CompareValue is string)
        {
          // seul le paramètre "equal to" est possible
          string[] values = filterParts.CompareValue.ToString().Split(',');
          for (int i = 0; i < values.Length; i++)
          {
            result = compareValue.CompareTo(values[i]);
            if (result == 0)
              break;
          }
        }
        else if (filterParts.CompareValue is DateTime)
        {
          if (
            (((DateTime)compareValue).Date == DateTime.MinValue.Date) &&
            (filterParts.OperatorValue != FilterOperator.EqualTo && filterParts.OperatorValue != FilterOperator.NotEqualTo)
            )
            result = 0;
          else
            result = ((DateTime)compareValue).Date.CompareTo(((DateTime)filterParts.CompareValue).Date);
        }
        else
        {
          result = compareValue.CompareTo(filterParts.CompareValue);
        }

        if (filterParts.OperatorValue ==
          FilterOperator.EqualTo && result == 0)
          return true;
        if (filterParts.OperatorValue ==
          FilterOperator.GreaterThan && result > 0)
          return true;
        if (filterParts.OperatorValue ==
          FilterOperator.LessThan && result < 0)
          return true;
        if (filterParts.OperatorValue == FilterOperator.NotEqualTo
          && (result > 0 || result < 0))
          return true;
        if (filterParts.OperatorValue == FilterOperator.GreaterOrEqualThan
          && (result > 0 || result == 0))
          return true;
        if (filterParts.OperatorValue == FilterOperator.LessOrEqualThan
          && (result < 0 || result == 0))
          return true;
      }
      return false;
    }

    #endregion



    #region PropertyChangedNotification
    bool raiseListChangeEvents = true;
    public bool RaiseListChangeEvents
    {
      get { return raiseListChangeEvents; }
      set
      {
        raiseListChangeEvents =
          _defaultview.RaiseListChangeEvents = value;
      }
    }

    [NonSerialized]
    private PropertyChangedEventHandler propertyChangedEventHandler;
    [NonSerialized]
    private int lastChangeIndex;
    [NonSerialized]
    private PropertyDescriptorCollection itemTypeProperties;



    public int IndexOf(TValue value)
    {
      //int i = -1;
      for (int i = 0; i < this.Count; i++)
      {
        if(this[i].Equals(value))
          return i;
      }
      return -1;
      //foreach (KeyValuePair<TKey, TValue> var in this)
      //{
      //  i++;
      //  if (var.Value.Equals(value))
      //    return i;
      //}
      //return -1;
    }
    private void HookPropertyChanged(TValue item)
    {
      INotifyPropertyChanged changed = item as INotifyPropertyChanged;
      if (changed != null)
      {
        if (this.propertyChangedEventHandler == null)
        {
          this.propertyChangedEventHandler = new PropertyChangedEventHandler(this.Child_PropertyChanged);
        }
        changed.PropertyChanged += this.propertyChangedEventHandler;
      }
    }
    private void UnhookPropertyChanged(TValue item)
    {
      INotifyPropertyChanged changed = item as INotifyPropertyChanged;
      if ((changed != null) && (this.propertyChangedEventHandler != null))
      {
        changed.PropertyChanged -= this.propertyChangedEventHandler;
      }
    }
    private void Child_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      TValue local;
      if (!this.raiseListChangeEvents)
      {
        return;
      }
      if (((sender == null) || (e == null)) || string.IsNullOrEmpty(e.PropertyName))
      {
        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        return;
      }
      try
      {
        local = (TValue)sender;
      }
      catch (InvalidCastException)
      {
        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        return;
      }
      int lastChangeIndex = this.lastChangeIndex;
      if ((lastChangeIndex >= 0) && (lastChangeIndex < base.Count))
      {
        TKey key = KeyByIndex(lastChangeIndex);
        TValue local2 = base[key];
        if (local2.Equals(local))
        {
          goto Label_007B;
        }
      }
      lastChangeIndex = IndexOf(local);
      this.lastChangeIndex = lastChangeIndex;
    Label_007B:
      if (lastChangeIndex == -1)
      {
        this.UnhookPropertyChanged(local);
        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
      }
      else
      {
        if (this.itemTypeProperties == null)
        {
          this.itemTypeProperties = TypeDescriptor.GetProperties(typeof(TValue));
        }
        PropertyDescriptor propDesc = this.itemTypeProperties.Find(e.PropertyName, true);
        ListChangedEventArgs args = new ListChangedEventArgs(ListChangedType.ItemChanged, lastChangeIndex, propDesc);
        this.OnListChanged(args);
      }
    }
    #endregion

  }
}
