using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class LiftBox : MonoBehaviour
{
    [SerializeField] private Transform boxRoot;
    [SerializeField] private Transform targetPoint;
    [SerializeField, Min(0.1f)] private float riseDistance = 25f;
    [SerializeField, Min(0.1f)] private float riseDuration = 8f;

    public UnityEvent onArrived = new UnityEvent();
    public bool IsRising { get; private set; }

    private void Awake()
    {
        if (boxRoot == null)
            boxRoot = transform;
    }

    public void Configure(Transform root, Transform target, float distance, float duration)
    {
        boxRoot = root;
        targetPoint = target;
        riseDistance = Mathf.Max(0.1f, distance);
        riseDuration = Mathf.Max(0.1f, duration);
    }

    public IEnumerator RaiseToTarget()
    {
        if (boxRoot == null)
        {
            Debug.LogError($"[{nameof(LiftBox)}] No lift box root is assigned.", this);
            yield break;
        }

        Vector3 startPosition = boxRoot.position;
        Vector3 endPosition = targetPoint != null
            ? targetPoint.position
            : startPosition + Vector3.up * riseDistance;

        IsRising = true;
        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / riseDuration));
            boxRoot.position = Vector3.LerpUnclamped(startPosition, endPosition, t);
            yield return null;
        }

        boxRoot.position = endPosition;
        IsRising = false;
        onArrived.Invoke();
    }
}
