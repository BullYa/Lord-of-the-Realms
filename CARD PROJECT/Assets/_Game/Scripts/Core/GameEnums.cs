namespace LordOfTheRealms
{
    // Cetiri igrive rase. Svaka ima svoj deck identitet i boju.
    public enum Race
    {
        Orcs,
        Elves,
        Humans,
        Demons
    }

    // Tip jedinice: Basic (obicna), Taunt (mora se napasti prva), Assassin (ignorira
    // taunt na jedinicama; igraca gada samo ako nema taunta), Scout (trosi napad da
    // otkrije skriveni spell).
    public enum UnitType
    {
        Basic,
        Taunt,
        Assassin,
        Scout
    }

    // Vrsta spella: Offensive (ciljani dmg, targeting kao Assassin), Defensive
    // (heal/zastita, neki idu hidden), Field (mijenja stanje ploce/buffevi),
    // Utility (ekonomija/ramp, ne cilja).
    public enum SpellType
    {
        Offensive,
        Defensive,
        Field,
        Utility
    }

    // Kako napadac rjesava taunt: Normal (taunt ga potpuno blokira), Precise
    // (ignorira taunt na jedinicama; igraca dohvaca samo bez taunta). Precise dijele
    // Assassin jedinice i offensive spellovi.
    public enum TargetingMode
    {
        Normal,
        Precise
    }

    // Kojoj strani ploce karta/entitet pripada.
    public enum Owner
    {
        Player,
        Opponent
    }

    // Gruba kategorija karte (za UI, filtriranje, slotove).
    public enum CardCategory
    {
        Unit,
        Spell,
        Hero
    }

    // Legacy enum keywordova (rani dizajn). Aktualne mehanike su u CardAbility;
    // ovo ostaje da se ne slome stari reference dok se potpuno ne ukloni.
    public enum AbilityKeyword
    {
        None,
        GeneratePower,   // privremeni POWER ovaj potez
        RaiseMaxPower,   // trajno digni max POWER (ramp)
        Heal,            // vrati zdravlje savezniku/heroju
        DirectDamage,    // nanesi stetu (on play / on attack)
        BuffAllies,      // buffaj savezne jedinice
        ScalingBuff,     // jaca s vremenom / svaki potez
        ComboPayoff,     // jace ako je druga karta odigrana prije ovaj potez
        Sacrifice,       // zrtvuj saveznu jedinicu za efekt
        DrawCard         // vuci karte
    }

    // Kad se ability okida: OnPlay (pri ulasku), Passive (dok je u igri), OnAttack
    // (kad ova jedinica napada), Secret (skriveni spell, auto-okida na uvjet).
    public enum AbilityTrigger
    {
        None,
        OnPlay,
        Passive,
        OnAttack,
        Secret
    }
}
