using System.Collections.Generic;
using Swarm.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.Game
{
    public class DummySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject dummyPrefab;
        [SerializeField] private InputField healthInput;
        [SerializeField] private InputField defenseInput;
        [SerializeField] private InputField magicDefenseInput;
        [SerializeField] private float screenMargin = 0.1f;

        private readonly List<GameObject> _dummies = new();

        public void SpawnDummy()
        {
            if (dummyPrefab == null) return;

            var health = int.TryParse(healthInput.text, out var parsedHealth) ? parsedHealth : 1000;
            var defense = (int.TryParse(defenseInput.text, out var parsedDefense) ? parsedDefense : 0) / 100f;
            var magicDefense = (int.TryParse(magicDefenseInput.text, out var parsedMagicDefense) ? parsedMagicDefense : 0) / 100f;

            var dummy = Instantiate(dummyPrefab, GetRandomVisiblePosition(), Quaternion.identity);
            _dummies.Add(dummy);

            if (dummy.TryGetComponent<EnemyHealth>(out var enemyHealth))
            {
                enemyHealth.Configure(health, defense, magicDefense);
            }
        }

        private Vector2 GetRandomVisiblePosition()
        {
            var camera = Camera.main;
            if (camera == null) return Vector2.zero;

            var distance = Mathf.Abs(camera.transform.position.z);
            var viewportPoint = new Vector3(
                Random.Range(screenMargin, 1f - screenMargin),
                Random.Range(screenMargin, 1f - screenMargin),
                distance);

            return camera.ViewportToWorldPoint(viewportPoint);
        }
    }
}
