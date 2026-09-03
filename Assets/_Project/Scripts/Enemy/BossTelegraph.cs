using UnityEngine;

namespace Swarm.Enemy
{
    /// <summary>
    /// A flat red circle drawn on the ground to warn where an attack is about to land.
    /// Every boss pattern needs one and they all need it to behave identically, so the mesh
    /// building lives here rather than being copied into each attack script.
    /// </summary>
    public class BossTelegraph
    {
        private const int CircleSegments = 24;

        private readonly Transform _transform;
        private readonly Mesh _mesh;
        private readonly MeshRenderer _renderer;

        private float _builtRadius = -1f;

        // The telegraph used to be left on the default sorting layer, which this project puts
        // BELOW "Ground" — so the warning circle was drawn underneath the floor and never appeared
        // on screen at all. "Decal" is the layer meant for markings painted on the ground: above
        // the floor, below the characters standing on it.
        private const string GroundMarkingLayer = "Decal";

        // The ground art of a pattern may be drawn in 3/4 perspective rather than flat top-down, in
        // which case a true circle on the floor reads as an ellipse on screen. Squashing the mesh
        // vertically by the same amount as the art keeps the warning and the dust the same shape.
        // 1 leaves the circle alone, which is what every pattern using flat art wants.
        private readonly float _verticalSquash;

        public BossTelegraph(string name, Color color, int sortingOrder = 10, float verticalSquash = 1f)
        {
            _verticalSquash = Mathf.Max(0.01f, verticalSquash);

            var telegraphObject = new GameObject(name);
            var meshFilter = telegraphObject.AddComponent<MeshFilter>();
            _renderer = telegraphObject.AddComponent<MeshRenderer>();
            _renderer.material = new Material(Shader.Find("Sprites/Default")) { color = color };

            if (SortingLayer.NameToID(GroundMarkingLayer) != 0)
            {
                _renderer.sortingLayerName = GroundMarkingLayer;
            }
            else
            {
                Debug.LogWarning($"Sorting layer '{GroundMarkingLayer}' is missing, so the boss " +
                                 "telegraph may be hidden behind the ground.");
            }

            _renderer.sortingOrder = sortingOrder;

            _mesh = new Mesh();
            meshFilter.mesh = _mesh;
            _transform = telegraphObject.transform;
            telegraphObject.SetActive(false);
        }

        /// <summary>Where the circle currently sits. Patterns damage against this rather than
        /// against the boss, so what the player sees is exactly what the attack hits.</summary>
        public Vector3 Position => _transform.position;

        public void Show(Vector3 position, float radius)
        {
            if (!Mathf.Approximately(radius, _builtRadius)) BuildMesh(radius);

            SetFill(0f);
            _transform.position = position;
            _transform.localScale = new Vector3(1f, _verticalSquash, 1f);
            _transform.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_transform != null) _transform.gameObject.SetActive(false);
        }

        /// <summary>Fades the circle in over its lifetime so the moment of impact is readable
        /// rather than arriving out of a flat, unchanging shape.</summary>
        public void SetFill(float t)
        {
            var color = _renderer.material.color;
            color.a = Mathf.Lerp(0.25f, 0.7f, Mathf.Clamp01(t));
            _renderer.material.color = color;
        }

        public void Destroy()
        {
            if (_transform != null) Object.Destroy(_transform.gameObject);
        }

        private void BuildMesh(float radius)
        {
            var vertices = new Vector3[CircleSegments + 2];
            var triangles = new int[CircleSegments * 3];

            vertices[0] = Vector3.zero;
            for (var i = 0; i <= CircleSegments; i++)
            {
                var segmentAngle = (float)i / CircleSegments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(segmentAngle), Mathf.Sin(segmentAngle), 0f) * radius;
            }

            for (var i = 0; i < CircleSegments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            _builtRadius = radius;
        }
    }
}
