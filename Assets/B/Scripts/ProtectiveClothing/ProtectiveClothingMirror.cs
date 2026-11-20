using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.B.ProtectiveClothing
{
    // Full-length mirror of the protective clothing scene (same plane-mirror method as the K1 mirror, K1ObserverView, which is not
    // used so K1 is neither changed nor needed). Its own camera renders into a texture this component creates on enable and
    // releases on disable, shown flipped left-right on a quad through a property block (the material asset is never changed). The
    // camera sits at the HMD camera reflected in the quad plane with an off-axis frustum through the quad and its near plane on the
    // mirror plane, so nothing behind the mirror is drawn. ONE centre-eye image shown to both eyes: correct perspective for the
    // head position, not a per-eye stereo mirror. The quad is not drawn by its own camera (no feedback). The camera is saved
    // disabled, is never MainCamera, has no AudioListener and never renders to the HMD.
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingMirror : MonoBehaviour
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        [Tooltip("Camera of the mirror. Saved disabled with no target texture; this component assigns and releases it.")]
        [SerializeField] Camera viewCamera;
        [Tooltip("Unity Quad showing the mirror image (1 x 1 in local XY, front seen from local -Z); also the mirror plane and size.")]
        [SerializeField] Renderer screen;
        [Tooltip("HMD camera: the mirror's eye point.")]
        [SerializeField] Transform head;
        [Tooltip("Vertical texture resolution (px); the width follows the screen's aspect.")]
        [SerializeField, Range(128, 2048)] int resolution = 1024;
        [Tooltip("Far clip (m): limits what the mirror renders.")]
        [SerializeField] float farClip = 6f;

        RenderTexture texture;
        MaterialPropertyBlock block;
        bool running;

        public Camera ViewCamera => viewCamera;
        public Renderer Screen => screen;

        void OnEnable()
        {
            if (running)
                return;
            if (!TryValidate(out var error))
            {
                Debug.LogError("[ProtectiveClothing] " + error + " Mirror stays off.", this);
                return;
            }

            var size = screen.transform.lossyScale;
            var width = Mathf.Clamp(Mathf.RoundToInt(resolution * Mathf.Abs(size.x) / Mathf.Abs(size.y)), 16, 4096);
            texture = new RenderTexture(width, resolution, 24, RenderTextureFormat.ARGB32) { name = name + "_View", antiAliasing = 1 };
            texture.Create();
            viewCamera.stereoTargetEye = StereoTargetEyeMask.None;
            viewCamera.targetTexture = texture;
            viewCamera.farClipPlane = farClip;

            block ??= new MaterialPropertyBlock();
            screen.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, texture);
            block.SetVector(BaseMapStId, new Vector4(-1f, 1f, 1f, 0f));
            screen.SetPropertyBlock(block);

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            running = true;
            UpdateMirror();
            viewCamera.enabled = true;
        }

        void OnDisable()
        {
            if (!running)
                return;
            running = false;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (viewCamera != null)
            {
                viewCamera.enabled = false;
                viewCamera.targetTexture = null;
                viewCamera.ResetProjectionMatrix();
            }
            if (screen != null)
            {
                screen.SetPropertyBlock(null);
                screen.forceRenderingOff = false;
            }
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        void LateUpdate() => UpdateMirror();

        void OnBeginCamera(ScriptableRenderContext _, Camera renderingCamera)
        {
            screen.forceRenderingOff = renderingCamera == viewCamera;
            // Latest head pose for this camera; URP reads the camera after this callback.
            if (renderingCamera == viewCamera)
                UpdateMirror();
        }

        public bool TryValidate(out string error)
        {
            error = viewCamera == null || screen == null || head == null ? $"ProtectiveClothingMirror '{name}': camera, screen or head missing."
                : viewCamera.transform == head || viewCamera.transform.IsChildOf(head) ? $"ProtectiveClothingMirror '{name}': mirror camera must not be the HMD camera or under it."
                : screen.transform.IsChildOf(viewCamera.transform) ? $"ProtectiveClothingMirror '{name}': screen must not be under its camera."
                : farClip <= 0.1f ? $"ProtectiveClothingMirror '{name}': far clip out of range."
                : null;
            return error == null;
        }

        // Off-axis frustum from the reflected eye through the quad (generalized perspective projection). Seen from behind the
        // mirror the corners swap left and right: lower-left pb, lower-right pa, upper-left pd. The camera looks along the mirror
        // normal toward the user, with the near plane exactly on the mirror plane.
        void UpdateMirror()
        {
            if (!running)
                return;
            var surface = screen.transform;
            var normal = -surface.forward;
            var eye = head.position;
            var distance = Vector3.Dot(eye - surface.position, normal);
            // Viewer on or behind the mirror plane: keep the last image.
            if (distance < 0.02f)
                return;

            var reflected = eye - 2f * distance * normal;
            var pa = surface.TransformPoint(new Vector3(-0.5f, -0.5f, 0f));
            var pb = surface.TransformPoint(new Vector3(0.5f, -0.5f, 0f));
            var pd = surface.TransformPoint(new Vector3(0.5f, 0.5f, 0f));
            var right = (pa - pb).normalized;
            var up = (pd - pb).normalized;
            var va = pb - reflected;
            var vb = pa - reflected;
            var vc = pd - reflected;
            var near = Vector3.Dot(va, normal);
            var far = Mathf.Max(farClip, near + 0.1f);

            viewCamera.transform.SetPositionAndRotation(reflected, Quaternion.LookRotation(normal, up));
            viewCamera.projectionMatrix = Matrix4x4.Frustum(Vector3.Dot(right, va), Vector3.Dot(right, vb), Vector3.Dot(up, va), Vector3.Dot(up, vc), near, far);
        }
    }
}
