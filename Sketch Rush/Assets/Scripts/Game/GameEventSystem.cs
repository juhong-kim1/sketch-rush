using System;
using System.Collections.Generic;

public static class GameEventSystem
{
    private static Dictionary<string, List<Action>> _events = new Dictionary<string, List<Action>>();
    private static Dictionary<string, List<Action<object>>> _eventsWithData = new Dictionary<string, List<Action<object>>>();

    // 데이터 없는 이벤트 구독
    public static void Subscribe(string eventName, Action callback)
    {
        if (!_events.ContainsKey(eventName))
            _events[eventName] = new List<Action>();

        _events[eventName].Add(callback);
    }

    // 데이터 있는 이벤트 구독
    public static void Subscribe(string eventName, Action<object> callback)
    {
        if (!_eventsWithData.ContainsKey(eventName))
            _eventsWithData[eventName] = new List<Action<object>>();

        _eventsWithData[eventName].Add(callback);
    }

    // 데이터 없는 이벤트 발생
    public static void Publish(string eventName)
    {
        if (_events.ContainsKey(eventName))
        {
            foreach (var callback in _events[eventName])
                callback?.Invoke();
        }
    }

    // 데이터 있는 이벤트 발생
    public static void Publish(string eventName, object data)
    {
        if (_eventsWithData.ContainsKey(eventName))
        {
            foreach (var callback in _eventsWithData[eventName])
                callback?.Invoke(data);
        }
    }

    // 구독 해제 (메모리 누수 방지)
    public static void Unsubscribe(string eventName, Action callback)
    {
        if (_events.ContainsKey(eventName))
            _events[eventName].Remove(callback);
    }

    public static void Unsubscribe(string eventName, Action<object> callback)
    {
        if (_eventsWithData.ContainsKey(eventName))
            _eventsWithData[eventName].Remove(callback);
    }

    public static void Clear()
    {
        _events.Clear();
        _eventsWithData.Clear();
    }
}