namespace Una.Drawing;

public class ObservableHashSet<T> : HashSet<T>
{
    public event Action<T>? ItemAdded;
    public event Action<T>? ItemRemoved;

    public new void Add(T item)
    {
        base.Add(item);
        ItemAdded?.Invoke(item);
    }

    public new void Remove(T item)
    {
        if (!Contains(item)) return;

        base.Remove(item);
        ItemRemoved?.Invoke(item);
    }

    public override string ToString()
    {
        return String.Join(", ", this);
    }
}
