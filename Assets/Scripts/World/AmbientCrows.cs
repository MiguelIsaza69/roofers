using System.Collections.Generic;
using UnityEngine;

namespace RoofingSimulator.World
{
    /// <summary>
    /// Ambient crows (Etapa 7 polish). A few low-poly crows perch on the ridge and the
    /// dumpster rim, idle with little hops and head-turns, and take off when the player gets
    /// close; new ones glide back in after a while. Pure atmosphere — no colliders, no cost.
    /// </summary>
    public class AmbientCrows : MonoBehaviour
    {
        private const int CrowCount = 3;
        private const float RespawnMin = 7f, RespawnMax = 14f;

        private Vector3[] perches;
        private Transform player;
        private readonly List<Crow> crows = new List<Crow>();
        private float nextSpawnTime;

        public static AmbientCrows Spawn(Vector3[] perchPoints, Transform playerBody)
        {
            var go = new GameObject("AmbientCrows");
            var m = go.AddComponent<AmbientCrows>();
            m.perches = perchPoints;
            m.player = playerBody;
            for (int i = 0; i < CrowCount; i++) m.SpawnOne();
            return m;
        }

        private void Update()
        {
            crows.RemoveAll(c => c == null);
            if (crows.Count < CrowCount && Time.time >= nextSpawnTime)
            {
                SpawnOne();
                nextSpawnTime = Time.time + Random.Range(RespawnMin, RespawnMax);
            }
        }

        private void SpawnOne()
        {
            if (perches == null || perches.Length == 0) return;

            // Prefer a perch far from the player (and not already taken).
            Vector3 best = perches[Random.Range(0, perches.Length)];
            float bestScore = -1f;
            foreach (Vector3 p in perches)
            {
                float score = player != null ? Vector3.Distance(p, player.position) : Random.value;
                foreach (Crow c in crows)
                    if (c != null && Vector3.Distance(c.transform.position, p) < 0.6f) score = -2f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            if (bestScore < 0f) return;

            crows.Add(Crow.Spawn(best, player));
        }
    }

    /// <summary>One crow: perched idle (hops, head-turns) until the player comes near, then flies off.</summary>
    public class Crow : MonoBehaviour
    {
        private const float FleeDistance = 3.4f;
        private const float FlightTime = 3.2f;

        private Transform player;
        private bool fleeing;
        private float fleeStart;
        private Vector3 fleeDir;
        private float nextFidget;
        private float baseY;

        public static Crow Spawn(Vector3 perch, Transform player)
        {
            var root = new GameObject("Crow");
            root.transform.position = perch;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var crow = root.AddComponent<Crow>();
            crow.player = player;
            crow.baseY = perch.y;
            crow.BuildVisual();
            return crow;
        }

        private void BuildVisual()
        {
            Color black = new Color(0.09f, 0.09f, 0.11f);
            Color beakC = new Color(0.35f, 0.32f, 0.28f);

            Part(PrimitiveType.Sphere, new Vector3(0f, 0.10f, 0f), new Vector3(0.15f, 0.13f, 0.26f), black);   // body
            Part(PrimitiveType.Sphere, new Vector3(0f, 0.19f, 0.11f), new Vector3(0.09f, 0.09f, 0.09f), black); // head
            Part(PrimitiveType.Cube, new Vector3(0f, 0.19f, 0.17f), new Vector3(0.025f, 0.02f, 0.06f), beakC);  // beak
            Part(PrimitiveType.Cube, new Vector3(0f, 0.10f, -0.16f), new Vector3(0.07f, 0.015f, 0.12f), black); // tail
        }

        private void Part(PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "CrowPart";
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
        }

        private void Update()
        {
            if (fleeing)
            {
                float t = Time.time - fleeStart;
                if (t > FlightTime) { Destroy(gameObject); return; }
                // Climb away with a bit of wobble, banking into the direction of flight.
                transform.position += (fleeDir * 6.5f + Vector3.up * 2.2f) * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(fleeDir) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 18f),
                    6f * Time.deltaTime);
                return;
            }

            if (player != null && Vector3.Distance(player.position, transform.position) < FleeDistance)
            {
                fleeing = true;
                fleeStart = Time.time;
                Vector3 away = transform.position - player.position;
                away.y = 0f;
                fleeDir = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
                return;
            }

            // Idle fidgets: a hop and a new heading every few seconds.
            if (Time.time >= nextFidget)
            {
                nextFidget = Time.time + Random.Range(1.6f, 4.2f);
                transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                baseY += 0f; // stays on its perch
            }
            float hop = Mathf.Max(0f, Mathf.Sin(Time.time * 7f)) * 0.015f;
            Vector3 p = transform.position;
            p.y = baseY + hop;
            transform.position = p;
        }
    }
}
