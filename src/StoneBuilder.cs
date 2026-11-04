using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // Each stone is a Jötunn location in its land: a tall rune pillar made from the game's own vegvisir,
    // the god's rune glowing on it in the colour of its kind, a ring of small stones, a few things that
    // belong to the god, and a faint light at night. All of it is static (nothing to break or carry off):
    // the game builds it again from this prefab whenever the place is loaded.
    static class StoneBuilder
    {
        internal const string Group = "SSoV_Stones";

        static readonly string[] Pillars = { "Vegvisir_Eikthyr", "Vegvisir_GDKing", "RuneStone_Meadows" };
        const float PillarHeight = 3.8f;
        const float RingRadius = 3.4f;
        const int RingStones = 9;

        static GameObject root;
        static float faceDepth; // how far the pillar's faces are from its middle
        static System.Random random;

        internal static void RegisterLocations()
        {
            foreach (Stone stone in Stone.Placed())
            {
                try { Register(stone); }
                catch (System.Exception e) { Plugin.Log.LogError("Could not build " + stone.LocationName + ": " + e); }
            }
        }

        static void Register(Stone stone)
        {
            root = ZoneManager.Instance.CreateLocationContainer(stone.LocationName);
            random = new System.Random(stone.Id.GetHashCode());

            GameObject pillar = Copy(Pillars);
            if (pillar == null)
            {
                Plugin.Log.LogWarning("No pillar prefab found; " + stone.LocationName + " left out.");
                return;
            }
            Bounds b = LocalBounds(pillar);
            if (b.size.y > 0.01f) pillar.transform.localScale = Vector3.one * (PillarHeight / b.size.y);
            b = LocalBounds(pillar);
            // Its faces look along z (the carvings and props are laid out that way).
            if (b.size.x < b.size.z)
            {
                pillar.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                b = LocalBounds(pillar);
            }
            Plugin.Debug(stone.LocationName + " pillar size " + b.size.ToString("F2"));
            Move(pillar, b, new Vector3(0f, -0.35f, 0f));
            b = LocalBounds(pillar);
            Tint(pillar, stone.Glow);
            pillar.AddComponent<StandingStone>().m_stoneId = stone.Id;
            Carvings(stone, b, pillar.transform);
            Light(stone, b);
            Ring();
            faceDepth = Mathf.Max(b.max.z, -b.min.z);
            Props(stone);
            Level();

            LocationConfig config = new LocationConfig();
            config.Biome = stone.Biome;
            config.BiomeArea = stone.Coast ? Heightmap.BiomeArea.Everything : Heightmap.BiomeArea.Median;
            config.Quantity = Plugin.CopiesPerStone.Value;
            config.Priotized = true;
            config.ExteriorRadius = 8f;
            config.ClearArea = true;
            config.RandomRotation = true;
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 2.5f;
            // Altitude is above the sea. The shore stone stands just above the waves; the swamp is low.
            config.MinAltitude = stone.Coast ? 0.6f : stone.Biome == Heightmap.Biome.Swamp ? 0.1f : 1f;
            config.MaxAltitude = stone.Coast ? 4f : 1000f;
            config.Group = Group;
            config.MinDistanceFromSimilar = Plugin.StoneSpacing.Value;
            root.AddComponent<Location>().m_applyRandomDamage = false;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + stone.LocationName + " (" + config.Quantity + " copies in " + stone.Biome + ")");
        }

        // The vegvisir's glow (its emissive runes, lights and particles) in the colour of the stone's kind.
        internal static void Tint(GameObject go, Color glow)
        {
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material m = materials[i];
                    if (m == null) continue;
                    LogMaterial(renderer.name, m);
                    if (!m.HasProperty("_EmissionColor")) continue;
                    Color e = m.GetColor("_EmissionColor");
                    float strength = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
                    if (strength < 0.01f) continue;
                    Material copy = new Material(m);
                    copy.name = m.name + "_SSoV";
                    copy.SetColor("_EmissionColor", glow * strength);
                    materials[i] = copy;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
            foreach (Light light in go.GetComponentsInChildren<Light>(true)) light.color = glow;
            foreach (ParticleSystem particles in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                Color start = main.startColor.color;
                main.startColor = new Color(glow.r, glow.g, glow.b, start.a);
            }
        }

        static void LogMaterial(string renderer, Material m)
        {
            if (!Plugin.DebugLog.Value) return;
            string text = "  material " + m.name + " on " + renderer + " shader " + (m.shader != null ? m.shader.name : "?");
            if (m.HasProperty("_EmissionColor")) text += " emission " + m.GetColor("_EmissionColor");
            if (m.HasProperty("_Color")) text += " color " + m.GetColor("_Color");
            Plugin.Debug(text);
        }

        // The god's rune, glowing on both faces of the pillar, about two thirds of the way up. The faces
        // aren't flat, so each rune settles onto its face when the stone is loaded (RuneCarving).
        static void Carvings(Stone stone, Bounds b, Transform pillar)
        {
            Sprite sprite = Runes.Carving(stone);
            float size = Mathf.Min(1.1f, b.size.x * 0.4f);
            for (int side = 0; side < 2; side++)
            {
                GameObject go = new GameObject("SSoV_Rune");
                go.transform.SetParent(root.transform, false);
                float z = side == 0 ? b.min.z - 0.04f : b.max.z + 0.04f;
                go.transform.localPosition = new Vector3(b.center.x, b.min.y + b.size.y * 0.62f, z);
                go.transform.localRotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
                go.transform.localScale = Vector3.one * size;
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = Color.white;
                go.AddComponent<RuneCarving>().m_pillar = pillar;
            }
        }

        // A faint light in the stone's colour that comes on at dusk.
        static void Light(Stone stone, Bounds b)
        {
            GameObject go = new GameObject("SSoV_NightLight");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, b.min.y + b.size.y * 0.55f, 0f);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = stone.Glow;
            light.range = 9f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            go.AddComponent<StoneGlow>();
        }

        // A ring of small stones around the pillar, each a little different.
        static void Ring()
        {
            for (int i = 0; i < RingStones; i++)
            {
                float deg = i * 360f / RingStones + Range(-8f, 8f);
                float rad = deg * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * (RingRadius + Range(-0.2f, 0.25f));
                GameObject rock = Copy(new[] { "Rock_3", "rock4_coast" });
                if (rock == null) return;
                rock.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                Bounds b = LocalBounds(rock);
                float height = Range(0.55f, 0.9f);
                float width = Range(0.6f, 0.9f);
                rock.transform.localScale = new Vector3(width / Mathf.Max(b.size.x, 0.01f), height / Mathf.Max(b.size.y, 0.01f), width / Mathf.Max(b.size.z, 0.01f));
                b = LocalBounds(rock);
                Move(rock, b, at + Vector3.down * (0.3f * b.size.y));
            }
        }

        // What belongs to each god, inside the ring, in front of (side -1) or behind (side 1) the pillar's
        // faces: x along the face, d metres out from it.
        static void Props(Stone stone)
        {
            switch (stone.Id)
            {
                case "Tyr":
                    Lying("SwordBronze", 0.9f, 0.9f, -1, 30f, 1.3f);
                    Lying("ShieldWood", -1f, 1.1f, -1, 200f, 1f);
                    Lying("SpearFlint", 0.2f, 1.1f, 1, 100f, 1.8f);
                    break;
                case "Bragi":
                    Standing("fire_pit", 0f, 1.5f, -1, 0f, 1.3f);
                    Standing("Tankard", 1.2f, 1.2f, -1, 40f, 0.3f);
                    Standing("Tankard", -1.2f, 1.3f, -1, 160f, 0.3f);
                    break;
                case "Frigg":
                    Standing("piece_spinningwheel", -1f, 1.3f, -1, 20f, 1.5f);
                    Standing("Pickable_Flax_Wild", 1f, 0.9f, -1, 0f, 1f);
                    Standing("Pickable_Flax_Wild", 1.4f, 1.5f, -1, 80f, 1f);
                    break;
                case "Freyr":
                    Lying("AxeBronze", 0.9f, 0.9f, -1, 20f, 1.1f);
                    Lying("PickaxeAntler", -1f, 1f, -1, 150f, 1.2f);
                    Lying("FishingRod", 0.2f, 1.1f, 1, 80f, 1.8f);
                    Standing("Pickable_Barley_Wild", -1.2f, 1.3f, 1, 0f, 1f);
                    break;
                case "Ullr":
                    Lying("Bow", 0.8f, 0.9f, -1, 25f, 1.4f);
                    Lying("ArrowWood", -0.9f, 1f, -1, 140f, 0.8f);
                    Lying("ArrowWood", -1.2f, 1.3f, -1, 120f, 0.8f);
                    Lying("TrophyDeer", 0.3f, 1.1f, 1, 200f, 0.8f);
                    break;
                case "Hel":
                    Standing("crypt_skeleton_laying", 0f, 1.3f, -1, 90f, 1.7f);
                    Standing("bone_stack", -1.2f, 1f, -1, 0f, 0.9f);
                    Lying("TrophyDraugr", 0.8f, 1f, 1, 160f, 0.7f);
                    break;
                case "Eir":
                    Standing("Pickable_Thistle", 1f, 0.9f, -1, 0f, 0.8f);
                    Standing("Pickable_Thistle", -1.1f, 1.1f, -1, 90f, 0.8f);
                    Standing("Pickable_Dandelion", 0.9f, 1f, 1, 0f, 0.6f);
                    Standing("Pickable_Dandelion", -0.8f, 1.2f, 1, 45f, 0.6f);
                    break;
                case "Skadi":
                    Lying("SpearWolfFang", 0.8f, 1f, -1, 30f, 1.8f);
                    Lying("WolfPelt", -1f, 1.1f, -1, 120f, 0.9f);
                    Lying("TrophyWolf", 0.2f, 1.1f, 1, 200f, 0.7f);
                    break;
                case "Heimdall":
                    Standing("piece_groundtorch_wood", -1.1f, 1f, -1, 0f, 1.4f);
                    Lying("ShieldSilver", 0.9f, 1f, -1, 210f, 1f);
                    Lying("SwordSilver", 0.2f, 1.1f, 1, 70f, 1.3f);
                    break;
                case "Thor":
                    Standing("StatueThor", -1.1f, 1.4f, -1, 180f, 1.4f);
                    Lying("SledgeIron", 0.9f, 1f, -1, 60f, 1.6f);
                    break;
                case "Baldr":
                    Standing("Pickable_Dandelion", 1f, 0.9f, -1, 0f, 0.6f);
                    Standing("Pickable_Dandelion", -1f, 1.1f, -1, 60f, 0.6f);
                    Lying("ShieldBlackmetal", 0.2f, 1.1f, 1, 30f, 1.1f);
                    break;
                case "Odin":
                    Lying("StaffFireball", 0.9f, 0.9f, -1, 20f, 1.8f);
                    Lying("StaffGreenRoots", -1f, 1f, -1, 160f, 1.8f);
                    Standing("Eitr", 0.2f, 1f, 1, 0f, 0.4f);
                    break;
                case "Freyja":
                    Lying("CapeFeather", 0f, 1.1f, -1, 20f, 1.4f);
                    Standing("Feathers", -1.1f, 1f, -1, 0f, 0.4f);
                    Standing("AmberPearl", 0.9f, 1f, 1, 0f, 0.3f);
                    break;
                case "Loki":
                    Leaning("ShieldFlametal", -0.9f, -1, 0f, 1.1f);
                    Leaning("KnifeSkollAndHati", 0.8f, -1, 90f, 1.1f);
                    Lying("KnifeBlackMetal", 0.3f, 1.1f, 1, 130f, 1.1f);
                    break;
                case "Vidarr":
                    Lying("AxeJotunBane", 0.9f, 1f, -1, 40f, 1.4f);
                    Lying("AxeBerzerkr", -1f, 1.1f, -1, 150f, 1.4f);
                    Lying("TrophyFenring", 0.2f, 1.1f, 1, 200f, 0.8f);
                    break;
                case "Njord":
                    Lying("FishingRod", 0.9f, 1f, -1, 30f, 1.8f);
                    Lying("Fish1", -1f, 1f, -1, 120f, 0.6f);
                    Lying("TrophySerpent", 0.2f, 1.2f, 1, 200f, 1.2f);
                    break;
            }
        }
        // Paths and fields don't run through a stone ring: the ground inside is levelled and bare.
        static void Level()
        {
            GameObject go = new GameObject("SSoV_Level");
            go.transform.SetParent(root.transform, false);
            TerrainModifier modifier = go.AddComponent<TerrainModifier>();
            modifier.m_level = true;
            modifier.m_levelRadius = RingRadius + 1f;
            modifier.m_levelOffset = 0f;
            modifier.m_smooth = true;
            modifier.m_smoothRadius = RingRadius + 4f;
            modifier.m_smoothPower = 3f;
            modifier.m_paintCleared = true;
            modifier.m_paintRadius = RingRadius + 0.6f;
            modifier.m_paintType = TerrainModifier.PaintType.Dirt;
        }

        // An item lying on the ground (turned on its side), at most maxSize across.
        static void Lying(string name, float x, float d, int side, float rotY, float maxSize)
        {
            GameObject go = Copy(new[] { name });
            if (go == null) return;
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 88f);
            Shrink(go, maxSize);
            Bounds b = LocalBounds(go);
            Move(go, b, new Vector3(x, 0.02f, side * (faceDepth + d))); // on the ground, not in it: thin things vanish in grass and ash
        }

        static void Standing(string name, float x, float d, int side, float rotY, float maxSize)
        {
            GameObject go = Copy(new[] { name });
            if (go == null) return;
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            Shrink(go, maxSize);
            Bounds b = LocalBounds(go);
            Move(go, b, new Vector3(x, -0.02f * b.size.y, side * (faceDepth + d)));
        }

        // Something leant against the pillar's face (side -1 front, 1 back), upright and tipped a little
        // towards the stone; turn spins it about its own height first.
        static void Leaning(string name, float x, int side, float turn, float maxSize)
        {
            GameObject go = Copy(new[] { name });
            if (go == null) return;
            const float tilt = 14f;
            go.transform.localRotation = Quaternion.Euler(-side * tilt, 0f, 0f) * Quaternion.Euler(0f, turn, 0f);
            Shrink(go, maxSize);
            Bounds b = LocalBounds(go);
            // Its top against the face; the face is a little nearer than the pillar's widest point up there.
            Move(go, b, new Vector3(x, -0.03f, side * (faceDepth * 0.85f + b.size.z * 0.5f)));
        }

        static void Shrink(GameObject go, float maxSize)
        {
            Bounds b = LocalBounds(go);
            float extent = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (extent > maxSize && extent > 0.01f) go.transform.localScale = go.transform.localScale * (maxSize / extent);
        }

        // A copy of the first of the candidates that exists, with every script and rigidbody taken off:
        // a static mesh with its collider. Made inside the inactive location container, so no script on it
        // ever wakes up.
        static GameObject Copy(string[] candidates)
        {
            foreach (string name in candidates)
            {
                GameObject prefab = PrefabManager.Instance.GetPrefab(name);
                if (prefab == null) continue;
                GameObject go = Object.Instantiate(prefab, root.transform, false);
                go.name = prefab.name;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                Strip(go);
                return go;
            }
            Plugin.Debug("None of " + string.Join(", ", candidates) + " found");
            return null;
        }

        static void Strip(GameObject go)
        {
            bool item = go.GetComponent<ItemDrop>() != null;
            for (int pass = 0; pass < 4; pass++)
            {
                MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
                if (scripts.Length == 0) break;
                for (int i = scripts.Length - 1; i >= 0; i--) Object.DestroyImmediate(scripts[i]);
            }
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            if (!item) return;
            // Items shine to show they can be picked up; decoration shouldn't. The glint is a particle
            // system on the item itself: silence it for good.
            foreach (ParticleSystem shine in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (shine.gameObject != go) { shine.gameObject.SetActive(false); continue; }
                ParticleSystem.MainModule main = shine.main;
                main.playOnAwake = false;
                ParticleSystem.EmissionModule emission = shine.emission;
                emission.enabled = false;
                ParticleSystemRenderer drawn = shine.GetComponent<ParticleSystemRenderer>();
                if (drawn != null) drawn.enabled = false;
            }
        }

        // Puts the bottom centre of b at pos.
        static void Move(GameObject go, Bounds b, Vector3 pos)
        {
            Vector3 p = go.transform.localPosition;
            go.transform.localPosition = new Vector3(p.x + pos.x - b.center.x, p.y + pos.y - b.min.y, p.z + pos.z - b.center.z);
        }

        // Bounds in the location's space of the meshes that would be visible (Renderer.bounds is empty
        // while the location container is inactive).
        static Bounds LocalBounds(GameObject go)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !ActiveBelow(filter.transform, go.transform)) continue;
                Bounds mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = root.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        // Whether t and its parents up to top are active (LOD copies and switched-off parts aren't).
        static bool ActiveBelow(Transform t, Transform top)
        {
            for (Transform c = t; c != null && c != top; c = c.parent)
                if (!c.gameObject.activeSelf) return false;
            return top.gameObject.activeSelf;
        }

        static float Range(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }

    // A rune carving settles onto the pillar's face when the stone is loaded: the face isn't flat, so it
    // looks for the face with a ray from in front and sits just on it, turned the way the face is.
    class RuneCarving : MonoBehaviour
    {
        public Transform m_pillar;

        void Start()
        {
            if (m_pillar == null) return;
            Vector3 inward = transform.forward;
            Ray ray = new Ray(transform.position - inward * 1.5f, inward);
            bool found = false;
            RaycastHit best = new RaycastHit();
            foreach (Collider collider in m_pillar.GetComponentsInChildren<Collider>())
            {
                RaycastHit hit;
                if (!collider.enabled || !collider.Raycast(ray, out hit, 4f)) continue;
                if (!found || hit.distance < best.distance) best = hit;
                found = true;
            }
            if (!found) return;
            transform.position = best.point + best.normal * 0.03f;
            transform.rotation = Quaternion.LookRotation(-best.normal, Vector3.up);
        }
    }

    // The stone's light: off by day, fading in at dusk.
    class StoneGlow : MonoBehaviour
    {
        Light lamp;
        const float NightIntensity = 0.9f;

        void Awake()
        {
            lamp = GetComponent<Light>();
        }

        void Update()
        {
            if (lamp == null) return;
            float target = EnvMan.IsNight() ? NightIntensity : 0f;
            lamp.intensity = Mathf.MoveTowards(lamp.intensity, target, Time.deltaTime * 0.3f);
            lamp.enabled = lamp.intensity > 0.01f;
        }
    }
}
