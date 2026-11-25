using UnityEditor;

namespace Backrooms.EditorTools
{
    /// Models in Resources/Models (made in Blender, see Art/Stage4Props.blend) are read at runtime and merged into
    /// the generated prop meshes, so their vertex data must stay readable. Lights and cameras are never imported.
    public sealed class ModelImportSettings : AssetPostprocessor
    {
        /// Baked monster maps (Resources/Monsters/*Tex): the normal maps must import as normal maps.
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/Monsters/") || !path.EndsWith("_normal.png")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.NormalMap;
        }

        void OnPreprocessModel()
        {
            string path = assetPath.Replace('\\', '/');
            bool creature = path.Contains("/Resources/Creatures/") || path.Contains("/Resources/Monsters/");   // Monsters: our own Blender rigs (Art/build_*.py)
            bool prop = path.Contains("/Resources/Models/");
            if (!prop && !creature) return;
            var importer = (ModelImporter)assetImporter;
            importer.isReadable = prop || path.Contains("/MothHive");   // the hive's mesh is its collider
            if (creature)
            {
                importer.importBlendShapes = false;
                importer.meshCompression = ModelImporterMeshCompression.Medium;
            }
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.globalScale = 1f;
        }
    }
}
