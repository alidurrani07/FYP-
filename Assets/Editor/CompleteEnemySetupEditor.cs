using Invector;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class CompleteEnemySetupEditor
{
    private const string EnemyFolder = "Assets/New/Enemy";
    private const string ModelPath = EnemyFolder + "/FInalEnemy.fbx";
    private const string GunPath = EnemyFolder + "/GUN.prefab";
    private const string ControllerPath = EnemyFolder + "/EnemyAIController.controller";
    private const string PrefabPath = EnemyFolder + "/CompleteEnemy.prefab";

    [MenuItem("Tools/Enemy/Create Complete Enemy From Assets/New/Enemy")]
    public static void CreateCompleteEnemy()
    {
        ConfigureModelImport(ModelPath, true);
        ConfigureModelImport(EnemyFolder + "/rifle aiming idle.fbx", true);
        ConfigureModelImport(EnemyFolder + "/walking.fbx", true);
        ConfigureModelImport(EnemyFolder + "/rifle run.fbx", true);
        ConfigureModelImport(EnemyFolder + "/firing rifle.fbx", true);
        ConfigureModelImport(EnemyFolder + "/reloading.fbx", true);
        ConfigureModelImport(EnemyFolder + "/hit reaction.fbx", true);
        ConfigureModelImport(EnemyFolder + "/Dead.fbx", true);

        AnimatorController controller = CreateAnimatorController();
        GameObject prefab = CreatePrefab(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        Debug.Log("Complete enemy created at " + PrefabPath);
    }

    private static void ConfigureModelImport(string path, bool importAnimation)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("Missing model importer: " + path);
            return;
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = importAnimation;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.SaveAndReimport();
    }

    private static AnimatorController CreateAnimatorController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("isShooting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = AddState(machine, "IdleAim", EnemyFolder + "/rifle aiming idle.fbx", new Vector3(250f, 70f, 0f), true);
        AnimatorState walk = AddState(machine, "Walk", EnemyFolder + "/walking.fbx", new Vector3(250f, 170f, 0f), true);
        AnimatorState run = AddState(machine, "Run", EnemyFolder + "/rifle run.fbx", new Vector3(250f, 270f, 0f), true);
        AnimatorState fire = AddState(machine, "Fire", EnemyFolder + "/firing rifle.fbx", new Vector3(560f, 70f, 0f), false);
        AnimatorState reload = AddState(machine, "Reload", EnemyFolder + "/reloading.fbx", new Vector3(560f, 170f, 0f), false);
        AnimatorState hit = AddState(machine, "Hit", EnemyFolder + "/hit reaction.fbx", new Vector3(560f, 270f, 0f), false);
        AnimatorState dead = AddState(machine, "Dead", EnemyFolder + "/Dead.fbx", new Vector3(870f, 170f, 0f), false);

        machine.defaultState = idle;

        AddFloatTransition(idle, walk, "Speed", AnimatorConditionMode.Greater, 0.1f);
        AddFloatTransition(walk, idle, "Speed", AnimatorConditionMode.Less, 0.1f);
        AddFloatTransition(walk, run, "Speed", AnimatorConditionMode.Greater, 2.8f);
        AddFloatTransition(run, walk, "Speed", AnimatorConditionMode.Less, 2.8f);
        AddFloatTransition(run, idle, "Speed", AnimatorConditionMode.Less, 0.1f);

        AddAnyStateTrigger(machine, fire, "Shoot");
        AddAnyStateTrigger(machine, reload, "Reload");
        AddAnyStateTrigger(machine, hit, "Hit");
        AddAnyStateTrigger(machine, dead, "Dead");
        AddExitTransition(fire, idle);
        AddExitTransition(reload, idle);
        AddExitTransition(hit, idle);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorState AddState(AnimatorStateMachine machine, string stateName, string clipPath, Vector3 position, bool loop)
    {
        AnimatorState state = machine.AddState(stateName, position);
        AnimationClip clip = LoadAnimationClip(clipPath);
        if (clip != null)
        {
            state.motion = clip;
            SerializedObject clipSettings = new SerializedObject(clip);
            SerializedProperty loopTime = clipSettings.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loopTime != null)
            {
                loopTime.boolValue = loop;
                clipSettings.ApplyModifiedProperties();
            }
        }
        else
        {
            Debug.LogWarning("No animation clip found in " + clipPath);
        }

        return state;
    }

    private static AnimationClip LoadAnimationClip(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview", System.StringComparison.OrdinalIgnoreCase))
            {
                return clip;
            }
        }

        return null;
    }

    private static void AddFloatTransition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.AddCondition(mode, threshold, parameter);
    }

    private static void AddAnyStateTrigger(AnimatorStateMachine machine, AnimatorState to, string trigger)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void AddExitTransition(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 0.9f;
        transition.duration = 0.1f;
    }

    private static GameObject CreatePrefab(RuntimeAnimatorController controller)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        GameObject gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GunPath);
        if (modelAsset == null)
        {
            throw new System.IO.FileNotFoundException("Missing enemy model", ModelPath);
        }

        GameObject root = new GameObject("CompleteEnemy");
        root.layer = 15;
        root.tag = "Enemy";
        GameObject visual = PrefabUtility.InstantiatePrefab(modelAsset, root.transform) as GameObject;
        if (visual == null)
        {
            visual = Object.Instantiate(modelAsset, root.transform);
        }

        visual.name = "Visual";
        SetLayerRecursively(visual, 15);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        Animator animator = visual.GetComponent<Animator>();
        if (animator == null)
        {
            animator = visual.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.45f;
        agent.height = 1.8f;
        agent.speed = 1f;
        agent.angularSpeed = 300f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 2f;

        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.9f, 0f);
        collider.height = 1.8f;
        collider.radius = 0.35f;

        vHealthController health = root.AddComponent<vHealthController>();
        health.maxHealth = 50;
        health.fillHealthOnStart = true;

        CompleteEnemyAI ai = root.AddComponent<CompleteEnemyAI>();
        ai.gunPrefab = gunPrefab;
        ai.detectionRange = 25f;
        ai.attackRange = 18f;
        ai.stopDistance = 10f;
        ai.requireLineOfSight = false;
        ai.enemyLayer = 15;
        ai.bodyPartLayer = 15;
        ai.faceTargetWhileFiring = true;
        ai.bulletDamage = 8;
        ai.burstShots = 3;
        ai.fireInterval = 0.35f;
        ai.restAfterBurst = 2.5f;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return savedPrefab;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        Transform[] children = target.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            children[i].gameObject.layer = layer;
        }
    }
}
