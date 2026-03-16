using UnityEngine;

public class Player
{
    public int HP, Mana, Manapool;
    public int MaxHP = 30;
    const int MAX_MANAPOOL = 10;

    public Player()
    {
        HP = 30;
        Mana = Manapool = 2;
        MaxHP = 30;
    }


    public void RestoreRoundMana()
    {
        Mana = Manapool;
    }

    public void IncreaseManapool()
    {
        Manapool = Mathf.Clamp(Manapool + 1, 0, MAX_MANAPOOL);
    }

    public void GetDamage(int damage)
    {
        HP = Mathf.Clamp(HP - damage, 0, int.MaxValue);
    }
}