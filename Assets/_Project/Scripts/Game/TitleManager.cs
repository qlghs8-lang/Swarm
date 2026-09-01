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

        private SlotListView _list;

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

            _list ??= SlotListView.Attach(slotContainer, slotPrefab);

            string selectedId = PlayerPrefs.GetString(SelectedCharacterKey, "");

            _list.Rebuild(characters.Length, (i, slot) =>
            {
                CharacterDefinition character = characters[i];
                if (character == null) return;

                // Characters unlock in one purchase, so there is no level track to show.
                slot.HideProgress();

                if (!character.IsUnlocked)
                {
                    var affordable = GoldWallet.Current >= character.UnlockCost;
                    slot.Setup($"{character.DisplayName}\n해금 비용: {character.UnlockCost} G", "해금", affordable, () =>
                    {
                        character.TryUnlock();
                        RefreshUI();
                    });
                    slot.SetAffordable(affordable);
                    return;
                }

                slot.SetAffordable(true);

                if (character.Id == selectedId)
                {
                    slot.Setup(character.DisplayName, "선택됨", false, null);
                    slot.SetSelected(true);
                }
                else
                {
                    slot.Setup(character.DisplayName, "선택", true, () =>
                    {
                        PlayerPrefs.SetString(SelectedCharacterKey, character.Id);
                        PlayerPrefs.Save();
                        RefreshUI();
                    });
                }
            });
        }
    }
}
