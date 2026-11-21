using UnityEditor;

namespace Backrooms.EditorTools
{
    /// Models in Resources/Models (made in Blender, see Art/Stage4Props.blend) are read at runtime and merged into
    /// the generated prop meshes, so their vertex data must stay readable. Lights and cameras are never imported.
    public sealed class ModelImportSettings : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            string path = assetPath.Replace('\\', '/');
            bool creature = path.Contains("/Resources/Creatures/");
            bool prop = path.Contains("/Resources/Models/");
            if (!prop && !creature) return;
            var importer = (ModelImporter)assetImporter;
            importer.isReadable = prop;
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
