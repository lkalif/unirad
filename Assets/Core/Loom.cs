using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Linq;

public class Loom : MonoBehaviour
{
    public struct DelayedQueueItem
    {
        public float time;
        public Action action;
    }

    static Loom _current;
    static int _mainThreadID;
    static bool initialized;

    List<Action> _actions = new List<Action>();
    List<DelayedQueueItem> _delayed = new List<DelayedQueueItem>();
    List<DelayedQueueItem> _currentDelayed = new List<DelayedQueueItem>();
    List<Action> _currentActions = new List<Action>();

    public static Loom Current
    {
        get
        {
            return _current;
        }
    }

    public static void Initialize(GameObject parent)
    {
        if (!initialized)
        {

            if (!Application.isPlaying)
                return;
            initialized = true;
            _current = parent.AddComponent<Loom>();
            _mainThreadID = Thread.CurrentThread.ManagedThreadId;
        }

    }

    public static void QueueOnMainThread(Action action)
    {
        QueueOnMainThread(action, 0f);
    }

    public static void QueueOnMainThread(Action action, float time)
    {
        if (time != 0)
        {
            lock (Current._delayed)
            {
                Current._delayed.Add(new DelayedQueueItem { time = Time.time + time, action = action });
            }
        }
        else
        {
            /*
            if (Thread.CurrentThread.ManagedThreadId == _mainThreadID)
            {
                RunAction(action);
            }
            
            else*/
            {
                lock (Current._actions)
                {
                    Current._actions.Add(action);
                }
            }
        }
    }

    public static void RunAsync(Action a)
    {
        OpenMetaverse.WorkPool.QueueUserWorkItem(RunAction, a);
    }

    private static void RunAction(object action)
    {
        try
        {
            ((Action)action)();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Thread task error: " + ex.ToString());
        }
    }


    // Update is called once per frame
    void Update()
    {
        lock (_actions)
        {
            _currentActions.Clear();
            _currentActions.AddRange(_actions);
            _actions.Clear();
        }
        foreach (var a in _currentActions)
        {
            a();
        }
        lock (_delayed)
        {
            _currentDelayed.Clear();
            _currentDelayed.AddRange(_delayed.Where(d => d.time <= Time.time));
            foreach (var item in _currentDelayed)
                _delayed.Remove(item);
        }
        foreach (var delayed in _currentDelayed)
        {
            delayed.action();
        }
    }
}
