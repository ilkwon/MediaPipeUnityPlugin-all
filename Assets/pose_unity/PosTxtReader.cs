using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

// pos.txtのデータ
// https://github.com/miu200521358/3d-pose-baseline-vmd/blob/master/doc/Output.md
// 0 :Hip
// 1 :RHip
// 2 :RKnee
// 3 :RFoot
// 4 :LHip
// 5 :LKnee
// 6 :LFoot
// 7 :Spine
// 8 :Thorax
// 9 :Neck/Nose
// 10:Head
// 11:LShoulder
// 12:LElbow
// 13:LWrist
// 14:RShoulder
// 15:RElbow
// 16:RWrist

public class PosTxtReader : MonoBehaviour
{
    public UIPanel ui;
    public String posFilename; // pos.txt 파일명

    //---------------------------------------------------------------------------
    [Header("Option")]
    public int startFrame; // 시작 프레임
    public String endFrame; // 종료 프레임
    public int nowFrame_readonly; // 현재 프레임 (읽기 전용)
    public float upPosition = 0.1f; // 발 위치 보정값(단위: m). 양수값을 설정하면 캐릭터 전체가 위로 이동
    public Boolean showDebugCube; // 디버그용 큐브 표시 여부

    //---------------------------------------------------------------------------
    [Header("Save Motion")]
    [Tooltip("이 플래그가 설정되면, 재생의 마지막 프레임에 모션을 저장합니다.")]
    public Boolean saveMotion; // 재생의 마지막 프레임에 모션을 저장

    [Tooltip("이것은 BVH 파일이 저장될 파일명입니다. 파일명이 주어지지 않으면, 타임스탬프를 기반으로 새 파일명이 생성됩니다. 파일이 이미 존재하면, 끝에 숫자가 추가됩니다.")]
    public String saveBVHFilename; // 저장 파일명

    [Tooltip("이 플래그가 설정되면, 기존 파일을 덮어쓰고 끝에 숫자를 추가하지 않습니다.")]
    public bool overwrite = false; // False인 경우, 덮어쓰지 않고 파일명 끝에 숫자를 추가

    [Tooltip("이 옵션이 활성화되면, 인간형 본만을 대상으로 본을 감지합니다. 이는 머리카락 본과 같은 것들이 본 감지 목록에 추가되지 않음을 의미합니다.")]
    public bool enforceHumanoidBones = true; // 머리카락 등 인간형 본이 아닌 것은 출력하지 않음

    //---------------------------------------------------------------------------
    private float scaleRatio = 0.001f;  // pos.txt와 Unity 모델의 스케일 비율
                                        // pos.txt의 단위는 mm이고 Unity는 m이므로, 0.001에 가까운 값을 지정. 모델의 크기에 따라 조절

    private float headAngle = 15f;      // 얼굴 방향 조정. 얼굴을 15도 올림
                                   

    private float playTime;                 // 재생 시간
    private int frame;                      // 재생 프레임
    private Transform[] boneT;              // 모델의 본 Transform
    private Transform[] cubeT;              // 디버그 표시용 큐브의 Transform
    private Vector3 rootPosition;           // 초기 Avatar의 위치
    private Quaternion rootRotation;        // 초기 Avatar의 회전
    private Quaternion[] initRot;           // 초기 회전값
    private Quaternion[] initInv;           // 초기 본 방향에서 계산된 Quaternion의 Inverse
    private float hipHeight;                // hip의 position.y
    private List<Vector3[]> pos;            // pos.txt 데이터를 보관하는 컨테이너
    private BVHRecorder recorder;           // BVH 저장용 컴포넌트
    private int[] bones      = new int[10] { 1, 2, 4, 5, 7,  8, 11, 12, 14, 15 };         // 부모 bone
    private int[] childBones = new int[10] { 2, 3, 5, 6, 8, 10, 12, 13, 15, 16 };   // bones에 해당하는 자식 bone
    private int boneNum = 17;
    private Animator anim;
    private int sFrame;
    private int eFrame;
    private bool bvhSaved = false;

    //-------------------------------------------------------------------------
    // pos.txt의 데이터를 읽고, 리스트로 반환
    private List<Vector3[]> ReadPosData(string filename)
    {
        List<Vector3[]> data = new List<Vector3[]>();

        List<string> lines = new List<string>();
        StreamReader sr = new StreamReader(filename);
        while (!sr.EndOfStream)
        {
            lines.Add(sr.ReadLine());
        }
        sr.Close();

        try
        {
            foreach (string line in lines)
            {
                string line2 = line.Replace(",", "");
                string[] str = line2.Split(new string[] { " " }, 
                    System.StringSplitOptions.RemoveEmptyEntries); // 스페이스로 분할하고, 빈 문자열은 제거

                Vector3[] vs = new Vector3[boneNum];
                for (int i = 0; i < str.Length; i += 4)
                {
                    int n = (int)(i / 4);
                    float x = -float.Parse(str[i + 1]);
                    float y =  float.Parse(str[i + 3]);
                    float z = -float.Parse(str[i + 2]);
                    vs[n] = new Vector3(x, y, z);
                    Debug.LogFormat("n:{0}x:{1},y:{2},z:{3}", n, x, y, z);
                }
                data.Add(vs);
            }
        }
        catch (Exception e)
        {
            ErrorMessage("<color=blue>Error! Pos File is broken(" + filename + ").</color>");
    
            return null;
        }
        return data;
    }
    
    //-------------------------------------------------------------------------
    // BoneTransform을 얻고, 회전의 초기값을 얻음
    private void GetInitInfo()
    {
        boneT = new Transform[boneNum];
        initRot = new Quaternion[boneNum];
        initInv = new Quaternion[boneNum];

        boneT[0] = anim.GetBoneTransform(HumanBodyBones.Hips);
        boneT[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        boneT[2] = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        boneT[3] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        boneT[4] = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        boneT[5] = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        boneT[6] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        boneT[7] = anim.GetBoneTransform(HumanBodyBones.Spine);
        boneT[8] = anim.GetBoneTransform(HumanBodyBones.Neck);
        boneT[10] = anim.GetBoneTransform(HumanBodyBones.Head);
        boneT[11] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        boneT[12] = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        boneT[13] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        boneT[14] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        boneT[15] = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        boneT[16] = anim.GetBoneTransform(HumanBodyBones.RightHand);

        if (boneT[0] == null)
        {
            ErrorMessage("<color=blue>Error! Failed to get Bone Transform. " +
                "Confirm wherther animation type of your model is Humanoid</color>");
            
            return;
        }

        // Spine, LHip, RHip을 이용해 삼각형을 만들고 그것을 전방 방향으로 설정한다.
        Vector3 initForward = TriangleNormal(boneT[7].position, boneT[4].position, boneT[1].position);
        initInv[0] = Quaternion.Inverse(Quaternion.LookRotation(initForward));

        // initPosition = boneT[0].position;
        rootPosition = this.transform.position;
        rootRotation = this.transform.rotation;
        initRot[0] = boneT[0].rotation;
        hipHeight = boneT[0].position.y - this.transform.position.y;
        for (int i = 0; i < bones.Length; i++)
        {
            int b = bones[i];
            int cb = childBones[i];

            // 대상 모델의 회전 초기값
            initRot[b] = boneT[b].rotation;
            // 초기 뼈의 방향에서 계산된 쿼터니언
            initInv[b] = Quaternion.Inverse(
                Quaternion.LookRotation(boneT[b].position - boneT[cb].position, initForward));
        }
    }
    
    //-------------------------------------------------------------------------
    // 지정된 3개의 점으로 이루어진 삼각형에 수직이고 길이가 1인 벡터를 반환한다
    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;

        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();

        return dd;
    }

    //-------------------------------------------------------------------------
    // 디버그용 cube를 생성한다. 이미 생성된 경우 위치를 업데이트한다
    private void UpdateCube(int frame)
    {
        if (cubeT == null)
        {
            // 초기화 하고、cube를 생성한다
            cubeT = new Transform[boneNum];

            for (var i = 0; i < boneNum; i++)
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                t.transform.parent = transform;
                t.localPosition = pos[frame][i] * scaleRatio;
                t.name = i.ToString();
                t.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                cubeT[i] = t;

                Destroy(t.GetComponent<BoxCollider>());
            }
        }
        else
        {
            // 모델과 겹치지 않도록 조금 이동하여 표시
            Vector3 offset = new Vector3(1.2f, 0, 0);

            // 이미 초기화된 경우, cube의 위치를 업데이트한다
            for (int i = 0; i < boneNum; i++)
            {
                cubeT[i].localPosition = pos[frame][i] * scaleRatio + new Vector3(0, upPosition, 0) + offset;
            }
        }
    }

    //---------------------------------------------------------------------------
    private void Start()
    {

        anim = GetComponent<Animator>();
        playTime = 0;
        if (posFilename == "")
        {
            ErrorMessage("<color=blue>Error! Pos filename  is empty.</color>");
            
            return;
        }
        if (System.IO.File.Exists(posFilename) == false)
        {
            ErrorMessage("<color=blue>Error! Pos file not found(" + posFilename + ").</color>");            
            
            return;
        }
        pos = ReadPosData(posFilename);
        GetInitInfo();
        if (pos != null)
        {
            // inspector에서 지정한 시작 프레임, 종료 프레임 번호를 설정
            if (startFrame >= 0 && startFrame < pos.Count)
            {
                sFrame = startFrame;
            }
            else
            {
                sFrame = 0;
            }
            int ef;
            if (int.TryParse(endFrame, out ef))
            {
                if (ef >= sFrame && ef < pos.Count)
                {
                    eFrame = ef;
                }
                else
                {
                    eFrame = pos.Count - 1;
                }
            }
            else
            {
                eFrame = pos.Count - 1;
            }
            frame = sFrame;
        }

        if (saveMotion)
        {
            recorder = gameObject.AddComponent<BVHRecorder>();
            recorder.scripted = true;
            recorder.targetAvatar = anim;
            recorder.blender = false;
            recorder.enforceHumanoidBones = enforceHumanoidBones;
            recorder.getBones();
            recorder.buildSkeleton();
            recorder.genHierarchy();
            recorder.frameRate = 30.0f;
        }
    }

    //---------------------------------------------------------------------------
    private void Update()
    {
        if (pos == null || boneT[0] == null)
        {
            return;
        }
        playTime += Time.deltaTime;

        if (saveMotion && recorder != null)
        {
            // 파일 출력의 경우 1프레임씩 진행
            frame += 1;            
        }
        else
        {
            frame = sFrame + (int)(playTime * 30.0f);  // pos.txt는 30fps를 가정            
        }

        ui.frame.text = string.Format("Frame:{0}", frame);
        
        if (frame > eFrame)
        {
            if (saveMotion && recorder != null)
            {
                if (!bvhSaved)
                {
                    bvhSaved = true;
                    if (saveBVHFilename != "")
                    {
                        string fullpath = Path.GetFullPath(saveBVHFilename);
                        // recorder.directory = Path.GetDirectoryName(fullpath);
                        // recorder.filename = Path.GetFileName(fullpath);
                        recorder.directory = "";
                        recorder.filename = fullpath;
                        recorder.overwrite = overwrite;
                        recorder.saveBVH();
                        Debug.Log("Saved Motion(BVH) to " + recorder.lastSavedFile);
                    }
                    else
                    {
                        ErrorMessage("<color=blue>Error! Save BVH Filename is empty.</color>");                        
                    }
                }
            }
            return;
        }
        nowFrame_readonly = frame; // Inspector 표시용

        if (showDebugCube)
        {
            UpdateCube(frame); // 디버그용 Cube를 표시한다
        }

        Vector3[] nowPos = pos[frame];

        // 센터의 이동과 회전
        Vector3 posForward = TriangleNormal(nowPos[7], nowPos[4], nowPos[1]);

        this.transform.position = rootRotation * nowPos[0] * scaleRatio + rootPosition + new Vector3(0, upPosition - hipHeight, 0);
        boneT[0].rotation = rootRotation * Quaternion.LookRotation(posForward) * initInv[0] * initRot[0];

        // 각 뼈의 회전
        for (int i = 0; i < bones.Length; i++)
        {
            int b = bones[i];
            int cb = childBones[i];
            boneT[b].rotation = rootRotation * Quaternion.LookRotation(nowPos[b] - nowPos[cb], posForward) * initInv[b] * initRot[b];
        }

        // 얼굴 방향을 위로 조절. 양 어깨를 잇는 선을 축으로 회전
        //boneT[8].rotation = Quaternion.AngleAxis(headAngle, boneT[11].position - boneT[14].position) * boneT[8].rotation;

        if (saveMotion && recorder != null)
        {
            recorder.captureFrame();
        }
    }
    //-------------------------------------------------------------------------
    private void ErrorMessage(string msg)
    {
        ui.message.text = msg;
        Debug.Log(msg);
    }
    //---------------------------------------------------------------------------
}
