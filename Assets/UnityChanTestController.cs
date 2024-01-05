using System;
using UnityEngine;

public class UnityChanTestController : MonoBehaviour
{
  public enum Parts
  {
    spine,
    chest,
    neck,
    rightShoulder,
    rightElbow,
    leftShoulder,
    leftElbow,
    rightHip,
    rightKnee,
    leftHip,
    leftKnee,
  };
  public GameObject cube, sphere;
  public Animator anim;
  public Parts target;
  private Transform from_bone;
  private Transform to_bone;
  private void Start()
  {
    AttatchTarget();

    rq = Quaternion.Inverse(Quaternion.LookRotation(from_bone.localPosition - to_bone.localPosition));
  }

  private void AttatchTarget()
  {
    switch(target)
    {
      case Parts.rightShoulder:
        from_bone = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        to_bone = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        break;

    }
  }
  private Quaternion rq;
  private void Update()
  {
    Quaternion c2sqt;
    Vector3 c2svec = cube.transform.position - sphere.transform.position;
    c2svec *= -1f;
    c2sqt = Quaternion.LookRotation(c2svec);

    from_bone.rotation = c2sqt * rq;

  }
}
