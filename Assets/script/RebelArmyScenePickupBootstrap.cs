using System.Collections;
using Invector.vCharacterController;
using Invector.vCharacterController.vActions;
using Invector.vItemManager;
using Invector.vShooter;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class RebelArmyScenePickupBootstrap : MonoBehaviour
{
    private const string RebelSceneName = "RebelScene";
    private const string ArmySceneName = "ArmyScene";
    private const string RifleCollectableAssetPath = "Assets/Invector-3rdPersonController/Shooter/Prefabs/Weapon/_weapons_WITH_Inventory/_collectables/vCollectibleAssaultRifle.prefab";
    private const string RifleAmmoCollectableAssetPath = "Assets/Invector-3rdPersonController/Shooter/Prefabs/Weapon/_weapons_WITH_Inventory/_ammo/vAmmoAssaultRifle_Inventory.prefab";

    public int rifleItemId = 11;
    public int rifleAmmoItemId = 13;
    public int ammoAmountPerPickup = 75;
    public float pickupForwardDistance = 4f;
    public float pickupGroundOffset = 0.12f;
    public LayerMask groundMask = ~0;

    private bool configured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsSupportedScene(scene.name) || FindObjectOfType<RebelArmyScenePickupBootstrap>() != null)
        {
            return;
        }

        GameObject runner = new GameObject("RebelArmyScenePickupBootstrap");
        runner.AddComponent<RebelArmyScenePickupBootstrap>();
    }

    private void Start()
    {
        if (!IsSupportedScene(SceneManager.GetActiveScene().name))
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(ConfigureSceneRoutine());
    }

    private IEnumerator ConfigureSceneRoutine()
    {
        yield return null;
        yield return new WaitForSeconds(0.35f);
        ConfigureScene();
        yield return new WaitForSeconds(0.75f);
        ConfigureScene();
    }

    private void ConfigureScene()
    {
        if (configured)
        {
            return;
        }

        GameObject player = ResolvePlayer();
        if (player == null)
        {
            return;
        }

        configured = true;
        RemoveStarterRifle(player);
        SpawnPickups(player);
    }

    private void RemoveStarterRifle(GameObject player)
    {
        vShooterManager shooterManager = player.GetComponent<vShooterManager>();
        if (shooterManager != null)
        {
            if (IsRifleWeapon(shooterManager.rWeapon))
            {
                GameObject weaponObject = shooterManager.rWeapon.gameObject;
                shooterManager.SetRightWeapon(null);
                weaponObject.SetActive(false);
            }

            if (IsRifleWeapon(shooterManager.lWeapon))
            {
                GameObject weaponObject = shooterManager.lWeapon.gameObject;
                shooterManager.SetLeftWeapon(null);
                weaponObject.SetActive(false);
            }
        }

        vItemManager itemManager = player.GetComponent<vItemManager>();
        if (itemManager == null || itemManager.items == null)
        {
            return;
        }

        vItem rifleItem = itemManager.GetItem(rifleItemId);
        int guard = 0;
        while (rifleItem != null && guard++ < 8)
        {
            itemManager.DestroyItem(rifleItem, Mathf.Max(1, rifleItem.amount));
            rifleItem = itemManager.GetItem(rifleItemId);
        }
    }

    private void SpawnPickups(GameObject player)
    {
        if (GameObject.Find("RebelArmy_RiflePickup") != null)
        {
            return;
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 riflePosition = Grounded(player.transform.position + forward * pickupForwardDistance);
        Vector3 ammoPositionA = Grounded(riflePosition + right * 1.1f + forward * 0.4f);
        Vector3 ammoPositionB = Grounded(riflePosition - right * 1.1f + forward * 0.4f);

        int rightArmArea = FindEquipAreaIndex(player.GetComponent<vItemManager>(), "RightArm");
        CreateRiflePickup("RebelArmy_RiflePickup", riflePosition, Quaternion.LookRotation(forward, Vector3.up), rightArmArea);
        CreateAmmoPickup("RebelArmy_RifleAmmoPickup_A", ammoPositionA, Quaternion.LookRotation(forward, Vector3.up));
        CreateAmmoPickup("RebelArmy_RifleAmmoPickup_B", ammoPositionB, Quaternion.LookRotation(forward, Vector3.up));
    }

    private void CreateRiflePickup(string objectName, Vector3 position, Quaternion rotation, int rightArmArea)
    {
        GameObject pickup = InstantiateInvectorPickup(RifleCollectableAssetPath, objectName, position, rotation);
        if (pickup != null)
        {
            ConfigureInvectorCollection(pickup, rifleItemId, 1, true, true, Mathf.Max(0, rightArmArea), new Vector3(0f, 0.45f, 0.25f), new Vector3(2.2f, 1.4f, 2.2f));
            return;
        }

        pickup = new GameObject(objectName);
        pickup.transform.SetPositionAndRotation(position, rotation);
        BuildRifleVisual(pickup.transform);
        ConfigureFallbackCollectible(pickup, rifleItemId, 1, true, true, Mathf.Max(0, rightArmArea), "Assault Rifle", new Vector3(0f, 0.45f, 0.25f), new Vector3(2.2f, 1.4f, 2.2f));
    }

    private void CreateAmmoPickup(string objectName, Vector3 position, Quaternion rotation)
    {
        GameObject pickup = InstantiateInvectorPickup(RifleAmmoCollectableAssetPath, objectName, position, rotation);
        if (pickup != null)
        {
            ConfigureInvectorCollection(pickup, rifleAmmoItemId, Mathf.Max(1, ammoAmountPerPickup), false, false, 0, new Vector3(0f, 0.35f, 0f), new Vector3(1.5f, 1.1f, 1.5f));
            return;
        }

        pickup = new GameObject(objectName);
        pickup.transform.SetPositionAndRotation(position, rotation);
        BuildAmmoVisual(pickup.transform);
        ConfigureFallbackCollectible(pickup, rifleAmmoItemId, Mathf.Max(1, ammoAmountPerPickup), false, false, 0, "Rifle Ammo", new Vector3(0f, 0.35f, 0f), new Vector3(1.5f, 1.1f, 1.5f));
    }

    private GameObject InstantiateInvectorPickup(string assetPath, string objectName, Vector3 position, Quaternion rotation)
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"RebelArmyScenePickupBootstrap could not find Invector collectable prefab at {assetPath}");
            return null;
        }

        GameObject pickup = Instantiate(prefab, position, rotation);
        pickup.name = objectName;
        return pickup;
#else
        return null;
#endif
    }

    private void ConfigureInvectorCollection(GameObject pickup, int itemId, int amount, bool addToEquipArea, bool autoEquip, int indexArea, Vector3 triggerCenter, Vector3 triggerSize)
    {
        vItemCollection collection = pickup.GetComponentInChildren<vItemCollection>(true);
        if (collection == null)
        {
            collection = pickup.AddComponent<vItemCollection>();
        }

        collection.items.Clear();
        collection.items.Add(new ItemReference(itemId)
        {
            amount = Mathf.Max(1, amount),
            addToEquipArea = addToEquipArea,
            autoEquip = autoEquip,
            indexArea = Mathf.Max(0, indexArea)
        });
        collection.textDelay = 0.25f;
        collection.ignoreItemAnimation = true;
        collection.destroyAfter = true;
        collection.disableOnStart = false;
        collection.inputType = vTriggerGenericAction.InputType.GetButtonDown;
        collection.actionInput = new GenericInput("E", "A", "A");

        pickup.tag = "Action";
        collection.gameObject.tag = "Action";
        EnsureTrigger(collection.gameObject, triggerCenter, triggerSize);
    }

    private void ConfigureFallbackCollectible(GameObject pickup, int itemId, int amount, bool addToEquipArea, bool autoEquip, int indexArea, string displayName, Vector3 triggerCenter, Vector3 triggerSize)
    {
        RebelArmyPickupCollectible collectible = pickup.AddComponent<RebelArmyPickupCollectible>();
        collectible.itemId = itemId;
        collectible.amount = Mathf.Max(1, amount);
        collectible.addToEquipArea = addToEquipArea;
        collectible.autoEquip = autoEquip;
        collectible.indexArea = Mathf.Max(0, indexArea);
        collectible.displayName = displayName;
        collectible.destroyAfterPickup = true;
        EnsureTrigger(pickup, triggerCenter, triggerSize);
    }

    private void BuildRifleVisual(Transform parent)
    {
        CreatePrimitivePart(parent, "Body", new Vector3(0.18f, 0.14f, 0.8f), new Vector3(0f, 0.45f, 0f), new Color(0.08f, 0.08f, 0.08f, 1f));
        CreatePrimitivePart(parent, "Barrel", new Vector3(0.055f, 0.055f, 0.75f), new Vector3(0f, 0.48f, 0.72f), new Color(0.04f, 0.04f, 0.04f, 1f));
        CreatePrimitivePart(parent, "Stock", new Vector3(0.16f, 0.13f, 0.36f), new Vector3(0f, 0.44f, -0.58f), new Color(0.12f, 0.1f, 0.08f, 1f));
        CreatePrimitivePart(parent, "Magazine", new Vector3(0.12f, 0.28f, 0.16f), new Vector3(0f, 0.25f, 0.18f), new Color(0.05f, 0.05f, 0.05f, 1f));
        CreatePrimitivePart(parent, "Grip", new Vector3(0.09f, 0.25f, 0.12f), new Vector3(0.04f, 0.25f, -0.16f), new Color(0.04f, 0.04f, 0.04f, 1f));
    }

    private void BuildAmmoVisual(Transform parent)
    {
        CreatePrimitivePart(parent, "AmmoBox", new Vector3(0.55f, 0.25f, 0.38f), new Vector3(0f, 0.22f, 0f), new Color(0.18f, 0.24f, 0.12f, 1f));
        CreatePrimitivePart(parent, "AmmoBand", new Vector3(0.58f, 0.03f, 0.41f), new Vector3(0f, 0.37f, 0f), new Color(0.05f, 0.05f, 0.04f, 1f));
    }

    private void CreatePrimitivePart(Transform parent, string partName, Vector3 scale, Vector3 localPosition, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localScale = scale;
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Destroy(partCollider);
        }

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(renderer.sharedMaterial);
            material.color = color;
            renderer.sharedMaterial = material;
        }
    }

    private void EnsureTrigger(GameObject pickup, Vector3 center, Vector3 size)
    {
        Collider[] colliders = pickup.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].isTrigger)
            {
                return;
            }
        }

        BoxCollider trigger = pickup.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = pickup.AddComponent<BoxCollider>();
        }

        trigger.isTrigger = true;
        trigger.center = center;
        trigger.size = size;
    }

    private Vector3 Grounded(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * 4f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 12f, groundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * pickupGroundOffset;
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 8f, NavMesh.AllAreas))
        {
            return navHit.position + Vector3.up * pickupGroundOffset;
        }

        return position + Vector3.up * pickupGroundOffset;
    }

    private GameObject ResolvePlayer()
    {
        if (Invector.vGameController.instance != null &&
            Invector.vGameController.instance.currentPlayer != null)
        {
            return Invector.vGameController.instance.currentPlayer;
        }

        vThirdPersonController controller = FindObjectOfType<vThirdPersonController>();
        if (controller != null)
        {
            return controller.gameObject;
        }

        try
        {
            return GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private int FindEquipAreaIndex(vItemManager itemManager, string equipAreaName)
    {
        if (itemManager == null || itemManager.inventory == null || itemManager.inventory.equipAreas == null)
        {
            return 0;
        }

        for (int i = 0; i < itemManager.inventory.equipAreas.Length; i++)
        {
            if (itemManager.inventory.equipAreas[i] != null && itemManager.inventory.equipAreas[i].equipPointName == equipAreaName)
            {
                return i;
            }
        }

        return 0;
    }

    private static bool IsRifleWeapon(vShooterWeapon weapon)
    {
        if (weapon == null)
        {
            return false;
        }

        string category = weapon.weaponCategory;
        return category == "Rifle" || category == "AssaultRifle" || category == "Machine-Gun" || category == "MyCategory";
    }

    private static bool IsSupportedScene(string sceneName)
    {
        return sceneName == RebelSceneName || sceneName == ArmySceneName;
    }
}

[DisallowMultipleComponent]
public class RebelArmyPickupCollectible : MonoBehaviour
{
    public int itemId;
    public int amount = 1;
    public bool addToEquipArea;
    public bool autoEquip;
    public int indexArea;
    public string displayName = "Pickup";
    public bool destroyAfterPickup = true;
    public KeyCode pickupKey = KeyCode.E;

    private bool playerInside;
    private GameObject currentPlayer;
    private bool collected;

    private void Update()
    {
        if (!playerInside || collected || currentPlayer == null)
        {
            return;
        }

        if (Input.GetKeyDown(pickupKey))
        {
            Collect(currentPlayer);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other, out GameObject player))
        {
            playerInside = true;
            currentPlayer = player;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentPlayer != null && (other.gameObject == currentPlayer || other.transform.IsChildOf(currentPlayer.transform)))
        {
            playerInside = false;
            currentPlayer = null;
        }
    }

    private void Collect(GameObject player)
    {
        vItemManager itemManager = player.GetComponent<vItemManager>();
        if (itemManager != null)
        {
            ItemReference reference = new ItemReference(itemId)
            {
                amount = Mathf.Max(1, amount),
                addToEquipArea = addToEquipArea,
                autoEquip = autoEquip,
                indexArea = Mathf.Max(0, indexArea)
            };
            itemManager.CollectItem(reference, textDelay: 0.25f, ignoreItemAnimation: true);
            collected = true;
        }
        else
        {
            vAmmoManager ammoManager = player.GetComponent<vAmmoManager>();
            if (ammoManager == null || addToEquipArea)
            {
                return;
            }

            ammoManager.AddAmmo(displayName, itemId, Mathf.Max(1, amount));
            collected = true;
        }

        if (destroyAfterPickup)
        {
            Destroy(gameObject, 0.05f);
        }
    }

    private bool IsPlayer(Collider other, out GameObject player)
    {
        player = null;
        if (other == null)
        {
            return false;
        }

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag("Player") || current.GetComponent<vThirdPersonController>() != null)
            {
                player = current.gameObject;
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
