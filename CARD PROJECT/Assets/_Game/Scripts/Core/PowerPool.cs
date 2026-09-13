using System;
using UnityEngine;

namespace LordOfTheRealms
{
    // POWER resurs (mana). Prirodni rast +1/rundi je capan na 10, ali ramp karte
    // (PowerRamp efekti) smiju PROBITI cap, do tvrdog stropa 15. Tako ramp na
    // 10/10 i dalje nesto radi (10 -> 11...), sto je i poanta tih karata.
    [Serializable]
    public class PowerPool
    {
        public const int HardMax = 15;

        [SerializeField] private int cap = 10;
        [SerializeField] private int maxPower;
        [SerializeField] private int currentPower;

        public int Cap => cap;
        public int MaxPower => maxPower;
        public int CurrentPower => currentPower;

        public event Action<PowerPool> OnChanged;

        public PowerPool(int cap = 10)
        {
            this.cap = Mathf.Max(1, cap);
            maxPower = 0;
            currentPower = 0;
        }

        // pocetak poteza: prirodni rast +1 (samo dok je ispod capa 10, rampani
        // max preko capa se NE reze), pa refill na max
        public void StartTurn()
        {
            if (maxPower < cap) maxPower++;
            currentPower = maxPower;
            OnChanged?.Invoke(this);
        }

        // moze li igrac platiti kartu zadane cijene
        public bool CanAfford(int cost) => currentPower >= cost;

        // potrosi POWER; false i nista se ne mijenja ako je preskupo
        public bool TrySpend(int cost)
        {
            if (cost < 0 || currentPower < cost) return false;
            currentPower -= cost;
            OnChanged?.Invoke(this);
            return true;
        }

        // jednokratni bonus power ovaj potez (moze preko capa, do tvrdog stropa)
        public void AddTemporary(int amount)
        {
            if (amount <= 0) return;
            currentPower = Mathf.Min(HardMax, currentPower + amount);
            OnChanged?.Invoke(this);
        }

        // trajno podigni max; ramp karte probijaju cap 10, strop je 15.
        // Napomena: NE daje trenutni power odmah (refill dolazi na pocetku poteza),
        // ista semantika kao prije da balans ostane netaknut.
        public void RaiseMax(int amount)
        {
            if (amount <= 0) return;
            maxPower = Mathf.Min(HardMax, maxPower + amount);
            OnChanged?.Invoke(this);
        }

        // cijena u max poweru (drain karte): spusti max i clampaj trenutni.
        // Prirodni rast (+1/turn dok je ispod capa) sam vraca prema 10, pa je
        // drain privremena rupa u tempu, ne trajna kazna.
        public void LowerMax(int amount)
        {
            if (amount <= 0) return;
            maxPower = Mathf.Max(0, maxPower - amount);
            currentPower = Mathf.Min(currentPower, maxPower);
            OnChanged?.Invoke(this);
        }

        // editor/debug: resetiraj pool
        public void ResetPool()
        {
            maxPower = 0;
            currentPower = 0;
            OnChanged?.Invoke(this);
        }
    }
}
