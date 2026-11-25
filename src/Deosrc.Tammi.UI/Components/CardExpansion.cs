namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// Which cards in a list are expanded, by their position, for a list whose cards can be collapsed, added, removed and
/// reordered. Cards start collapsed. A card keeps its state when others are added or removed, or when it's moved.
/// </summary>
public sealed class CardExpansion
{
    private readonly HashSet<int> _expanded = [];

    public bool IsExpanded(int index) => _expanded.Contains(index);

    /// <summary>
    /// Expands a collapsed card, and collapses an expanded one.
    /// </summary>
    public void Toggle(int index)
    {
        if (!_expanded.Remove(index))
        {
            _expanded.Add(index);
        }
    }

    /// <summary>
    /// Expands a card, e.g. one that's just been added so it can be filled in.
    /// </summary>
    public void Expand(int index) => _expanded.Add(index);

    /// <summary>
    /// Collapses a card, e.g. one whose content no longer applies.
    /// </summary>
    public void Collapse(int index) => _expanded.Remove(index);

    /// <summary>
    /// A card has been removed from the list, so the cards after it move up.
    /// </summary>
    public void Removed(int index)
    {
        var after = _expanded.Where(position => position > index).Select(position => position - 1).ToList();
        _expanded.RemoveWhere(position => position >= index);
        _expanded.UnionWith(after);
    }

    /// <summary>
    /// A card has been moved from one position to another, so the cards between them shift by one.
    /// </summary>
    public void Moved(int from, int to)
    {
        var moved = _expanded.Select(position =>
            position == from ? to
            : from < to && position > from && position <= to ? position - 1
            : to < from && position >= to && position < from ? position + 1
            : position).ToList();
        _expanded.Clear();
        _expanded.UnionWith(moved);
    }
}
