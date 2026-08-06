using UnityEngine;
using UnityEngine.SceneManagement;

namespace Swarm.Game
{
    public class TestStageManager : MonoBehaviour
    {
        private const string SelectedCharacterKey = "Swarm_SelectedCharacterId";

        [SerializeField] private CharacterDefinition[] characters;
        [SerializeField] private PassiveUpgrade[] passiveUpgrades;
        [SerializeField] private string titleSceneName = "Title";

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            string selectedId = PlayerPrefs.GetString(SelectedCharacterKey, "");
            CharacterDefinition character = System.Array.Find(characters, c => c.Id == selectedId) ??
                                             (characters.Length > 0 ? characters[0] : null);
            character?.ApplyToPlayer(player);

            foreach (var upgrade in passiveUpgrades)
            {
                if (upgrade != null)
                {
                    upgrade.ApplyToPlayer(player);
                }
            }
        }

        public void GoToTitle()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(titleSceneName);
        }
    }
}
