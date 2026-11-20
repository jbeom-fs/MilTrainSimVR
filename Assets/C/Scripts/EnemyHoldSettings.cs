using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 적 병사가 총을 드는 모양 (11/03) — 두 손 자리 · 손가락 굽힘 · 조준 자세 / 낮춘 자세의 총 자리.
    // EnemySoldier 가 이 값으로 계산한다. 모든 적이 같은 에셋(Models/Enemy/C_EnemyHold)을 쓰고, 에디터에서 Play 중에 값을 바꾸면
    // 바로 보인다. 에셋이라 Play 를 멈춰도 바꾼 값이 남고, 프리팹을 다시 만들 필요도 없다 (오른눈 자리만 '적 병사 만들기'를 다시 눌러야 한다).
    // 맞추는 법: Docs/C/1103_C_적_총잡기_조정법.md
    // 좌표: '총 기준' = K2C1 기준 (X 오른쪽 · Y 위 · Z 총구 쪽, 원점 = 권총손잡이 Anchor_Grip), '몸 기준' = 적 발밑 기준 (X 오른쪽 · Y 위 · Z 앞)
    [CreateAssetMenu(fileName = "C_EnemyHold", menuName = "MilTrainSimVR/C/적 총 잡기 설정")]
    public class EnemyHoldSettings : ScriptableObject
    {
        // 한 손 손가락 굽힘(°). 0 = T 자세 그대로(편 손가락), + = 손바닥 쪽으로 굽힘
        [System.Serializable]
        public struct FingerCurl
        {
            [Tooltip("집게손가락 뿌리 마디")] [Range(-20f, 110f)] public float indexBase;
            [Tooltip("집게손가락 가운데 마디")] [Range(-20f, 110f)] public float indexMid;
            [Tooltip("가운뎃손가락 뿌리 마디 (약지 · 새끼도 이 뼈를 따라간다)")] [Range(-20f, 110f)] public float middleBase;
            [Tooltip("가운뎃손가락 가운데 마디")] [Range(-20f, 110f)] public float middleMid;
            [Tooltip("가운뎃손가락 끝 마디")] [Range(-20f, 110f)] public float middleTip;
            [Tooltip("엄지를 손바닥 앞으로 모으기")] [Range(-40f, 110f)] public float thumbIn;
            [Tooltip("엄지 가운데 마디")] [Range(-40f, 110f)] public float thumbMid;
            [Tooltip("엄지 끝 마디")] [Range(-40f, 110f)] public float thumbTip;

            public FingerCurl(float indexBase, float indexMid, float middleBase, float middleMid, float middleTip, float thumbIn, float thumbMid, float thumbTip)
            {
                this.indexBase = indexBase;
                this.indexMid = indexMid;
                this.middleBase = middleBase;
                this.middleMid = middleMid;
                this.middleTip = middleTip;
                this.thumbIn = thumbIn;
                this.thumbMid = thumbMid;
                this.thumbTip = thumbTip;
            }
        }

        [Header("오른손 — 권총손잡이")]
        [Tooltip("손을 통째로 옮긴다 (cm, 총 기준 X 오른쪽 · Y 위 · Z 앞)")]
        public Vector3 rightShift = new Vector3(-1.3f, -1.4f, -0.9f);
        [Tooltip("손바닥이 보는 쪽을 손잡이 축 둘레로 돌린다 (°). 0 = 바로 왼쪽(손바닥이 손잡이 오른쪽 면에 붙음), + = 손바닥이 뒤쪽으로 → 손가락 뿌리가 앞 · 왼쪽으로")]
        [Range(-60f, 60f)] public float rightPalmTurn = 16f;
        [Tooltip("손등 줄(손목 → 손가락 뿌리)을 손바닥 축 둘레로 기울인다 (°)")]
        [Range(-45f, 45f)] public float rightTilt;
        public FingerCurl rightCurl = new FingerCurl(20f, 100f, 77f, 20f, 23f, 66f, 0f, 0f);

        [Header("왼손 — 총열덮개")]
        [Tooltip("손을 통째로 옮긴다 (cm, 총 기준)")]
        public Vector3 leftShift = new Vector3(0f, 0.7f, 0f);
        [Tooltip("손가락이 뻗는 쪽 (°, 위에서 볼 때) — 0 = 바로 오른쪽, + = 총구 쪽으로")]
        [Range(-60f, 90f)] public float leftFingerAngle = 19f;
        [Tooltip("손을 손가락 줄 둘레로 굴린다 (°) — + = 손바닥이 오른쪽(총열덮개 옆)으로")]
        [Range(-60f, 60f)] public float leftTilt = -7.5f;
        public FingerCurl leftCurl = new FingerCurl(37f, 61f, 81f, 34f, 50f, 6f, 17f, 15f);
        [Tooltip("왼팔이 닿지 않으면 총열덮개를 따라 이만큼(cm)까지 뒤를 쥔다 — K2C1 은 앞손 자리 8cm 뒤가 탄창 앞")]
        [Range(0f, 15f)] public float leftSlide = 8f;

        [Header("조준 자세 — 가늠자가 오른눈 앞")]
        [Tooltip("가늠자와 오른눈 사이 (cm)")]
        [Range(2f, 20f)] public float eyeRelief = 9f;
        [Tooltip("총을 통째로 옮긴다 (cm, 몸 기준 X 오른쪽 · Y 위 · Z 앞)")]
        public Vector3 aimShift = new Vector3(3f, -1f, 0f);
        [Tooltip("총을 돌린다 (°, X 총구 숙임 · Y 좌우 · Z 기울임)")]
        public Vector3 aimTilt;

        [Header("낮춘 자세 — 대기 · 걷기 · 뛰기 (가슴 앞을 비스듬히 가로질러 든다)")]
        [Tooltip("권총손잡이 가운데 자리 (cm, 몸 기준 · 발밑 원점) — 대기 애니메이션 첫 자세의 가슴을 따라 움직인다")]
        public Vector3 lowGrip = new Vector3(8f, 104f, 22f);
        [Tooltip("총구가 보는 쪽 좌우 (°) — 0 = 앞, − = 왼쪽")]
        [Range(-90f, 90f)] public float lowYaw = -51f;
        [Tooltip("총구 위아래 (°) — + = 위")]
        [Range(-70f, 70f)] public float lowPitch = 17f;
        [Tooltip("총을 총구 축 둘레로 기울인다 (°)")]
        [Range(-90f, 90f)] public float lowRoll;
        [Tooltip("총 방향이 상체 기울기를 따라가는 정도 — 1 = 가슴에 붙은 듯 같이 숙임(뛸 때 총이 배에 파묻히지 않음), 0 = 몸 방향 그대로")]
        [Range(0f, 1f)] public float lowFollowChest = 1f;

        [Header("팔꿈치 · 손목")]
        [Tooltip("팔꿈치를 손등 줄(손목에서 아래팔 길이만큼 뒤)에서 더 옮긴다 (cm, 몸 기준) — 오른팔")]
        public Vector3 rightElbowBias = new Vector3(12f, -25f, 0f);
        [Tooltip("왼팔 팔꿈치")]
        public Vector3 leftElbowBias = new Vector3(-6f, -25f, 0f);
        [Tooltip("손목 비틀림 가운데 아래팔이 나눠 맡는 몫 — 손목 한 곳에서 다 비틀면 피부가 꼬여 늘어나 보인다")]
        [Range(0f, 1f)] public float twistToForearm = 0.5f;

        [Header("고급 — 치수 (cm). 모델을 바꿀 때만")]
        [Tooltip("K2C1 권총손잡이 가운데 (총 기준) — 메시에서 잼: y −6 ~ +3.5, 폭 4, 앞뒤 3~5")]
        public Vector3 gripCenter = new Vector3(0f, -1.2f, 0.2f);
        [Tooltip("권총손잡이 축 (아래 → 위, 총 기준 방향)")]
        public Vector3 gripUp = new Vector3(0f, 0.994f, 0.11f);
        [Tooltip("총열덮개 가운데 줄 · 앞손 자리 (총 기준) — y 7 ~ 13.2, 폭 5.4")]
        public Vector3 handguardCenter = new Vector3(0f, 10.1f, 33f);
        [Tooltip("가늠자 (총 기준, A 안내 문서의 Anchor_RearSight)")]
        public Vector3 rearSight = new Vector3(0f, 15.5f, 2f);
        [Tooltip("오른손이 쥔 것의 가운데 (손 뼈 기준: Y 손가락 · Z 손바닥 쪽 · 엄지 쪽 −X) — 손가락 뿌리 10.8, 손바닥 겉면 3.3")]
        public Vector3 rightFistCenter = new Vector3(-0.4f, 7.5f, 5.3f);
        [Tooltip("왼손이 받친 것의 가운데 (손 뼈 기준: Y 손가락 · Z 손바닥 쪽 · 엄지 쪽 +X)")]
        public Vector3 leftCupCenter = new Vector3(0f, 6.6f, 6.4f);
        [Tooltip("오른눈 (T 자세 머리 뼈에서, 몸 기준) — 바꾸면 '적 병사 만들기'를 다시 눌러야 한다")]
        public Vector3 rightEyeFromHead = new Vector3(3.2f, 9f, 8.5f);

        // 값이 바뀔 때마다 하나씩 는다 — EnemySoldier 가 보고 다시 계산한다 (에디터에서 Play 중 조정)
        public int Version { get; private set; }

        void OnValidate()
        {
            Version++;
        }

        // 두 손 뼈(손목)의 자리 · 방향 (총 기준). knuckle = 그 손 가운뎃손가락 뿌리 뼈의 localPosition (손 뼈 기준)
        public void HandPoses(Vector3 rightKnuckle, Vector3 leftKnuckle, out Vector3 rightPosition, out Quaternion rightRotation, out Vector3 leftPosition, out Quaternion leftRotation)
        {
            // 오른손: 손바닥 쪽 = 손잡이 축에 수직, 엄지 쪽 = 손잡이 위
            Vector3 up = gripUp.normalized;
            Vector3 palm = Vector3.ProjectOnPlane(Quaternion.AngleAxis(-rightPalmTurn, up) * Vector3.left, up).normalized;
            Quaternion right = Quaternion.AngleAxis(rightTilt, palm) * Quaternion.LookRotation(palm, -Vector3.Cross(palm, up));
            rightRotation = right * Quaternion.Inverse(HandFrame(rightKnuckle));
            rightPosition = (gripCenter - rightRotation * rightFistCenter + rightShift) * 0.01f;
            // 왼손: 손바닥 쪽 = 위, 손가락 쪽 = 오른쪽에서 앞으로 leftFingerAngle
            Vector3 fingers = Quaternion.AngleAxis(-leftFingerAngle, Vector3.up) * Vector3.right;
            Quaternion left = Quaternion.AngleAxis(leftTilt, fingers) * Quaternion.LookRotation(Vector3.up, fingers);
            leftRotation = left * Quaternion.Inverse(HandFrame(leftKnuckle));
            leftPosition = (handguardCenter - leftRotation * leftCupCenter + leftShift) * 0.01f;
        }

        // 손 뼈 기준 손바닥 · 손가락 틀 — LookRotation(손바닥 쪽 +Z, 손가락 쪽 = 손목 → 가운뎃손가락 뿌리)
        static Quaternion HandFrame(Vector3 knuckle)
        {
            Vector3 fingers = knuckle.normalized;
            return Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, fingers).normalized, fingers);
        }

        // 한 손 손가락의 부모 기준 회전. bones · rest = EnemySoldier.Fingers 순서로 그 손 15칸(엄지 · 집게 · 가운뎃 · 약지 · 새끼, 뿌리 → 끝)이
        // offset 부터 들어 있다. 손 뼈 기준으로 T 자세 마디를 차례로 굽힌다: 손가락은 손 X 축 둘레로 손바닥(+Z) 쪽,
        // 엄지 뿌리는 손 길이 축(+Y) 둘레로 손바닥 앞으로, 엄지 가운데 · 끝은 손바닥 쪽으로. 집게 첫 마디(이 모델은 손바닥 뿌리)는 굽히지 않는다.
        // 약지 · 새끼는 뼈가 없으면 그대로 둔다
        public void FingerRotations(bool rightHand, Transform[] bones, Quaternion[] rest, int offset, Quaternion[] result)
        {
            FingerCurl c = rightHand ? rightCurl : leftCurl;
            Chain(bones, rest, offset + 0, result, true, c.thumbIn, c.thumbMid, c.thumbTip);
            Chain(bones, rest, offset + 3, result, false, 0f, c.indexBase, c.indexMid);
            Chain(bones, rest, offset + 6, result, false, c.middleBase, c.middleMid, c.middleTip);
            for (int i = offset + 9; i < offset + 15; i++)
                result[i] = rest[i];
        }

        static void Chain(Transform[] bones, Quaternion[] rest, int start, Quaternion[] result, bool thumb, float bend0, float bend1, float bend2)
        {
            float[] bend = { bend0, bend1, bend2 };
            Quaternion parentRotation = Quaternion.identity;   // 손 뼈 기준
            for (int k = 0; k < 3; k++)
            {
                Transform joint = bones[start + k];
                if (joint == null)
                {
                    result[start + k] = rest[start + k];
                    continue;
                }
                Quaternion rotation = parentRotation * rest[start + k];
                Transform child = k < 2 ? bones[start + k + 1] : (joint.childCount > 0 ? joint.GetChild(0) : null);
                if (child != null && bend[k] != 0f)
                {
                    Vector3 dir = rotation * child.localPosition;
                    Vector3 axis = thumb ? (k == 0 ? Vector3.up : Vector3.Cross(dir, Vector3.forward)) : Vector3.right;
                    if (axis.sqrMagnitude < 1e-8f)
                        axis = Vector3.right;
                    rotation = Quaternion.AngleAxis(bend[k], SignToward(axis.normalized, dir, Vector3.forward)) * rotation;
                }
                result[start + k] = Quaternion.Inverse(parentRotation) * rotation;
                parentRotation = rotation;
            }
        }

        // axis 둘레로 조금 돌렸을 때 dir 이 toward 쪽으로 가면 axis, 아니면 −axis
        static Vector3 SignToward(Vector3 axis, Vector3 dir, Vector3 toward)
        {
            Vector3 turned = Quaternion.AngleAxis(5f, axis) * dir;
            return Vector3.Dot(turned - dir, toward) >= 0f ? axis : -axis;
        }

        // 조준 자세의 총 (가슴 뼈 기준) — eye · basis = 조준 애니메이션 첫 자세의 오른눈 자리 · 몸 방향 (가슴 뼈 기준)
        public void AimRifle(Vector3 eye, Quaternion basis, out Vector3 position, out Quaternion rotation)
        {
            rotation = basis * Quaternion.Euler(aimTilt);
            position = eye + basis * ((Vector3.forward * eyeRelief + aimShift) * 0.01f) - rotation * (rearSight * 0.01f);
        }

        // 낮춘 자세의 총 — 자리는 가슴 뼈 기준, 방향은 몸 기준. chestPosition · chestRotation = 대기 애니메이션 첫 자세의 가슴 뼈 (몸 기준)
        public void LowRifle(Vector3 chestPosition, Quaternion chestRotation, out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(-lowPitch, lowYaw, 0f) * Quaternion.Euler(0f, 0f, lowRoll);
            position = Quaternion.Inverse(chestRotation) * ((lowGrip - rotation * gripCenter) * 0.01f - chestPosition);
        }
    }
}
