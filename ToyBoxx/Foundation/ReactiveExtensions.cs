using System.ComponentModel;

namespace ToyBoxx.Foundation;

public static class PropertyChangedExtensions
{
    private static readonly Dictionary<INotifyPropertyChanged, Dictionary<string, List<Action>>> _subscriptions = [];
    private static readonly Lock _lock = new();

    public static void WhenChanged(this INotifyPropertyChanged source, Action callback, params string[] propertyNames)
    {
        bool needsBinding = false;

        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(source, out Dictionary<string, List<Action>>? value))
            {
                value = [];

                _subscriptions[source] = value;
                needsBinding = true;
            }

            foreach (var name in propertyNames)
            {
                if (!_subscriptions[source].ContainsKey(name))
                {
                    _subscriptions[source][name] = [];
                }

                value[name].Add(callback);
            }
        }

        callback();

        if (!needsBinding)
        {
            return;
        }

        source.PropertyChanged += (s, e) =>
        {
            List<Action>? actions;

            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(source, out var propertyMap))
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.PropertyName) || !propertyMap.TryGetValue(e.PropertyName, out actions))
                {
                    return;
                }
            }

            foreach (var action in actions ?? [])
            {
                action.Invoke();
            }
        };
    }
}