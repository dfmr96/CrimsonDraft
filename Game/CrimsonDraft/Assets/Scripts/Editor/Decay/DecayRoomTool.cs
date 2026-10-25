#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CrimsonDraft.Rendering.Decay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CrimsonDraft.Editor.Decay
{
    // Operaciones sobre una sala = la instancia en escena de un prefab de sala. Todo lo que se
    // guarda (DecayVolume, decals, ids en los renderers) se escribe en el PREFAB, no en la escena:
    // se carga con LoadPrefabContents, se edita y se guarda, y la instancia se actualiza sola.
    // Lo que se calcula en mundo (caja, rayos de los decals) se hace sobre la instancia y se
    // traduce a coordenadas locales de la sala, así funciona con salas rotadas o espejadas.
    internal static class DecayRoomTool
    {
        public const string VolumeObjectName = "Decay";

        private const int MinRenderers = 5; // menos que esto no es una sala (un prop suelto)

        private static readonly string[] DefaultExcluded = { "Interactables" };
        private static readonly string[] BoxSources      = { "Floor", "Bounds" };

        // --- Encontrar salas -------------------------------------------------------------------

        /// <summary>La sala (prefab más externo) que contiene a go, o null.</summary>
        public static GameObject? RoomOf(GameObject? go)
        {
            if (go == null || !go.scene.IsValid()) return null;
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            return root != null && CountRenderers(root) >= MinRenderers ? root : null;
        }

        public static List<GameObject> RoomsInOpenScenes()
        {
            var rooms = new List<GameObject>();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects()) Collect(root.transform, rooms);
            }
            rooms.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return rooms;
        }

        private static void Collect(Transform t, List<GameObject> rooms)
        {
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
            {
                if (CountRenderers(t.gameObject) >= MinRenderers) rooms.Add(t.gameObject);
                return;
            }
            foreach (Transform child in t) Collect(child, rooms);
        }

        private static int CountRenderers(GameObject go) => go.GetComponentsInChildren<MeshRenderer>(true).Length;

        public static DecayVolume? VolumeOf(GameObject room)
        {
            foreach (Transform child in room.transform)
                if (child.TryGetComponent<DecayVolume>(out var v)) return v;
            return null;
        }

        public static string AssetPath(GameObject room) => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(room);

        /// <summary>Motivo por el que no se puede editar esta sala ahora, o null.</summary>
        public static string? Problem(GameObject room)
        {
            string path = AssetPath(room);
            if (string.IsNullOrEmpty(path)) return "No es un prefab.";
            if (PrefabUtility.GetPrefabAssetType(room) == PrefabAssetType.Model) return "Es un modelo importado, no un prefab editable.";
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path) return "Cerrá el Prefab Mode de esta sala para usar la tool.";
            return null;
        }

        // --- Aplicar / quitar ---------------------------------------------------------------------

        public static void Apply(GameObject room, DecayPalette palette, bool guessSurface)
        {
            string path = AssetPath(room);
            int id = NextFreeId();
            var (center, size) = FitBox(room);

            EditPrefab(path, contents =>
            {
                var go = new GameObject(VolumeObjectName);
                go.transform.SetParent(contents.transform, false);
                go.transform.localPosition = center;

                var volume = go.AddComponent<DecayVolume>();
                volume.Palette = palette;
                volume.RoomId  = id;
                volume.Center  = Vector3.zero;
                volume.Size    = size;
                foreach (var name in DefaultExcluded)
                {
                    var excluded = contents.transform.Find(name);
                    if (excluded != null) volume.Excluded.Add(excluded);
                }
            });

            var instanceVolume = VolumeOf(room);
            if (instanceVolume == null) throw new System.InvalidOperationException($"No apareció el DecayVolume en {room.name} después de guardar el prefab.");

            StampRenderers(room, instanceVolume);
            DecayMaterialMigrator.Migrate(
                DecayMaterialMigrator.FindMaterials(RoomRenderers(instanceVolume), DecayMaterialMigrator.SourceShader), guessSurface);
        }

        public static void Remove(GameObject room)
        {
            var volume = VolumeOf(room);
            string path = AssetPath(room);
            var renderers = new List<MeshRenderer>(room.GetComponentsInChildren<MeshRenderer>(true));

            EditPrefab(path, contents =>
            {
                foreach (var r in renderers)
                {
                    var target = InContents(r, path, contents);
                    if (target != null) SetRoomId(target, 0);
                }
                if (volume != null)
                {
                    var v = InContents(volume, path, contents);
                    if (v != null) Object.DestroyImmediate(v.gameObject);
                }
            });
        }

        /// <summary>Reescribe el id en los renderers de la sala (después de agregar o mover cosas, o cambiar exclusiones).</summary>
        public static int StampRenderers(GameObject room, DecayVolume volume)
        {
            string path = AssetPath(room);
            int stamped = 0;
            var all = room.GetComponentsInChildren<MeshRenderer>(true);

            EditPrefab(path, contents =>
            {
                foreach (var r in all)
                {
                    var target = InContents(r, path, contents);
                    if (target == null) continue;
                    bool inRoom = DecayDecalGenerator.IsEnvironment(r.gameObject) && DecayDecalGenerator.BelongsToRoom(volume, r.transform);
                    SetRoomId(target, inRoom ? volume.RoomId : 0);
                    if (inRoom) stamped++;
                }
            });
            return stamped;
        }

        public static void RefitBox(GameObject room, DecayVolume volume)
        {
            string path = AssetPath(room);
            var (center, size) = FitBox(room);
            EditPrefab(path, contents =>
            {
                var v = InContents(volume, path, contents);
                if (v == null) return;
                v.transform.localPosition = center;
                v.transform.localRotation = Quaternion.identity;
                v.Center = Vector3.zero;
                v.Size   = size;
            });
        }

        // --- Decals -------------------------------------------------------------------------------

        public static int GenerateDecals(GameObject room, DecayVolume volume, bool reseed)
        {
            var materials = new Material?[System.Enum.GetValues(typeof(DecayDecal.Kind)).Length];
            var available = new bool[materials.Length];
            for (int k = 0; k < materials.Length; k++)
            {
                materials[k] = AssetDatabase.LoadAssetAtPath<Material>($"{DecayTextureGenerator.MaterialsDir}/Decay_{(DecayDecal.Kind)k}.mat");
                available[k] = materials[k] != null;
            }

            int seed = reseed ? Random.Range(1, 99999) : volume.Seed;
            var locked = new List<Vector3>();
            foreach (var d in volume.GetComponentsInChildren<DecayDecal>(true))
                if (d.Locked) locked.Add(d.transform.position);

            var placements = DecayDecalGenerator.Compute(volume, seed, locked, available);

            // Coordenadas relativas al volumen de la instancia. Posición y direcciones pasan por la
            // matriz completa (incluido el espejo de las salas con escala negativa), así la
            // instancia reconstruye exactamente la misma pose en mundo.
            var volumeT = volume.transform;
            var local = new List<(Vector3 pos, Quaternion rot)>(placements.Count);
            foreach (var p in placements)
            {
                var forward = volumeT.InverseTransformDirection(p.Rotation * Vector3.forward);
                var up      = volumeT.InverseTransformDirection(p.Rotation * Vector3.up);
                local.Add((volumeT.InverseTransformPoint(p.Position), Quaternion.LookRotation(forward, up)));
            }

            string path = AssetPath(room);
            EditPrefab(path, contents =>
            {
                var v = InContents(volume, path, contents);
                if (v == null) return;
                if (reseed) v.Seed = seed;

                var container = v.transform.Find(DecayDecalGenerator.ContainerName);
                if (container == null)
                {
                    container = new GameObject(DecayDecalGenerator.ContainerName).transform;
                    container.SetParent(v.transform, false);
                }
                foreach (var d in container.GetComponentsInChildren<DecayDecal>(true))
                    if (!d.Locked) Object.DestroyImmediate(d.gameObject);

                for (int i = 0; i < placements.Count; i++)
                {
                    var p = placements[i];
                    var go = new GameObject($"{p.Kind}_{i:00}");
                    go.transform.SetParent(container, false);
                    go.transform.localPosition = local[i].pos;
                    go.transform.localRotation = local[i].rot;

                    var projector = go.AddComponent<DecalProjector>();
                    projector.material     = materials[(int)p.Kind];
                    projector.size         = new Vector3(p.Size.x, p.Size.y, DecayDecalGenerator.ProjectionDepth);
                    projector.pivot        = Vector3.zero;
                    projector.drawDistance = 60f;
                    projector.fadeFactor   = p.Fade;
                    // Variación sin texturas extra: espejado horizontal.
                    projector.uvScale = new Vector2(p.FlipU ? -1f : 1f, 1f);
                    projector.uvBias  = new Vector2(p.FlipU ? 1f : 0f, 0f);

                    var decal = go.AddComponent<DecayDecal>();
                    decal.DecalKind     = p.Kind;
                    decal.MinimumDetail = p.MinimumDetail;
                }
            });

            return placements.Count;
        }

        public static int ClearUnlockedDecals(GameObject room, DecayVolume volume)
        {
            string path = AssetPath(room);
            int removed = 0;
            EditPrefab(path, contents =>
            {
                var v = InContents(volume, path, contents);
                if (v == null) return;
                foreach (var d in v.GetComponentsInChildren<DecayDecal>(true))
                {
                    if (d.Locked) continue;
                    Object.DestroyImmediate(d.gameObject);
                    removed++;
                }
            });
            return removed;
        }

        // --- Guardar lo que se tocó en la escena ---------------------------------------------------

        /// <summary>Cantidad de cambios hechos en la escena sobre el deterioro de la sala que no están en el prefab.</summary>
        public static int PendingOverrides(GameObject room, DecayVolume volume)
        {
            int count = 0;
            foreach (var o in PrefabUtility.GetObjectOverrides(room, false))
                if (IsUnder(o.instanceObject, volume.transform)) count++;
            foreach (var a in PrefabUtility.GetAddedGameObjects(room))
                if (a.instanceGameObject.transform.IsChildOf(volume.transform)) count++;
            foreach (var r in PrefabUtility.GetRemovedGameObjects(room))
                if (r.assetGameObject != null && IsUnderAsset(r.assetGameObject.transform)) count++;
            return count;
        }

        /// <summary>
        /// Lleva al prefab lo que se cambió en la escena dentro del objeto Decay (sliders, caja,
        /// decals movidos, borrados o fijados). Nada fuera del deterioro se aplica.
        /// </summary>
        public static int ApplyOverrides(GameObject room, DecayVolume volume)
        {
            string path = AssetPath(room);
            int applied = 0;

            foreach (var o in PrefabUtility.GetObjectOverrides(room, false))
                if (IsUnder(o.instanceObject, volume.transform)) { o.Apply(path, InteractionMode.UserAction); applied++; }
            foreach (var a in PrefabUtility.GetAddedGameObjects(room))
                if (a.instanceGameObject.transform.IsChildOf(volume.transform)) { a.Apply(path, InteractionMode.UserAction); applied++; }
            foreach (var r in PrefabUtility.GetRemovedGameObjects(room))
                if (r.assetGameObject != null && IsUnderAsset(r.assetGameObject.transform)) { r.Apply(path, InteractionMode.UserAction); applied++; }

            return applied;
        }

        private static bool IsUnder(Object o, Transform root) => o switch
        {
            Component c  => c.transform.IsChildOf(root),
            GameObject g => g.transform.IsChildOf(root),
            _            => false,
        };

        private static bool IsUnderAsset(Transform assetTransform)
        {
            for (var t = assetTransform; t != null; t = t.parent)
                if (t.GetComponent<DecayVolume>() != null) return true;
            return false;
        }

        // --- Helpers --------------------------------------------------------------------------

        public static List<MeshRenderer> RoomRenderers(DecayVolume volume)
        {
            var list = new List<MeshRenderer>();
            foreach (var r in volume.Room.GetComponentsInChildren<MeshRenderer>(true))
                if (DecayDecalGenerator.IsEnvironment(r.gameObject) && DecayDecalGenerator.BelongsToRoom(volume, r.transform))
                    list.Add(r);
            return list;
        }

        // En las paredes y props que son prefabs anidados, el id queda como override dentro del
        // prefab de la sala. Si el valor vuelve a ser el del prefab anidado (quitar deterioro,
        // excluir), se revierte el override en vez de dejar uno con el mismo valor.
        private static void SetRoomId(MeshRenderer target, int roomId)
        {
            uint mask = DecayRoomId.With(target.renderingLayerMask, roomId);
            if (target.renderingLayerMask != mask) target.renderingLayerMask = mask;

            if (!PrefabUtility.IsPartOfPrefabInstance(target)) return;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(target);
            if (source == null || source.renderingLayerMask != mask) return;

            var property = new SerializedObject(target).FindProperty("m_RenderingLayerMask");
            if (property != null && property.prefabOverride)
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
        }

        // Caja en el espacio local de la sala: piso + paredes (hijos Floor/Bounds) si existen,
        // si no, todos los renderers de la sala.
        private static (Vector3 center, Vector3 size) FitBox(GameObject room)
        {
            var sources = new List<Transform>();
            foreach (var name in BoxSources)
            {
                var t = room.transform.Find(name);
                if (t != null) sources.Add(t);
            }
            if (sources.Count == 0) sources.Add(room.transform);

            bool any = false;
            var local = new Bounds();
            foreach (var source in sources)
            foreach (var r in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!DecayDecalGenerator.IsEnvironment(r.gameObject) || r.GetComponentInParent<DecayVolume>() != null) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = room.transform.InverseTransformPoint(corner);
                    if (!any) { local = new Bounds(p, Vector3.zero); any = true; }
                    else local.Encapsulate(p);
                }
            }

            return any ? (local.center, local.size) : (Vector3.up * 1.5f, new Vector3(6f, 3f, 6f));
        }

        private static void EditPrefab(string path, System.Action<GameObject> edit)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, path, out bool success);
                if (!success) throw new System.InvalidOperationException($"No se pudo guardar {path}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // El objeto de la instancia -> el mismo objeto dentro de los contenidos cargados del prefab.
        // Se busca su par en el asset (sirve también para prefabs anidados dentro de la sala) y se
        // recorre el mismo camino de hijos en los contenidos, que son una copia exacta del asset.
        private static T? InContents<T>(T instanceObject, string path, GameObject contents) where T : Component
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(instanceObject, path);
            if (source == null) return null;

            var indices = new List<int>();
            for (var t = source.transform; t.parent != null; t = t.parent) indices.Add(t.GetSiblingIndex());

            var c = contents.transform;
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                if (indices[i] >= c.childCount) return null;
                c = c.GetChild(indices[i]);
            }
            return c.GetComponent<T>();
        }

        // Ids usados por cualquier prefab del proyecto (una sala puede cargarse junto a otras de
        // otro deck) y por lo que haya en las escenas abiertas.
        private static int NextFreeId()
        {
            var used = new HashSet<int>();
            foreach (var v in DecayVolume.Active) used.Add(v.RoomId);

            string? scriptGuid = null;
            foreach (var guid in AssetDatabase.FindAssets("DecayVolume t:MonoScript"))
                if (AssetDatabase.GUIDToAssetPath(guid).EndsWith("/DecayVolume.cs")) scriptGuid = guid;

            if (scriptGuid != null)
            {
                var roomIdPattern = new Regex(@"roomId: (\d+)");
                foreach (var file in Directory.EnumerateFiles("Assets", "*.prefab", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    if (!text.Contains(scriptGuid)) continue;
                    foreach (Match m in roomIdPattern.Matches(text)) used.Add(int.Parse(m.Groups[1].Value));
                }
            }

            for (int id = 1; id <= DecayRoomId.Max; id++)
                if (!used.Contains(id)) return id;
            throw new System.InvalidOperationException("No quedan ids de sala libres (máximo 255).");
        }
    }
}
