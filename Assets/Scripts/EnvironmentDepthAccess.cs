using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using static OVRPlugin;
using static Unity.XR.Oculus.Utils;

public class EnvironmentDepthAccess : MonoBehaviour
{
    private static readonly int raycastResultsId = Shader.PropertyToID("RaycastResults");
    private static readonly int raycastRequestsId = Shader.PropertyToID("RaycastRequests");

    [SerializeField] private ComputeShader _computeShader;

    private ComputeBuffer _requestsCB;
    private ComputeBuffer _resultsCB;
    private readonly Matrix4x4[] _threeDofReprojectionMatrices = new Matrix4x4[2];

    public struct DepthRaycastResult
    {
        public Vector3 Position;
        public Vector3 Normal;
    }

    /**
     * Perform a raycast at multiple view space coordinates and fill the result list.
     * Blocking means that this function will immediately return the result but is performance heavy.
     * List is expected to be the size of the requested coordinates.
     */
    public void RaycastViewSpaceBlocking(List<Vector2> viewSpaceCoords, out List<DepthRaycastResult> result)
    {
        result = DispatchCompute(viewSpaceCoords);
    }

    /**
     * Perform a raycast at a view space coordinate and return the result.
     * Blocking means that this function will immediately return the result but is performance heavy.
     */
    public DepthRaycastResult RaycastViewSpaceBlocking(Vector2 viewSpaceCoord)
    {
        var depthRaycastResult = DispatchCompute(new List<Vector2>() { viewSpaceCoord });
        return depthRaycastResult[0];
    }


    private List<DepthRaycastResult> DispatchCompute(List<Vector2> requestedPositions)
    {
        UpdateCurrentRenderingState();

        int count = requestedPositions.Count;
        int threads = Mathf.CeilToInt(count / 32f);

        var (requestsCB, resultsCB) = GetComputeBuffers(count);
        requestsCB.SetData(requestedPositions);

        _computeShader.SetBuffer(0, raycastRequestsId, requestsCB);
        _computeShader.SetBuffer(0, raycastResultsId, resultsCB);

        _computeShader.Dispatch(0, threads, 1, 1);

        var raycastResults = new DepthRaycastResult[count];
        resultsCB.GetData(raycastResults);

        return raycastResults.ToList();
    }

    (ComputeBuffer, ComputeBuffer) GetComputeBuffers(int size)
    {
        if (_requestsCB != null && _resultsCB != null && _requestsCB.count != size)
        {
            _requestsCB.Release();
            _requestsCB = null;
            _resultsCB.Release();
            _resultsCB = null;
        }

        if (_requestsCB == null || _resultsCB == null)
        {
            _requestsCB = new ComputeBuffer(size, Marshal.SizeOf<Vector2>(), ComputeBufferType.Structured);
            _resultsCB = new ComputeBuffer(size, Marshal.SizeOf<DepthRaycastResult>(),
                ComputeBufferType.Structured);
        }

        return (_requestsCB, _resultsCB);
    }

    private void UpdateCurrentRenderingState()
    {
        var leftEyeData = GetEnvironmentDepthFrameDesc(0);
        var rightEyeData = GetEnvironmentDepthFrameDesc(1);

        OVRPlugin.GetNodeFrustum2(OVRPlugin.Node.EyeLeft, out var leftEyeFrustrum);
        OVRPlugin.GetNodeFrustum2(OVRPlugin.Node.EyeRight, out var rightEyeFrustrum);

        // Get the center eye FOV
        OVRPlugin.GetNodeFrustum2(OVRPlugin.Node.EyeCenter, out var centerEyeFrustrum);
        _threeDofReprojectionMatrices[0] = Calculate3DOFReprojection(leftEyeData, leftEyeFrustrum.Fov, centerEyeFrustrum.Fov);
        _threeDofReprojectionMatrices[1] = Calculate3DOFReprojection(rightEyeData, rightEyeFrustrum.Fov, centerEyeFrustrum.Fov);

        _computeShader.SetTextureFromGlobal(0, Shader.PropertyToID("_EnvironmentDepthTexture"),
            Shader.PropertyToID("_EnvironmentDepthTexture"));
        _computeShader.SetMatrixArray(Shader.PropertyToID("_EnvironmentDepthReprojectionMatrices"),
            _threeDofReprojectionMatrices);
        _computeShader.SetVector(Shader.PropertyToID("_EnvironmentDepthZBufferParams"),
            Shader.GetGlobalVector(Shader.PropertyToID("_EnvironmentDepthZBufferParams")));

        // See UniversalRenderPipelineCore for property IDs
        _computeShader.SetVector("_ZBufferParams", Shader.GetGlobalVector("_ZBufferParams"));
        _computeShader.SetMatrixArray("unity_StereoMatrixInvVP",
            Shader.GetGlobalMatrixArray("unity_StereoMatrixInvVP"));
    }

    private void OnDestroy()
    {
        _resultsCB.Release();
    }

    internal static Matrix4x4 Calculate3DOFReprojection(EnvironmentDepthFrameDesc frameDesc, Fovf eyeFov, Fovf centerFov)
    {
        // Screen To Depth represents the transformation matrix used to map normalised screen UV coordinates to
        // normalised environment depth texture UV coordinates. This needs to account for 2 things:
        // 1. The field of view of the two textures may be different, Unreal typically renders using a symmetric fov.
        //    That is to say the FOV of the left and right eyes is the same. The environment depth on the other hand
        //    has a different FOV for the left and right eyes. So we need to scale and offset accordingly to account
        //    for this difference.
        var screenCameraToScreenNormCoord = MakeUnprojectionMatrix(
            eyeFov.RightTan, eyeFov.LeftTan,
            eyeFov.UpTan, eyeFov.DownTan);

        var depthNormCoordToDepthCamera = MakeProjectionMatrix(
            frameDesc.fovRightAngle, frameDesc.fovLeftAngle,
            frameDesc.fovTopAngle, frameDesc.fovDownAngle);

        // 2. The headset may have moved in between capturing the environment depth and rendering the frame. We
        //    can only account for rotation of the headset, not translation.
        var depthCameraToScreenCamera = MakeScreenToDepthMatrix(frameDesc);

        // Adjust for center eye FOV
        var centerEyeAdjustment = MakeCenterEyeAdjustmentMatrix(eyeFov, centerFov);
        var screenToDepth = depthNormCoordToDepthCamera * depthCameraToScreenCamera * centerEyeAdjustment * screenCameraToScreenNormCoord;

        return screenToDepth;
    }

    private static Matrix4x4 MakeCenterEyeAdjustmentMatrix(Fovf eyeFov, Fovf centerFov)
    {
        float eyeHorizontalFov = Mathf.Atan(eyeFov.LeftTan) + Mathf.Atan(eyeFov.RightTan);
        float centerHorizontalFov = Mathf.Atan(centerFov.LeftTan) + Mathf.Atan(centerFov.RightTan);
        float scaleFactor = eyeHorizontalFov / centerHorizontalFov;
        var matrix = Matrix4x4.identity;
        matrix.m00 = scaleFactor;

        // Adjust the offset to center the scaled view
        matrix.m03 = (1 - scaleFactor) * 0.5f;
        return matrix;
    }


    private static Matrix4x4 MakeScreenToDepthMatrix(EnvironmentDepthFrameDesc frameDesc)
    {
        // The pose extrapolated to the predicted display time of the current frame
        // assuming left eye rotation == right eye
        var screenOrientation =
            GetNodePose(Node.EyeLeft, Step.Render).Orientation.FromQuatf();

        var depthOrientation = new Quaternion(
            -frameDesc.createPoseRotation.x,
            -frameDesc.createPoseRotation.y,
            frameDesc.createPoseRotation.z,
            frameDesc.createPoseRotation.w
        );

        var screenToDepthQuat = (Quaternion.Inverse(screenOrientation) * depthOrientation).eulerAngles;
        screenToDepthQuat.z = -screenToDepthQuat.z;

        return Matrix4x4.Rotate(Quaternion.Euler(screenToDepthQuat));
    }

    private static Matrix4x4 MakeProjectionMatrix(float rightTan, float leftTan, float upTan, float downTan)
    {
        var matrix = Matrix4x4.identity;
        float tanAngleWidth = rightTan + leftTan;
        float tanAngleHeight = upTan + downTan;

        // Scale
        matrix.m00 = 1.0f / tanAngleWidth;
        matrix.m11 = 1.0f / tanAngleHeight;

        // Offset
        matrix.m03 = leftTan / tanAngleWidth;
        matrix.m13 = downTan / tanAngleHeight;
        matrix.m23 = -1.0f;

        return matrix;
    }

    private static Matrix4x4 MakeUnprojectionMatrix(float rightTan, float leftTan, float upTan, float downTan)
    {
        var matrix = Matrix4x4.identity;

        // Scale
        matrix.m00 = rightTan + leftTan;
        matrix.m11 = upTan + downTan;

        // Offset
        matrix.m03 = -leftTan;
        matrix.m13 = -downTan;
        matrix.m23 = 1.0f;

        return matrix;
    }
}
