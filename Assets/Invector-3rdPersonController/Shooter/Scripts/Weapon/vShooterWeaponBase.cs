using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Invector.vShooter
{
    [vClassHeader("Shooter Weapon", openClose = false)]
    public class vShooterWeaponBase : vMonoBehaviour
    {
        #region Variables

        [vEditorToolbar("Weapon Settings")]
        [Tooltip("The category of the weapon\n Used to the IK offset system. \nExample: HandGun, Pistol, Machine-Gun")]
        public string weaponCategory = "MyCategory";
        [Tooltip("Frequency of shots")]
        public float shootFrequency;

        [vEditorToolbar("Ammo")]
        public bool isInfinityAmmo;
        [Tooltip("Starting ammo")]
        [SerializeField, vHideInInspector("isInfinityAmmo", true)]
        public int ammo;

        [vEditorToolbar("Layer & Tag")]
        public List<string> ignoreTags = new List<string>();
        public LayerMask hitLayer = 1 << 0;

        [vEditorToolbar("Projectile")]
        [Tooltip("Prefab of the projectile")]
        public GameObject projectile;
        [Tooltip("Assign the muzzle of your weapon")]
        public Transform muzzle;
        [Tooltip("How many projectiles will spawn per shot")]
        [Range(1, 20)]
        public int projectilesPerShot = 1;
        [Range(0, 90)]
        [Tooltip("how much dispersion the weapon have")]
        public float dispersion = 0;
        [Range(0, 1000)]
        [Tooltip("Velocity of your projectile")]
        public float velocity = 380;
        [Tooltip("Use the DropOffStart and DropOffEnd to calc damage by distance using min and max damage")]
        public bool damageByDistance = true;
        [Tooltip("Min distance to apply damage, used to evaluate the damage between minDamage and maxDamage")]
        [UnityEngine.Serialization.FormerlySerializedAs("DropOffStart")]
        public float minDamageDistance = 8f;
        [Tooltip("Max distance to apply damage, used to evaluate the damage between minDamage and maxDamage")]
        [UnityEngine.Serialization.FormerlySerializedAs("DropOffEnd")]
        public float maxDamageDistance = 50f;
        [Tooltip("Minimum damage caused by the shot, regardless the distance")]
        public int minDamage;
        [Tooltip("Maximum damage caused by the close shot")]
        public int maxDamage;

        [vEditorToolbar("Audio & VFX")]
        [Header("Audio")]
        public AudioSource source;
        public AudioClip fireClip;
        public AudioClip emptyClip;

        [Header("Effects")]
        public bool testShootEffect;
        public Light lightOnShot;
        [Tooltip("Repairs muzzle particle materials at runtime so old particle assets do not render as opaque white squares in URP.")]
        public bool repairMuzzleParticles = true;
        public Color muzzleFlashFallbackColor = new Color(0.48f, 0.48f, 0.45f, 0.34f);
        public Color muzzleSmokeFallbackColor = new Color(0.34f, 0.34f, 0.32f, 0.42f);
        [Range(0.05f, 5f)]
        public float muzzleParticleMaxSize = 0.85f;
        [SerializeField]
        public ParticleSystem[] emittShurykenParticle;
        protected Transform sender;
        protected readonly HashSet<ParticleSystem> repairedMuzzleParticles = new HashSet<ParticleSystem>();

        [HideInInspector]
        public OnDestroyEvent onDestroy;
        [System.Serializable]
        public class OnDestroyEvent : UnityEvent<GameObject> { }
        [System.Serializable]
        public class OnInstantiateProjectile : UnityEvent<vProjectileControl> { }

        [vEditorToolbar("Events")]
        public UnityEvent onShot, onEmptyClip;

        public OnInstantiateProjectile onInstantiateProjectile;

        protected float _nextShootTime;
        protected float _nextEmptyClipTime;
        #endregion

        #region Public Methods
        /// <summary>
        /// Apply additional velocity to the Shot projectile 
        /// </summary>
        public virtual float velocityMultiplierMod { get; set; }

        /// <summary>
        /// Apply additional damage to the projectile
        /// </summary>
        public virtual float damageMultiplierMod { get; set; }

        /// <summary>
        /// Weapon Name
        /// </summary>
        public virtual string weaponName
        {
            get
            {
                var value = gameObject.name.Replace("(Clone)", string.Empty);
                return value;
            }
        }

        /// <summary>
        /// Shoot to direction of the muzzle forward
        /// </summary>
        public virtual void Shoot()
        {
            Shoot(muzzle.position + muzzle.forward * 100f);
        }

        /// <summary>
        /// Shoot to direction of the muzzle forward
        /// </summary>
        /// <param name="sender">Sender to reference of the damage</param>
        /// <param name="successfulShot">Action to check if shoot is sucessful</param>
        public virtual void Shoot(Transform _sender = null, UnityAction<bool> successfulShot = null)
        {
            Shoot(muzzle.position + muzzle.forward * 100f, _sender, successfulShot);
        }

        /// <summary>
        /// Shoot to direction of the aim Position
        /// </summary>
        /// <param name="aimPosition">Aim position to override direction of the projectile</param>
        /// <param name="sender">ender to reference of the damage</param>
        /// <param name="successfulShot">Action to check if shoot is sucessful</param>
        public virtual void Shoot(Vector3 aimPosition, Transform _sender = null, UnityAction<bool> successfulShot = null)
        {
            if (HasAmmo())
            {
                if (!CanDoShot)
                {
                    return;
                }

                UseAmmo();
                this.sender = _sender != null ? _sender : transform;
                HandleShot(aimPosition);
                if (successfulShot != null)
                {
                    successfulShot.Invoke(true);
                }

                _nextShootTime = Time.time + shootFrequency;
                _nextEmptyClipTime = _nextShootTime;
            }
            else
            {
                if (!CanDoEmptyClip)
                {
                    return;
                }

                EmptyClipEffect();
                if (successfulShot != null)
                {
                    successfulShot.Invoke(false);
                }

                _nextEmptyClipTime = Time.time + shootFrequency;
            }
        }

        /// <summary>
        /// Check if can shoot by <seealso cref="shootFrequency"/>
        /// </summary>
        public virtual bool CanDoShot
        {
            get
            {
                bool _canShot = _nextShootTime < Time.time;
                return _canShot;
            }
        }
        /// <summary>
        /// Check if can do empty clip effect, <seealso cref="shootFrequency"/>
        /// </summary>
        public virtual bool CanDoEmptyClip
        {
            get
            {
                bool _canShot = _nextEmptyClipTime < Time.time;
                return _canShot;
            }
        }

        /// <summary>
        /// Use weapon Ammo
        /// </summary>
        /// <param name="count">count to use</param>
        public virtual void UseAmmo(int count = 1)
        {
            if (ammo <= 0)
            {
                return;
            }

            ammo -= count;
            if (ammo <= 0)
            {
                ammo = 0;
            }
        }

        /// <summary>
        /// Check if Weapon Has Ammo
        /// </summary>
        /// <returns></returns>
        public virtual bool HasAmmo()
        {

            return isInfinityAmmo || ammo > 0;
        }
        #endregion

        #region Protected Methods

        protected virtual void OnDestroy()
        {
            onDestroy.Invoke(gameObject);
        }

        private void OnApplicationQuit()
        {
            onDestroy.RemoveAllListeners();
        }
        protected virtual void HandleShot(Vector3 aimPosition)
        {
            ShootBullet(aimPosition);
            ShotEffect();
        }

        protected virtual Vector3 Dispersion(Vector3 aim, float distance, float variance)
        {
            aim.Normalize();
            Vector3 v3 = Vector3.zero;
            do
            {
                v3 = Random.insideUnitSphere;
            }
            while (v3 == aim || v3 == -aim);
            v3 = Vector3.Cross(aim, v3);
            v3 = v3 * Random.Range(0f, variance);
            return aim * distance + v3;
        }

        protected virtual void ShootBullet(Vector3 aimPosition)
        {
            var dir = aimPosition - muzzle.position;

            var rotation = Quaternion.LookRotation(dir);
            GameObject bulletObject = null;
            var velocityChanged = 0f;
            if (dispersion > 0 && projectile)
            {
                for (int i = 0; i < projectilesPerShot; i++)
                {
                    var dispersionDir = Dispersion(dir.normalized, maxDamageDistance, dispersion);
                    var spreadRotation = Quaternion.LookRotation(dispersionDir);
                    bulletObject = Instantiate(projectile, muzzle.transform.position, spreadRotation);

                    var pCtrl = bulletObject.GetComponent<vProjectileControl>();

                    pCtrl.shooterTransform = sender;
                    pCtrl.ignoreTags = ignoreTags;
                    pCtrl.hitLayer = hitLayer;
                    pCtrl.damage.sender = sender;
                    pCtrl.startPosition = bulletObject.transform.position;
                    pCtrl.damageByDistance = damageByDistance;
                    pCtrl.maxDamage = (int)((maxDamage / projectilesPerShot) * damageMultiplier);
                    pCtrl.minDamage = (int)((minDamage / projectilesPerShot) * damageMultiplier);
                    pCtrl.minDamageDistance = minDamageDistance;
                    pCtrl.maxDamageDistance = maxDamageDistance;
                    onInstantiateProjectile.Invoke(pCtrl);
                    velocityChanged = velocity * velocityMultiplier;
                    StartCoroutine(ApplyForceToBullet(bulletObject, dispersionDir, velocityChanged));

                    pCtrl = CreateProjectileData(aimPosition, velocityChanged, dispersionDir, pCtrl);
                }
            }
            else if (projectilesPerShot > 0 && projectile)
            {
                bulletObject = Instantiate(projectile, muzzle.transform.position, rotation);
                var pCtrl = bulletObject.GetComponent<vProjectileControl>();
                pCtrl.shooterTransform = sender;
                pCtrl.ignoreTags = ignoreTags;
                pCtrl.hitLayer = hitLayer;
                pCtrl.damage.sender = sender;
                pCtrl.startPosition = bulletObject.transform.position;
                pCtrl.damageByDistance = damageByDistance;
                pCtrl.maxDamage = (int)((maxDamage / projectilesPerShot) * damageMultiplier);
                pCtrl.minDamage = (int)((minDamage / projectilesPerShot) * damageMultiplier);
                pCtrl.minDamageDistance = minDamageDistance;
                pCtrl.maxDamageDistance = maxDamageDistance;
                onInstantiateProjectile.Invoke(pCtrl);
                velocityChanged = velocity * velocityMultiplier;

                StartCoroutine(ApplyForceToBullet(bulletObject, dir, velocityChanged));
            }
        }

        protected virtual vProjectileControl CreateProjectileData(Vector3 aimPosition, float velocityChanged, Vector3 dispersionDir, vProjectileControl pCtrl)
        {
            pCtrl.instantiateData = new vProjectileInstantiateData
            {
                aimPos = aimPosition,
                dir = dispersionDir,
                vel = velocityChanged
            };
            return pCtrl;
        }

        protected virtual IEnumerator ApplyForceToBullet(GameObject bulletObject, Vector3 direction, float velocityChanged)
        {
            yield return new WaitForSeconds(0.01f);
            try
            {
                var _rigidbody = bulletObject.GetComponent<Rigidbody>();
                _rigidbody.mass = _rigidbody.mass / projectilesPerShot;//Change mass per projectiles count.

                _rigidbody.AddForce((direction.normalized * velocityChanged), ForceMode.VelocityChange);
            }
            catch
            {

            }
        }

        protected virtual float damageMultiplier
        {
            get
            {
                return 1 + damageMultiplierMod;
            }
        }

        protected virtual float velocityMultiplier
        {
            get
            {
                return 1 + velocityMultiplierMod;
            }
        }

        #region Effects
        protected virtual void ShotEffect()
        {
            bool useFinalSceneExMuzzle = ShouldUseFinalSceneExMuzzleEffect();
            if (useFinalSceneExMuzzle)
            {
                StopAndClearEmitters();
            }

            onShot.Invoke();

            StopCoroutine(LightOnShoot());
            if (source && fireClip)
            {
               
                source.PlayOneShot(fireClip);
            }

            StartCoroutine(LightOnShoot(0.037f));
            if (useFinalSceneExMuzzle)
            {
                StopAndClearEmitters();
                StartFinalSceneExMuzzleEffect();
            }
            else
            {
                StartEmitters();
            }
        }

        protected virtual void StopSound()
        {
            if (source)
            {
                source.Stop();
            }
        }

        protected virtual IEnumerator LightOnShoot(float time = 0)
        {
            if (lightOnShot)
            {
                lightOnShot.enabled = true;

                yield return new WaitForSeconds(time);
                lightOnShot.enabled = false;
            }
        }

        protected virtual void StartEmitters()
        {
            if (emittShurykenParticle != null)
            {
                foreach (ParticleSystem pe in emittShurykenParticle)
                {
                    if (pe)
                    {
                        ConfigureMuzzleParticle(pe);
                        pe.Emit(1);
                    }
                }
            }
        }

        protected virtual bool ShouldUseFinalSceneExMuzzleEffect()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "FinalScene")
            {
                return false;
            }

            return emittShurykenParticle != null && emittShurykenParticle.Length > 0;
        }

        protected virtual void StartFinalSceneExMuzzleEffect()
        {
            bool spawned = false;
            if (emittShurykenParticle != null)
            {
                foreach (ParticleSystem pe in emittShurykenParticle)
                {
                    if (!pe)
                    {
                        continue;
                    }

                    Transform particleTransform = pe.transform;
                    global::FinalSceneRuntimeEffects.SpawnEx(particleTransform.position, particleTransform.rotation, 0.75f, 1.4f);
                    spawned = true;
                }
            }

            if (!spawned && muzzle)
            {
                global::FinalSceneRuntimeEffects.SpawnEx(muzzle.position, muzzle.rotation, 0.75f, 1.4f);
            }
        }

        protected virtual void StopAndClearEmitters()
        {
            if (emittShurykenParticle == null)
            {
                return;
            }

            foreach (ParticleSystem pe in emittShurykenParticle)
            {
                if (pe)
                {
                    pe.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    pe.Clear(true);
                }
            }
        }

        protected virtual void ConfigureMuzzleParticle(ParticleSystem particle)
        {
            if (!repairMuzzleParticles || !particle || repairedMuzzleParticles.Contains(particle))
            {
                return;
            }

            var isSmoke = particle.name.IndexOf("smoke", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          particle.name.IndexOf("muzzle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          particle.name.IndexOf("shot", System.StringComparison.OrdinalIgnoreCase) >= 0;
            var fallbackColor = isSmoke ? muzzleSmokeFallbackColor : muzzleFlashFallbackColor;
            var renderer = particle.GetComponent<ParticleSystemRenderer>();

            if (renderer)
            {
                renderer.maxParticleSize = Mathf.Min(renderer.maxParticleSize, muzzleParticleMaxSize);
                renderer.minParticleSize = Mathf.Min(renderer.minParticleSize, muzzleParticleMaxSize);

                var materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    ConfigureMuzzleParticleMaterial(materials[i], fallbackColor, isSmoke);
                    fallbackColor = ReadParticleColor(materials[i], fallbackColor);
                }

                renderer.materials = materials;
            }

            var main = particle.main;
            main.startColor = new ParticleSystem.MinMaxGradient(fallbackColor);
            repairedMuzzleParticles.Add(particle);
        }

        protected virtual void ConfigureMuzzleParticleMaterial(Material material, Color fallbackColor, bool isSmoke)
        {
            if (!material)
            {
                return;
            }

            var color = ReadParticleColor(material, fallbackColor);
            var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                                 Shader.Find("Particles/Standard Unlit") ??
                                 Shader.Find("Legacy Shaders/Particles/Additive");
            if (particleShader)
            {
                material.shader = particleShader;
            }

            var mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            if (mainTexture && material.HasProperty("_BaseMap") && !material.GetTexture("_BaseMap"))
            {
                material.SetTexture("_BaseMap", mainTexture);
            }

            SetMaterialColor(material, "_BaseColor", color);
            SetMaterialColor(material, "_Color", color);
            SetMaterialColor(material, "_TintColor", color);
            SetMaterialColor(material, "_BaseColorAddSubDiff", color);
            SetMaterialColor(material, "_EmissionColor", isSmoke ? Color.black : new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a)));

            SetMaterialFloat(material, "_Surface", 1f);
            SetMaterialFloat(material, "_Blend", isSmoke ? 0f : 2f);
            SetMaterialFloat(material, "_AlphaClip", 0f);
            SetMaterialFloat(material, "_ZWrite", 0f);
            SetMaterialFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetMaterialFloat(material, "_DstBlend", (float)(isSmoke ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : UnityEngine.Rendering.BlendMode.One));

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        protected virtual Color ReadParticleColor(Material material, Color fallbackColor)
        {
            if (!material)
            {
                return fallbackColor;
            }

            if (material.HasProperty("_TintColor"))
            {
                var color = material.GetColor("_TintColor");
                if (color.a > 0f)
                {
                    return color;
                }
            }

            if (material.HasProperty("_BaseColor"))
            {
                var color = material.GetColor("_BaseColor");
                if (color.a > 0f)
                {
                    return color;
                }
            }

            if (material.HasProperty("_Color"))
            {
                var color = material.GetColor("_Color");
                if (color.a > 0f)
                {
                    return color;
                }
            }

            return fallbackColor;
        }

        protected virtual void SetMaterialColor(Material material, string propertyName, Color color)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        protected virtual void SetMaterialFloat(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        protected virtual void StopEmitters()
        {
            if (emittShurykenParticle != null)
            {
                foreach (ParticleSystem pe in emittShurykenParticle)
                {
                    pe.Stop();
                }
            }
        }

        protected virtual void EmptyClipEffect()
        {
            if (source && emptyClip)
            {              
                source.PlayOneShot(emptyClip);
            }

            onEmptyClip.Invoke();
        }
        #endregion

        #endregion
    }
}
