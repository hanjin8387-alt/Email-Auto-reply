using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using FluentAssertions;
using MailTriageAssistant.Helpers;
using Xunit;

namespace MailTriageAssistant.Tests.Helpers;

public sealed class RangeObservableCollectionTests
{
    [Fact]
    public void AddRange_Null_DoesNothing()
    {
        var sut = new RangeObservableCollection<int>();

        var changed = new List<string?>();
        var collectionChanged = 0;
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        sut.CollectionChanged += (_, _) => collectionChanged++;

        sut.AddRange(null!);

        sut.Should().BeEmpty();
        changed.Should().BeEmpty();
        collectionChanged.Should().Be(0);
    }

    [Fact]
    public void AddRange_AddsItems_RaisesSingleReset()
    {
        var sut = new RangeObservableCollection<int>();

        var events = 0;
        NotifyCollectionChangedEventArgs? last = null;
        sut.CollectionChanged += (_, e) =>
        {
            events++;
            last = e;
        };

        sut.AddRange(new[] { 1, 2, 3 });

        sut.Should().Equal(new[] { 1, 2, 3 });
        events.Should().Be(1);
        last.Should().NotBeNull();
        last!.Action.Should().Be(NotifyCollectionChangedAction.Reset);
    }

    [Fact]
    public void AddRange_RaisesCountAndIndexerPropertyChanged()
    {
        var sut = new RangeObservableCollection<int>();

        var properties = new List<string?>();
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, e) => properties.Add(e.PropertyName);

        sut.AddRange(new[] { 1 });

        properties.Should().Contain(nameof(sut.Count));
        properties.Should().Contain("Item[]");
    }

    [Fact]
    public void AddRange_Empty_RaisesReset()
    {
        var sut = new RangeObservableCollection<int>();

        NotifyCollectionChangedEventArgs? last = null;
        sut.CollectionChanged += (_, e) => last = e;

        sut.AddRange(new int[0]);

        last.Should().NotBeNull();
        last!.Action.Should().Be(NotifyCollectionChangedAction.Reset);
    }
}
