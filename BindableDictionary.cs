using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Threading;
using System.Collections;
using System.Diagnostics;
using Vac.Tools.Log;

namespace Vac.Graphics.Dictionary
{
  [DebuggerDisplay("Count={this.Count}")]
  public class BindableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IBindingList
  {
    // Permet d'identifier le dictionnaire
    string name;
    public string Name { get { return name; } set { name = value; } }

    Queue<TValue> removedQueue = new Queue<TValue>();
    List<TKey> indexTable = new List<TKey>();
    SynchronizationContext ctx = SynchronizationContext.Current;
    public SynchronizationContext Context { set { ctx = value; } }

    public Queue<TValue> RemovedQueue
    {
      get { return removedQueue; }
    }


    bool raiseListChangeEvents = true;
    public bool RaiseListChangeEvents
    {
      get { return raiseListChangeEvents; }
      set { raiseListChangeEvents = value; }
    }



    bool issorted = false;
    ListSortDirection sortdirection;
    PropertyDescriptor sortproperty;

    void System.Collections.ICollection.CopyTo(System.Array array, int index)
    {
      foreach (object var in base.Values)
      {
        array.SetValue(var, index++);
      }
    }
    bool System.Collections.ICollection.IsSynchronized
    {
      get
      {
        return true;
      }
    }

    void IBindingList.AddIndex(PropertyDescriptor property) { }
    object IBindingList.AddNew() { return (TValue)((IBindingList)this).AddNew(); }
    bool IBindingList.AllowEdit { get { return true; } }
    bool IBindingList.AllowNew { get { return true; } }
    bool IBindingList.AllowRemove { get { return true; } }
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

    new public virtual TValue this[TKey key]
    {
      get
      {
        // 18.11.2011
        lock (((ICollection)this).SyncRoot)
        {
          return base[key];
        }
      }
      set
      {
        int index = IndexOf(key);
        lock (((ICollection)this).SyncRoot)
        {
          UnhookPropertyChanged(value);
          base[key] = value;
          HookPropertyChanged(value);
        }
        ItemChanged(index);
      }
    }
    public virtual TValue this[int index]
    {
      get
      {
        // 18.11.2011
        lock (((ICollection)this).SyncRoot)
        {
          return base[KeyByIndex(index)];
        }
      }
      set
      {
        // 18.11.2011
        lock (((ICollection)this).SyncRoot)
        {
          base[KeyByIndex(index)] = value;
        }
      }
    }
    protected TKey KeyByIndex(int index)
    {
      TKey key = default(TKey);
      if (indexTable.Count <= index)
        return key;
      else
      {
        try
        {
          key = indexTable[index];
        }
        catch (Exception ex)
        {
          LogWriter.WriteLineError(GetType().ToString() + "::" + System.Reflection.MethodBase.GetCurrentMethod().Name + " {index:" + index + "} {index.count:" + indexTable.Count + "}", ex);
        }
        return key;
      }
    }
    public int IndexOf(TKey key)
    {
      return indexTable.IndexOf(key);
    }

    protected ListChangedEventHandler listChanged;

    event ListChangedEventHandler IBindingList.ListChanged
    {
      add { listChanged += value; }
      remove { listChanged -= value; }
    }

    protected virtual void ItemChanged(int index)
    {
      OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, index));
    }

    protected virtual void OnListChanged(ListChangedEventArgs e)
    {
      if (raiseListChangeEvents)
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
          {
            ctx.Post(new SendOrPostCallback(delegate { if (raiseListChangeEvents) evt(this, e); }), null);
          }
        }
      }
    }

    object System.Collections.IList.this[int index]
    {
      get
      {
        // 18.11.2011
        lock (((ICollection)this).SyncRoot)
        {
          TKey key = KeyByIndex(index);

          if (key != null)
          {
            if (base.ContainsKey(key))
              return base[key];
          }
        }
        return null;
      }
      set { throw new NotImplementedException(); }
    }
    bool System.Collections.IList.Contains(object value)
    {
      if (value is TKey)
      {
        // 18.11.2011
        lock (((ICollection)this).SyncRoot)
        {
          return base.ContainsKey((TKey)value);
        }
      }
      else if (value is TValue)
      {
        // 18.11.2011
        lock (((ICollection)this).SyncRoot)
        {
          return base.ContainsValue((TValue)value);
        }
      }
      return false;
    }
    int System.Collections.IList.Add(object value) { throw new NotImplementedException(); }
    int System.Collections.IList.IndexOf(object value) { return -1; }
    void System.Collections.IList.Insert(int index, object value) { throw new NotImplementedException(); }
    void System.Collections.IList.Remove(object value)
    {
      if (value is TKey)
      {
        // 18.11.2011
        lock (((ICollection)this).SyncRoot)
        {
          base.Remove((TKey)value);
        }
      }
    }
    void System.Collections.IList.RemoveAt(int index) { throw new NotImplementedException(); }
    bool System.Collections.IList.IsFixedSize { get { return false; } }
    bool System.Collections.IList.IsReadOnly { get { return true; } }

    new public virtual void Add(TKey key, TValue value)
    {
      if (!base.ContainsKey(key))
      {
        lock (((ICollection)this).SyncRoot)
        {
          base.Add(key, value);
          HookPropertyChanged(value);
          if (sortproperty != null)
          {
            // 20.02.2013 - Exception peut se produire ici
            // ajout de log pour comprendre l'origine si possible
            try
            {
              ((IBindingList)this).ApplySort(sortproperty, sortdirection);
            }
            catch (Exception ex)
            {
              LogWriter.WriteLineError(GetType().ToString() + "::" + System.Reflection.MethodBase.GetCurrentMethod().Name + "{sortproperty:" + sortproperty.Name + "} {sortdirection:" + sortdirection.ToString() + "}", ex);
              LogWriter.WriteLineError(GetType().ToString() + "::" + System.Reflection.MethodBase.GetCurrentMethod().Name + "{TKey:" + key.ToString() + ", typeof(TKey):" + typeof(TKey).ToString() + "} {TValue:" + value.ToString() + ", typeof(TValue):" + typeof(TValue).ToString() + "}", ex);
            }
          }
          else
          {
            // n'ajouter dans cette collection que si aucun tri n'est demandé
            indexTable.Add(key);
          }
        }
        // ne déclencher cet event que si aucun tri n'est demandé, sinon c'est ApplySort()
        // qui s'en charge
        if (sortproperty == null)
          OnListChanged(new ListChangedEventArgs(ListChangedType.ItemAdded, this.Count));
      }
    }
    new public virtual bool Remove(TKey key)
    {
      bool result = false;
      int index = this.IndexOf(key);
      TValue value = this[index];
      UnhookPropertyChanged(value);
      // 18.11.2011
      lock (((ICollection)this).SyncRoot)
      {
        result = base.Remove(key);
        indexTable.Remove(key);
        // ajouter la clé dans la pile des élements supprimés
        removedQueue.Enqueue(value);
      }

      if (result)
      {
        if (listChanged != null)
        {
          if (index != -1)
            OnListChanged(new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
          else
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }
      }

      return result;
    }
    new public void Clear()
    {
      ((IBindingList)this).RemoveSort();
      base.Clear();
      indexTable.Clear();
      OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }


    #region PropertyChangedNotification
    [NonSerialized]
    private PropertyChangedEventHandler propertyChangedEventHandler;
    [NonSerialized]
    private int lastChangeIndex;
    [NonSerialized]
    private PropertyDescriptorCollection itemTypeProperties;



    public int IndexOf(TValue value)
    {
      // 18.11.2011
      lock (((ICollection)this).SyncRoot)
      {
        for (int i = 0; i < this.Count; i++)
        {
          if (this[i].Equals(value))
            return i;
        }
      }
      return -1;
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
