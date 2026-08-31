using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Swarm.Game
{
    public class TitleManager : MonoBehaviour
    {
        private const string SelectedCharacterKey = "Swarm_SelectedCharacterId";

        [SerializeField] private Text goldText;
        [SerializeField] private CharacterDefinition[] characters;
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private ShopSlot slotPrefab;
        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private string testStageSceneName = "TestStage";

        private const float SlotHeight = 100f;

        private void OnEnable()
        {
            // The shop panel spends gold through its own UI, so this screen's total would sit at
            // the pre-purchase value until the scene reloaded.
            GoldWallet.OnChanged += UpdateGoldText;
        }

        private void OnDisable()
        {
            GoldWallet.OnChanged -= UpdateGoldText;
        }

        private void UpdateGoldText()
        {
            if (goldText != null) goldText.text = $"보유 골드: {GoldWallet.Current}";
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(PlayerPrefs.GetString(SelectedCharacterKey, "")) && characters.Length > 0)
            {
                PlayerPrefs.SetString(SelectedCharacterKey, characters[0].Id);
                PlayerPrefs.Save();
            }

            RefreshUI();
        }

        public void StartGame()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        public void OpenTestStage()
        {
            SceneManager.LoadScene(testStageSceneName);
        }

        public void ResetProgress()
        {
            PlayerPrefs.DeleteAll();
            GoldWallet.Invalidate();

            if (characters.Length > 0)
            {
                PlayerPrefs.SetString(SelectedCharacterKey, characters[0].Id);
            }

            PlayerPrefs.Save();
            RefreshUI();
        }

        private void RefreshUI()
        {
            UpdateGoldText();

            for (int i = slotContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(slotContainer.GetChild(i).gameObject);
            }

            string selectedId = PlayerPrefs.GetString(SelectedCharacterKey, "");
            float startY = (characters.Length - 1) * SlotHeight * 0.5f;

            for (int i = 0; i < characters.Length; i++)
            {
                CharacterDefinition character = characters[i];
                ShopSlot slot = Instantiate(slotPrefab, slotContainer);
                slot.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, startY - i * SlotHeight);

                bool isSelected = character.Id == selectedId;
                string label = $"{character.DisplayName}" + (character.IsUnlocked ? "" : $"\n해금 비용: {character.UnlockCost} G");

                if (!character.IsUnlocked)
                {
                    slot.Setup(label, "해금", true, () =>
                    {
                        character.TryUnlock();
                        RefreshUI();
                    });
                }
                else if (isSelected)
                {
                    slot.Setup(label, "선택됨", false, null);
                }
                else
                {
                    slot.Setup(label, "선택", true, () =>
                    {
                        PlayerPrefs.SetString(SelectedCharacterKey, character.Id);
                        PlayerPrefs.Save();
                        RefreshUI();
                    });
                }
            }
        }
    }
}
