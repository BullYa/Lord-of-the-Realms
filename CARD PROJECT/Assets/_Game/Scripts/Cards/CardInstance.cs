using System;
using System.Collections.Generic;
using UnityEngine;

namespace LordOfTheRealms
{
    // Karta U IGRI: omata nepromjenjivi CardData asset i drzi zivo stanje
    // (trenutni HP, moze li napasti, je li skrivena, stit, primljene aure).
    [Serializable]
    public class CardInstance
    {
        // izlijeci jedinicu do punog (Tranquility i slicni efekti)
        public void HealFull() { CurrentHealth = MaxHealth; }

        public CardData Data { get; private set; }
        public Owner Owner { get; private set; }

        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public int Attack { get; private set; }

        public bool CanAttack { get; set; }
        public bool IsHidden { get; set; }
        public bool IsAlive => CurrentHealth > 0;

        // Divine Shield: prvi hit ne prolazi (potrosi se stit umjesto stete)
        public bool HasShield { get; private set; }

        // stit je upio zadnji hit; UI to pretvori u "BLOCKED" umjesto broja stete
        public bool BlockedLastHit { get; private set; }

        // procitaj i ocisti zastavicu (UI je zove tocno jednom po pogotku)
        public bool ConsumeBlocked()
        {
            if (!BlockedLastHit) return false;
            BlockedLastHit = false;
            return true;
        }

        // Koje aure su vec buffale ovaj unit (da se ne primijene dvaput).
        private readonly HashSet<CardInstance> _auraSources = new();

        public event Action<CardInstance> OnStatsChanged;
        public event Action<CardInstance> OnDied;

        public CardInstance(CardData data, Owner owner)
        {
            Data = data;
            Owner = owner;

            switch (data)
            {
                case UnitCardData unit:
                    Attack = unit.attack;
                    MaxHealth = unit.health;
                    break;
                case HeroCardData hero:
                    Attack = hero.attack;
                    MaxHealth = hero.health;
                    break;
                default:
                    Attack = 0;
                    MaxHealth = 0;
                    break;
            }

            CurrentHealth = MaxHealth;
            CanAttack = false;
            HasShield = data.ability == CardAbility.DivineShield;
        }

        public UnitType? UnitType =>
            Data is UnitCardData u ? u.unitType : (UnitType?)null;

        public bool IsTaunt => Data is UnitCardData u && u.IsTaunt;
        public bool IsScout => Data is UnitCardData su && su.unitType == LordOfTheRealms.UnitType.Scout;
        public bool IsHiddenSpell => Data is SpellCardData && IsHidden;
        public bool IsHero => Data is HeroCardData;

        public TargetingMode AttackTargeting
        {
            get
            {
                return Data switch
                {
                    UnitCardData u => u.AttackTargeting,
                    HeroCardData h => h.AttackTargeting,
                    SpellCardData s => s.CastTargeting,
                    _ => TargetingMode.Normal
                };
            }
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            // stit upija cijeli prvi hit
            if (HasShield)
            {
                HasShield = false;
                BlockedLastHit = true;
                OnStatsChanged?.Invoke(this);
                return;
            }
            BlockedLastHit = false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            OnStatsChanged?.Invoke(this);
            if (CurrentHealth == 0)
                OnDied?.Invoke(this);
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            OnStatsChanged?.Invoke(this);
        }

        public void Buff(int attackDelta, int healthDelta)
        {
            Attack = Mathf.Max(0, Attack + attackDelta);
            MaxHealth = Mathf.Max(1, MaxHealth + healthDelta);
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + Mathf.Max(0, healthDelta));
            OnStatsChanged?.Invoke(this);
        }

        public bool Reveal()
        {
            if (!IsHidden) return false;
            IsHidden = false;
            OnStatsChanged?.Invoke(this);
            return true;
        }

        // -- passive aura bookkeeping --
        public bool HasAura(CardInstance source) => _auraSources.Contains(source);
        public void MarkAura(CardInstance source) => _auraSources.Add(source);
    }
}
