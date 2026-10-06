using UnityEngine;

namespace PreRenderBackgrounds
{
    public class PreRenderedUtils : MonoBehaviour
    {
        public static void DrawCameraFrustum(Camera cam)
        {
            if (cam == null) return;

            // Store frustum corners
            Vector3[] nearCorners = new Vector3[4];
            Vector3[] farCorners = new Vector3[4];

            // Get near and far plane corners in camera space
            cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), cam.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearCorners);
            cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), cam.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, farCorners);

            // Convert to world space
            Transform camTransform = cam.transform;
            for (int i = 0; i < 4; i++)
            {
                nearCorners[i] = camTransform.TransformPoint(nearCorners[i]);
                farCorners[i] = camTransform.TransformPoint(farCorners[i]);
            }

            // Draw near plane (red)
            Debug.DrawLine(nearCorners[0], nearCorners[1], Color.red);
            Debug.DrawLine(nearCorners[1], nearCorners[2], Color.red);
            Debug.DrawLine(nearCorners[2], nearCorners[3], Color.red);
            Debug.DrawLine(nearCorners[3], nearCorners[0], Color.red);

            // Draw far plane (blue)
            Debug.DrawLine(farCorners[0], farCorners[1], Color.blue);
            Debug.DrawLine(farCorners[1], farCorners[2], Color.blue);
            Debug.DrawLine(farCorners[2], farCorners[3], Color.blue);
            Debug.DrawLine(farCorners[3], farCorners[0], Color.blue);

            // Connect near and far planes (green)
            Debug.DrawLine(nearCorners[0], farCorners[0], Color.green);
            Debug.DrawLine(nearCorners[1], farCorners[1], Color.green);
            Debug.DrawLine(nearCorners[2], farCorners[2], Color.green);
            Debug.DrawLine(nearCorners[3], farCorners[3], Color.green);
        }

        public static Matrix4x4 GetClippingProjectionMatrix(Camera camera, Rect clippingRect)
        {
            Matrix4x4 m = new Matrix4x4();

            PreRenderedUtils.CalculateFrustumBounds(camera, clippingRect, out float leftNear, out float rightNear,
                            out float bottomNear, out float topNear, out float leftFar, out float rightFar,
                            out float bottomFar, out float topFar);

            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;

            PreRenderedUtils.DrawDebugFrustum(camera.transform, near, far, leftNear, rightNear, bottomNear, topNear, leftFar, rightFar, bottomFar, topFar);

            m[0, 0] = (2.0f * near) / (rightNear - leftNear);
            m[0, 1] = 0.0f;
            m[0, 2] = (rightNear + leftNear) / (rightNear - leftNear);
            m[0, 3] = 0.0f;

            m[1, 0] = 0.0f;
            m[1, 1] = (2.0f * near) / (topNear - bottomNear);
            m[1, 2] = (topNear + bottomNear) / (topNear - bottomNear);
            m[1, 3] = 0.0f;

            m[2, 0] = 0.0f;
            m[2, 1] = 0.0f;
            m[2, 2] = -(far + near) / (far - near);
            m[2, 3] = -(2.0f * far * near) / (far - near);

            m[3, 0] = 0.0f;
            m[3, 1] = 0.0f;
            m[3, 2] = -1.0f;
            m[3, 3] = 0.0f;

            return m;
        }

        public static void CalculateFrustumBounds(Camera camera, Rect clippingRect, out float leftNear, out float rightNear,
                            out float bottomNear, out float topNear, out float leftFar, out float rightFar,
                            out float bottomFar, out float topFar)
        {
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;
            float fov = camera.fieldOfView;
            float aspect = camera.aspect;

            // Compute the full near plane dimensions
            float tanFov = Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
            float nearHeight = 2.0f * near * tanFov;
            float nearWidth = nearHeight * aspect;

            // Compute the full far plane dimensions
            float farHeight = 2.0f * far * tanFov;
            float farWidth = farHeight * aspect;

            // Convert clipping rect (0 to 1) into [-1, 1] NDC space
            float xMinNDC = clippingRect.x * 2.0f - 1.0f;
            float xMaxNDC = (clippingRect.x + clippingRect.width) * 2.0f - 1.0f;
            float yMinNDC = clippingRect.y * 2.0f - 1.0f;
            float yMaxNDC = (clippingRect.y + clippingRect.height) * 2.0f - 1.0f;

            // Scale to near plane
            leftNear = Mathf.Lerp(-nearWidth * 0.5f, nearWidth * 0.5f, (xMinNDC + 1) * 0.5f);
            rightNear = Mathf.Lerp(-nearWidth * 0.5f, nearWidth * 0.5f, (xMaxNDC + 1) * 0.5f);
            bottomNear = Mathf.Lerp(-nearHeight * 0.5f, nearHeight * 0.5f, (yMinNDC + 1) * 0.5f);
            topNear = Mathf.Lerp(-nearHeight * 0.5f, nearHeight * 0.5f, (yMaxNDC + 1) * 0.5f);

            // Scale to far plane
            /* xMinNDC = -1;
             xMaxNDC = 1;
             yMinNDC = -1;
             yMaxNDC = 1;*/
            leftFar = Mathf.Lerp(-farWidth * 0.5f, farWidth * 0.5f, (xMinNDC + 1) * 0.5f);
            rightFar = Mathf.Lerp(-farWidth * 0.5f, farWidth * 0.5f, (xMaxNDC + 1) * 0.5f);
            bottomFar = Mathf.Lerp(-farHeight * 0.5f, farHeight * 0.5f, (yMinNDC + 1) * 0.5f);
            topFar = Mathf.Lerp(-farHeight * 0.5f, farHeight * 0.5f, (yMaxNDC + 1) * 0.5f);

        }

        public static void DrawDebugFrustum(Transform camTransform, float near, float far, float leftNear, float rightNear,
                            float bottomNear, float topNear, float leftFar, float rightFar,
                            float bottomFar, float topFar)
        {
            Vector3 nearTopLeft = camTransform.position + camTransform.forward * near + camTransform.up * topNear + camTransform.right * leftNear;
            Vector3 nearTopRight = camTransform.position + camTransform.forward * near + camTransform.up * topNear + camTransform.right * rightNear;
            Vector3 nearBottomLeft = camTransform.position + camTransform.forward * near + camTransform.up * bottomNear + camTransform.right * leftNear;
            Vector3 nearBottomRight = camTransform.position + camTransform.forward * near + camTransform.up * bottomNear + camTransform.right * rightNear;

            Vector3 farTopLeft = camTransform.position + camTransform.forward * far + camTransform.up * topFar + camTransform.right * leftFar;
            Vector3 farTopRight = camTransform.position + camTransform.forward * far + camTransform.up * topFar + camTransform.right * rightFar;
            Vector3 farBottomLeft = camTransform.position + camTransform.forward * far + camTransform.up * bottomFar + camTransform.right * leftFar;
            Vector3 farBottomRight = camTransform.position + camTransform.forward * far + camTransform.up * bottomFar + camTransform.right * rightFar;

            // Draw near plane
            Debug.DrawLine(nearTopLeft, nearTopRight, Color.red);
            Debug.DrawLine(nearTopRight, nearBottomRight, Color.red);
            Debug.DrawLine(nearBottomRight, nearBottomLeft, Color.red);
            Debug.DrawLine(nearBottomLeft, nearTopLeft, Color.red);

            // Draw far plane
            Debug.DrawLine(farTopLeft, farTopRight, Color.blue);
            Debug.DrawLine(farTopRight, farBottomRight, Color.blue);
            Debug.DrawLine(farBottomRight, farBottomLeft, Color.blue);
            Debug.DrawLine(farBottomLeft, farTopLeft, Color.blue);

            // Connect near and far planes
            Debug.DrawLine(nearTopLeft, farTopLeft, Color.green);
            Debug.DrawLine(nearTopRight, farTopRight, Color.green);
            Debug.DrawLine(nearBottomLeft, farBottomLeft, Color.green);
            Debug.DrawLine(nearBottomRight, farBottomRight, Color.green);
        }
    }
}