﻿using wServer.logic.behaviors;
using wServer.logic.loot;
using wServer.logic.transitions;

namespace wServer.logic
{
    partial class BehaviorDb
    {
        private _ IceCave = () => Behav()
             .Init("Big Yeti",
             new State(
                   new State("geticey",
                       new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                        new PlayerWithinTransition(8, "leggo")
                        ),
                  new State("leggo",
                     new Follow(0.3, 8, 1),
                     new ConditionalEffect(ConditionEffectIndex.Armored),
                       new Taunt(0.50, "MRRRRRRTTTTTT!"),
                        new Taunt(0.25, "Mwruuuu!"),
                       new Flash(0xFF0000, 2, 18),
                       new Spawn("Mini Yeti", 1, 4, 10000)
                        )
                  )
            )
            .Init("Mini Yeti",
                new State(
                    new Flash(0xFF0000, 2, 6),
                     new Orbit(0.3, 3, 20, "Big Yeti", 0.1),
                     new Shoot(8.4, 1, projectileIndex: 0, predictive: 1, coolDown: 1750),
                     new Shoot(8.4, 1, projectileIndex: 0, coolDown: 1200)
                                      )
            )
            .Init("Snow Bat",
                new State(
                    new Flash(0xFF0000, 2, 6),
                     new Orbit(0.3, 3, 20, "Big Yeti", 0.1),
                     new Shoot(8.4, 1, projectileIndex: 0, predictive: 1, coolDown: 1750),
                     new Shoot(8.4, 1, projectileIndex: 0, coolDown: 1200)
                    )
            )
                     .Init("Snow Bat Mama",
             new State(
                   new State("geticey",
                       new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                        new PlayerWithinTransition(8, "leggo")
                        ),
                  new State("leggo",
                     new Follow(0.4, 8, 1),
                      new Shoot(8.4, 1, projectileIndex: 0, predictive: 1, coolDown: 2750),
                     new Shoot(8.4, 1, projectileIndex: 1, coolDown: 2300),
                      new Shoot(8.4, 1, projectileIndex: 2, coolDown: 1000),
                      new Shoot(8.4, 1, projectileIndex: 3, coolDown: 1250),
                       new Spawn("Snow Bat", 1, 4, 10000)
                        ))

              )


             //ESBEN LETS GOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOO!
             .Init("ic Esben the Unwilling",
                new State(
                    new RealmPortalDrop(),
                    new State("Esben1",
                        new Taunt(0.40, "Your souls will soon nourish me."),
                        new Taunt(0.35, "Icicles, rend their flesh."),
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                        new Shoot(10, 8, 7, 3, coolDown: 1350),
                        new Shoot(10, 6, 7, 3, coolDown: 4250),
                        new Shoot(10, 4, 7, 3, coolDown: 3250),
                        new Shoot(10, 2, 7, 3, predictive: 2, coolDown: 2250),
                        new EntitiesNotExistsTransition(9999, "spooked", "ic boss purifier"),
                        new Spawn("ic boss manager", initialSpawn: 1, maxChildren: 1, coolDown: 10000),
                        new TimedTransition(8750, "Esben2")

                        ),
                    new State("Esben2",
                        new Taunt(0.50, "Ice is cold, blood is think, you being alive makes me sick!"),
                        new Taunt(0.35, "The ice I throw is of my construction, but when it hits you it means destruction!"),
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                        new Shoot(8.4, 69, 6, 0, coolDown: 3250),
                        new Shoot(8.4, 2, fixedAngle: 0, projectileIndex: 2, coolDown: 1000),
                        new Shoot(8.4, 2, fixedAngle: 90, projectileIndex: 2, coolDown: 1000),
                        new Shoot(8.4, 2, fixedAngle: 180, projectileIndex: 2, coolDown: 1000),
                        new Shoot(8.4, 2, fixedAngle: 270, projectileIndex: 2, coolDown: 1000),
                        new Shoot(8.4, 1, fixedAngle: 40, projectileIndex: 2, coolDown: 1000),
                        new Shoot(8.4, 1, fixedAngle: 135, projectileIndex: 2, coolDown: 1200),
                        new Shoot(8.4, 1, fixedAngle: 225, projectileIndex: 2, coolDown: 1200),
                        new Shoot(8.4, 1, fixedAngle: 315, projectileIndex: 2, coolDown: 1200),
                          new EntitiesNotExistsTransition(9999, "spooked", "ic boss purifier"),
                        new TimedTransition(8750, "Esben3")
                        ),
                    new State("Esben3",
                        new Taunt(0.60, "I will cut you to shreds."),
                         new Taunt(0.30, "Bad puns I may use to cause offense, but the pain you feel shall be immense!"),
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                        new Shoot(18, 2, 7, 1, coolDown: 700),
                        new Shoot(10, 6, 7, 1, predictive: 2, coolDown: 800),
                        new Shoot(18, 1, 7, 1, coolDown: 14),
                          new EntitiesNotExistsTransition(9999, "spooked", "ic boss purifier"),
                        new TimedTransition(8750, "Esben1")
                        ),
                    new State("spooked",
                        new Wander(0.1),
                        new SetAltTexture(1),
                        new ConditionalEffect(ConditionEffectIndex.Invincible),
                        new Shoot(8.4, 16, 60, 1, coolDown: 1000),
                        new Taunt(1.00, "H..help me...."),
                        new RemoveEntity(9999, "ic boss manager"),
                        new TimedTransition(6200, "spookedwaitforkill")
                        ),
                    new State("spookedwaitforkill",
                        new Wander(0.1),
                        new ConditionalEffect(ConditionEffectIndex.Invincible),
                        new EntitiesNotExistsTransition(9999, "spooked1", "ic shielded king")
                        ),
                    new State("spooked1",
                            new Wander(0.1),
                        new SetAltTexture(2),
                        new Taunt(1.00, "Help..help me....PLEASE!"),
                        new TimedTransition(6570, "transformationstart")
                        ),
                    new State("transformationstart",
                         new Prioritize(
                            new StayCloseToSpawn(0.5, 10),
                            new Wander(1)
                        ),
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new Taunt(1.00, "NO, NO I DON'T WANNA GO BACK TooOooo..AAHHH!!!!"),
                         new Taunt(0.30, "LET ME GOOOOOOO!"),
                         new SetAltTexture(4),
                        new TimedTransition(250, "transformation1")
                        ),
                      new State("transformation1",
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new SetAltTexture(2),
                        new TimedTransition(10, "transformation2")
                        ),
                    new State("transformation2",
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new SetAltTexture(4),
                        new TimedTransition(1, "transformation3")
                        ),
                   new State("transformation3",
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new SetAltTexture(2),
                        new TimedTransition(1, "transformation4")
                        ),
                  new State("transformation4",
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new SetAltTexture(4),
                        new TimedTransition(1, "transformation5")
                        ),
                         new State("transformation5",
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new SetAltTexture(2),
                        new TimedTransition(1, "transformation6")
                        ),
                      new State("transformation6",
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new SetAltTexture(4),
                        new TimedTransition(1, "transformation7")
                        ),
                        new State("transformation7",
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new SetAltTexture(2),
                        new TimedTransition(1, "transformation8")
                        ),
                       new State("transformation8",
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new SetAltTexture(4),
                        new TimedTransition(1, "transformation9")
                        ),
                        new State("transformation9",
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new SetAltTexture(2),
                        new TimedTransition(1, "transformation10")
                        ),
                         new State("transformation10",
                        new ConditionalEffect(ConditionEffectIndex.Invulnerable),
                         new SetAltTexture(4),
                         new Spawn("ic boss manager", initialSpawn: 1, maxChildren: 1, coolDown: 10000),
                        new TimedTransition(10, "Esben1")
                        )
                    ),
                    new MostDamagers(2,
                        new TierLoot(2, ItemType.Potion, 1)
                        ),
                    new Threshold(0.01,
                        new TierLoot(12, ItemType.Weapon, 0.05),
                        new TierLoot(11, ItemType.Weapon, 0.1),
                        new TierLoot(10, ItemType.Weapon, 0.2),
                        new TierLoot(13, ItemType.Armor, 0.05),
                        new TierLoot(12, ItemType.Armor, 0.1),
                        new ItemLoot("Potion of Mana", 0.5),
                        new ItemLoot("Staff of Esben", 0.02),
                        new ItemLoot("Skullish Remains of Esben", 0.02)
                )
            )
            .Init("ic boss spawner live",
                new State(
                    new DropPortalOnDeath("Inner Sanctum Portal", 100),
                    new EntitiesNotExistsTransition(999, "die", "Big Yeti", "Snow Bat Mama"),
                    new State("beABum"),
                    new State("die",
                        new Taunt(true, "Innocent souls. So delicious. You have sated me. Now come i shall give you your reward."),
                        new Suicide()
                    )
                )
            )
            .Init("ic boss purifier",
                new State(
                    new State("spawnDemBois",
                        new Spawn("ic Whirlwind", maxChildren: 2, coolDown: 5000),
                        new Reproduce("ic whirlwind", 10, 3, 4000),
                        new Shoot(5, coolDown: 1500)
                    )
                )
            )
            .Init("ic Whirlwind",
                new State(
                    new State("fight",
                        new Prioritize(
                            new Follow(1.2, 10, 3),
                            new Wander(1.2)
                            ),
                        new Shoot(5, 6, coolDown: 4000),
                        new Shoot(5, 6, projectileIndex: 1, coolDownOffset: 2000, coolDown: 4000)
                    )
                )
            );
    }
}