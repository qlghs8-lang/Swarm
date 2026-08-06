using System;
using UnityEngine;

namespace Swarm.Player
{
    public class PlayerExperience : MonoBehaviour
    {
        [SerializeField] private int baseExpToLevel = 10;

        private int _level = 1;
        private int _currentExp;

        public int Level => _level;
        public int CurrentExp => _currentExp;
        public int ExpToNextLevel => baseExpToLevel * _level;

        public event Action<int, int> OnExperienceChanged;
        public event Action<int> OnLevelUp;

        public void AddExperience(int amount)
        {
            _currentExp += amount;

            while (_currentExp >= ExpToNextLevel)
            {
                _currentExp -= ExpToNextLevel;
                _level++;
                OnLevelUp?.Invoke(_level);
            }

            OnExperienceChanged?.Invoke(_currentExp, ExpToNextLevel);
        }
    }
}
