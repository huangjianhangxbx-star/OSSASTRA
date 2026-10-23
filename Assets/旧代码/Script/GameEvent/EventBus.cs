using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EventBus
{
    private readonly Dictionary<Type, Delegate> listeners = new Dictionary<Type, Delegate>();

    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
            return;

        Type eventType = typeof(TEvent);

        if (listeners.TryGetValue(eventType, out Delegate existing))
        {
            listeners[eventType] = Delegate.Combine(existing, handler);
        }
        else
        {
            listeners.Add(eventType, handler);
        }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
            return;

        Type eventType = typeof(TEvent);

        if (!listeners.TryGetValue(eventType, out Delegate existing))
            return;

        Delegate current = Delegate.Remove(existing, handler);

        if (current == null)
        {
            listeners.Remove(eventType);
        }
        else
        {
            listeners[eventType] = current;
        }
    }

    public void Publish<TEvent>(TEvent eventData)
    {
        Type eventType = typeof(TEvent);

        if (!listeners.TryGetValue(eventType, out Delegate existing))
            return;

        if (existing is Action<TEvent> callback)
        {
            callback.Invoke(eventData);
        }
        else
        {
            Debug.LogError($"[EventBus] 事件类型监听器不匹配: {eventType.Name}");
        }
    }

    public void Clear()
    {
        listeners.Clear();
    }

    public int GetListenerCount<TEvent>()
    {
        Type eventType = typeof(TEvent);

        if (!listeners.TryGetValue(eventType, out Delegate existing))
            return 0;

        return existing.GetInvocationList().Length;
    }
}