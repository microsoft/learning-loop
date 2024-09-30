// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace CommonTest.Fakes.Configuration
{
    /// <summary>
    /// An IOptionsMonitor implementation for testing purposes.
    /// </summary>
    public class AnOptionsMonitor<T> : IOptionsMonitor<T> where T : class, new()
    {
        private readonly List<Action<T, string>> _listeners = new();

        public AnOptionsMonitor(T options)
        {
            CurrentValue = options;
        }

        public T CurrentValue { get; private set; }

        public T Get(string name)
        {
            throw new NotImplementedException();
        }

        public IDisposable OnChange(Action<T, string> listener)
        {
            _listeners.Add(listener);
            return new DisposableAction(() => _listeners.Remove(listener));
        }

        public void TriggerChange(string name)
        {
            foreach (var listener in _listeners)
            {
                listener(CurrentValue, name);
            }
        }

        private class DisposableAction : IDisposable
        {
            private readonly Action _action;

            public DisposableAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action?.Invoke();
            }
        }
    }
}
