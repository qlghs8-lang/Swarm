using System;
using System.Collections.Generic;
using Swarm.Player;
using Swarm.Weapon;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.LevelUp
{
    public class LevelUpManager : MonoBehaviour
    {
        private const float InvestedWeightMultiplier = 3f;

        private readonly struct Candidate
        {
            public readonly string Title;
            public readonly string Description;
            public readonly Action Apply;
            public readonly float Weight;
            public readonly bool IsEvolution;

            public Candidate(string title, string description, Action apply, float weight, bool isEvolution = false)
            {
                Title = title;
                Description = description;
                Apply = apply;
                Weight = weight;
                IsEvolution = isEvolution;
            }
        }

        [SerializeField] private RunPassive[] runPassives;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text card1Text;
        [SerializeField] private Text card2Text;
        [SerializeField] private Text card3Text;

        private GameObject _player;
        private PlayerExperience _experience;
        private readonly Dictionary<RunPassive, int> _passiveLevels = new();

        private Action _pickedApply1;
        private Action _pickedApply2;
        private Action _pickedApply3;

        private void Awake()
        {
            _player = GameObject.FindGameObjectWithTag("Player");
            if (_player == null) return;

            _player.TryGetComponent(out _experience);
            if (_experience != null)
            {
                _experience.OnLevelUp += HandleLevelUp;
            }
        }

        private void OnDestroy()
        {
            if (_experience != null)
            {
                _experience.OnLevelUp -= HandleLevelUp;
            }
        }

        private void HandleLevelUp(int newLevel)
        {
            var candidates = BuildCandidates();
            var picks = PickThree(candidates);

            _pickedApply1 = picks[0].Apply;
            _pickedApply2 = picks[1].Apply;
            _pickedApply3 = picks[2].Apply;

            card1Text.text = FormatCard(picks[0]);
            card2Text.text = FormatCard(picks[1]);
            card3Text.text = FormatCard(picks[2]);

            panelRoot.SetActive(true);
            Time.timeScale = 0f;
        }

        public void SelectCard1() => Select(_pickedApply1);
        public void SelectCard2() => Select(_pickedApply2);
        public void SelectCard3() => Select(_pickedApply3);

        private void Select(Action apply)
        {
            apply?.Invoke();
            panelRoot.SetActive(false);
            Time.timeScale = 1f;
        }

        private List<Candidate> BuildCandidates()
        {
            var result = new List<Candidate>();

            foreach (var passive in runPassives)
            {
                if (passive == null) continue;

                var level = _passiveLevels.GetValueOrDefault(passive, 0);
                if (level >= passive.MaxLevel) continue;

                var weight = level > 0 ? InvestedWeightMultiplier : 1f;
                result.Add(new Candidate($"{passive.DisplayName} Lv.{level + 1}", passive.Description, () =>
                {
                    _passiveLevels[passive] = level + 1;
                    passive.ApplyLevel(_player, level + 1);
                }, weight));
            }

            foreach (var weapon in _player.GetComponents<ILevelableWeapon>())
            {
                if (!weapon.IsAvailable) continue;

                if (weapon.Level >= weapon.MaxLevel)
                {
                    if (weapon is LevelableWeapon leveledWeapon && leveledWeapon.EvolutionTarget != null)
                    {
                        var evolution = leveledWeapon.EvolutionTarget;
                        result.Add(new Candidate(evolution.CardDisplayName, evolution.CardDescription,
                            () =>
                            {
                                leveledWeapon.Retire();
                                evolution.SetAvailable(true);
                                evolution.SetStartingLevel(1);
                            },
                            InvestedWeightMultiplier, isEvolution: true));
                    }

                    continue;
                }

                var weight = weapon.Level > 0 ? InvestedWeightMultiplier : 1f;
                result.Add(new Candidate(weapon.CardDisplayName, weapon.CardDescription, weapon.LevelUp, weight));
            }

            return result;
        }

        private static List<Candidate> PickThree(List<Candidate> candidates)
        {
            var result = new List<Candidate>();
            var remaining = new List<Candidate>(candidates);

            for (var i = remaining.Count - 1; i >= 0 && result.Count < 3; i--)
            {
                if (!remaining[i].IsEvolution) continue;

                result.Add(remaining[i]);
                remaining.RemoveAt(i);
            }

            var pool = new List<int>();
            for (var i = 0; i < remaining.Count; i++) pool.Add(i);

            while (result.Count < 3)
            {
                if (pool.Count == 0)
                {
                    if (remaining.Count == 0) break;
                    for (var j = 0; j < remaining.Count; j++) pool.Add(j);
                }

                var pickedIndex = PickWeightedIndex(remaining, pool);
                result.Add(remaining[pickedIndex]);
                pool.Remove(pickedIndex);
            }

            return result;
        }

        private static int PickWeightedIndex(List<Candidate> candidates, List<int> pool)
        {
            var totalWeight = 0f;
            foreach (var index in pool) totalWeight += candidates[index].Weight;

            var roll = UnityEngine.Random.Range(0f, totalWeight);
            var cumulative = 0f;

            foreach (var index in pool)
            {
                cumulative += candidates[index].Weight;
                if (roll < cumulative) return index;
            }

            return pool[^1];
        }

        private static string FormatCard(Candidate candidate)
        {
            return $"{candidate.Title}\n{candidate.Description}";
        }
    }
}
