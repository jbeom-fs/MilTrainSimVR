using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 엄폐물 겉모양 (사용자 10/29): Tripo 로 만든 모델을 회색 상자 자리에 씌운다 — 바리케이드·가구·가판대.
    // 상자의 콜라이더는 그대로 남아서 부딪힘·총알·길찾기는 전과 같고, 보이는 면만 모델로 바뀐다.
    // 모델은 Assets/C/Models/Tripo/<종류>/ 의 모델 파일 하나 — 폴더가 없으면 그 종류는 지금처럼 회색 상자로 남는다.
    // 판정이 상자라서 모델도 옆면이 곧게 선 모양을 고른다 (위가 좁으면 경사면 앞 허공에 탄착이 생긴다).
    public static partial class UrbanGreyboxBuilder
    {
        const string k_PropFolder = "Assets/C/Models/Tripo";
        const float k_PropLodBias = 2f;   // Mesh LOD 를 몇 단계 일찍 낮출지 — 모델이 1,500개 넘게 깔려서 (10/30 Play 측정)

        // tile: 상자 길이에 맞춰 토막을 이어 붙인다 (모델 비율대로 몇 토막인지 정하고 조금씩 늘이거나 줄인다)
        // symmetric: 앞뒤가 같아서 토막마다 뒤집어 같은 모양이 줄지어 반복되는 걸 덜 보이게 한다
        // flip: 모델 앞면이 반대쪽이면 true — 모델마다 다르니 받아 본 뒤 정한다
        // pair: 뒤가 뚫린 모델 — 깊이를 반씩 나눠 두 개를 등을 맞대 놓으면 닫힌 상자로 보인다 (앞면이 양쪽을 본다)
        // facade: 건물 바깥 장식(Models/Facade — 세라믹 외벽의 금속 해돋이 · 이름 글자) — 햇빛 아래라 그림자를 드리운다
        // exact: 기계처럼 상자 하나에 모델 하나를 정해진 앞 방향으로 (토막 · 긴 변 규칙 없이 축마다 상자 크기에 맞춤). frontZ: 모델 앞이 +Z
        // swapCards: 가챠 기계 — 두 대씩 번갈아 위 · 아래 진열 카드를 바꿔 끼워 줄이 덜 반복돼 보이게 (사용자 11/09)
        // variants: 같은 모델에 바꿔 끼울 재질(종류 폴더의 .mat 이름) — Skin(상자, 종류, 앞, 몇 번째) 로 고른다 (exact 만)
        // bulletMesh: 총알 판정 = 보이는 모델 — 상자(판정)보다 모델이 안으로 들어간 곳이 많아 총알이 허공에 박히는 종류.
        //   상자는 'Ignore Raycast' 층으로 남겨 몸 · 길찾기만 막고(Skin), 모델에 MeshCollider 를 붙인다(PlaceProps → BulletMesh) — 차량과 같은 방식.
        //   11/19 사용자: "green_metal_kiosk_3d_model 에 투명벽이 있어서 총탄이 박히는게 있어" (KioskTall — 지하 홀의 키 큰 가판대 둘.
        //   잰 값: 옆에서 10 cm 격자로 쏜 1,980발 가운데 578발이 보이는 면보다 5 cm 넘게(최대 19 cm) 앞에서 멈췄고 38발은 빈 곳에 맞았다).
        //   같은 날 다른 소품도 ("다른 소품도"): 선반 · 책상 · 의자 · 기계 · 탁자 · 카운터 · 모래주머니 — 선반 사이 · 책상 밑으로 총알과 시선이 지나간다.
        //   낮은 가판대(KioskLow) · 콘크리트 블록은 상자와 모델이 같아서(허공 0 %) 그대로 둔다
        struct PropKind
        {
            public bool tile, symmetric, flip, pair, facade, exact, frontZ, swapCards, bulletMesh;
            public string[] variants;
        }

        // 받은 모델을 앞뒤로 렌더해 보고 정한 값 (10/29~30)
        static readonly Dictionary<string, PropKind> k_PropKinds = new Dictionary<string, PropKind>
        {
            ["ConcreteBlock"] = new PropKind { tile = true, symmetric = true },
            ["SandbagWall"] = new PropKind { tile = true, symmetric = true, bulletMesh = true },
            ["TallShelf"] = new PropKind { tile = true, symmetric = true, bulletMesh = true },   // 뒤판 없이 앞뒤 모두 물건이 보인다
            ["LowCabinet"] = new PropKind { tile = true, flip = true, bulletMesh = true },       // 문이 +Z 쪽
            ["OfficeDesk"] = new PropKind { bulletMesh = true },
            ["ShopCounter"] = new PropKind { flip = true, bulletMesh = true },                    // +Z = 손님 쪽 막힌 판, -Z = 서랍·계산대 화면
            ["KioskLow"] = new PropKind { tile = true, pair = true },          // 문·서랍은 -Z, 뒤(+Z)가 뚫려 있다
            ["KioskTall"] = new PropKind { flip = true, bulletMesh = true },   // 셔터가 +Z 쪽. 총알은 보이는 모델에 맞는다 (11/19)
            // 청운백화점 외벽 장식 (11/06, 블렌더 스크립트 Models/Facade/Source~/make_facade_ornaments.py) — 블렌더 -Y 앞면이 Unity 에선 +Z 쪽
            // (처음 그대로 놓았더니 '청운백화점'이 거울 글자로 보였다)
            ["Sunburst"] = new PropKind { facade = true, flip = true },
            ["StoreName"] = new PropKind { facade = true, flip = true },
            // 1층 가챠 가게 기계 (사용자 11/09 — UrbanGreyboxBuilder.Shop.cs). 줄인 모델: Source~/make_prop_lowpoly.py · gacha/make_gacha_lowpoly.py
            ["ClawMachine"] = new PropKind { exact = true, frontZ = true, bulletMesh = true },
            ["CapsuleMachine"] = new PropKind { exact = true, frontZ = true, swapCards = true, bulletMesh = true },
            ["CoinChanger"] = new PropKind { exact = true, frontZ = true, bulletMesh = true },
            // 2층 당구장 (사용자 11/09 — 제일당구 N09, UrbanGreyboxBuilder.Shop.cs). 당구대는 긴 변이 +Z, 큐 장은 큐가 보이는 쪽이 +Z
            ["BilliardTable"] = new PropKind { exact = true, frontZ = true, bulletMesh = true },
            ["CueRack"] = new PropKind { exact = true, frontZ = true, bulletMesh = true },
            // 1층 고깃집 (사용자 11/09 — 도야지천국 N23, UrbanGreyboxBuilder.Shop.cs). 탁자는 둥글어 앞이 없고, 의자는 앉는 쪽이 +Z
            ["BbqTable"] = new PropKind { exact = true, frontZ = true, bulletMesh = true },
            ["PlasticChair"] = new PropKind { exact = true, frontZ = true, bulletMesh = true, variants = new[] { "Plastic_Chair_White", "Plastic_Chair_Green", "Plastic_Chair_Blue" } },
        };

        // 씌울 상자 → (종류, 앞쪽). 앞쪽은 상자 좌표 — 벽에 붙인 선반은 방 안쪽, 카운터는 길 쪽. 0 이면 긴 변과 직각인 아무 쪽
        static readonly Dictionary<GameObject, (string kind, Vector3 front)> s_Skins = new Dictionary<GameObject, (string, Vector3)>();
        static readonly Dictionary<string, GameObject> s_PropModels = new Dictionary<string, GameObject>();
        static readonly Dictionary<GameObject, int> s_SkinVariants = new Dictionary<GameObject, int>();

        // 합칠 때 받아 둔 자리 (root 좌표)
        struct PropSlot
        {
            public string kind;
            public Vector3 front;
            public Matrix4x4 toRoot;
            public Vector3 size;
            public uint layer;
            public int variant;   // PropKind.variants 중 몇 번째 재질 (-1 = 모델 그대로)
        }

        static int s_PropCount;   // 이번에 놓은 모델 수 — 맵 만들기 끝 로그에 적는다

        // 맵을 다시 만들 때마다 처음부터 — 그 사이 모델이 새로 들어왔을 수 있다
        static void ResetProps()
        {
            s_Skins.Clear();
            s_PropModels.Clear();
            s_SkinVariants.Clear();
            s_BulletMeshes.Clear();
            s_PropCount = 0;
        }

        // 상자에 겉모양을 씌우라고 적어 둔다. 그 종류 모델이 아직 없으면 그냥 지나간다 (상자 그대로)
        static GameObject Skin(GameObject box, string kind, Vector3 front = default)
        {
            if (PropModel(kind) != null)
            {
                s_Skins[box] = (kind, front);
                // 총알은 모델에 맞는 종류 — 상자는 몸 · 길찾기만 막게 층을 옮긴다 (층이 달라 MergeColliders 가 부모에 합치지 않고 그대로 둔다)
                if (k_PropKinds[kind].bulletMesh && box.GetComponent<Collider>() != null)
                    box.layer = k_IgnoreRaycastLayer;
            }
            return box;
        }

        // 총알 판정 = 보이는 모델: 모델의 메시마다 MeshCollider (읽을 수 있는 사본 — FBX 는 Read/Write 가 꺼져 있고 배율이 축마다 다르다).
        // 층은 Default — 총알 · 시야 · 엄폐 판단(Gunfire.ShotMask)이 본다
        static readonly Dictionary<Mesh, Mesh> s_BulletMeshes = new Dictionary<Mesh, Mesh>();   // 보이는 메시 → 판정 메시 (선반 460개가 같은 메시를 쓴다)

        static void BulletMesh(GameObject model)
        {
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                MeshCollider collider = filter.GetComponent<MeshCollider>();
                if (collider == null)
                    collider = filter.gameObject.AddComponent<MeshCollider>();
                if (!s_BulletMeshes.TryGetValue(filter.sharedMesh, out Mesh collision) || collision == null)
                    s_BulletMeshes[filter.sharedMesh] = collision = VehicleBulletColliderBuilder.SaveCollisionMesh(filter.sharedMesh);
                collider.sharedMesh = collision;
                collider.convex = false;
                filter.gameObject.layer = 0;
            }
        }

        // 재질을 골라 씌운다 — variant 는 PropKind.variants 의 순서 (넘치면 돌려 씀)
        static GameObject Skin(GameObject box, string kind, Vector3 front, int variant)
        {
            Skin(box, kind, front);
            if (variant >= 0 && s_Skins.ContainsKey(box))
                s_SkinVariants[box] = variant;
            return box;
        }

        static GameObject PropModel(string kind)
        {
            if (s_PropModels.TryGetValue(kind, out GameObject model))
                return model;
            string folder = $"{k_PropFolder}/{kind}";
            if (!AssetDatabase.IsValidFolder(folder))
                folder = $"{k_FacadeModelFolder}/{kind}";   // 외벽 장식은 Tripo 가 아니라 블렌더로 만든 것
            if (AssetDatabase.IsValidFolder(folder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                {
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (model != null)
                        break;
                }
            }
            s_PropModels[kind] = model;
            return model;
        }

        // CombineVisuals 가 상자를 합치기 전에 묻는다 — 씌울 상자면 자리·크기·렌더링 레이어를 받아 두고 true (면은 합치지 않는다)
        static bool TakeSkin(MeshFilter piece, Matrix4x4 toRoot, List<PropSlot> slots)
        {
            if (!s_Skins.TryGetValue(piece.gameObject, out (string kind, Vector3 front) skin))
                return false;
            slots.Add(new PropSlot
            {
                kind = skin.kind,
                front = skin.front,
                toRoot = toRoot,
                size = piece.sharedMesh.bounds.size,
                layer = piece.GetComponent<MeshRenderer>().renderingLayerMask,
                variant = s_SkinVariants.TryGetValue(piece.gameObject, out int variant) ? variant : -1,
            });
            return true;
        }

        // 받아 둔 자리마다 모델을 놓는다 (root 아래 Props). 모델엔 콜라이더가 없어 MergeColliders·내비 굽기에 안 걸린다
        // (bulletMesh 종류만 MeshCollider 가 붙는다 — 상자 안쪽이라 길찾기 지도는 달라지지 않는다).
        // 정적 배칭은 켜지 않는다 — 배칭이 메시 법선 형식에 따라 법선을 깨뜨린 적이 있다(10/26). 그리기 호출은 SRP Batcher 가 묶는다
        static void PlaceProps(Transform root, List<PropSlot> slots)
        {
            if (slots.Count == 0)
                return;
            Transform group = Group("Props", root);
            int cardCount = 0;
            for (int s = 0; s < slots.Count; s++)
            {
                PropSlot slot = slots[s];
                GameObject model = PropModel(slot.kind);
                PropKind kind = k_PropKinds[slot.kind];
                Bounds mb = ModelBounds(model);

                if (kind.exact)
                {
                    // 모델 앞(frontZ 면 +Z, 아니면 긴 변과 직각인 -쪽)을 상자 앞으로 돌리고, 돌린 모델 크기를 상자 크기에 축마다 맞춘다
                    Vector3 boxFront = slot.front != Vector3.zero ? slot.front : Vector3.back;
                    Vector3 mFront = kind.frontZ ? Vector3.forward : (mb.size.x >= mb.size.z ? Vector3.back : Vector3.left);
                    if (kind.flip)
                        mFront = -mFront;
                    Quaternion turnE = Quaternion.Euler(0f, Vector3.SignedAngle(mFront, boxFront, Vector3.up), 0f);
                    Vector3 tE = turnE * mb.size;
                    tE = new Vector3(Mathf.Abs(tE.x), mb.size.y, Mathf.Abs(tE.z));
                    var kE = new Vector3(slot.size.x / tE.x, slot.size.y / tE.y, slot.size.z / tE.z);
                    Transform topE = model.transform;
                    Quaternion rotE = turnE * topE.localRotation;
                    Vector3 backE = Quaternion.Inverse(rotE) * kE;
                    Vector3 scaleE = Vector3.Scale(new Vector3(Mathf.Abs(backE.x), Mathf.Abs(backE.y), Mathf.Abs(backE.z)), topE.localScale);
                    Vector3 posE = Vector3.Scale(kE, turnE * (topE.localPosition - mb.center));   // 모델 가운데 = 상자 가운데 (높이가 같아 바닥이 닿는다)
                    var goE = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
                    goE.transform.SetLocalPositionAndRotation(slot.toRoot.MultiplyPoint3x4(posE), slot.toRoot.rotation * rotE);
                    goE.transform.localScale = scaleE;
                    bool swap = kind.swapCards && (cardCount++ / 2) % 2 == 1;   // 등 맞댄 두 대씩 번갈아
                    Material variantMat = kind.variants != null && slot.variant >= 0
                        ? AssetDatabase.LoadAssetAtPath<Material>($"{k_PropFolder}/{slot.kind}/{kind.variants[slot.variant % kind.variants.Length]}.mat")
                        : null;
                    foreach (Renderer r in goE.GetComponentsInChildren<Renderer>(true))
                    {
                        r.renderingLayerMask = slot.layer;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.meshLodSelectionBias = k_PropLodBias;
                        if (swap)
                            SwapCards(r);
                        if (variantMat != null)
                            r.sharedMaterial = variantMat;
                    }
                    if (kind.bulletMesh)
                        BulletMesh(goE);
                    s_PropCount++;
                    continue;
                }

                // 상자 쪽: 긴 변 방향과 앞쪽 (앞쪽은 긴 변과 직각)
                bool lengthX = slot.size.x >= slot.size.z;
                Vector3 front = slot.front != Vector3.zero ? slot.front : (lengthX ? Vector3.back : Vector3.left);
                // 모델 쪽: 긴 변과 직각인 -쪽을 앞으로 본다 (flip 이면 반대)
                Vector3 modelFront = mb.size.x >= mb.size.z ? Vector3.back : Vector3.left;
                if (kind.flip)
                    modelFront = -modelFront;
                Quaternion turn = Quaternion.Euler(0f, Vector3.SignedAngle(modelFront, front, Vector3.up), 0f);
                // 90° 단위로 돌리니 돌린 뒤 크기는 x·z 만 바뀐다
                Vector3 turned = turn * mb.size;
                turned = new Vector3(Mathf.Abs(turned.x), mb.size.y, Mathf.Abs(turned.z));

                float length = lengthX ? slot.size.x : slot.size.z;
                int count = 1;
                if (kind.tile)
                {
                    // 높이를 맞췄을 때 모델 한 토막의 길이 — 그 길이에 가장 가깝게 몇 토막인지 정한다
                    float natural = (lengthX ? turned.x : turned.z) * slot.size.y / turned.y;
                    count = Mathf.Max(1, Mathf.RoundToInt(length / natural));
                }
                float piece = length / count;
                // 짝으로 놓으면 깊이를 반씩 나눈다
                float depth = lengthX ? slot.size.z : slot.size.x;
                int sides = kind.pair ? 2 : 1;
                float part = depth / sides;
                Vector3 target = lengthX ? new Vector3(piece, slot.size.y, part) : new Vector3(part, slot.size.y, piece);
                // 상자 축 기준 배율 — 모델을 돌린 뒤 상자 축으로 늘이거나 줄인다
                var k = new Vector3(target.x / turned.x, target.y / turned.y, target.z / turned.z);
                // FBX 모델 맨 위 오브젝트에는 축 변환 회전(x 270° 등)이 붙어 있다 → 그 회전·배율을 덮어쓰지 말고 이어 붙인다
                Transform top = model.transform;
                for (int i = 0; i < count; i++)
                {
                    float u = -length * 0.5f + piece * (i + 0.5f);
                    for (int side = 0; side < sides; side++)
                    {
                        // 짝의 둘째는 반대쪽 반에 반대로 돌려 놓아 등이 맞닿게
                        Vector3 center = (lengthX ? new Vector3(u, 0f, 0f) : new Vector3(0f, 0f, u)) + (kind.pair ? front * (side == 0 ? part : -part) * 0.5f : Vector3.zero);
                        bool reverse = side == 1 || (kind.symmetric && (s + i) % 2 == 1);
                        Quaternion turnI = reverse ? Quaternion.Euler(0f, 180f, 0f) * turn : turn;
                        Quaternion rotation = turnI * top.localRotation;
                        // 상자 축 배율을 모델 자기 축으로 옮긴다 (90° 단위 회전이라 축이 맞바뀔 뿐)
                        Vector3 back = Quaternion.Inverse(rotation) * k;
                        Vector3 scale = Vector3.Scale(new Vector3(Mathf.Abs(back.x), Mathf.Abs(back.y), Mathf.Abs(back.z)), top.localScale);
                        // 모델 가운데가 토막 가운데에 오게 — 높이도 상자와 같아서 바닥이 상자 밑면에 닿는다
                        Vector3 position = center + Vector3.Scale(k, turnI * (top.localPosition - mb.center));

                        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
                        go.transform.SetLocalPositionAndRotation(slot.toRoot.MultiplyPoint3x4(position), slot.toRoot.rotation * rotation);
                        go.transform.localScale = scale;
                        // 지하처럼 해가 안 드는 곳은 상자와 같은 렌더링 레이어여야 한다.
                        // 그림자는 끈다 — 모델은 모두 건물 안·지하라 햇빛 그림자가 거의 안 보이는데, 켜 두면 그림자 지도에 한 번 더 그려
                        // 시작 자리에서 삼각형이 93만 → 1200만이 됐다(10/30, 선반 1120개).
                        // Mesh LOD(모델 가져오기 설정)는 멀수록 단순한 단계를 그리는데, 2단계 일찍 낮추면 모델 몫이 360만 → 120만
                        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                        {
                            r.renderingLayerMask = slot.layer;
                            r.shadowCastingMode = kind.facade ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                            r.meshLodSelectionBias = k_PropLodBias;
                        }
                        if (kind.bulletMesh)
                            BulletMesh(go);
                        s_PropCount++;
                    }
                }
            }
        }

        // 가챠 기계의 위 · 아래 진열 카드 재질을 맞바꾼다 (인스턴스 재질만 — 모델 파일은 그대로)
        static void SwapCards(Renderer r)
        {
            Material[] ms = r.sharedMaterials;
            int a = -1, b = -1;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i] == null)
                    continue;
                if (ms[i].name == "Gacha_Card_Top")
                    a = i;
                else if (ms[i].name == "Gacha_Card_Bottom")
                    b = i;
            }
            if (a < 0 || b < 0)
                return;
            (ms[a], ms[b]) = (ms[b], ms[a]);
            r.sharedMaterials = ms;
        }

        // 모델을 그대로 꺼내 놓았을 때 전체를 감싸는 상자 — 맨 위 오브젝트의 축 변환 회전까지 넣어서 '선 모양' 기준
        static Bounds ModelBounds(GameObject model)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                    continue;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (first)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }
            return bounds;
        }
    }
}
