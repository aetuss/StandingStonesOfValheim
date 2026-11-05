using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // Stones in worlds that were made before the mod. Valheim decides where locations go when a world is
    // created, so an older world has none of the stones. Once per world, the server (or the host) counts
    // each god's stones and adds the missing ones:
    // - the first missing copy of a god's stone in land players have already explored (a generated zone),
    //   built there at once, the way the game builds a location when a zone is first generated;
    // - the others in land nobody has been to yet, registered like any location, so the game builds them
    //   when someone gets there.
    // A spot in explored land must be fairly flat, in the stone's land, not near water or lava, away from
    // the game's own locations and from anything players built, and clear of big rocks. Trees, bushes and
    // rocks right in the stone ring go; nothing a player built is ever touched, and the ground isn't dug.
    // When it's done a world key is set, so it doesn't run again.
    static class ExistingWorlds
    {
        internal const string DoneKey = "standingstones_existing_v1";
        const float BuildingDistance = 50f;   // no stone this close to a player's piece
        const float RingClear = 5f;          // natural things inside this radius are removed
        const float NaturalScoreRadius = 12f;

        static readonly System.Reflection.MethodInfo registerLocation = AccessTools.Method(typeof(ZoneSystem), "RegisterLocation");
        static readonly System.Reflection.MethodInfo spawnLocation = AccessTools.Method(typeof(ZoneSystem), "SpawnLocation");
        static readonly System.Reflection.FieldInfo generatedZones = AccessTools.Field(typeof(ZoneSystem), "m_generatedZones");
        static readonly System.Reflection.FieldInfo objectsByID = AccessTools.Field(typeof(ZDOMan), "m_objectsByID");

        static bool started;
        static float readySince = -1f;

        // Called every frame (StoneUpdater): waits until the world and its locations are ready on the
        // server, then runs the pass once if this world hasn't had it.
        internal static void Tick()
        {
            if (ZNet.instance == null)
            {
                started = false; // back at the main menu: the next world gets its own check
                readySince = -1f;
                return;
            }
            if (started || !Plugin.PlaceInExistingWorlds.Value) return;
            ZoneSystem zones = ZoneSystem.instance;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || zones == null || ZNetScene.instance == null || ZDOMan.instance == null ||
                WorldGenerator.instance == null || !zones.LocationsGenerated)
            {
                readySince = -1f;
                return;
            }
            if (readySince < 0f) readySince = Time.time;
            if (Time.time - readySince < 10f) return; // let the world settle first
            started = true;
            if (zones.GetGlobalKey(DoneKey))
            {
                Plugin.Debug("Stones in existing worlds: this world already had its pass.");
                return;
            }
            Plugin.Instance.StartCoroutine(Run(null));
        }

        // From the console (ssov_placestones): run again now, whether or not the world had its pass.
        internal static void RunNow(Terminal context)
        {
            Plugin.Instance.StartCoroutine(Run(context));
        }

        // ---- what's around a spot ----

        enum Thing { Other, Building, Natural, BigRock }

        static readonly Dictionary<int, Thing> kinds = new Dictionary<int, Thing>();

        static Thing KindOf(int prefabHash)
        {
            Thing kind;
            if (kinds.TryGetValue(prefabHash, out kind)) return kind;
            kind = Thing.Other;
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabHash);
            if (prefab != null)
            {
                if (prefab.GetComponent<Piece>() != null) kind = Thing.Building;
                else if (prefab.GetComponent<Character>() != null || prefab.GetComponent<ItemDrop>() != null || prefab.GetComponent<Container>() != null ||
                         prefab.GetComponent<TombStone>() != null || prefab.GetComponent<Ship>() != null || prefab.GetComponent<RuneStone>() != null ||
                         prefab.GetComponent<Vegvisir>() != null || prefab.GetComponent<LocationProxy>() != null) kind = Thing.Other;
                else if (prefab.GetComponent<MineRock5>() != null || prefab.GetComponent<MineRock>() != null) kind = Thing.BigRock;
                else if (prefab.GetComponent<TreeBase>() != null || prefab.GetComponent<TreeLog>() != null || prefab.GetComponent<Destructible>() != null ||
                         prefab.GetComponent<Pickable>() != null) kind = Thing.Natural;
            }
            kinds[prefabHash] = kind;
            return kind;
        }

        // The world's networked things that matter here, by zone: players' pieces, rocks, trees and bushes.
        class Index
        {
            internal readonly Dictionary<Vector2s, List<ZDO>> Buildings = new Dictionary<Vector2s, List<ZDO>>();
            internal readonly Dictionary<Vector2s, List<ZDO>> Nature = new Dictionary<Vector2s, List<ZDO>>();
        }

        static Index BuildIndex()
        {
            Index index = new Index();
            IDictionary all = objectsByID != null ? objectsByID.GetValue(ZDOMan.instance) as IDictionary : null;
            if (all == null) return index;
            foreach (object value in all.Values)
            {
                ZDO zdo = value as ZDO;
                if (zdo == null) continue;
                Thing kind = KindOf(zdo.GetPrefab());
                if (kind == Thing.Other) continue;
                Dictionary<Vector2s, List<ZDO>> map = kind == Thing.Building ? index.Buildings : index.Nature;
                Vector2s zone = ZoneSystem.GetZone(zdo.GetPosition());
                List<ZDO> list;
                if (!map.TryGetValue(zone, out list)) map[zone] = list = new List<ZDO>();
                list.Add(zdo);
            }
            return index;
        }

        static IEnumerable<ZDO> Near(Dictionary<Vector2s, List<ZDO>> map, Vector3 p, float radius)
        {
            Vector2s zone = ZoneSystem.GetZone(p);
            int reach = Mathf.CeilToInt(radius / 64f);
            for (int dx = -reach; dx <= reach; dx++)
                for (int dy = -reach; dy <= reach; dy++)
                {
                    List<ZDO> list;
                    if (!map.TryGetValue(new Vector2s(zone.x + dx, zone.y + dy), out list)) continue;
                    foreach (ZDO zdo in list)
                    {
                        Vector3 q = zdo.GetPosition();
                        if (new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude <= radius * radius) yield return zdo;
                    }
                }
        }

        // ---- the pass ----

        static IEnumerator Run(Terminal context)
        {
            ZoneSystem zones = ZoneSystem.instance;
            HashSet<Vector2s> generated = generatedZones != null ? generatedZones.GetValue(zones) as HashSet<Vector2s> : null;
            if (generated == null || registerLocation == null || spawnLocation == null)
            {
                Plugin.Log.LogWarning("Stones in existing worlds: this version of the game isn't the one the mod knows; nothing placed.");
                yield break;
            }
            Plugin.Log.LogInfo("Stones in existing worlds: looking for missing stones...");
            Index index = BuildIndex();
            yield return null;

            List<Vector3> stones = new List<Vector3>();
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (ZoneSystem.LocationInstance instance in zones.GetLocationList())
            {
                if (instance.m_location == null) continue;
                Stone stone = Stone.ByLocation(instance.m_location.m_prefabName);
                if (stone == null) continue;
                stones.Add(instance.m_position);
                int n;
                counts.TryGetValue(stone.Id, out n);
                counts[stone.Id] = n + 1;
            }

            System.Random random = new System.Random(WorldGenerator.instance.GetSeed() ^ 0x55a1);
            int inExplored = 0, inUnexplored = 0;
            List<string> report = new List<string>();
            foreach (Stone stone in Stone.All)
            {
                ZoneSystem.ZoneLocation location = null;
                foreach (ZoneSystem.ZoneLocation l in zones.m_locations)
                    if (l != null && l.m_prefabName == stone.LocationName) { location = l; break; }
                if (location == null) continue;
                int have;
                counts.TryGetValue(stone.Id, out have);
                int missing = Plugin.CopiesPerStone.Value - have;
                int placedHere = 0, explored = 0;
                for (int copy = 0; copy < missing; copy++)
                {
                    // The first one goes where players have been, so an old world has stones to find now.
                    bool wantExplored = copy == 0;
                    Vector3 spot = Vector3.zero;
                    bool found = wantExplored && FindExplored(stone, generated, index, stones, random, out spot);
                    if (found)
                    {
                        Build(location, stone, spot, index, random);
                        explored++;
                    }
                    else
                    {
                        found = FindUnexplored(stone, generated, stones, random, out spot);
                        if (!found) found = FindExplored(stone, generated, index, stones, random, out spot);
                        if (!found) break;
                        if (generated.Contains(ZoneSystem.GetZone(spot))) { Build(location, stone, spot, index, random); explored++; }
                        else registerLocation.Invoke(zones, new object[] { location, spot, false });
                    }
                    stones.Add(spot);
                    placedHere++;
                    yield return null;
                }
                inExplored += explored;
                inUnexplored += placedHere - explored;
                if (placedHere > 0) report.Add(stone.Id + " " + placedHere + (explored > 0 ? " (" + explored + " in explored land)" : ""));
                yield return null;
            }

            zones.SetGlobalKey(DoneKey);
            string summary = report.Count == 0 ? "every god already had its stones; nothing added."
                : "added " + (inExplored + inUnexplored) + " stones, " + inExplored + " in explored land and " + inUnexplored + " where nobody has been yet: " + string.Join(", ", report.ToArray()) + ".";
            Plugin.Log.LogInfo("Stones in existing worlds: " + summary);
            if (context != null) context.AddString("Stones in existing worlds: " + summary);
        }

        // Builds the stone at once in a zone that already exists, as the game does when it generates a
        // zone: the location is registered as placed and a location proxy (saved with the world) is made.
        static void Build(ZoneSystem.ZoneLocation location, Stone stone, Vector3 spot, Index index, System.Random random)
        {
            // Natural things right in the ring go (never a player's piece: those were kept 50 m away).
            int removed = 0;
            foreach (ZDO zdo in new List<ZDO>(Near(index.Nature, spot, RingClear)))
            {
                if (!zdo.IsOwner()) zdo.SetOwner(ZDOMan.GetSessionID());
                ZDOMan.instance.DestroyZDO(zdo);
                removed++;
            }
            ZoneSystem zones = ZoneSystem.instance;
            registerLocation.Invoke(zones, new object[] { location, spot, true });
            Quaternion rotation = Quaternion.Euler(0f, random.Next(16) * 22.5f, 0f);
            spawnLocation.Invoke(zones, new object[] { location, random.Next(), spot, rotation, ZoneSystem.SpawnMode.Full, new List<GameObject>(), false });
            Plugin.Debug("Stones in existing worlds: built " + stone.Id + " at " + spot.ToString("F0") + " in explored land (" + removed + " trees/rocks cleared)");
        }

        // ---- where ----

        // Ground that suits the stone: its land (well inside it), the right height above the sea, fairly
        // flat around, no lava, away from other stones and from the game's other locations.
        static bool Suits(Stone stone, Vector3 p, List<Vector3> stones, out float height)
        {
            WorldGenerator world = WorldGenerator.instance;
            height = world.GetHeight(p.x, p.z);
            float altitude = height - 30f; // above the sea
            float min = stone.Coast ? 0.6f : stone.Biome == Heightmap.Biome.Swamp ? 0.1f : 1f;
            float max = stone.Coast ? 4f : 1000f;
            if (altitude < min || altitude > max) return false;
            if ((world.GetBiome(p) & stone.Biome) == 0) return false;
            // Cheap checks first: other stones, the game's own locations (dungeons, villages, ruins...).
            float spacing = Plugin.StoneSpacing.Value;
            foreach (Vector3 s in stones)
                if (new Vector2(s.x - p.x, s.z - p.z).sqrMagnitude < spacing * spacing) return false;
            Vector2s zone = ZoneSystem.GetZone(p);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    ZoneSystem.LocationInstance other;
                    if (!ZoneSystem.instance.m_locationInstances.TryGetValue(new Vector2s(zone.x + dx, zone.y + dy), out other) || other.m_location == null) continue;
                    if (dx == 0 && dy == 0) return false; // one location per zone
                    float keep = Mathf.Max(other.m_location.m_exteriorRadius, other.m_location.m_interiorRadius) + 20f;
                    if (new Vector2(other.m_position.x - p.x, other.m_position.z - p.z).sqrMagnitude < keep * keep) return false;
                }
            if (!stone.Coast)
                for (int i = 0; i < 4; i++)
                {
                    float rad = i * Mathf.PI / 2f;
                    if ((world.GetBiome(p + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * 24f) & stone.Biome) == 0) return false;
                }
            float low = height, high = height;
            for (int ring = 1; ring <= 2; ring++)
                for (int i = 0; i < 8; i++)
                {
                    float rad = (i * 45f + ring * 22.5f) * Mathf.Deg2Rad;
                    float h = world.GetHeight(p.x + Mathf.Sin(rad) * ring * 6f, p.z + Mathf.Cos(rad) * ring * 6f);
                    low = Mathf.Min(low, h);
                    high = Mathf.Max(high, h);
                }
            if (high - low > 2f) return false;
            if (LavaCheck.NearLava(p)) return false;
            return true;
        }

        static float BigRockRadius(Stone stone)
        {
            return (stone.Biome & (Heightmap.Biome.AshLands | Heightmap.Biome.Mountain | Heightmap.Biome.DeepNorth)) != 0 ? 26f : 14f;
        }

        // The best spot in land players have been to: no building near, no big rock near, and the fewest
        // trees and rocks in the way.
        static bool FindExplored(Stone stone, HashSet<Vector2s> generated, Index index, List<Vector3> stones, System.Random random, out Vector3 spot)
        {
            spot = Vector3.zero;
            float best = float.MaxValue;
            float bigRocks = BigRockRadius(stone);
            // The explored zones of the stone's land, in a random order; a big world has thousands, so
            // only a few hundred are looked at, and the search stops once it has enough good spots.
            List<Vector2s> candidates = new List<Vector2s>();
            foreach (Vector2s zone in generated)
                if (stone.Coast || (WorldGenerator.instance.GetBiome(ZoneSystem.GetZonePos(zone)) & stone.Biome) != 0) candidates.Add(zone);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Vector2s swap = candidates[i]; candidates[i] = candidates[j]; candidates[j] = swap;
            }
            int good = 0;
            for (int z = 0; z < candidates.Count && z < 600 && good < 30; z++)
            {
                Vector3 center = ZoneSystem.GetZonePos(candidates[z]);
                for (int tries = 0; tries < 3; tries++)
                {
                    Vector3 p = center + new Vector3((float)random.NextDouble() * 48f - 24f, 0f, (float)random.NextDouble() * 48f - 24f);
                    bool blocked = false;
                    foreach (ZDO building in Near(index.Buildings, p, BuildingDistance)) { blocked = true; break; }
                    if (blocked) continue;
                    float height;
                    if (!Suits(stone, p, stones, out height)) continue;
                    p.y = height;
                    float score = 0f;
                    foreach (ZDO thing in Near(index.Nature, p, bigRocks))
                    {
                        if (KindOf(thing.GetPrefab()) == Thing.BigRock) { blocked = true; break; }
                        Vector3 q = thing.GetPosition();
                        if (new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude <= NaturalScoreRadius * NaturalScoreRadius) score += 1f;
                    }
                    if (blocked) continue;
                    good++;
                    if (score >= best) continue;
                    best = score;
                    spot = p;
                }
            }
            return best < float.MaxValue;
        }

        // A random good spot where nobody has been yet (the game builds it when somebody gets there).
        static bool FindUnexplored(Stone stone, HashSet<Vector2s> generated, List<Vector3> stones, System.Random random, out Vector3 spot)
        {
            spot = Vector3.zero;
            const int reach = 158; // zones out from the middle: a 10 km world radius
            for (int tries = 0; tries < 4000; tries++)
            {
                Vector2s zone = new Vector2s(random.Next(-reach, reach + 1), random.Next(-reach, reach + 1));
                Vector3 center = ZoneSystem.GetZonePos(zone);
                if (center.magnitude > 9800f || generated.Contains(zone) || ZoneSystem.instance.m_locationInstances.ContainsKey(zone)) continue;
                if (!stone.Coast && (WorldGenerator.instance.GetBiome(center) & stone.Biome) == 0) continue;
                Vector3 p = center + new Vector3((float)random.NextDouble() * 48f - 24f, 0f, (float)random.NextDouble() * 48f - 24f);
                float height;
                if (!Suits(stone, p, stones, out height)) continue;
                p.y = height;
                spot = p;
                return true;
            }
            return false;
        }
    }
}
