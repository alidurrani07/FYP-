using System.Collections.Generic;
using UnityEngine;
namespace Invector.vShooter
{
    [CreateAssetMenu(menuName = "Invector/Effects/New ImpactEffect", fileName = "ImpactEffect@")]
    public class vImpactEffect : vImpactEffectBase
    {
        public List<GameObject> decals;
        public List<GameObject> hitEffects;
        public bool spawnDecals;
        public bool spawnHitParticles;
        public bool recolorHitParticles = true;
        public Color hitParticleColor = new Color(0.75f, 0.02f, 0.02f, 1f);
        [Range(0.1f, 1f)]
        public float hitParticleSizeMultiplier = 0.35f;

        protected virtual GameObject GetRandomObject(List<GameObject> referenceList)
        {
            if (referenceList.Count > 1)
            {
                var index = Random.Range(0, referenceList.Count);
                return referenceList[index];
            }
            else if (referenceList.Count == 1)
                return referenceList[0];
            else
                return null;
        }

        protected virtual GameObject CreateDecal(Vector3 position, Quaternion rotation)
        {
            return CreateInstance(GetRandomObject(decals), position, rotation);
        }
        protected virtual GameObject CreateHitEffect(Vector3 position, Quaternion rotation)
        {
            return CreateInstance(GetRandomObject(hitEffects), position, rotation);
        }

        protected GameObject CreateInstance(GameObject target, Vector3 position, Quaternion rotation)
        {
            if (target == null) return null;
            else return Instantiate(target, position, rotation);
        }

        public override void DoImpactEffect(Vector3 position, Quaternion rotation, GameObject sender, GameObject receiver)
        {
            var decal = spawnDecals ? CreateInstance(GetRandomObject(decals), position, rotation) : null;
            if (decal)
            {
                decal.transform.Rotate(Vector3.forward, Random.Range(0, 360), Space.Self);
            }

            var hitEffect = spawnHitParticles ? CreateInstance(GetRandomObject(hitEffects), position, rotation) : null;
            ConfigureHitEffect(hitEffect);
            if (decal && receiver)
            {
                decal.transform.SetParent(receiver.transform, true);
            }
            if (hitEffect)
            {
                hitEffect.transform.SetParent(vObjectContainer.root, true);
            }
        }

        protected virtual void ConfigureHitEffect(GameObject hitEffect)
        {
            if (!recolorHitParticles || hitEffect == null)
            {
                return;
            }

            var particles = hitEffect.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                var main = particles[i].main;
                main.startColor = hitParticleColor;
                main.startSize = ScaleMinMaxCurve(main.startSize, hitParticleSizeMultiplier);
            }
        }

        protected virtual ParticleSystem.MinMaxCurve ScaleMinMaxCurve(ParticleSystem.MinMaxCurve curve, float multiplier)
        {
            curve.constant *= multiplier;
            curve.constantMin *= multiplier;
            curve.constantMax *= multiplier;
            return curve;
        }
    }
}
