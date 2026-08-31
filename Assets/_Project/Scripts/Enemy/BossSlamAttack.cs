using System.Collections;
using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Enemy
{
    public class BossSlamAttack : MonoBehaviour
    {
        // The slam only ever needs the player, so the query is filtered to the Player
        // layer instead of collecting every collider in the blast radius.
        private static ContactFilter2D _playerFilter;
        private static bool _playerFilterReady;

        private static ContactFilter2D PlayerFilter
        {
            get
            {
                if (!_playerFilterReady)
                {
                    var mask = LayerMask.GetMask("Player");
                    if (mask == 0)
                    {
                        Debug.LogError("Layer 'Player' is not defined in Tags & Layers. " +
                                       "Boss slam falls back to all layers until it is added.");
                        mask = Physics2D.AllLayers;
                    }

                    _playerFilter = new ContactFilter2D
                    {
                        useLayerMask = true,
                        layerMask = mask,
                        useTriggers = true,
                    };
                    _playerFilterReady = true;
                }

                return _playerFilter;
            }
        }

        private readonly List<Collider2D> _hitBuffer = new();

        private const int CircleSegments = 24;

        [SerializeField] private float interval = 4f;
        [SerializeField] private float radius = 3f;
        [SerializeField] private int damage = 25;
        [SerializeField] private float telegraphDuration = 0.6f;
        [SerializeField] private Color telegraphColor = new(1f, 0.2f, 0.2f, 0.35f);

        private float _timer;
        private Transform _telegraph;
        private Mesh _telegraphMesh;
        private bool _isSlamming;

        private void Awake()
        {
            CreateTelegraph();
        }

        private void OnDestroy()
        {
            if (_telegraph != null) Destroy(_telegraph.gameObject);
        }

        private void CreateTelegraph()
        {
            var telegraphObject = new GameObject("BossSlamTelegraph (Temp)");
            var meshFilter = telegraphObject.AddComponent<MeshFilter>();
            var meshRenderer = telegraphObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Sprites/Default")) { color = telegraphColor };
            meshRenderer.sortingOrder = 1;

            _telegraphMesh = new Mesh();
            meshFilter.mesh = _telegraphMesh;
            _telegraph = telegraphObject.transform;
            telegraphObject.SetActive(false);
        }

        private void Update()
        {
            if (_isSlamming) return;

            _timer += Time.deltaTime;
            if (_timer < interval) return;

            _timer = 0f;
            StartCoroutine(SlamRoutine());
        }

        private IEnumerator SlamRoutine()
        {
            _isSlamming = true;

            BuildCircleMesh();
            _telegraph.position = transform.position;
            _telegraph.gameObject.SetActive(true);

            yield return new WaitForSeconds(telegraphDuration);

            _telegraph.gameObject.SetActive(false);
            ApplyDamage();

            _isSlamming = false;
        }

        private void ApplyDamage()
        {
            var count = Physics2D.OverlapCircle(transform.position, radius, PlayerFilter, _hitBuffer);
            for (var i = 0; i < count; i++)
            {
                var hit = _hitBuffer[i];
                if (!hit.CompareTag("Player")) continue;

                if (hit.TryGetComponent<PlayerHealth>(out var health))
                {
                    health.TakeDamage(damage);
                }
            }
        }

        private void BuildCircleMesh()
        {
            var vertices = new Vector3[CircleSegments + 2];
            var triangles = new int[CircleSegments * 3];

            vertices[0] = Vector3.zero;
            for (var i = 0; i <= CircleSegments; i++)
            {
                var t = (float)i / CircleSegments;
                var segmentAngle = t * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(segmentAngle), Mathf.Sin(segmentAngle), 0f) * radius;
            }

            for (var i = 0; i < CircleSegments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            _telegraphMesh.Clear();
            _telegraphMesh.vertices = vertices;
            _telegraphMesh.triangles = triangles;
            _telegraphMesh.RecalculateBounds();
        }
    }
}
