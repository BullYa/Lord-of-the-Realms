using System;

namespace LordOfTheRealms
{
    // Ne-kartno stanje igraca: zdravlje (uvjet poraza) i njegov POWER pool.
    // Implementira ITargetable pa se moze napasti kao jedinica, uz taunt pravila
    // iz TargetingRules. Ovo je "player" kojeg napadas da pobijedis, za razliku
    // od herojskih *karata* (Kragmaw, Faelan...) koje su jedinice na plodi.
    public class PlayerEntity : ITargetable
    {
        public Owner Owner { get; }
        public Race Race { get; }
        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; }
        public PowerPool Power { get; }

        public bool IsAlive => CurrentHealth > 0;
        public string TargetName => $"{Owner} player";

        public event Action<PlayerEntity> OnHealthChanged;
        public event Action<PlayerEntity> OnDefeated;

        public PlayerEntity(Owner owner, Race race, int maxHealth = 40, int powerCap = 10)
        {
            Owner = owner;
            Race = race;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            Power = new PowerPool(powerCap);
        }

        public void ReceiveDamage(int amount)
        {
            if (amount <= 0) return;
            CurrentHealth = Math.Max(0, CurrentHealth - amount);
            OnHealthChanged?.Invoke(this);
            if (CurrentHealth == 0)
                OnDefeated?.Invoke(this);
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(this);
        }
    }
}
