namespace Dashboard.React;

using H5;

/// <summary>
/// React hooks wrapper for H5.
/// </summary>
public static class Hooks
{
    /// <summary>
    /// React useState hook - manages component state.
    /// </summary>
    public static (T State, Action<T> SetState) UseState<T>(T initialValue)
    {
        var result = Script.Call<object[]>("React.useState", initialValue);
        var state = (T)result[0];
        var setState = (Action<T>)result[1];
        return (state, setState);
    }

    /// <summary>
    /// React useState hook with functional update.
    /// </summary>
    public static (T State, Action<Func<T, T>> SetState) UseStateFunc<T>(T initialValue)
    {
        var result = Script.Call<object[]>("React.useState", initialValue);
        var state = (T)result[0];
        var setState = (Action<Func<T, T>>)result[1];
        return (state, setState);
    }

    /// <summary>
    /// React useEffect hook - manages side effects.
    /// </summary>
    public static void UseEffect(Action effect, object[]? deps = null)
    {
        Script.Call<object>("React.useEffect", (Func<object?>)(() =>
        {
            effect();
            return null;
        }), deps);
    }

    /// <summary>
    /// React useEffect hook with cleanup function.
    /// </summary>
    public static void UseEffect(Action effect, Func<Action> cleanup, object[]? deps = null)
    {
        Script.Call<object>("React.useEffect", (Func<Action>)(() =>
        {
            effect();
            return cleanup();
        }), deps);
    }

    /// <summary>
    /// React useRef hook - creates a mutable ref object.
    /// </summary>
    public static RefObject<T> UseRef<T>(T? initialValue = default) =>
        Script.Call<RefObject<T>>("React.useRef", initialValue);

    /// <summary>
    /// React useMemo hook - memoizes expensive computations.
    /// </summary>
    public static T UseMemo<T>(Func<T> factory, object[] deps) =>
        Script.Call<T>("React.useMemo", factory, deps);

    /// <summary>
    /// React useCallback hook - memoizes callback functions.
    /// </summary>
    public static T UseCallback<T>(T callback, object[] deps) where T : Delegate =>
        Script.Call<T>("React.useCallback", callback, deps);

    /// <summary>
    /// React useContext hook - consumes a React context.
    /// </summary>
    public static T UseContext<T>(object context) =>
        Script.Call<T>("React.useContext", context);
}
