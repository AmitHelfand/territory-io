using UnityEngine;

namespace TerritoryIO.View
{
    /// <summary>Smoothly follows a target on the XY plane with an orthographic camera.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float Smooth = 8f;

        public void SnapToTarget()
        {
            if (Target == null) return;
            var p = Target.position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        private void LateUpdate()
        {
            if (Target == null || !Target.gameObject.activeInHierarchy) return;
            Vector3 p = Target.position;
            Vector3 goal = new Vector3(p.x, p.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-Smooth * Time.deltaTime));
        }
    }
}
