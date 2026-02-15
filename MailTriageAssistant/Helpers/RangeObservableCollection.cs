using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MailTriageAssistant.Helpers;

public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressCollectionChanged;

    public void AddRange(IEnumerable<T> items)
    {
        if (items is null)
        {
            return;
        }

        _suppressCollectionChanged = true;
        try
        {
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressCollectionChanged = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suppressCollectionChanged)
        {
            return;
        }

        base.OnCollectionChanged(e);
    }
}

