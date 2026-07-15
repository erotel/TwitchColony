using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwitchColony
{
    /// <summary>
    ///     Runs queued callbacks on Unity's main thread. Network callbacks (IRC, Helix) arrive on
    ///     background threads and must not touch the game/UnityEngine directly; enqueue here instead.
    /// </summary>
    public sealed class MainThread : MonoBehaviour
    {
        private static MainThread instance;
        private static readonly Queue<System.Action> Queue = new Queue<System.Action>();

        public static void Ensure()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject("TwitchColony.MainThread");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MainThread>();
        }

        public static void Run(System.Action action)
        {
            if (action == null)
            {
                return;
            }

            lock (Queue)
            {
                Queue.Enqueue(action);
            }
        }

        private void Update()
        {
            while (true)
            {
                System.Action next;
                lock (Queue)
                {
                    if (Queue.Count == 0)
                    {
                        return;
                    }

                    next = Queue.Dequeue();
                }

                try { next(); } catch (Exception e) { Log.Warn("Main-thread action threw: " + e.Message); }
            }
        }
    }
}
