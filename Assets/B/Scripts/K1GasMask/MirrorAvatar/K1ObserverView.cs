using System.Collections.Generic;
using MilTrainSimVR.B.K1GasMask.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.B.K1GasMask.MirrorAvatar
{
    // One observation screen: its own camera renders into a texture this component creates on enable and releases on
    // disable (also on scene unload), shown on a quad through a property block (the shared B material is never changed).
    //   Mirror: full-length mirror. The camera sits at the HMD camera's position reflected in the quad plane, aligned with
    //     the quad, with an off-axis frustum through the quad and its near plane on the mirror plane, so nothing behind the
    //     mirror is drawn; the texture is shown flipped left-right. Follows the head, but it is ONE centre-eye image shown to
    //     both eyes: correct perspective for the head position, not a per-eye stereo mirror.
    //   RearFollow: "후면 보기". The camera stays behind the head along its heading (smoothed) and looks at the back of the
    //     head; the screen itself stays where the scene puts it.
    // Both views show the scene as it is (the real mask and its current harness pose, the avatar); nothing is posed for them.
    // Every observer screen is hidden from every observer camera, so no view ever draws a view texture (no feedback loop).
    // The camera is saved disabled and is enabled only while its texture exists; it never renders to the HMD.
    public sealed class K1ObserverView : MonoBehaviour
    {
        public enum Mode
        {
            Mirror = 0,
            RearFollow = 1
        }

        static readonly List<K1ObserverView> Active = new List<K1ObserverView>();
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        [SerializeField] Mode mode;
        [Tooltip("Camera of this view. Saved disabled with no target texture; this component assigns and releases it.")]
        [SerializeField] Camera viewCamera;
        [Tooltip("Unity Quad showing the view (1 x 1 in local XY, front seen from local -Z). For the mirror it is also the mirror plane and size.")]
        [SerializeField] Renderer screen;
        [Tooltip("HMD camera: the mirror's eye point, the rear view's followed head.")]
        [SerializeField] Transform head;
        [Tooltip("Vertical texture resolution (px); the width follows the screen's aspect.")]
        [SerializeField, Range(128, 2048)] int resolution = 1024;
        [Tooltip("Far clip (m): limits what this view renders.")]
        [SerializeField] float farClip = 6f;

        [Header("Rear view")]
        [Tooltip("Camera distance behind the back of the head (m).")]
        [SerializeField] float rearDistance = 0.55f;
        [Tooltip("Camera height above the eye point (m).")]
        [SerializeField] float rearHeight = 0.06f;
        [Tooltip("Back of the head behind the eye point (m); the camera looks at it.")]
        [SerializeField] float headDepth = 0.09f;
        [Tooltip("Seconds for the camera to swing behind a new head heading.")]
        [SerializeField] float rearTurnTime = 0.2f;
        [Tooltip("Label above the rear screen; text from TrainingSubtitles.")]
        [SerializeField] TMP_Text label;

        RenderTexture texture;
        MaterialPropertyBlock block;
        bool running;
        float rearYaw;
        bool hasYaw;

        public Mode ViewMode => mode;
        public Camera ViewCamera => viewCamera;
        public Renderer Screen => screen;
        public TMP_Text Label => label;

        void OnEnable()
        {
            if (running)
                return;
            if (!TryValidate(out var error))
            {
                Debug.LogError(error + " View stays off.", this);
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
            block.SetVector(BaseMapStId, mode == Mode.Mirror ? new Vector4(-1f, 1f, 1f, 0f) : new Vector4(1f, 1f, 0f, 0f));
            screen.SetPropertyBlock(block);
            if (label != null)
                label.text = TrainingSubtitles.RearViewLabel;

            Active.Add(this);
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            running = true;
            UpdateView(0f);
            viewCamera.enabled = true;
        }

        void OnDisable()
        {
            if (!running)
                return;

            running = false;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Active.Remove(this);
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

        void LateUpdate() => UpdateView(Time.deltaTime);

        void OnBeginCamera(ScriptableRenderContext _, Camera renderingCamera)
        {
            screen.forceRenderingOff = IsObserverCamera(renderingCamera);
            // Latest head pose for this camera; URP reads the camera after this callback.
            if (renderingCamera == viewCamera)
                UpdateView(0f);
        }

        static bool IsObserverCamera(Camera camera)
        {
            foreach (var view in Active)
            {
                if (view.viewCamera == camera)
                    return true;
            }

            return false;
        }

        public bool TryValidate(out string error)
        {
            error = viewCamera == null || screen == null || head == null ? $"K1ObserverView '{name}': camera, screen or head missing."
                : viewCamera.transform == head || viewCamera.transform.IsChildOf(head) ? $"K1ObserverView '{name}': view camera must not be the HMD camera or under it."
                : screen.transform.IsChildOf(viewCamera.transform) ? $"K1ObserverView '{name}': screen must not be under its camera."
                : farClip <= 0.1f || rearDistance <= 0f ? $"K1ObserverView '{name}': far clip or rear distance out of range."
                : null;
            return error == null;
        }

        void UpdateView(float deltaTime)
        {
            if (!running)
                return;
            if (mode == Mode.Mirror)
                UpdateMirror();
            else
                UpdateRear(deltaTime);
        }

        // Off-axis frustum from the reflected eye through the quad (generalized perspective projection). Seen from behind the
        // mirror the corners swap left and right: lower-left pb, lower-right pa, upper-left pd. The camera looks along the
        // mirror normal toward the user, with the near plane exactly on the mirror plane.
        void UpdateMirror()
        {
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
            // Distance from the reflected eye to the plane (= the eye's distance to the mirror).
            var near = Vector3.Dot(va, normal);
            var far = Mathf.Max(farClip, near + 0.1f);

            viewCamera.transform.SetPositionAndRotation(reflected, Quaternion.LookRotation(normal, up));
            viewCamera.projectionMatrix = Matrix4x4.Frustum(Vector3.Dot(right, va), Vector3.Dot(right, vb), Vector3.Dot(up, va), Vector3.Dot(up, vc), near, far);
        }

        void UpdateRear(float deltaTime)
        {
            var heading = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (heading.sqrMagnitude > 1e-4f)
            {
                var targetYaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
                if (!hasYaw || rearTurnTime <= 0f)
                    rearYaw = targetYaw;
                else if (deltaTime > 0f)
                    rearYaw = Mathf.LerpAngle(rearYaw, targetYaw, 1f - Mathf.Exp(-deltaTime / rearTurnTime));
                hasYaw = true;
            }

            var forward = Quaternion.Euler(0f, rearYaw, 0f) * Vector3.forward;
            var focus = head.position - forward * headDepth;
            var position = focus - forward * rearDistance + Vector3.up * rearHeight;
            viewCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, Vector3.up));
        }
    }
}
