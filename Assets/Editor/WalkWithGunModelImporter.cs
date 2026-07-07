#if UNITY_EDITOR
using UnityEditor;

public sealed class WalkWithGunModelImporter : AssetPostprocessor
{
    private const string WalkWithGunPath = "Assets/New/Enemy/walkwithgun.fbx";

    private void OnPreprocessModel()
    {
        if (assetPath.Replace('\\', '/') != WalkWithGunPath)
        {
            return;
        }

        ModelImporter importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.clipAnimations;
        }

        if (clips == null || clips.Length == 0)
        {
            return;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = true;
            clips[i].loopPose = true;
        }

        importer.clipAnimations = clips;
    }
}
#endif
