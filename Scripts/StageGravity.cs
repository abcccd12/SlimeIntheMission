using UnityEngine;
using System.Collections.Generic;

public class StageGravity : MonoBehaviour
{
    public enum Gravity
    {
        inner,
        outer
    }

    [SerializeField] private Gravity gravitytype = Gravity.inner;
    [SerializeField] private float strength = 20f;
    [SerializeField] private float radius = 5f;
    [SerializeField] private float playLineWidth = 0.08f;

    private static readonly List<StageGravity> _active = new List<StageGravity>();
    private static Material _lineMaterial;

    private readonly List<LineRenderer> _playLines = new List<LineRenderer>();
    private Transform _playRoot;
    private Gravity _builtType;
    private float _builtRadius = -1f;
    private float _builtWidth = -1f;

    private void OnEnable()
    {
        if (!_active.Contains(this)) _active.Add(this);
        EnsurePlayLines();
        if (_playRoot != null) _playRoot.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        _active.Remove(this);
        if (_playRoot != null) _playRoot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_playRoot != null) Destroy(_playRoot.gameObject);
    }

    public Vector3 GetAcceleration(Vector3 pos)
    {
        Vector3 toCenter = transform.position - pos;
        toCenter.z = 0f;
        float dist = toCenter.magnitude;

        if (dist > radius || dist < 0.0001f)
            return Vector3.zero;

        float falloff = 1f - (dist / radius);
        Vector3 dir = toCenter / dist;

        if (gravitytype == Gravity.outer)
            dir = -dir;

        return dir * strength * falloff;
    }

    public static Vector3 SumAcceleration(Vector3 pos)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < _active.Count; i++)
            sum += _active[i].GetAcceleration(pos);
        return sum;
    }

    private void LateUpdate()
    {
        EnsurePlayLines();
        if (_playRoot == null) return;

        _playRoot.position = transform.position;
        _playRoot.rotation = Quaternion.identity;
        _playRoot.localScale = Vector3.one;
    }

    private void EnsurePlayLines()
    {
        if (_playRoot == null)
        {
            var go = new GameObject("GravityPlayGizmo");
            go.hideFlags = HideFlags.DontSave;
            _playRoot = go.transform;
        }

        bool dirty = _builtRadius != radius
                     || _builtType != gravitytype
                     || _builtWidth != playLineWidth
                     || _playLines.Count == 0;
        if (!dirty) return;

        _builtRadius = radius;
        _builtType = gravitytype;
        _builtWidth = playLineWidth;

        List<Vector3> starts = new List<Vector3>();
        List<Vector3> ends = new List<Vector3>();
        CollectLines(Vector3.zero, radius, starts, ends);

        Color color = (gravitytype == Gravity.inner) ? Color.cyan : Color.darkRed;
        EnsureLinePool(starts.Count, color);

        for (int i = 0; i < starts.Count; i++)
        {
            LineRenderer lr = _playLines[i];
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = playLineWidth;
            lr.endWidth = playLineWidth;
            lr.positionCount = 2;
            lr.SetPosition(0, starts[i]);
            lr.SetPosition(1, ends[i]);
            lr.enabled = true;
        }

        for (int i = starts.Count; i < _playLines.Count; i++)
            _playLines[i].enabled = false;
    }

    private void EnsureLinePool(int count, Color color)
    {
        while (_playLines.Count < count)
        {
            var go = new GameObject("line");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(_playRoot, false);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = GetLineMaterial();
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.allowOcclusionWhenDynamic = false;
            lr.numCapVertices = 2;
            lr.textureMode = LineTextureMode.Stretch;
            lr.startColor = color;
            lr.endColor = color;
            _playLines.Add(lr);
        }
    }

    private static Material GetLineMaterial()
    {
        if (_lineMaterial != null) return _lineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");

        _lineMaterial = new Material(shader);
        _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        _lineMaterial.color = Color.white;
        if (_lineMaterial.HasProperty("_BaseColor"))
            _lineMaterial.SetColor("_BaseColor", Color.white);
        return _lineMaterial;
    }

    private void CollectLines(Vector3 center, float r, List<Vector3> starts, List<Vector3> ends)
    {
        const int circleSegments = 40;
        float angleStep = 360f / circleSegments;
        for (int i = 0; i < circleSegments; i += 2)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;
            starts.Add(center + new Vector3(Mathf.Cos(angle1), Mathf.Sin(angle1), 0f) * r);
            ends.Add(center + new Vector3(Mathf.Cos(angle2), Mathf.Sin(angle2), 0f) * r);
        }

        const int arrowCount = 8;
        float arrowAngleStep = 360f / arrowCount;
        float arrowHeadLength = r * 0.07f;
        const float arrowHeadAngle = 30f;

        for (int i = 0; i < arrowCount; i++)
        {
            float angle = i * arrowAngleStep * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 innerPos = center + dir * (r * 0.4f);
            Vector3 outerPos = center + dir * r;

            if (gravitytype == Gravity.inner)
                AddArrow(outerPos, innerPos, arrowHeadLength, arrowHeadAngle, starts, ends);
            else
                AddArrow(innerPos, outerPos, arrowHeadLength, arrowHeadAngle, starts, ends);
        }
    }

    private static void AddArrow(Vector3 start, Vector3 end, float headLength, float headAngle,
        List<Vector3> starts, List<Vector3> ends)
    {
        starts.Add(start);
        ends.Add(end);

        Vector3 dir = (end - start).normalized;
        Vector3 right = Quaternion.Euler(0f, 0f, headAngle) * -dir;
        Vector3 left = Quaternion.Euler(0f, 0f, -headAngle) * -dir;

        starts.Add(end);
        ends.Add(end + right * headLength);
        starts.Add(end);
        ends.Add(end + left * headLength);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        Gizmos.color = (gravitytype == Gravity.inner) ? Color.cyan : Color.darkRed;
        List<Vector3> starts = new List<Vector3>();
        List<Vector3> ends = new List<Vector3>();
        CollectLines(transform.position, radius, starts, ends);
        for (int i = 0; i < starts.Count; i++)
            Gizmos.DrawLine(starts[i], ends[i]);
    }
#endif
}
