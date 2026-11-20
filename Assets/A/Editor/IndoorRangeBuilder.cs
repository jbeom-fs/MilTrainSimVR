using MilTrainSimVR.A.Rifle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.A.EditorTools
{
    // 사로를 실내 사격장 부스로 짓는다 (10/29 — 사용자 참고 사진: 유리 칸막이 부스 + 서서 쏘는 철망 테이블).
    //   · 내무반과 떨어진 따로 된 방 (사용자 10/29: 같은 씬, 맵만 분리 — 오가는 것은 문 전환(순간이동)으로). Room_Range 를 k_RoomPosition 에 두고
    //     사격장 점(Points/Point_FiringPosition 등 k_RangePoints)도 같이 옮긴다. 사로 안의 값은 모두 Room_Range 기준이다.
    //   · 부스 7개(폭 2.2 m)를 사선에 나란히. 방(바닥·벽·방호둑·천장)은 부스 폭에 맞춘다. 칸막이 = 짙은 기둥 + 유리(높이 2.4 m), 부스 바닥 = 인조잔디, 위 = 가로보·번호판·상황판
    //   · 테이블 = 철망 상자 + 인조잔디 윗면. 윗면 1.0 m · 앞끝 z 4.25 — 총·탄창 자리, 손 닿는 거리가 그대로다
    //   · 교육은 가운데(4번) 부스 하나 — 사격용 총·사로 패널·영점 표적지가 있는 Lane 을 그 자리에 둔다. 나머지 부스·표적은 보기용
    // 기능이 참조하는 것(총, RangePanel, 영점 표적 Target_Lane*, Points/Point_FiringPosition)은 지우지 않고 옮기기만 한다.
    // 모양은 Room_Range/Booths 아래에 새로 짓는다 — 값을 바꾸고 메뉴를 다시 누르면 Booths 를 지우고 다시 짓는다.
    static class IndoorRangeBuilder
    {
        const string k_Room = "Room_Range";
        const string k_MaterialFolder = "Assets/A/Materials/Range";

        const int k_LaneCount = 7;
        const int k_TrainingLane = 4;            // 1부터 — 가운데
        const float k_LaneWidth = 2.2f;            // 10/29 1.4 → 1.8 → 2.2 (사용자)

        // 부스 앞뒤 (z) — 사선 z 3.4 에 서서 앞 테이블에 총을 둔다
        const float k_BoothBack = 2.6f;
        const float k_BoothFront = 5.1f;          // 테이블 뒤끝(5.05)보다 조금 뒤
        const float k_PartitionHeight = 2.4f;
        const float k_PostSize = 0.08f;
        const float k_GlassThickness = 0.02f;

        // 테이블 (사진 4) — 앞끝·윗면 높이는 기능 값이라 바꾸지 않는다
        const float k_TableFront = 4.25f;
        const float k_TableDepth = 0.8f;          // 10/29 0.6 → 0.8, 1.0 → 1.4 (사용자: 더 크게)
        const float k_TableWidth = 1.4f;
        const float k_TableTop = 1.0f;
        const float k_TurfThickness = 0.03f;

        const float k_TargetZ = 29f;             // 25 m 표적 (기존 그대로)

        // 사로 패널 (Lane 기준) — 11/03 헤드셋 사용자: 정면 위(가운데 높이 2.05)는 너무 높고 표적 쪽을 가린다 →
        // 부스 오른쪽, 눈높이보다 조금 아래, 사수(사선 z 3.4)를 보게 돌린다. 패널 폭 0.9 m — 왼쪽 끝이 x 0.2 라 쏘는 줄(x 0)을 가리지 않는다
        static readonly Vector3 k_RangePanelPosition = new Vector3(0.62f, 1.45f, 4.55f);
        static readonly Vector3 k_RangePanelRotation = new Vector3(8f, 27f, 0f);

        // 표적 이송 레일 (10/29, 사용자가 Test 큐브로 잡은 자리를 다듬음) — 부스 앞 천장 덮개(Header) 앞에서 방호둑 앞까지, 사로마다 하나.
        // 표적지가 이 레일을 타고 사수 쪽으로 왔다가 바꾼 뒤 다시 멀리 간다 (움직임 = TargetCarrier, 11/02)
        const float k_HeaderBottom = 3.4f;       // 부스 위 천장 덮개 아래면
        const float k_HeaderBack = 2.45f;
        const float k_HeaderFront = 5.6f;
        const float k_RailEndZ = 30.8f;          // 방호둑(z 31) 바로 앞
        const float k_RailY = 4.45f;             // 레일 가운데 높이 (천장 4.67 아래)
        // 레일 단면 = I 형강 하나 (사용자 10/29 그림): 위·아래 날개 + 가운데 기둥
        const float k_BeamWidth = 0.16f;
        const float k_BeamHeight = 0.16f;
        const float k_FlangeThickness = 0.035f;
        const float k_WebThickness = 0.05f;
        const float k_HangerSpacing = 4f;        // 천장에 매다는 걸이 간격

        // 레일에 매단 표적 (11/02, 임시 상자) — 이동대가 레일 아래 날개를 감싸고, 긴 막대로 표적판 위를 잡는다. 표적판·표적지 높이(1.0 m)는 그대로
        const string k_Carrier = "Carrier";
        const float k_BoardTop = 1.4f;           // 표적판(가운데 1.0, 높이 0.8) 윗면
        const float k_TrolleyHeight = 0.1f;
        static readonly Vector3 k_TrolleySize = new Vector3(0.22f, k_TrolleyHeight, 0.3f);
        const float k_RodWidth = 0.2f;           // 11/02 0.03 → 0.2 (사용자)
        const float k_RodDepth = 0.03f;
        const float k_RodZ = 0.03f;              // 표적판 뒤 (예전 바닥 기둥 자리)

        // 방 자리 — 내무반(x ±5.2, z -9.6 ~ 0)과 떨어지게. 사격장에 속한 점들을 같이 옮긴다
        static readonly Vector3 k_RoomPosition = new Vector3(30f, 0f, 0f);
        static readonly string[] k_RangePoints = { "Point_FiringPosition", "Point_RangeRifleRest", "Point_RangeMagazineRest", "Point_RangeEntry" };
        const string k_Points = "Points";

        // 방 — 부스 양 끝에서 벽 안쪽 면까지 여유, 벽 두께
        const float k_WallGap = 0.05f;
        const float k_WallThickness = 0.2f;
        const float k_WallHeight = 4.67f;        // 10/29 사용자가 Wall_West 를 올려 둔 높이 (윗면)
        const float k_CeilingThickness = 0.1f;
        const float k_RoomBack = -3.4f;          // 사선 뒤 벽 안쪽 면 — 부스 뒤 대기 공간 2.6 → 6 m (10/30 사용자)
        const float k_RoomLength = 32f;          // 바닥 z k_RoomBack ~ 32
        // 형광등 (11/02 — 내무반 형광등 CeilingPanel.fbx 를 사격장에도. 10/29 임시 점조명 격자를 대신한다).
        // 형광등마다 아래로 넓게 비추는 스포트 하나 (내무반 CeilingBuilder 와 같은 방식). 그림자 없음. PC 렌더러가 Forward+ 라 물체당 조명 수 제한(4)에 걸리지 않는다
        const string k_PanelModel = "Assets/A/Models/CeilingPanel/CeilingPanel.fbx";
        static readonly Quaternion k_FiringPanelRotation = Quaternion.Euler(270f, 180f, 0f);   // 내무반(270, 90, 0)에서 90° 돌려 긴 변이 가로(x) (11/02 사용자)
        static readonly float[] k_FiringPanelX = { -0.375f, -0.125f, 0.125f, 0.375f };    // 사수 쪽 천장: 방 안쪽 폭에 대한 비율 (4열)
        static readonly float[] k_FiringPanelRows = { 0.25f, 0.75f };                     // 사수 쪽 천장 길이(뒷벽 ~ 덮개)에 대한 비율 (2줄)
        // 사수 쪽 형광등 조명 (11/02 사용자: 내무반 방식대로) — 형광등마다 아래로 넓게 비추는 스포트 + 줄마다 가운데 채움 점조명 (CeilingBuilder.BuildLights 와 같은 값)
        const float k_PanelLightBelow = 0.04f;   // 발광면(0.032) 바로 아래
        // 내무반(천장 2.644 m)과 바닥 밝기가 같도록 천장 높이(4.67 m) 차이를 맞춘다 — 세기 × (4.67/2.644)² ≈ 3.1, 거리 × 1.77 (11/02 사용자: 내무반과 똑같이)
        const float k_PanelLightRange = 14f;
        const float k_PanelLightIntensity = 7.8f;
        static readonly Color k_PanelLightColor = new Color(1f, 0.97f, 0.92f);   // 내무반 조명 색 (표적 조명은 k_LightColor 노란빛 그대로)
        const float k_PanelLightOuterAngle = 160f;
        const float k_PanelLightInnerAngle = 100f;
        const float k_GlowBelow = 0.3f;          // 천장 비춤 점조명 — 형광등 아래 (11/03)
        const float k_GlowRange = 3.5f;
        const float k_GlowIntensity = 2f;
        const float k_FillY = 1.3f;
        const float k_FillRange = 7f;
        const float k_FillIntensity = 0.5f;

        // 11/02 사용자: 표적 쪽 천장 형광등은 없애고, 표적지에는 좁은 스포트.
        // 표적마다 좁은 스포트 하나 — 레일 바로 아래에서 **수직으로** 내리쬔다 (사용자: 좁고 선명하게 → 수직으로, 11/02)
        const float k_TargetLightZ = 28.6f;      // 표적(z 29) 0.4 m 앞 — 26 → 23 → 29 → 31 → 28 → 28.6 (사용자). 바로 위(29)면 앞면에 빛이 안 닿아 조금 앞에 둔다
        const float k_TargetLightY = 4.3f;       // 레일 아래면(4.37) 바로 아래
        const float k_TargetLightRange = 10f;
        const float k_TargetLightOuterAngle = 24f;   // 표적판 높이(1.0)에서 지름 약 1.4 m — 가장자리가 표적판 앞면을 스친다
        const float k_TargetLightInnerAngle = 16f;   // 안쪽과 바깥 각도 차이가 작을수록 가장자리가 또렷하다
        const float k_TargetShaftAngle = 14f;        // 보이는 빛 기둥은 조명보다 좁게 — 바닥 쪽 반지름 약 0.58 m (11/02 사용자: 덜 퍼지게)
        const float k_TargetLightIntensity = 25f;  // 11/02 6 → 25 (사용자: 더 강하게)
        static readonly Color k_LightColor = new Color(1f, 0.9f, 0.75f);   // 11/02 조금 노랗게 (사용자)

        [MenuItem("A/만들기/실내 사격장 (부스 7개)")]
        static void Build()
        {
            var room = GameObject.Find(k_Room);
            if (room == null)
            {
                Debug.LogError($"[A] {k_Room} 이 없다");
                return;
            }
            Transform lane = FindChild(room.transform, "Lane4", "Lane2");
            Transform targets = room.transform.Find("Targets_25m");
            Transform target = targets != null ? FindChild(targets, "Target_Lane4", "Target_Lane2") : null;
            if (lane == null || target == null)
            {
                Debug.LogError("[A] 교육 사로(Lane4 / Lane2) 또는 영점 표적(Target_Lane4 / Target_Lane2)이 없다");
                return;
            }

            MoveRoom(room.transform);
            var mats = new Materials();
            Undo.SetCurrentGroupName("A: 실내 사격장");
            int group = Undo.GetCurrentGroup();

            // 1) 예전 화이트박스 (사로 칸막이·거치대·보기용 표적) — 기능 참조가 없는 것만 지운다
            DestroyIfExists(room.transform.Find("Booths"));
            DestroyIfExists(room.transform.Find("FiringLine"));
            DestroyIfExists(room.transform.Find("Lane1"));
            DestroyIfExists(room.transform.Find("Lane3"));
            DestroyIfExists(lane.Find("Barrier"));
            foreach (Transform t in targets.GetComponentsInChildren<Transform>(true))
                if (t != null && t.parent == targets && t != target && t.name.StartsWith("Target_Lane"))
                    DestroyIfExists(t);

            // 2) 교육 사로 이름을 부스 번호에 맞춘다 (참조는 오브젝트라 이름은 바꿔도 된다)
            Undo.RecordObject(lane.gameObject, "A: 실내 사격장");
            lane.name = $"Lane{k_TrainingLane}";
            Undo.RecordObject(target.gameObject, "A: 실내 사격장");
            target.name = $"Target_Lane{k_TrainingLane}";

            // 3) 부스
            var booths = new GameObject("Booths").transform;
            Undo.RegisterCreatedObjectUndo(booths.gameObject, "A: 실내 사격장");
            booths.SetParent(room.transform, false);

            float span = k_LaneCount * k_LaneWidth;
            float left = -span * 0.5f;
            float depth = k_BoothFront - k_BoothBack;
            float midZ = (k_BoothBack + k_BoothFront) * 0.5f;

            // 방을 부스 폭에 맞춘다
            FitShell(room.transform.Find("Shell"), targets, span * 0.5f + k_WallGap);

            // 부스 바닥 인조잔디 — 한 장으로
            Box("Turf_Floor", booths, new Vector3(0f, 0.01f, midZ), new Vector3(span, 0.02f, depth), mats.Turf, false);

            // 칸막이 (부스 사이 + 양 끝)
            for (int i = 0; i <= k_LaneCount; i++)
            {
                float x = left + i * k_LaneWidth;
                var partition = Group($"Partition_{i}", booths, new Vector3(x, 0f, 0f));
                Box("Post_Front", partition, new Vector3(0f, k_PartitionHeight * 0.5f, k_BoothFront - k_PostSize * 0.5f),
                    new Vector3(k_PostSize, k_PartitionHeight, k_PostSize), mats.Frame, true);
                Box("Post_Back", partition, new Vector3(0f, k_PartitionHeight * 0.5f, k_BoothBack + k_PostSize * 0.5f),
                    new Vector3(k_PostSize, k_PartitionHeight, k_PostSize), mats.Frame, true);
                Box("Glass", partition, new Vector3(0f, k_PartitionHeight * 0.5f + 0.05f, midZ),
                    new Vector3(k_GlassThickness, k_PartitionHeight - 0.1f, depth - k_PostSize * 2f), mats.Glass, true);
                Box("Rail_Bottom", partition, new Vector3(0f, 0.04f, midZ),
                    new Vector3(0.04f, 0.08f, depth - k_PostSize * 2f), mats.Frame, false);
            }

            // 앞 가로보 (칸막이 꼭대기를 잇는다)
            Box("Beam_Front", booths, new Vector3(0f, k_PartitionHeight + 0.06f, k_BoothFront - k_PostSize * 0.5f),
                new Vector3(span + k_PostSize, 0.12f, k_PostSize), mats.Frame, false);

            for (int n = 1; n <= k_LaneCount; n++)
            {
                float x = left + (n - 0.5f) * k_LaneWidth;
                var booth = Group($"Booth_{n}", booths, new Vector3(x, 0f, 0f));

                // 테이블 — 철망 상자 + 잔디 윗면
                float tableZ = k_TableFront + k_TableDepth * 0.5f;
                float bodyHeight = k_TableTop - k_TurfThickness;
                Box("Table_Body", booth, new Vector3(0f, bodyHeight * 0.5f, tableZ),
                    new Vector3(k_TableWidth, bodyHeight, k_TableDepth), mats.Mesh, false);
                Box("Table_Top", booth, new Vector3(0f, k_TableTop - k_TurfThickness * 0.5f, tableZ),
                    new Vector3(k_TableWidth + 0.02f, k_TurfThickness, k_TableDepth + 0.02f), mats.Turf, false);
                // 부딪힘은 BuildTables 가 모델 모양대로 단다 (11/03 — 선반에 탄창을 둔다)

                // 번호판 (둥근 판) — 부스 왼쪽 유리 칸막이 안쪽 면, 뒤쪽에 붙여 부스 안(+x)을 본다 (11/03 사용자가 1번을 옮긴 자리). 교육 부스가 아니면 상황판도 단다
                PlaceSign(booth, n, mats);
                if (n != k_TrainingLane)
                    Box("Monitor", booth, new Vector3(0f, 2.05f, k_BoothFront - 0.1f), new Vector3(0.5f, 0.3f, 0.1f), mats.Frame, false);

                // 보기용 표적
                if (n != k_TrainingLane)
                    DecoTarget(booths, target, $"Target_Lane{n}", new Vector3(x, 0f, k_TargetZ));
            }

            BuildRails(booths, mats, span * 0.5f + k_WallGap, left);

            // 4) 교육 사로를 4번 부스 자리로 (지금은 x 0 = 그대로). 사로 패널은 칸막이 안쪽에 들어오도록 부스 위 상황판 자리로
            Undo.RecordObject(lane, "A: 실내 사격장");
            lane.localPosition = new Vector3(left + (k_TrainingLane - 0.5f) * k_LaneWidth, 0f, 0f);
            Undo.RecordObject(target, "A: 실내 사격장");
            target.localPosition = new Vector3(lane.localPosition.x, 0f, k_TargetZ);
            Transform panel = lane.Find("RangePanel");
            if (panel != null)
            {
                Undo.RecordObject(panel, "A: 실내 사격장");
                // 11/19: 사로 패널은 오른쪽 칸막이 모니터 화면 안 (RangeMonitorBuilder). 모니터가 없으면 예전 자리
                bool onMonitor = lane.Find("Monitor_RangePanel") != null;
                panel.localPosition = onMonitor ? RangeMonitorBuilder.PanelLocalPosition : k_RangePanelPosition;
                panel.localRotation = Quaternion.Euler(onMonitor ? RangeMonitorBuilder.PanelLocalRotation : k_RangePanelRotation);
            }

            HangTargets(room.transform, mats);
            CeilingBuilder.BuildRange();   // 사수 쪽 천장만 내무반과 같은 텍스·줄눈 (11/02)
            Walls(room.transform);   // 벽·천장 덮개 = 내무반 벽 (11/02)
            BuildPanels(room.transform);   // 형광등 + 조명 (11/02)
            BuildFloor(room.transform);    // 바닥 Floor_2 텍스처 (11/02)
            BuildTurf(room.transform);     // 부스 바닥·받침대 윗면 잔디 (11/02)
            BuildSteel(room.transform);    // 레일·걸이·표적 이동대 철골 텍스처 (11/02)
            BuildTables(room.transform);   // 받침대 몸통 = 사용자 모델 Table_2 (11/03)
            BuildSunBlocker(room.transform);   // 바깥 햇빛 막이 (11/03)

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[A] 실내 사격장: 부스 {k_LaneCount}개 × {k_LaneWidth} m, 교육 부스 {k_TrainingLane}번 (x {lane.localPosition.x:0.##}), 칸막이 {k_PartitionHeight} m");
        }

        // 바닥·양 벽·방호둑·25 m 줄을 방 안쪽 반폭(innerHalf)에 맞추고, 조립실 벽보다 넓은 만큼 z 0 에 벽을 채운다
        static void FitShell(Transform shell, Transform targets, float innerHalf)
        {
            if (shell == null)
                return;
            float inner = innerHalf * 2f;
            SetWidth(shell.Find("Floor"), inner, null);
            SetWidth(shell.Find("Backstop_Berm"), inner, null);
            SetWidth(shell.Find("Wall_West"), null, -(innerHalf + k_WallThickness * 0.5f));
            SetWidth(shell.Find("Wall_East"), null, innerHalf + k_WallThickness * 0.5f);
            SetHeight(shell.Find("Wall_West"), k_WallHeight);
            SetHeight(shell.Find("Wall_East"), k_WallHeight);
            SetWidth(targets.Find("Marker_25m_Stripe"), inner - 1f, null);
            SetLength(shell.Find("Floor"));
            SetLength(shell.Find("Wall_West"));
            SetLength(shell.Find("Wall_East"));

            foreach (string n in new[] { "Wall_South", "Wall_South_L", "Wall_South_R", "Wall_South_Upper", "Wall_South_DoorCover", "Wall_North" })
                DestroyIfExists(shell.Find(n));
            float outer = innerHalf + k_WallThickness;
            var material = shell.Find("Wall_West")?.GetComponent<MeshRenderer>()?.sharedMaterial;

            // 사선 뒤 벽 (z k_RoomBack) — 따로 된 방이라 통째로 막는다. 사격장 쪽 문은 나중에 따로 디자인한다
            Made(Box("Wall_South", shell, new Vector3(0f, k_WallHeight * 0.5f, k_RoomBack - k_WallThickness * 0.5f),
                new Vector3(outer * 2f, k_WallHeight, k_WallThickness), material, true));

            // 끝벽 (방호둑 뒤) · 천장
            Made(Box("Wall_North", shell, new Vector3(0f, k_WallHeight * 0.5f, k_RoomLength + k_WallThickness * 0.5f),
                new Vector3(outer * 2f, k_WallHeight, k_WallThickness), material, true));
            BuildCeilings(shell);
        }

        // 천장 둘 (11/02 사용자) — 사수 쪽(뒷벽 ~ 부스 위 천장 덮개 뒤끝)은 내무반과 같은 텍스(CeilingBuilder.BuildRange 가 재질·줄눈을 입힌다),
        // 그 앞(표적 쪽)은 따로 쓸 재질 Range_Ceiling. 경계는 천장 덮개(Header)에 가려 보이지 않는다
        internal const string k_FiringCeiling = "Ceiling_Firing";

        internal static void BuildCeilings(Transform shell)
        {
            DestroyIfExists(shell.Find("Ceiling"));
            DestroyIfExists(shell.Find(k_FiringCeiling));
            float outer = k_LaneCount * k_LaneWidth * 0.5f + k_WallGap + k_WallThickness;
            float y = k_WallHeight + k_CeilingThickness * 0.5f;
            float back = k_RoomBack - k_WallThickness;
            float front = k_RoomLength + k_WallThickness;
            var plain = Load("Range_Ceiling", new Color(0.82f, 0.82f, 0.8f), 0.1f, false);
            Made(Box(k_FiringCeiling, shell, new Vector3(0f, y, (back + k_HeaderBack) * 0.5f),
                new Vector3(outer * 2f, k_CeilingThickness, k_HeaderBack - back), plain, true));
            Made(Box("Ceiling", shell, new Vector3(0f, y, (k_HeaderBack + front) * 0.5f),
                new Vector3(outer * 2f, k_CeilingThickness, front - k_HeaderBack), plain, true));
        }

        // 바닥 (11/02 사용자: Floor_2 텍스처) — 이음새 없는 에폭시 무늬 한 장을 k_FloorTileMeters 마다 반복. 바닥은 늘린 Cube 라 재질 반복 수를 바닥 크기에 맞춘다
        const string k_FloorFolder = "Assets/A/Models/Floor_2";
        const string k_FloorTexture = k_FloorFolder + "/Tile_DefaultMaterial_";
        const string k_FloorMaterialPath = k_FloorFolder + "/Materials/Range_Floor.mat";
        const float k_FloorTileMeters = 2f;

        [MenuItem("A/만들기/사격장 바닥 (Floor_2)")]
        static void FloorMenu()
        {
            var room = GameObject.Find(k_Room);
            if (room == null)
            {
                Debug.LogError($"[A] {k_Room} 이 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 사격장 바닥");
            int group = Undo.GetCurrentGroup();
            BuildFloor(room.transform);
            Undo.CollapseUndoOperations(group);
        }

        // 철골 (11/02 사용자: Models/Steel 텍스처) — 재질 Range_Steel 을 쓰는 것 모두 (표적 이송 레일 I 형강·천장 걸이·표적 이동대·막대).
        // 길이가 제각각(수십 cm ~ 25 m)이라 m 단위 UV 메시(WallUvBuilder)로 무늬 크기를 같게 한다
        const string k_SteelTexture = "Assets/A/Models/Steel/Tile_DefaultMaterial_";
        const float k_SteelTileMeters = 1f;

        [MenuItem("A/만들기/사격장 철골 (Steel)")]
        static void SteelMenu()
        {
            var room = GameObject.Find(k_Room);
            if (room == null)
            {
                Debug.LogError($"[A] {k_Room} 이 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 사격장 철골");
            int group = Undo.GetCurrentGroup();
            BuildSteel(room.transform);
            Undo.CollapseUndoOperations(group);
        }

        static void BuildSteel(Transform room)
        {
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(k_SteelTexture + "AlbedoTransparency.png");
            if (albedo == null)
            {
                Debug.LogError($"[A] {k_SteelTexture}AlbedoTransparency.png 가 없다");
                return;
            }
            SetImport(k_SteelTexture + "Normal.png", TextureImporterType.NormalMap, false);
            SetImport(k_SteelTexture + "MetallicSmoothness.png", TextureImporterType.Default, false);

            var material = new Materials().Steel;
            Undo.RecordObject(material, "A: 사격장 철골");
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(k_SteelTexture + "Normal.png"));
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(k_SteelTexture + "MetallicSmoothness.png"));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Smoothness", 1f);   // 맵의 알파에 곱해진다
            foreach (string prop in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap" })
                material.SetTextureScale(prop, Vector2.one);   // 반복은 m 단위 UV 가 한다
            EditorUtility.SetDirty(material);

            int count = 0;
            foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.sharedMaterial == material)
                {
                    WallUvBuilder.Apply(renderer.transform, material, k_SteelTileMeters);
                    count++;
                }
            AssetDatabase.SaveAssets();
            Debug.Log($"[A] 사격장 철골: {count}개, 한 장 {k_SteelTileMeters} m");
        }

        // 잔디 (11/02 사용자: GrassTextures.jpg) — 부스 바닥(Booths/Turf_Floor)과 받침대 윗면(Booths/Booth_*/Table_Top). 재질 Range_Turf 하나에
        // 반복용 사본(FloorTextureBuilder.BuildGrass)을 넣고, 두 곳 크기가 달라 m 단위 UV 메시(WallUvBuilder)로 잔디 크기를 같게 한다
        const float k_TurfTileMeters = 1.5f;   // 텍스처 가로 한 장 = 1.5 m (세로는 비율대로 1 m)

        [MenuItem("A/만들기/사격장 잔디 (GrassTextures)")]
        static void TurfMenu()
        {
            var room = GameObject.Find(k_Room);
            if (room == null)
            {
                Debug.LogError($"[A] {k_Room} 이 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 사격장 잔디");
            int group = Undo.GetCurrentGroup();
            BuildTurf(room.transform);
            Undo.CollapseUndoOperations(group);
        }

        static void BuildTurf(Transform room)
        {
            Transform booths = room.Find("Booths");
            if (booths == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTextureBuilder.k_GrassPath) == null)
                FloorTextureBuilder.BuildGrass();
            var grass = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTextureBuilder.k_GrassPath);
            if (grass == null)
                return;
            var material = new Materials().Turf;
            Undo.RecordObject(material, "A: 사격장 잔디");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.05f);
            Vector2 scale = new Vector2(1f, grass.width / (float)grass.height);   // UV 1 = 1.5 m → 세로는 비율대로
            foreach (string prop in new[] { "_BaseMap", "_MainTex" })
            {
                material.SetTexture(prop, grass);
                material.SetTextureScale(prop, scale);
            }
            EditorUtility.SetDirty(material);

            int count = 0;
            WallUvBuilder.Apply(booths.Find("Turf_Floor"), material, k_TurfTileMeters);
            foreach (Transform booth in booths)
                if (booth.name.StartsWith("Booth_"))
                {
                    WallUvBuilder.Apply(booth.Find("Table_Top"), material, k_TurfTileMeters);
                    count++;
                }
            AssetDatabase.SaveAssets();
            Debug.Log($"[A] 사격장 잔디: 부스 바닥 + 받침대 윗면 {count}개, 한 장 {k_TurfTileMeters} m");
        }

        // 사수 쪽 바닥 (11/02 사용자) — 뒷벽 ~ 부스 뒤끝(k_BoothBack, 인조잔디가 시작해 경계가 덮인다)은 내무반 바닥(테라조 60 cm 타일)과 같게.
        // 바닥을 Floor_Firing(사수 쪽) + Floor(표적 쪽)로 나눈다. 타일 반복 수가 재질에 들어가므로 Floor.mat 을 복사한 사격장 전용 재질
        const string k_FiringFloor = "Floor_Firing";
        const string k_BarracksFloorMaterial = "Assets/A/Models/Floor/Materials/Floor.mat";
        const string k_FiringFloorMaterialPath = k_MaterialFolder + "/Range_FloorFiring.mat";
        const float k_BarracksTileMeters = 0.6f;   // 내무반 바닥 타일 한 장

        static void BuildFloor(Transform room)
        {
            Transform shell = room.Find("Shell");
            Transform floorT = shell != null ? shell.Find("Floor") : null;
            if (floorT != null)
            {
                // 방 길이만큼인 바닥을 표적 쪽만 남기고, 사수 쪽 조각을 복사해 만든다 — 다시 불러도 같다
                DestroyIfExists(shell.Find(k_FiringFloor));
                Undo.RecordObject(floorT, "A: 사격장 바닥");
                SetSpanZ(floorT, k_BoothBack, k_RoomLength);
                var firing = Object.Instantiate(floorT.gameObject, shell);
                firing.name = k_FiringFloor;
                firing.transform.SetSiblingIndex(floorT.GetSiblingIndex() + 1);
                Made(firing);
                SetSpanZ(firing.transform, k_RoomBack, k_BoothBack);
                FiringFloorMaterial(firing.GetComponent<MeshRenderer>());
            }
            var floor = floorT != null ? floorT.GetComponent<MeshRenderer>() : null;
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(k_FloorTexture + "AlbedoTransparency.png");
            if (floor == null || albedo == null)
            {
                Debug.LogError($"[A] {k_Room}/Shell/Floor 또는 {k_FloorTexture}AlbedoTransparency.png 가 없다");
                return;
            }
            // 노멀은 노멀 맵으로, 금속·매끈함은 색이 아닌 값이라 sRGB 끔
            SetImport(k_FloorTexture + "Normal.png", TextureImporterType.NormalMap, false);
            SetImport(k_FloorTexture + "MetallicSmoothness.png", TextureImporterType.Default, false);

            // 반복 무늬 줄이기 (11/02): 큰 얼룩을 평평하게 한 색 텍스처 + 바닥 전체에 한 번 펴는 큰 얼룩 맵 (FloorTextureBuilder 메뉴로 만든다).
            // 있으면 쓰고, 없으면 원본만 쓴다 (사용자가 얼룩 없는 텍스처로 다시 만들어 지금은 없음)
            var flat = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTextureBuilder.k_FlatPath);
            var variation = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTextureBuilder.k_VariationPath);
            if (flat != null)
                albedo = flat;

            var material = AssetDatabase.LoadAssetAtPath<Material>(k_FloorMaterialPath);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(k_FloorFolder + "/Materials"))
                    AssetDatabase.CreateFolder(k_FloorFolder, "Materials");
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Range_Floor" };
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(k_FloorTexture + "Normal.png"));
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(k_FloorTexture + "MetallicSmoothness.png"));
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Smoothness", 1f);   // 맵의 알파에 곱해진다
                AssetDatabase.CreateAsset(material, k_FloorMaterialPath);
            }
            Undo.RecordObject(material, "A: 사격장 바닥");
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            // 큰 얼룩 맵 = 디테일 색 (곱하기 ×2, 회색 128 = 그대로). 반복 1 × 1 — 바닥 전체에 한 장
            material.SetTexture("_DetailAlbedoMap", variation);
            material.SetTextureScale("_DetailAlbedoMap", Vector2.one);
            material.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
            material.SetFloat("_DetailAlbedoMapScale", 1f);
            if (variation != null)
                material.EnableKeyword("_DETAIL_MULX2");
            else
                material.DisableKeyword("_DETAIL_MULX2");
            // 반복 수 = 바닥 크기 ÷ 한 장 크기 (손으로 고친 재질도 반복 수만 다시 맞춘다)
            Vector3 size = floor.transform.lossyScale;
            Vector2 tiling = new Vector2(size.x / k_FloorTileMeters, size.z / k_FloorTileMeters);
            foreach (string prop in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap" })
                material.SetTextureScale(prop, tiling);
            EditorUtility.SetDirty(material);
            Undo.RecordObject(floor, "A: 사격장 바닥");
            floor.sharedMaterial = material;
            AssetDatabase.SaveAssets();
            Debug.Log($"[A] 사격장 바닥: Floor_2, 한 장 {k_FloorTileMeters} m, 반복 {tiling.x:0.##} × {tiling.y:0.##}");
        }

        // 사수 쪽 바닥 재질 — 내무반 Floor.mat 복사본에 이 바닥 크기만큼 60 cm 타일 반복. 줄눈이 방 가운데(x 0)와 뒷벽에서 시작
        static void FiringFloorMaterial(MeshRenderer renderer)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(k_BarracksFloorMaterial);
            if (source == null)
            {
                Debug.LogError($"[A] {k_BarracksFloorMaterial} 이 없다");
                return;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_FiringFloorMaterialPath);
            if (material == null)
            {
                material = new Material(source) { name = "Range_FloorFiring" };
                AssetDatabase.CreateAsset(material, k_FiringFloorMaterialPath);
            }
            Vector3 size = renderer.transform.lossyScale;
            Vector2 tiling = new Vector2(size.x / k_BarracksTileMeters, size.z / k_BarracksTileMeters);
            Vector2 offset = new Vector2(Frac(-tiling.x * 0.5f), 0f);
            Undo.RecordObject(material, "A: 사격장 바닥");
            foreach (string prop in new[] { "_BaseMap", "_MainTex", "_BumpMap" })
            {
                material.SetTextureScale(prop, tiling);
                material.SetTextureOffset(prop, offset);
            }
            EditorUtility.SetDirty(material);
            Undo.RecordObject(renderer, "A: 사격장 바닥");
            renderer.sharedMaterial = material;
            Debug.Log($"[A] 사격장 사수 쪽 바닥: 내무반 테라조 {k_BarracksTileMeters} m 타일, 반복 {tiling.x:0.##} × {tiling.y:0.##}");
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        static void SetImport(string path, TextureImporterType type, bool sRGB)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || (importer.textureType == type && importer.sRGBTexture == sRGB))
                return;
            importer.textureType = type;
            importer.sRGBTexture = sRGB;
            importer.SaveAndReimport();
        }

        [MenuItem("A/만들기/사격장 형광등")]
        static void PanelsMenu()
        {
            var room = GameObject.Find(k_Room);
            if (room == null)
            {
                Debug.LogError($"[A] {k_Room} 이 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 사격장 형광등");
            int group = Undo.GetCurrentGroup();
            BuildPanels(room.transform);
            Undo.CollapseUndoOperations(group);
        }

        // 형광등(CeilingPanels)과 조명(Lights_Temp)을 다시 짓는다
        static void BuildPanels(Transform room)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_PanelModel);
            if (model == null)
            {
                Debug.LogError($"[A] {k_PanelModel} 이 없다");
                return;
            }
            DestroyIfExists(room.Find("Lights_Temp"));
            DestroyIfExists(room.Find("CeilingPanels"));
            var panels = Group("CeilingPanels", room, Vector3.zero);
            var lights = Group("Lights_Temp", room, Vector3.zero);
            Made(panels.gameObject);
            Made(lights.gameObject);

            float inner = (k_LaneCount * k_LaneWidth * 0.5f + k_WallGap) * 2f;
            int count = 0;
            // 사수 쪽 천장 — 형광등 + 그 자리 스포트, 줄마다 채움 점조명 (내무반과 같은 방식)
            var down = Quaternion.Euler(90f, 0f, 0f);
            foreach (float row in k_FiringPanelRows)
            {
                float z = Mathf.Lerp(k_RoomBack, k_HeaderBack, row);
                foreach (float x in k_FiringPanelX)
                {
                    var at = new Vector3(x * inner, k_WallHeight, z);
                    Panel(model, panels, at, k_FiringPanelRotation, ref count);
                    var panelLight = Spot(lights, $"Light_{at.x:0.#}_{at.z:0.#}", at + Vector3.down * k_PanelLightBelow, down,
                        k_PanelLightOuterAngle, k_PanelLightInnerAngle, k_PanelLightRange, k_PanelLightIntensity);
                    panelLight.color = k_PanelLightColor;
                    // 천장 비춤 (11/03 사용자: 형광등 주변 천장이 까맣다) — 아래로만 비추는 스포트는 천장·벽 위쪽에 닿지 않는다.
                    // 형광등 바로 아래 약한 점조명으로 둘레 천장과 벽 위쪽을 밝힌다
                    var glow = new GameObject($"Glow_{at.x:0.#}_{at.z:0.#}");
                    glow.transform.SetParent(lights, false);
                    glow.transform.localPosition = at + Vector3.down * k_GlowBelow;
                    var glowLight = glow.AddComponent<Light>();
                    glowLight.type = LightType.Point;
                    glowLight.range = k_GlowRange;
                    glowLight.intensity = k_GlowIntensity;
                    glowLight.color = k_PanelLightColor;
                    glowLight.shadows = LightShadows.None;
                }
                var fill = new GameObject($"Fill_{z:0.#}");
                fill.transform.SetParent(lights, false);
                fill.transform.localPosition = new Vector3(0f, k_FillY, z);
                var fillLight = fill.AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.range = k_FillRange;
                fillLight.intensity = k_FillIntensity;
                fillLight.color = k_PanelLightColor;
                fillLight.shadows = LightShadows.None;
            }
            // 부스 위 천장 덮개 아래·표적 쪽 천장에는 형광등을 두지 않는다 (11/02 사용자)
            // 조명 — 표적마다 하나, 수직으로 (down 은 위에서 정함)
            float left = -k_LaneCount * k_LaneWidth * 0.5f;
            for (int n = 1; n <= k_LaneCount; n++)
            {
                float x = left + (n - 0.5f) * k_LaneWidth;
                var at = new Vector3(x, k_TargetLightY, k_TargetLightZ);
                var light = Spot(lights, $"Light_Target{n}", at, down, k_TargetLightOuterAngle, k_TargetLightInnerAngle, k_TargetLightRange, k_TargetLightIntensity);
                LightShaftBuilder.Attach(light, k_TargetLightRange, k_TargetShaftAngle);   // 보이는 빛 기둥 + 먼지 (11/02)
                // 11/03 사용자: 사수가 쏘는 사로(교육 부스) 표적만 켠다 — 나머지는 꺼 둔다 (조명·빛 기둥·먼지 모두, 켜려면 오브젝트를 켠다)
                light.gameObject.SetActive(n == k_TrainingLane);
            }
            Debug.Log($"[A] 사격장 형광등 {count}개 (사수 쪽, 스포트 {count} + 채움 {k_FiringPanelRows.Length}) + 천장 비춤 {count} + 표적 조명 {k_LaneCount}개 중 {k_TrainingLane}번만 켬 ({k_TargetLightOuterAngle}°, 세기 {k_TargetLightIntensity})");
        }

        // 형광등 하나 — at = 천장(또는 덮개) 아래면의 자리 (방 기준)
        static void Panel(GameObject model, Transform panels, Vector3 at, Quaternion rotation, ref int count)
        {
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(model, panels);
            panel.name = $"CeilingPanel_{count + 1}";
            panel.transform.localPosition = at;
            panel.transform.localRotation = rotation;
            count++;
        }

        // 스포트 하나 (방 기준 자리·방향). 그림자 없음
        static Light Spot(Transform lights, string name, Vector3 at, Quaternion rotation, float outer, float inner, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(lights, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = range;
            light.spotAngle = outer;
            light.innerSpotAngle = inner;
            light.intensity = intensity;
            light.color = k_LightColor;
            light.shadows = LightShadows.None;
            return light;
        }

        // 방을 k_RoomPosition 으로 — 사격장 점들도 같은 만큼 옮긴다 (이미 그 자리면 아무 일 없음)
        static void MoveRoom(Transform room)
        {
            Vector3 delta = k_RoomPosition - room.position;
            if (delta.sqrMagnitude < 1e-6f)
                return;
            Undo.RecordObject(room, "A: 실내 사격장");
            room.position = k_RoomPosition;
            var points = GameObject.Find(k_Points);
            if (points == null)
                return;
            foreach (string n in k_RangePoints)
            {
                Transform p = points.transform.Find(n);
                if (p == null)
                    continue;
                Undo.RecordObject(p, "A: 실내 사격장");
                p.position += delta;
            }
            Debug.Log($"[A] 사격장을 {k_RoomPosition} 로 옮김 (점 {k_RangePoints.Length}개 같이)");
        }

        static void Made(GameObject go) => Undo.RegisterCreatedObjectUndo(go, "A: 실내 사격장");

        // 부스 위 천장 덮개 + 사로마다 표적 이송 레일 (I 형강, 천장에 매다는 걸이)
        static void BuildRails(Transform booths, Materials mats, float innerHalf, float left)
        {
            float ceilingY = k_WallHeight;
            Box("Header", booths, new Vector3(0f, (k_HeaderBottom + ceilingY) * 0.5f, (k_HeaderBack + k_HeaderFront) * 0.5f),
                new Vector3(innerHalf * 2f, ceilingY - k_HeaderBottom, k_HeaderFront - k_HeaderBack), mats.Frame, false);

            var rails = Group("TargetRails", booths, Vector3.zero);
            float length = k_RailEndZ - k_HeaderFront;
            float midZ = (k_HeaderFront + k_RailEndZ) * 0.5f;
            for (int n = 1; n <= k_LaneCount; n++)
            {
                float x = left + (n - 0.5f) * k_LaneWidth;
                var rail = Group($"Rail_{n}", rails, new Vector3(x, 0f, 0f));
                float top = k_RailY + k_BeamHeight * 0.5f;
                float bottom = k_RailY - k_BeamHeight * 0.5f;
                Box("Flange_Top", rail, new Vector3(0f, top - k_FlangeThickness * 0.5f, midZ),
                    new Vector3(k_BeamWidth, k_FlangeThickness, length), mats.Steel, false);
                Box("Web", rail, new Vector3(0f, k_RailY, midZ),
                    new Vector3(k_WebThickness, k_BeamHeight - k_FlangeThickness * 2f, length), mats.Steel, false);
                Box("Flange_Bottom", rail, new Vector3(0f, bottom + k_FlangeThickness * 0.5f, midZ),
                    new Vector3(k_BeamWidth, k_FlangeThickness, length), mats.Steel, false);
                // 걸이: 위 날개에서 천장까지 막대 + 천장 받침판
                for (float z = k_HeaderFront + 0.5f; z < k_RailEndZ; z += k_HangerSpacing)
                {
                    Box("Hanger_Rod", rail, new Vector3(0f, (top + ceilingY) * 0.5f, z), new Vector3(0.03f, ceilingY - top, 0.03f), mats.Steel, false);
                    Box("Hanger_Plate", rail, new Vector3(0f, ceilingY - 0.005f, z), new Vector3(0.12f, 0.01f, 0.12f), mats.Steel, false);
                }
                // 끝 막이 (방호둑 쪽)
                Box("End_Stop", rail, new Vector3(0f, k_RailY, k_RailEndZ), new Vector3(k_BeamWidth + 0.02f, k_BeamHeight + 0.02f, 0.04f), mats.Frame, false);
            }
        }

        // 받침대 몸통 (11/03 사용자 모델 Models/Table_2/Table_2.fbx — 0.8 × 0.97 × 1.4 m, 긴 변이 z) — 부스마다 Table_Body 상자를 모델로 바꾼다.
        // 긴 변이 가로(x)가 되게 돌리고(선반 트인 면이 사수 쪽), 상자와 같은 자리(가운데 x 0 · z 테이블 가운데, 바닥 0)에 맞춘다. 잔디 윗면(Table_Top)은 그대로.
        // 부딪힘: 모델 = MeshCollider(선반 안이 비어 있게), 잔디 윗면 = 상자 (총이 놓이는 면 1.0 m). 예전 부스 통짜 상자는 지운다.
        // 탄창 자리(Points/Point_RangeMagazineRest)는 교육 부스 받침대 아래 선반 (11/03 사용자) — 선반 면 y 0.621, 선반 위 틈 0.876 까지, 안쪽 폭 ±0.615, z 4.25 ~ 4.97
        const string k_TableModel = "Assets/A/Models/Table_2/Table_2.fbx";
        const float k_TableModelYaw = 90f;
        static readonly Vector3 k_MagazineRestLocal = new Vector3(0.35f, 0.645f, 4.45f);   // Room_Range 기준 — 눕힌 탄창(두께 0.034) 가운데, 선반 앞끝에서 0.2 m 안

        // 햇빛 막이 (11/03 — 사용자: 각도에 따라 바깥 햇빛이 샌다). 노을 해가 지평선 위 10°로 낮아 얇은 천장(0.1 m)이 해 쪽에서 거의 옆면만 보인다 →
        // 그림자 맵에서 천장이 빛을 못 막아 천장 바로 아래 레일 윗면 등에 햇빛이 샌다 (그림자 바이어스). 공용 그림자 설정(PC_RPAsset)은 바꾸지 않고,
        // 방 바깥에 그림자만 만드는(보이지 않는) 두꺼운 상자로 지붕과 사방 벽을 덮는다. 문 구멍도 덮는다 — 사격장 안에는 햇빛이 들 곳이 없다
        const float k_BlockerThickness = 2f;
        const float k_BlockerRoofTop = 7f;

        [MenuItem("A/만들기/사격장 햇빛 막이 (그림자만)")]
        static void SunBlockerMenu()
        {
            var room = GameObject.Find(k_Room);
            if (room == null)
            {
                Debug.LogError($"[A] {k_Room} 이 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 사격장 햇빛 막이");
            int group = Undo.GetCurrentGroup();
            BuildSunBlocker(room.transform);
            Undo.CollapseUndoOperations(group);
        }

        static void BuildSunBlocker(Transform room)
        {
            DestroyIfExists(room.Find("SunBlocker"));
            var root = Group("SunBlocker", room, Vector3.zero);
            Made(root.gameObject);
            float innerHalf = k_LaneCount * k_LaneWidth * 0.5f + k_WallGap;
            float outerX = innerHalf + k_WallThickness;                 // 벽 바깥 면
            float back = k_RoomBack - k_WallThickness;
            float front = k_RoomLength + k_WallThickness;
            float t = k_BlockerThickness;
            float roofBottom = k_WallHeight + k_CeilingThickness;       // 천장 윗면
            float midZ = (back + front) * 0.5f, lenZ = front - back + 2f * t;
            var material = new Materials().Frame;
            void Blocker(string name, Vector3 center, Vector3 size)
            {
                var box = Box(name, root, center, size, material, false);
                var r = box.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;   // 보이지 않고 그림자만
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            Blocker("Roof", new Vector3(0f, (roofBottom + k_BlockerRoofTop) * 0.5f, midZ), new Vector3(2f * (outerX + t), k_BlockerRoofTop - roofBottom, lenZ));
            Blocker("South", new Vector3(0f, k_BlockerRoofTop * 0.5f, back - t * 0.5f), new Vector3(2f * (outerX + t), k_BlockerRoofTop, t));
            Blocker("North", new Vector3(0f, k_BlockerRoofTop * 0.5f, front + t * 0.5f), new Vector3(2f * (outerX + t), k_BlockerRoofTop, t));
            Blocker("East", new Vector3(outerX + t * 0.5f, k_BlockerRoofTop * 0.5f, midZ), new Vector3(t, k_BlockerRoofTop, lenZ));
            Blocker("West", new Vector3(-outerX - t * 0.5f, k_BlockerRoofTop * 0.5f, midZ), new Vector3(t, k_BlockerRoofTop, lenZ));
            Debug.Log($"[A] 사격장 햇빛 막이: 지붕 + 사방 벽 바깥 {t} m (그림자만)");
        }

        // 사격장 안전수칙 게시판 (11/03 사용자 그림 Models/SafetyRules.png, 1058 × 1486) — 사선 뒤 벽(Wall_South) 안쪽 면, 교육 부스(4번) 뒤 가운데.
        // 판(짙은 테두리) + 그림. 폭 1.5 m(세로는 그림 비율), 가운데 높이 2.86 m — VR 에서 대기 공간(3 ~ 6 m)에서 읽히는 크기
        const string k_PosterTexture = "Assets/A/Models/SafetyRules.png";
        const float k_PosterWidth = 1.5f;      // 11/03 1.0 → 1.5 (사용자)
        const float k_PosterCenterY = 2.864f;  // 11/03 1.6 → 2.0 → 2.864 (사용자가 그림을 직접 올린 자리) — 1.81 ~ 3.92 m
        const float k_PosterX = 0f;

        [MenuItem("A/만들기/사격장 안전수칙 게시판")]
        static void PosterMenu()
        {
            var room = GameObject.Find(k_Room);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_PosterTexture);
            if (room == null || texture == null)
            {
                Debug.LogError($"[A] {k_Room} 또는 {k_PosterTexture} 이 없다");
                return;
            }
            if (AssetImporter.GetAtPath(k_PosterTexture) is TextureImporter ti && ti.npotScale != TextureImporterNPOTScale.None)
            {
                ti.npotScale = TextureImporterNPOTScale.None;   // 2의 거듭제곱으로 늘리지 않는다 — 글자가 흐려지지 않게
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.anisoLevel = 4;
                ti.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_PosterTexture);
            }
            Undo.SetCurrentGroupName("A: 안전수칙 게시판");
            int group = Undo.GetCurrentGroup();
            DestroyIfExists(room.transform.Find("SafetyPoster"));

            string matPath = k_MaterialFolder + "/Range_SafetyRules.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Range_SafetyRules" };
                material.SetFloat("_Smoothness", 0.15f);   // 코팅 종이 — 형광등이 살짝 비친다
                AssetDatabase.CreateAsset(material, matPath);
            }
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);

            float height = k_PosterWidth * texture.height / texture.width;
            float wallFace = k_RoomBack;   // 사선 뒤 벽 안쪽 면 (z)
            var poster = new GameObject("SafetyPoster").transform;
            Made(poster.gameObject);
            poster.SetParent(room.transform, false);
            poster.localPosition = new Vector3(k_PosterX, k_PosterCenterY, wallFace);
            Box("Board", poster, new Vector3(0f, 0f, 0.01f), new Vector3(k_PosterWidth + 0.06f, height + 0.06f, 0.02f), new Materials().Frame, false);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Sheet";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(poster, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.021f);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 쿼드 앞면(-z) → 방 안쪽(+z)
            quad.transform.localScale = new Vector3(k_PosterWidth, height, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;

            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            Debug.Log($"[A] 안전수칙 게시판: {k_PosterWidth} × {height:0.##} m, 사선 뒤 벽 x {k_PosterX} · 가운데 높이 {k_PosterCenterY}");
        }

        [MenuItem("A/만들기/사격장 받침대 (Table_2 모델)")]
        static void TablesMenu()
        {
            var room = GameObject.Find(k_Room);
            if (room == null)
            {
                Debug.LogError($"[A] {k_Room} 이 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 사격장 받침대");
            int group = Undo.GetCurrentGroup();
            BuildTables(room.transform);
            Undo.CollapseUndoOperations(group);
        }

        static void BuildTables(Transform room)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_TableModel);
            Transform booths = room.Find("Booths");
            if (model == null || booths == null)
            {
                Debug.LogError($"[A] {k_TableModel} 또는 {k_Room}/Booths 가 없다");
                return;
            }
            Material material = PaintedMaterial.Ensure("Assets/A/Models/Table_2", "Table_2");
            float tableZ = k_TableFront + k_TableDepth * 0.5f;
            int count = 0;
            foreach (Transform booth in booths)
            {
                if (!booth.name.StartsWith("Booth_"))
                    continue;
                Transform old = booth.Find("Table_Body");
                int index = old != null ? old.GetSiblingIndex() : booth.childCount;
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, booth);
                Undo.RegisterCreatedObjectUndo(instance, "A: 사격장 받침대");
                instance.name = "Table_Body";
                instance.transform.SetSiblingIndex(index);
                instance.transform.localRotation = Quaternion.Euler(0f, k_TableModelYaw, 0f) * model.transform.localRotation;
                instance.transform.localPosition = Vector3.zero;
                PaintedMaterial.Assign(instance, material);
                GameObjectUtility.SetStaticEditorFlags(instance, GameObjectUtility.GetStaticEditorFlags(booth.gameObject));

                // 모델 원점이 가운데가 아니라서 겉 상자로 맞춘다: 가운데 (0, tableZ), 아래 0
                Bounds b = WorldBounds(instance);
                Vector3 local = booth.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
                instance.transform.localPosition = new Vector3(-local.x, -local.y, tableZ - local.z);

                foreach (var c in booth.GetComponents<BoxCollider>())
                    Undo.DestroyObjectImmediate(c);
                foreach (var mf in instance.GetComponentsInChildren<MeshFilter>())
                    Undo.AddComponent<MeshCollider>(mf.gameObject).sharedMesh = mf.sharedMesh;
                Transform top = booth.Find("Table_Top");
                if (top != null && top.GetComponent<Collider>() == null)
                    Undo.AddComponent<BoxCollider>(top.gameObject);
                // 총을 걸치는 면 — 총구 막힘 판정에서 뺀다 (11/10: 책상에 걸치면 손잡이가 상판 아래로 내려가 탄이 안 나갔다)
                foreach (var surface in new[] { instance.transform, top })
                    if (surface != null && surface.GetComponent<RifleRestSurface>() == null)
                        Undo.AddComponent<RifleRestSurface>(surface.gameObject);
                count++;
            }

            Transform rest = GameObject.Find(k_Points)?.transform.Find("Point_RangeMagazineRest");
            if (rest != null)
            {
                Undo.RecordObject(rest, "A: 사격장 받침대");
                rest.position = room.TransformPoint(k_MagazineRestLocal);
            }
            else
                Debug.LogWarning("[A] Points/Point_RangeMagazineRest 가 없다");
            Debug.Log($"[A] 사격장 받침대: 부스 {count}개의 Table_Body → Table_2 모델 (MeshCollider), 탄창 자리 → 받침대 아래 선반");
        }

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        // 사격장 벽 (11/02 사용자) — 네 벽(Wall_West·East·South·North)과 부스 위 천장 덮개(Header)를 내무반 벽(Wall.mat, m 단위 UV)으로.
        // 처음엔 사수 쪽만 바꾸려고 옆벽을 z k_HeaderBack 에서 나눴다(Wall_*_Firing) — 모두 같게 하면서 다시 하나로 합친다. 사격장 전체를 다시 짓지 않는다
        [MenuItem("A/만들기/사격장 벽 (내무반 벽)")]
        static void WallsMenu()
        {
            var room = GameObject.Find(k_Room);
            if (room == null)
            {
                Debug.LogError($"[A] {k_Room} 이 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 사격장 벽");
            int group = Undo.GetCurrentGroup();
            Walls(room.transform);
            Undo.CollapseUndoOperations(group);
        }

        static void Walls(Transform room)
        {
            Transform shell = room.Find("Shell");
            if (shell == null)
            {
                Debug.LogError($"[A] {k_Room}/Shell 이 없다");
                return;
            }
            foreach (string side in new[] { "Wall_West", "Wall_East" })
            {
                DestroyIfExists(shell.Find(side + "_Firing"));
                Transform wall = shell.Find(side);
                if (wall == null)
                    continue;
                Undo.RecordObject(wall, "A: 사격장 벽");
                SetSpanZ(wall, k_RoomBack, k_RoomLength);
            }
            foreach (string n in new[] { "Wall_West", "Wall_East", "Wall_South", "Wall_North" })
                WallUvBuilder.Apply(shell.Find(n));
            WallUvBuilder.Apply(room.Find("Booths/Header"));
            RangeDoorBuilder.CutWall(shell);   // 사격장 문(11/03)이 있으면 Wall_East 에 문 구멍을 다시 낸다
            AssetDatabase.SaveAssets();
        }

        static void SetSpanZ(Transform t, float z0, float z1)
        {
            t.localScale = new Vector3(t.localScale.x, t.localScale.y, z1 - z0);
            t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, (z0 + z1) * 0.5f);
        }

        // 표적만 레일에 다시 매단다 — 사격장 전체를 다시 짓지 않는다 (Booths·방은 그대로)
        [MenuItem("A/만들기/표적 레일에 매달기 (임시 상자)")]
        static void HangTargetsMenu()
        {
            var room = GameObject.Find(k_Room);
            if (room == null)
            {
                Debug.LogError($"[A] {k_Room} 이 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 표적 매달기");
            int group = Undo.GetCurrentGroup();
            HangTargets(room.transform, new Materials());
            Undo.CollapseUndoOperations(group);
        }

        // 교육 표적(Targets_25m/Target_Lane4)과 보기용 표적(Booths/Target_Lane*)의 바닥 기둥을 없애고 레일에 매단다.
        // 교육 표적에만 TargetCarrier(움직임)를 붙인다
        static void HangTargets(Transform room, Materials mats)
        {
            int count = 0;
            Transform target = room.Find($"Targets_25m/Target_Lane{k_TrainingLane}");
            if (target != null)
            {
                Hang(target, mats);
                var carrier = target.GetComponent<Range.TargetCarrier>();
                if (carrier == null)
                    Undo.AddComponent<Range.TargetCarrier>(target.gameObject);
                count++;
            }
            else
                Debug.LogError($"[A] 교육 표적 Targets_25m/Target_Lane{k_TrainingLane} 이 없다");

            Transform booths = room.Find("Booths");
            if (booths != null)
                foreach (Transform t in booths)
                    if (t.name.StartsWith("Target_Lane"))
                    {
                        Hang(t, mats);
                        count++;
                    }
            Debug.Log($"[A] 표적 {count}개를 레일에 매닮 (교육 표적만 움직임 — TargetCarrier)");
        }

        static void Hang(Transform target, Materials mats)
        {
            DestroyIfExists(target.Find("Post"));
            DestroyIfExists(target.Find(k_Carrier));
            var carrier = Group(k_Carrier, target, Vector3.zero);
            Made(carrier.gameObject);

            // 이동대 — 레일 아래 날개(가운데 높이)를 감싼다
            float flangeY = k_RailY - k_BeamHeight * 0.5f + k_FlangeThickness * 0.5f;
            float trolleyBottom = flangeY - k_TrolleyHeight * 0.5f;
            Box("Trolley", carrier, new Vector3(0f, flangeY, 0f), k_TrolleySize, mats.Steel, false);
            // 긴 막대 — 이동대 아래에서 표적판 윗면까지
            float rodLength = trolleyBottom - k_BoardTop;
            Box("Rod", carrier, new Vector3(0f, k_BoardTop + rodLength * 0.5f, k_RodZ), new Vector3(k_RodWidth, rodLength, k_RodDepth), mats.Steel, false);
            // 집게 — 표적판 윗변을 잡는 가로대
            Box("Clamp", carrier, new Vector3(0f, k_BoardTop + 0.02f, 0f), new Vector3(0.54f, 0.04f, 0.06f), mats.Frame, false);
        }

        // 바닥(0)부터 height 까지 서는 높이로
        static void SetHeight(Transform t, float height)
        {
            if (t == null)
                return;
            Undo.RecordObject(t, "A: 실내 사격장");
            t.localScale = new Vector3(t.localScale.x, height, t.localScale.z);
            t.localPosition = new Vector3(t.localPosition.x, height * 0.5f, t.localPosition.z);
        }

        // 앞뒤(z)를 방 길이(k_RoomBack ~ k_RoomLength)에 맞춘다
        static void SetLength(Transform t)
        {
            if (t == null)
                return;
            Undo.RecordObject(t, "A: 실내 사격장");
            t.localScale = new Vector3(t.localScale.x, t.localScale.y, k_RoomLength - k_RoomBack);
            t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, (k_RoomBack + k_RoomLength) * 0.5f);
        }

        // 폭(x 크기)이나 x 위치를 바꾼다 — null 이면 그대로
        static void SetWidth(Transform t, float? width, float? x)
        {
            if (t == null)
                return;
            Undo.RecordObject(t, "A: 실내 사격장");
            if (width.HasValue)
                t.localScale = new Vector3(width.Value, t.localScale.y, t.localScale.z);
            if (x.HasValue)
                t.localPosition = new Vector3(x.Value, t.localPosition.y, t.localPosition.z);
        }

        // --- 모양 ---

        static Transform Group(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        // 번호판 자리 (11/03 — 사용자가 1번을 손으로 옮긴 자리를 반듯하게): 부스 왼쪽 칸막이 유리 안쪽 면에 판 뒷면이 닿게, 뒤 기둥(z 2.6 ~ 2.68) 바로 앞
        const float k_SignDiameter = 0.26f;
        const float k_SignThickness = 0.02f;   // 원기둥 높이 2 × 0.01
        const float k_SignY = 2.25f;
        const float k_SignZ = 2.84f;
        const float k_NumberGap = 0.025f;      // 판 가운데에서 글자까지 (흰 면 앞 0.022 보다 조금 앞)

        [MenuItem("A/만들기/사격장 번호판 (칸막이 안쪽)")]
        static void SignsMenu()
        {
            var booths = GameObject.Find(k_Room)?.transform.Find("Booths");
            if (booths == null)
            {
                Debug.LogError($"[A] {k_Room}/Booths 가 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 사격장 번호판");
            int group = Undo.GetCurrentGroup();
            var mats = new Materials();
            for (int n = 1; n <= k_LaneCount; n++)
            {
                Transform booth = booths.Find($"Booth_{n}");
                if (booth == null)
                    continue;
                DestroyIfExists(booth.Find("Sign"));
                DestroyIfExists(booth.Find("Number"));
                PlaceSign(booth, n, mats);
                Made(booth.Find("Sign").gameObject);
                Made(booth.Find("Number").gameObject);
            }
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[A] 사격장 번호판 {k_LaneCount}개: 부스 왼쪽 칸막이 안쪽, 높이 {k_SignY} · z {k_SignZ}");
        }

        static void PlaceSign(Transform booth, int n, Materials mats)
        {
            float glassFace = -k_LaneWidth * 0.5f + k_GlassThickness * 0.5f;   // 왼쪽 칸막이 유리의 부스 쪽 면 (부스 기준 x)
            float x = glassFace + k_SignThickness * 0.5f;
            var rotation = Quaternion.Euler(0f, 270f, 0f);                      // 앞(-z, 사수 쪽)을 부스 안(+x)으로
            Disc("Sign", booth, new Vector3(x, k_SignY, k_SignZ), k_SignDiameter, mats.Sign, mats.SignRing);
            booth.Find("Sign").localRotation = rotation * Quaternion.Euler(90f, 0f, 0f);
            Number(booth, n.ToString(), new Vector3(x + k_NumberGap, k_SignY, k_SignZ));
            booth.Find("Number").localRotation = rotation;
        }

        static void Disc(string name, Transform parent, Vector3 localPosition, float diameter, Material face, Material ring)
        {
            var outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outer.name = name;
            Object.DestroyImmediate(outer.GetComponent<Collider>());
            outer.transform.SetParent(parent, false);
            outer.transform.localPosition = localPosition;
            outer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 원판이 사수 쪽(-Z)을 보게
            outer.transform.localScale = new Vector3(diameter, 0.01f, diameter);
            outer.GetComponent<MeshRenderer>().sharedMaterial = ring;

            var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = "Face";
            Object.DestroyImmediate(inner.GetComponent<Collider>());
            inner.transform.SetParent(outer.transform, false);
            inner.transform.localPosition = new Vector3(0f, -0.6f, 0f);      // 사수 쪽으로 조금 나온다
            inner.transform.localScale = new Vector3(0.82f, 1f, 0.82f);
            inner.GetComponent<MeshRenderer>().sharedMaterial = face;
        }

        static void Number(Transform parent, string text, Vector3 localPosition)
        {
            var go = new GameObject("Number");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;   // TextMesh 는 +Z 를 등지고 -Z 쪽(사수)에서 읽힌다
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 96;
            mesh.characterSize = 0.012f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold;
            mesh.color = new Color(0.08f, 0.2f, 0.45f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }

        // 교육 표적의 판·기둥·영점 표적지 모양을 복사한다 (기록하는 ZeroTarget 은 빼고 — 보기용)
        static void DecoTarget(Transform parent, Transform source, string name, Vector3 localPosition)
        {
            var t = Group(name, parent, localPosition);
            foreach (Transform part in source)
            {
                if (part.name != "Board" && part.name != "Post" && part.name != "ZeroSheet")
                    continue;
                var copy = Object.Instantiate(part.gameObject, t);
                foreach (var recorder in copy.GetComponentsInChildren<Range.ZeroTarget>(true))
                    Object.DestroyImmediate(recorder);
                copy.name = part.name;
                copy.transform.localPosition = part.localPosition;
                copy.transform.localRotation = part.localRotation;
                copy.transform.localScale = part.localScale;
            }
        }

        static Transform FindChild(Transform parent, params string[] names)
        {
            foreach (string n in names)
            {
                Transform t = parent.Find(n);
                if (t != null)
                    return t;
            }
            return null;
        }

        static void DestroyIfExists(Transform t)
        {
            if (t != null)
                Undo.DestroyObjectImmediate(t.gameObject);
        }

        // --- 재질 (Assets/A/Materials/Range, 있으면 그대로 쓴다) ---

        class Materials
        {
            public readonly Material Frame = Load("Range_Frame", new Color(0.12f, 0.13f, 0.15f), 0.4f, false);
            public readonly Material Glass = Load("Range_Glass", new Color(0.75f, 0.88f, 0.95f, 0.22f), 0.9f, true);
            public readonly Material Turf = Load("Range_Turf", new Color(0.24f, 0.5f, 0.2f), 0.05f, false);
            public readonly Material Mesh = Load("Range_TableMesh", new Color(0.08f, 0.08f, 0.09f), 0.3f, false);
            public readonly Material Sign = Load("Range_SignFace", new Color(0.95f, 0.95f, 0.95f), 0.3f, false);
            public readonly Material SignRing = Load("Range_SignRing", new Color(0.1f, 0.45f, 0.85f), 0.3f, false);
            public readonly Material Steel = Load("Range_Steel", new Color(0.55f, 0.57f, 0.6f), 0.6f, false);
        }

        static Material Load(string name, Color color, float smoothness, bool transparent)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;   // 손으로 고친 재질은 덮어쓰지 않는다

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);   // Transparent
                material.SetFloat("_Blend", 0f);     // Alpha
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
