using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Invector.IK
{  
    public class vArmAimAlign
    {
        Transform root;
        Transform upperArm;
        Transform foreArm;
        Transform hand;

        public Transform _upperArm;
        public Transform _foreArm;
        public Transform _hand;
        public Transform _aimReference;
        public float smooth;
        public float maxAimAngle;
        Quaternion _upperArmRotation;
        Quaternion _foreArmRotation;
        Quaternion _handRotation;
        Quaternion _aimReferenceRotation;

        Vector3 _upperArmPosition;
        Vector3 _foreArmPosition;
        Vector3 _handPosition;
        Vector3 _aimReferencePosition;

        Quaternion upperArmAlignment;
        Quaternion handAlignment;

       
        public vArmAimAlign(Transform upperArm, Transform forearm, Transform hand)
        {
            if (upperArm) this.upperArm = upperArm;
            if (forearm) this.foreArm = forearm;
            if (hand) this.hand = hand;

            if (!IsValid) return;

            root = upperArm.parent;
            _upperArm = new GameObject("UpperArmHelper").transform;
            _foreArm = new GameObject("ForeArmHelper").transform;
            _hand = new GameObject("HandHelper").transform;
            _aimReference = new GameObject("AimReferenceHelper").transform;

            _upperArm.parent = root;
            _foreArm.parent = _upperArm;
            _hand.parent = _foreArm;
            _aimReference.parent = _hand;
        }

        public bool IsValid => this.upperArm && this.foreArm && this.hand;       

        public void UpdatePose(Transform aimReference)
        {
            if (!IsValid) return;
            _upperArm.SetPositionAndRotation(upperArm.position, upperArm.rotation);
            _upperArm.localScale = upperArm.localScale;
            _foreArm.SetPositionAndRotation(foreArm.position, foreArm.rotation);
            _foreArm.localScale = foreArm.localScale;
            _hand.SetPositionAndRotation(hand.position, hand.rotation);
            _hand.localScale = hand.localScale;
            _aimReference.SetPositionAndRotation(aimReference.position, aimReference.rotation);
            _aimReference.localScale = aimReference.localScale;

            _upperArmRotation = _upperArm.localRotation;
            _foreArmRotation = _foreArm.localRotation;
            _handRotation = _hand.localRotation;
            _aimReferenceRotation = _aimReference.localRotation;

            _upperArmPosition = _upperArm.localPosition;
            _foreArmPosition = _foreArm.localPosition;
            _handPosition = _hand.localPosition;
            _aimReferencePosition = _aimReference.localPosition;
        }

        public void RestoreLastPose(Transform aimReference)
        {
            if (!IsValid) return;
            _upperArm.localRotation = _upperArmRotation;
            _foreArm.localRotation = _foreArmRotation;
            _hand.localRotation = _handRotation;
            _aimReference.localRotation = _aimReferenceRotation;

            _upperArm.localPosition = _upperArmPosition;
            _foreArm.localPosition = _foreArmPosition;
            _hand.localPosition = _handPosition;
            _aimReference.localPosition = _aimReferencePosition;
        }

        public void AlighPoseToAimPoint(Vector3 aimPoint, float weight, bool alignUpperArm = true, bool alignHand = true)
        {
            if (!IsValid) return;
            if (alignUpperArm) AlignUpperArm(aimPoint, alignHand ? weight * 0.5f : weight);
            if (alignHand) AlignHand(aimPoint, weight);
        }

        void AlignUpperArm(Vector3 aimPoint, float weight)
        {
            Vector3 v = aimPoint - _aimReference.position;
            var orientation = _aimReference.forward;

            var rot = Quaternion.FromToRotation(_upperArm.InverseTransformDirection(orientation), _upperArm.InverseTransformDirection(v));

            if ((!float.IsNaN(rot.x) && !float.IsNaN(rot.y) && !float.IsNaN(rot.z)))
            {
                upperArmAlignment = Quaternion.Lerp(upperArmAlignment, rot, smooth * (.001f + Time.deltaTime));

                if (!float.IsNaN(upperArmAlignment.x) && !float.IsNaN(upperArmAlignment.y) && !float.IsNaN(upperArmAlignment.z))
                {

                    var additiveRotation = Quaternion.Lerp(Quaternion.identity, upperArmAlignment, weight);
                    upperArm.localRotation *= additiveRotation;
                    _upperArm.localRotation *= additiveRotation;
                }
            }

        }

        void AlignHand(Vector3 aimPoint, float weight)
        {
            Vector3 v = aimPoint - _aimReference.position;
            var orientation = _aimReference.forward;

            var rot = Quaternion.FromToRotation(_hand.InverseTransformDirection(orientation), _hand.InverseTransformDirection(v));

            if ((!float.IsNaN(rot.x) && !float.IsNaN(rot.y) && !float.IsNaN(rot.z)))
            {
                handAlignment = Quaternion.Lerp(handAlignment, rot, smooth * (.001f + Time.deltaTime));

                if (!float.IsNaN(handAlignment.x) && !float.IsNaN(handAlignment.y) && !float.IsNaN(handAlignment.z))
                {

                    var additiveRotation = Quaternion.Lerp(Quaternion.identity, handAlignment, weight);
                    hand.localRotation *= additiveRotation;
                    _hand.localRotation *= additiveRotation;
                }
            }
        }
        public void DrawBones(Color color)
        {
            if (!IsValid) return;
            Debug.DrawLine(upperArm.position, foreArm.position, color);
            Debug.DrawLine(foreArm.position, hand.position, color);
         
        
        }
        public void DrawHelpers(Color color)
        {
            if (!IsValid) return;
            Debug.DrawLine(_upperArm.position, _foreArm.position, color);
            Debug.DrawLine(_foreArm.position, _hand.position, color);
            Debug.DrawLine(_hand.position, _aimReference.position, color);
            Debug.DrawRay(_aimReference.position, _aimReference.forward, color);         
        }

       
    }
}