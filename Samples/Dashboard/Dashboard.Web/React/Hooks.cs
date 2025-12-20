namespace Dashboard.React
{
    using System;
    using H5;

    /// <summary>
    /// State tuple for useState hook.
    /// </summary>
    public class StateResult<T>
    {
        /// <summary>Current state value.</summary>
        public T State { get; set; }

        /// <summary>State setter function.</summary>
        public Action<T> SetState { get; set; }
    }

    /// <summary>
    /// State tuple for useState hook with functional update.
    /// </summary>
    public class StateFuncResult<T>
    {
        /// <summary>Current state value.</summary>
        public T State { get; set; }

        /// <summary>State setter function.</summary>
        public Action<Func<T, T>> SetState { get; set; }
    }

    /// <summary>
    /// React hooks wrapper for H5.
    /// </summary>
    public static class Hooks
    {
        /// <summary>
        /// React useState hook - manages component state.
        /// </summary>
        public static StateResult<T> UseState<T>(T initialValue)
        {
            var result = Script.Call<object[]>("React.useState", initialValue);
            var state = (T)result[0];
            var setState = (Action<T>)result[1];
            return new StateResult<T> { State = state, SetState = setState };
        }

        /// <summary>
        /// React useState hook with functional update.
        /// </summary>
        public static StateFuncResult<T> UseStateFunc<T>(T initialValue)
        {
            var result = Script.Call<object[]>("React.useState", initialValue);
            var state = (T)result[0];
            var setState = (Action<Func<T, T>>)result[1];
            return new StateFuncResult<T> { State = state, SetState = setState };
        }

        /// <summary>
        /// React useEffect hook - manages side effects.
        /// </summary>
        public static void UseEffect(Action effect, object[] deps = null)
        {
            Script.Call<object>(
                "React.useEffect",
                (Func<object>)(
                    () =>
                    {
                        effect();
                        return null;
                    }
                ),
                deps
            );
        }

        /// <summary>
        /// React useEffect hook with cleanup function.
        /// </summary>
        public static void UseEffect(Action effect, Func<Action> cleanup, object[] deps = null)
        {
            Script.Call<object>(
                "React.useEffect",
                (Func<Action>)(
                    () =>
                    {
                        effect();
                        return cleanup();
                    }
                ),
                deps
            );
        }

        /// <summary>
        /// React useRef hook - creates a mutable ref object.
        /// </summary>
        public static RefObject<T> UseRef<T>(T initialValue = default(T))
        {
            return Script.Call<RefObject<T>>("React.useRef", initialValue);
        }

        /// <summary>
        /// React useMemo hook - memoizes expensive computations.
        /// </summary>
        public static T UseMemo<T>(Func<T> factory, object[] deps)
        {
            return Script.Call<T>("React.useMemo", factory, deps);
        }

        /// <summary>
        /// React useCallback hook - memoizes callback functions.
        /// </summary>
        public static T UseCallback<T>(T callback, object[] deps)
            where T : Delegate
        {
            return Script.Call<T>("React.useCallback", callback, deps);
        }

        /// <summary>
        /// React useContext hook - consumes a React context.
        /// </summary>
        public static T UseContext<T>(object context)
        {
            return Script.Call<T>("React.useContext", context);
        }
    }
}
