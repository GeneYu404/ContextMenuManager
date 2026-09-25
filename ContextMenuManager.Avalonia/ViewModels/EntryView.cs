using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;

namespace ContextMenuManager.ViewModels;

/// <summary>按属性分组后的条目组，供列表分组渲染使用。</summary>
public sealed record EntryGroup(string Name, IReadOnlyList<MenuEntryViewModel> Items);

/// <summary>
/// WPF ICollectionView 的最小替代：过滤、多级排序、按属性分组。
/// 源集合增删时自动重建；Refresh 主动重建并发出 Reset 通知。
/// 排序键按属性名反射读取（属性是字符串，调用频率仅在重建时，开销可忽略）。
/// </summary>
public sealed class EntryView : IReadOnlyList<MenuEntryViewModel>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly ObservableCollection<MenuEntryViewModel> _source;
    private readonly List<(string Property, bool Descending)> _sorts = [];
    private readonly Dictionary<string, Func<MenuEntryViewModel, object?>> _readers = new(StringComparer.Ordinal);
    private string? _groupProperty;
    private List<MenuEntryViewModel> _flat = [];
    private List<EntryGroup> _groups = [];

    public EntryView(ObservableCollection<MenuEntryViewModel> source)
    {
        _source = source;
        _source.CollectionChanged += (_, _) => Rebuild();
    }

    /// <summary>过滤谓词，null 表示全部可见。</summary>
    public Func<MenuEntryViewModel, bool>? Filter { get; set; }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 分组结果。未设置分组时返回单个匿名组（组名为空 → 视图隐藏组头），
    /// 让列表只走一条渲染路径；Count / Groups 变化通过 PropertyChanged 通知绑定。
    /// </summary>
    public IReadOnlyList<EntryGroup> Groups => _groups;

    public void SortBy(string property, bool descending = false) => _sorts.Add((property, descending));

    public void ClearSorts() => _sorts.Clear();

    public void GroupBy(string? property) => _groupProperty = property;

    public void Refresh() => Rebuild();

    public MenuEntryViewModel this[int index] => _flat[index];
    public int Count => _flat.Count;
    public IEnumerator<MenuEntryViewModel> GetEnumerator() => _flat.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Rebuild()
    {
        IEnumerable<MenuEntryViewModel> items = _source;
        if (Filter is { } filter) items = items.Where(filter);
        if (_sorts.Count > 0) items = Sort(items);

        _flat = items.ToList();

        _groups = _groupProperty is { } groupProp
            ? _flat.GroupBy(Reader(groupProp))
                    .Select(g => new EntryGroup(g.Key as string ?? "", g.ToList()))
                    .ToList()
            : [new EntryGroup("", _flat)]; // 未分组：单个匿名组，视图按组名为空隐藏组头

        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Groups)));
    }

    private IEnumerable<MenuEntryViewModel> Sort(IEnumerable<MenuEntryViewModel> items)
    {
        IOrderedEnumerable<MenuEntryViewModel>? ordered = null;
        foreach (var (prop, descending) in _sorts)
        {
            var reader = Reader(prop);
            ordered = ordered is null
                ? descending ? items.OrderByDescending(reader, KeyComparer) : items.OrderBy(reader, KeyComparer)
                : descending ? ordered.ThenByDescending(reader, KeyComparer) : ordered.ThenBy(reader, KeyComparer);
        }
        return ordered ?? items;
    }

    private Func<MenuEntryViewModel, object?> Reader(string property)
    {
        if (_readers.TryGetValue(property, out var reader)) return reader;
        var pi = typeof(MenuEntryViewModel).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new ArgumentException($"MenuEntryViewModel 上没有属性 {property}");
        reader = e => pi.GetValue(e);
        _readers[property] = reader;
        return reader;
    }

    /// <summary>字符串按当前文化忽略大小写比较（与程序筛选下拉的排序口径一致），null 排最前。</summary>
    private static readonly IComparer<object?> KeyComparer = Comparer<object?>.Create(Compare);

    private static int Compare(object? a, object? b) => (a, b) switch
    {
        (null, null) => 0,
        (null, _) => -1,
        (_, null) => 1,
        (string x, string y) => StringComparer.CurrentCultureIgnoreCase.Compare(x, y),
        (IComparable x, object y) => x.CompareTo(y),
        _ => 0,
    };
}
