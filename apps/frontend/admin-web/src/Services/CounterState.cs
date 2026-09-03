namespace AdminWeb.Services;

public sealed class CounterState
{
    public int Count { get; private set; }

    public event Action? Changed;

    public void Increment()
    {
        Count++;
        Changed?.Invoke();
    }

    public void Decrement()
    {
        Count--;
        Changed?.Invoke();
    }

    public void Reset()
    {
        Count = 0;
        Changed?.Invoke();
    }
}
