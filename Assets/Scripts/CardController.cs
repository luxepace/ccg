using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardController : MonoBehaviour
{
    public Card Card;

    public bool IsPlayerCard;

    public CardInfo Info;
    public CardMovement Movement;
    public CardAbility Ability;

    GameManager gameManager;


    public void Init(Card card, bool isPlayerCard)
    {
        Card = card;
        gameManager = GameManager.Instance;
        IsPlayerCard = isPlayerCard;

        if (IsPlayerCard)
        {
            Info.ShowCardInfo();
            GetComponent<AttackedCard>().enabled = false;
        }
        else
        {
            Info.HideCardInfo();
        }
    }

    public void OnCast()
    {
        if (Card.IsSpell && ((SpellCard)Card).SpellTarget != SpellCard.TargetType.NO_TARGET)
            return;

        if (IsPlayerCard)
        {
            gameManager.PlayerHandCards.Remove(this);
            gameManager.PlayerFieldCards.Add(this);
            gameManager.ReduceMana(true, Card.Manacost);
            gameManager.CheckCardsForManaAvailability();
        }
        else
        {
            gameManager.EnemyHandCards.Remove(this);
            gameManager.EnemyFieldCards.Add(this);
            gameManager.ReduceMana(false, Card.Manacost);
            Info.ShowCardInfo();
        }

        Card.IsPlaced = true;

        if (Card.HasAbility)
            Ability.OnCast();

        if (Card.IsSpell)
            UseSpell(null);

        UIController.Instance.UpdateHPAndMana();
    }

    public void OnTakeDamage(CardController attacker = null)
    {
        CheckForAlive();
        Ability.OnDamageTake(attacker);
    }   
    
    public void OnDamageDeal()
    {
        Card.TimesDealedDamage++;
        Card.CanAttack = false;
        Info.HighlightCard(false);

        if (Card.HasAbility)
            Ability.OnDamageDeal();
    }

    public void UseSpell(CardController target)
    {
        var spellcard = (SpellCard)Card;

        switch (spellcard.Spell)
        {
            case SpellCard.SpellType.HEAL_ALLY_FIELD_CARDS:
                var allyCards = IsPlayerCard ?
                                 gameManager.PlayerFieldCards :
                                 gameManager.EnemyFieldCards;

                foreach (var card in allyCards)
                {
                    card.Card.Defense += spellcard.SpellValue;
                    card.Info.RefreshData();
                }
                break;

            case SpellCard.SpellType.DAMAGE_ENEMY_FIELD_CARDS:
                var enemyCards = IsPlayerCard ?
                                  new List<CardController>(gameManager.EnemyFieldCards) :
                                  new List<CardController>(gameManager.PlayerFieldCards);

                foreach (var card in enemyCards)
                {
                    GiveDamageTo(card, spellcard.SpellValue);
                }
                break;

            case SpellCard.SpellType.HEAL_ALLY_HERO:
                if (IsPlayerCard)
                    gameManager.CurrentGame.Player.HP += spellcard.SpellValue;
                else
                    gameManager.CurrentGame.Enemy.HP += spellcard.SpellValue;

                UIController.Instance.UpdateHPAndMana();
                break;

            case SpellCard.SpellType.DAMAGE_ENEMY_HERO:
                if (IsPlayerCard)
                    gameManager.CurrentGame.Enemy.HP -= spellcard.SpellValue;
                else
                    gameManager.CurrentGame.Player.HP -= spellcard.SpellValue;

                UIController.Instance.UpdateHPAndMana();
                gameManager.CheckForResult();
                break;

            case SpellCard.SpellType.HEAL_ALLY_CARD:
                target.Card.Defense += spellcard.SpellValue;
                break;

            case SpellCard.SpellType.DAMAGE_ENEMY_CARD:
                GiveDamageTo(target, spellcard.SpellValue);
                break;

            case SpellCard.SpellType.SHIELD_ON_ALLY_CARD:
                if (!target.Card.Abilities.Exists(x => x == SpellCard.AbilityType.SHIELD))
                    target.Card.Abilities.Add(SpellCard.AbilityType.SHIELD);
                break;

            case SpellCard.SpellType.PROVOCATION_ON_ALLY_CARD:
                if (!target.Card.Abilities.Exists(x => x == SpellCard.AbilityType.PROVOCATION))
                    target.Card.Abilities.Add(SpellCard.AbilityType.PROVOCATION);
                break;

            case SpellCard.SpellType.BUFF_CARD_DAMAGE:
                target.Card.Attack += spellcard.SpellValue;
                break;

            case SpellCard.SpellType.DEBUFF_CARD_DAMAGE:
                target.Card.Attack = Mathf.Clamp(target.Card.Attack - spellcard.SpellValue, 0, int.MaxValue);
                break;
        }

        if (target != null)
        {
            target.Ability.OnCast();
            target.CheckForAlive();
        }

        DestroyCard();
    }

    void GiveDamageTo(CardController card, int damage)
    {
        card.Card.GetDamage(damage);
        card.CheckForAlive();
        card.OnTakeDamage();
    }

    public void CheckForAlive()
    {
        if (Card.IsAlive)
            Info.RefreshData();
        else
            DestroyCard();
    }

    public void DestroyCard()
    {
        Movement.OnEndDrag(null);

        // Остановить все анимации DOTween на этом объекте
        transform.DOKill();

        // Удаляем из списков
        RemoveCardFromList(gameManager.EnemyFieldCards);
        RemoveCardFromList(gameManager.EnemyHandCards);
        RemoveCardFromList(gameManager.PlayerFieldCards);
        RemoveCardFromList(gameManager.PlayerHandCards);

        // Уничтожаем объект
        Destroy(gameObject);
    }

    void RemoveCardFromList(List<CardController> list)
    {
        if (list.Exists(x => x == this))
            list.Remove(this);
    }

}