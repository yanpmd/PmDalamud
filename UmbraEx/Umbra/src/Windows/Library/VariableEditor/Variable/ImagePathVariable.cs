namespace Umbra.Windows.Library.VariableEditor;

public sealed class ImagePathVariable(string id) : Variable(id), IDisposable
{
    public string Value {
        get => _value;
        set
        {
            if (_value == value) return;

            _value = value;
            ValueChanged?.Invoke(value);
        }
    }

    private string _value = string.Empty;

    public event Action<string>? ValueChanged;

    public override void Dispose()
    {
        foreach (Delegate handler in ValueChanged?.GetInvocationList() ?? []) {
            ValueChanged -= (Action<string>)handler;
        }

        ValueChanged = null;
    }
}
