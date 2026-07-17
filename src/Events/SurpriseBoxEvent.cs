using TwitchColony.Config;
using UnityEngine;

namespace TwitchColony.Events
{
    /// <summary>
    ///     Surprise box: spawns a box marker (optionally pans/zooms the camera to it), counts down a
    ///     few seconds, then bursts out 1–5 random pickupable items with a little launch arc.
    ///     Independent implementation — vanilla API only, no assets/code from the original mod. The
    ///     "box" is a lightweight marker (emoji bubble); swap in a real anim later if desired.
    /// </summary>
    public sealed class SurpriseBoxEvent : GameEvent
    {
        public override string Id => "surprise_box";
        public override string DisplayName => "Surprise box (care package)";

        public override void Trigger()
        {
            Log.Info("Event: SurpriseBox");

            // Prefer just above a printing pod (always a safe spot); else on a random dupe.
            var cell = Grid.InvalidCell;
            var pads = Components.Telepads?.Items;
            if (pads != null && pads.Count > 0)
            {
                cell = Grid.CellAbove(Grid.PosToCell(pads[Random.Range(0, pads.Count)].transform.position));
            }

            if (!Grid.IsValidCell(cell))
            {
                cell = Cells.RandomDupeCell();
            }

            SurpriseBoxController.Spawn(cell, ModConfig.Instance.SurpriseBoxZoom);
        }
    }

    /// <summary>Drives a single surprise box: optional camera zoom, a countdown, then the item burst.</summary>
    internal sealed class SurpriseBoxController : MonoBehaviour
    {
        private const float CountdownSeconds = 5f;
        private const float ZoomOrthoSize = 10f; // smaller = more zoomed in

        private Vector3 pos;
        private float fireAt;
        private int lastShown = -1;
        private GameObject countdownBubble;

        public static void Spawn(int cell, bool zoom)
        {
            if (!Grid.IsValidCell(cell))
            {
                Log.Warn("SurpriseBox: no valid spawn cell.");
                return;
            }

            var pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Front);
            var go = new GameObject("TwitchColony.SurpriseBox");
            go.transform.position = pos;

            var c = go.AddComponent<SurpriseBoxController>();
            c.pos = pos;
            c.fireAt = Time.unscaledTime + CountdownSeconds;

            if (zoom && CameraController.Instance != null)
            {
                try
                {
                    CameraController.Instance.SetTargetPos(pos, ZoomOrthoSize, true);
                }
                catch (System.Exception e)
                {
                    Log.Warn("SurpriseBox: camera zoom failed: " + e.Message);
                }
            }
        }

        private void Update()
        {
            var remaining = fireAt - Time.unscaledTime;
            if (remaining > 0f)
            {
                var secs = Mathf.CeilToInt(remaining);
                if (secs != lastShown)
                {
                    lastShown = secs;
                    if (countdownBubble != null)
                    {
                        Destroy(countdownBubble);
                    }

                    countdownBubble = UI.SpeechBubbles.ShowRaw(transform, "box " + secs);
                }

                return;
            }

            if (countdownBubble != null)
            {
                Destroy(countdownBubble);
            }

            Burst();
            Destroy(gameObject);
        }

        private void Burst()
        {
            var count = Random.Range(1, 6); // 1..5 inclusive
            Log.Info($"SurpriseBox: bursting {count} gifts.");
            for (var i = 0; i < count; i++)
            {
                try
                {
                    SpawnGift();
                }
                catch (System.Exception e)
                {
                    Log.Warn("SurpriseBox: gift spawn failed: " + e.Message);
                }
            }

            UI.SpeechBubbles.ShowRaw(transform, "yay!");
        }

        private void SpawnGift()
        {
            var prefab = PickValidPrefab();
            if (prefab == null)
            {
                return;
            }

            var go = Util.KInstantiate(prefab.gameObject, pos);
            go.SetActive(true);

            // Element chunks spawn with sensible default mass/temperature.
            if (go.TryGetComponent(out ElementChunk _) && go.TryGetComponent(out PrimaryElement pe))
            {
                pe.Mass = pe.Element.defaultValues.mass;
                pe.Temperature = pe.Element.defaultValues.temperature;
            }

            // Launch it out of the box on an upward arc.
            GameComps.Fallers.Add(go, 5f * UpwardArc());
        }

        private static KPrefabID PickValidPrefab()
        {
            var prefabs = Assets.Prefabs;
            if (prefabs == null || prefabs.Count == 0)
            {
                return null;
            }

            for (var attempt = 0; attempt < 40; attempt++)
            {
                var p = prefabs[Random.Range(0, prefabs.Count)];
                if (IsValidGift(p))
                {
                    return p;
                }
            }

            return null;
        }

        private static bool IsValidGift(KPrefabID prefab)
        {
            if (prefab == null || prefab.GetComponent<Pickupable>() == null)
            {
                return false;
            }

            // Skip the compost-marked duplicate copies of items.
            return !prefab.TryGetComponent(out Compostable compostable) || !compostable.isMarkedForCompost;
        }

        private static Vector2 UpwardArc()
        {
            // A random direction between 30° and 150° — always launching upward-ish.
            var rad = Random.Range(30f, 150f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
