using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Smartomano.OptiWater
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class OptiWaterPlanarReflectionRenderer : MonoBehaviour
    {
        public const string OptiWaterReflectionTexName = "_OptiWaterReflectionTex";
        public const string OptiWaterMirrorVPName = "_OptiWaterMirrorVP";
        public const string OptiWaterPlanePosName = "_OptiWaterPlanePosWS";
        public const string OptiWaterPlaneNormalName = "_OptiWaterPlaneNormalWS";

        [Header("Reflection Settings")]
        [SerializeField] private int rtWidth = 1024;
        [SerializeField] private int rtHeight = 1024;
        [SerializeField] private LayerMask reflectionLayer = -1;
        [SerializeField] private float clipPlaneOffset = 0.05f;

        [Header("OptiWater Surface Reference")]
        [SerializeField] private Transform optiWaterSurface;

        public bool reflectionEnabled = true;

        private Camera reflectionCamera;
        private RenderTexture reflectionRT;
        private bool initialized;

        private int reflectionTexID;
        private int mirrorVPID;
        private int planePosID;
        private int planeNormalID;

        private static readonly HashSet<Camera> s_ReflectionCameras = new HashSet<Camera>();
        public static bool IsRenderingReflection { get; private set; }

        private void OnEnable()
        {
            ResolveSurface();
            reflectionCamera = GetComponent<Camera>();
            if (reflectionCamera == null) return;
            reflectionCamera.enabled = false;
            s_ReflectionCameras.Add(reflectionCamera);
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            if (reflectionCamera != null) s_ReflectionCameras.Remove(reflectionCamera);
        }

        // 解析水面 Transform：优先使用手动指定，其次按名字查找，最后回退到 OptiWaterController
        private void ResolveSurface()
        {
            if (optiWaterSurface != null) return;

            var found = GameObject.Find("OptiWaterSurface");
            if (found != null)
            {
                optiWaterSurface = found.transform;
                return;
            }

            var ctrl = Object.FindAnyObjectByType<OptiWaterController>();
            if (ctrl != null) optiWaterSurface = ctrl.transform;
        }

        // 水面高度一律取自水面 Transform（动态），不再使用任何写死的固定高度字段
        private float GetWaterSurfaceHeight()
        {
            ResolveSurface();
            if (optiWaterSurface != null)
                return optiWaterSurface.position.y;
            return 0f;
        }

        private void TryInit()
        {
            if (initialized) return;

            reflectionRT = new RenderTexture(rtWidth, rtHeight, 24, RenderTextureFormat.ARGB32);
            reflectionRT.name = "OptiWaterPlanarReflectionRT";
            reflectionRT.wrapMode = TextureWrapMode.Clamp;
            reflectionCamera.targetTexture = reflectionRT;

            reflectionTexID = Shader.PropertyToID(OptiWaterReflectionTexName);
            mirrorVPID = Shader.PropertyToID(OptiWaterMirrorVPName);
            planePosID = Shader.PropertyToID(OptiWaterPlanePosName);
            planeNormalID = Shader.PropertyToID(OptiWaterPlaneNormalName);

            var acd = reflectionCamera.GetUniversalAdditionalCameraData();
            if (acd != null)
            {
                acd.renderShadows = true;
                acd.renderPostProcessing = false;
                acd.requiresColorOption = CameraOverrideOption.Off;
                acd.requiresDepthOption = CameraOverrideOption.Off;
            }

            Shader.SetGlobalTexture(reflectionTexID, reflectionRT);
            initialized = true;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
        {
            if (s_ReflectionCameras.Contains(cam) || cam.cameraType == CameraType.Preview) return;
            if (cam == reflectionCamera) return;
            if (!isActiveAndEnabled) return;
            if (!reflectionEnabled) return;
            if (!initialized) TryInit();

            float h = GetWaterSurfaceHeight();
            Vector3 planePosWS = new Vector3(0, h, 0);
            Vector3 planeNormalWS = Vector3.up;

            var planeWS4 = new Vector4(planeNormalWS.x, planeNormalWS.y, planeNormalWS.z, -Vector3.Dot(planeNormalWS, planePosWS));
            Matrix4x4 reflectionM = CalculateReflectionMatrix(planeWS4);

            CopyCommonCameraSettings(cam, reflectionCamera);

            reflectionCamera.worldToCameraMatrix = cam.worldToCameraMatrix * reflectionM;

            Vector3 srcPos = cam.transform.position;
            Vector3 reflPos = ReflectPointAcrossPlane(srcPos, planePosWS, planeNormalWS);
            reflectionCamera.transform.position = reflPos;
            reflectionCamera.transform.forward = ReflectVectorAcrossPlane(cam.transform.forward, planeNormalWS);
            reflectionCamera.transform.up = ReflectVectorAcrossPlane(cam.transform.up, planeNormalWS);

            reflectionCamera.fieldOfView = cam.fieldOfView;
            reflectionCamera.targetTexture = reflectionRT;

            Vector4 clipPlaneCameraSpace = CameraSpacePlane(reflectionCamera, planePosWS, planeNormalWS, 1.0f, clipPlaneOffset);
            reflectionCamera.projectionMatrix = cam.CalculateObliqueMatrix(clipPlaneCameraSpace);
            reflectionCamera.cullingMask = reflectionLayer;

            GL.invertCulling = true;
            IsRenderingReflection = true;
            try
            {
                reflectionCamera.Render();
            }
            finally
            {
                IsRenderingReflection = false;
            }
            GL.invertCulling = false;

            Matrix4x4 vp = reflectionCamera.projectionMatrix * reflectionCamera.worldToCameraMatrix;
            Shader.SetGlobalMatrix(mirrorVPID, vp);

            Shader.SetGlobalVector(planePosID, new Vector4(planePosWS.x, planePosWS.y, planePosWS.z, 0));
            Shader.SetGlobalVector(planeNormalID, new Vector4(planeNormalWS.x, planeNormalWS.y, planeNormalWS.z, 0));
        }

        private void OnDestroy()
        {
            if (reflectionRT != null)
            {
                reflectionRT.Release();
                DestroyImmediate(reflectionRT);
            }
        }

        static void CopyCommonCameraSettings(Camera src, Camera dst)
        {
            dst.cameraType = CameraType.Game;
            dst.forceIntoRenderTexture = true;
            dst.useOcclusionCulling = src.useOcclusionCulling;
            dst.nearClipPlane = src.nearClipPlane;
            dst.farClipPlane = src.farClipPlane;
            dst.clearFlags = src.clearFlags;
            dst.backgroundColor = src.backgroundColor;

            var srcACD = src.GetUniversalAdditionalCameraData();
            var dstACD = dst.GetUniversalAdditionalCameraData();
            if (srcACD != null && dstACD != null)
            {
                dstACD.renderPostProcessing = srcACD.renderPostProcessing;
                dstACD.antialiasing = srcACD.antialiasing;
                dstACD.antialiasingQuality = srcACD.antialiasingQuality;
                dstACD.renderShadows = srcACD.renderShadows && !dst.orthographic;
            }
        }

        static Vector3 ReflectPointAcrossPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            Vector3 v = point - planePoint;
            float dist = Vector3.Dot(v, planeNormal);
            return point - 2f * dist * planeNormal;
        }

        static Vector3 ReflectVectorAcrossPlane(Vector3 dir, Vector3 planeNormal)
        {
            return Vector3.Reflect(dir, planeNormal);
        }

        static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = 1F - 2F * plane.x * plane.x;
            m.m01 = -2F * plane.x * plane.y;
            m.m02 = -2F * plane.x * plane.z;
            m.m03 = -2F * plane.w * plane.x;
            m.m10 = -2F * plane.y * plane.x;
            m.m11 = 1F - 2F * plane.y * plane.y;
            m.m12 = -2F * plane.y * plane.z;
            m.m13 = -2F * plane.w * plane.y;
            m.m20 = -2F * plane.z * plane.x;
            m.m21 = -2F * plane.z * plane.y;
            m.m22 = 1F - 2F * plane.z * plane.z;
            m.m23 = -2F * plane.w * plane.z;
            return m;
        }

        static Vector4 CameraSpacePlane(Camera cam, Vector3 planePointWS, Vector3 planeNormalWS, float sideSign, float clipPlaneOffset)
        {
            Vector3 offsetPos = planePointWS + planeNormalWS * clipPlaneOffset;
            Matrix4x4 worldToCamera = cam.worldToCameraMatrix;
            Vector3 cPos = worldToCamera.MultiplyPoint(offsetPos);
            Vector3 cNormal = worldToCamera.MultiplyVector(planeNormalWS).normalized * sideSign;
            return new Vector4(cNormal.x, cNormal.y, cNormal.z, -Vector3.Dot(cPos, cNormal));
        }
    }
}
