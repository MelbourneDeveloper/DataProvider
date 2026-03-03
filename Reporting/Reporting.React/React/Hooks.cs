using System;
using H5;

namespace Reporting.React.Core
{
    /// <summary>
    /// State result from useState hook.
    /// </summary>
    public class StateResult<T>
    {
        /// <summary>Current state value.</summary>
        public T State { get; set; }

        /// <summary>State setter function.</summary>
        public Action<T> SetState { get; set; }
    }

    /// <summary>
    /// React hooks for H5.
    /// </summary>
    public static class Hooks
    {
        /// <summary>
        /// React useState hook.
        /// </summary>
        public static StateResult<T> UseState<T>(T initialValue)
        {
            var result = Script.Call<object[]>("React.useState", initialValue);
            return new StateResult<T>
            {
                State = Script.Write<T>("result[0]"),
                SetState = Script.Write<Action<T>>("result[1]"),
            };
        }

        /// <summary>
        /// React useEffect hook.
        /// </summary>
        public static void UseEffect(Action effect, object[] deps = null)
        {
            if (deps != null)
            {
                Script.Call<object>("React.useEffect", effect, deps);
            }
            else
            {
                Script.Call<object>("React.useEffect", effect);
            }
        }
    }
}
