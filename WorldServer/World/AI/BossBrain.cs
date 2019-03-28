﻿using System;
using System.Collections.Generic;
using System.Linq;
using SystemData;
using Common;
using Common.Database.World.Creatures;
using FrameWork;
using GameData;
using NLog;
using WorldServer.Services.World;
using WorldServer.World.Abilities;
using WorldServer.World.Abilities.Components;
using WorldServer.World.Interfaces;
using WorldServer.World.Map;
using WorldServer.World.Objects;
using Object = WorldServer.World.Objects.Object;

//test with .spawnmobinstance 2000681
namespace WorldServer.World.AI
{
    public class BossBrain : ABrain
    {
        // Melee range for the boss - could use baseradius perhaps?
        public static int BOSS_MELEE_RANGE = 25;

        // Cooldown between special attacks 
        public static int NEXT_ATTACK_COOLDOWN = 2000;


        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public BossBrain(Unit myOwner)
            : base(myOwner)
        {
            AbilityTracker = new Dictionary<BossSpawnAbilities, long>();
            SpawnList = new List<Creature>();
            CurrentPhase = 0;
        }

        public List<BossSpawnAbilities> Abilities { get; set; }

        public Dictionary<BossSpawnAbilities, long> AbilityTracker { get; set; }


        // List of Adds that the boss has spawned, and their states.
        public List<Creature> SpawnList { get; set; }
        public List<BossSpawnPhase> Phases { get; set; }
        public int CurrentPhase { get; set; }

        public override void Think(long tick)
        {
            if (_unit.IsDead)
                return;

            base.Think(tick);

            // Only bother to seek targets if we're actually being observed by a player
            if (Combat.CurrentTarget == null && _unit.PlayersInRange.Count > 0)
            {
                if (_pet != null && (_pet.IsHeeling || ((CombatInterface_Pet) _pet.CbtInterface).IgnoreDamageEvents))
                    return;

                var target = _unit.AiInterface.GetAttackableUnit();
                if (target != null)
                    _unit.AiInterface.ProcessCombatStart(target);
            }

            if (Combat.IsFighting && Combat.CurrentTarget != null &&
                _unit.AbtInterface.CanCastCooldown(0) &&
                tick > NextTryCastTime)
            {
                var phaseAbilities = GetPhaseAbilities();

                // Get abilities that can fire now.
                FilterAbilities(tick, phaseAbilities);

                // Sort dictionary in value (time) order.
                var myList = AbilityTracker.ToList();
                myList.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));

                foreach (var keyValuePair in myList)
                    _logger.Debug($"***{keyValuePair.Key.Name} => {keyValuePair.Value}");

                ExecuteNextAbilityFromList(tick, myList);
            }
        }

        private void FilterAbilities(long tick, List<BossSpawnAbilities> phaseAbilities)
        {
            foreach (var ability in phaseAbilities)
            {
                var t = GetType();
                var method = t.GetMethod(ability.Condition);
                _logger.Debug($"Checking condition: {ability.Condition} ");
                var conditionTrue = (bool)method.Invoke(this, null);
                if (conditionTrue)
                {
                    // If the ability is not in the ability tracker, add it
                    if (!AbilityTracker.ContainsKey(ability))
                    {
                        lock (AbilityTracker)
                        {
                            AbilityTracker.Add(ability, TCPManager.GetTimeStamp() + NEXT_ATTACK_COOLDOWN);
                        }

                        _logger.Debug($"Adding ability to the tracker : {AbilityTracker.Count} {ability.Name} 0");
                    }
                    else // If this ability is already in the abilitytracker  -- can probably remove this as it should be removed on execution.
                    {
                        long nextInvocation = 0;

                        // If it's next invocation > now, dont add.
                        AbilityTracker.TryGetValue(ability, out nextInvocation);
                        if (nextInvocation > tick)
                        {
                            // Do nothing
                        }
                    }
                }
            }
        }

        private void ExecuteNextAbilityFromList(long tick, List<KeyValuePair<BossSpawnAbilities, long>> myList)
        {
            // This contains the list of abilities that can possibly be executed.
            var rand = StaticRandom.Instance.Next(1, 100);
            lock (myList)
            {
                foreach (var keyValuePair in myList)
                {
                    if (keyValuePair.Value < tick)
                    {
                        if (keyValuePair.Key.ExecuteChance >= rand)
                        {
                            var method = GetType().GetMethod(keyValuePair.Key.Execution);

                            _logger.Trace($"Executing  : {keyValuePair.Key.Name} => {keyValuePair.Value} ");

                            PerformSpeech(keyValuePair.Key);
                           
                            PerformSound(keyValuePair.Key);
                            
                            _logger.Debug($"Executing  : {keyValuePair.Key.Name} => {keyValuePair.Value} ");

                            NextTryCastTime = TCPManager.GetTimeStampMS() + NEXT_ATTACK_COOLDOWN;

                            lock (AbilityTracker)
                            {
                                // TODO : See if this is required, or can use ability cool down instead
                                AbilityTracker[keyValuePair.Key] = tick + keyValuePair.Key.CoolDown * 1000;
                            }

                            try
                            {
                                method.Invoke(this, null);
                            }
                            catch (Exception e)
                            {
                                _logger.Error($"{e.Message} {e.StackTrace}");
                                throw;
                            }

                            _logger.Trace(
                                $"Updating the tracker : {keyValuePair.Key.Name} => {tick + keyValuePair.Key.CoolDown * 1000} ");
                            _logger.Debug($"CoolDowns : {_unit.AbtInterface.Cooldowns.Count}");
                            break; // Leave the loop, come back on next tick
                        }

                        _logger.Debug($"Skipping : {keyValuePair.Key.Name} => {keyValuePair.Value} (random)");
                    }
                }
            }
        }

        public void PerformSound(BossSpawnAbilities key)
        {
            if (!string.IsNullOrEmpty(key.Sound))
                foreach (var plr in GetClosePlayers())
                    plr.PlaySound(Convert.ToUInt16(key.Sound));
        }

        public void PerformSpeech(BossSpawnAbilities key)
        {
            if (!string.IsNullOrEmpty(key.Speech))
                _unit.Say(key.Speech, ChatLogFilters.CHATLOGFILTERS_SHOUT);

        }

        public List<BossSpawnAbilities> GetStartCombatAbilities()
        {
            var result = new List<BossSpawnAbilities>();
            foreach (var ability in Abilities)
            {
                if (ability.Phase == "!")
                {
                    result.Add(ability);
                    continue;
                }
            }
            return result;
        }


        private List<BossSpawnAbilities> GetPhaseAbilities()
        {
            var result = new List<BossSpawnAbilities>();
            foreach (var ability in Abilities)
            {
                // Any phase ability
                if (ability.Phase == "*")
                {
                    result.Add(ability);
                    continue;
                }

                // Start up ability
                if (ability.Phase == "!")
                    continue;

                if (Convert.ToInt32(ability.Phase) == CurrentPhase) result.Add(ability);
            }

            return result;
        }

        private List<Player> GetClosePlayers()
        {
            return _unit.GetPlayersInRange(300, false);
        }

        public bool PlayersWithinRange()
        {
            if (_unit != null)
            {
                var players = _unit.GetPlayersInRange(30, false);
                if (players == null)
                    return false;
                else
                {
                    return true;
                }
            }

            return false;
        }

        public bool TargetInMeleeRange()
        {
            if (Combat.HasTarget(TargetTypes.TARGETTYPES_TARGET_ENEMY))
            {
                if (_unit.GetDistanceToObject(_unit.CbtInterface.GetCurrentTarget()) < BOSS_MELEE_RANGE
                ) // In melee range
                    return true;
                return false;
            }

            return false;
        }

        public bool HasBlessing()
        {
            if (Combat.HasTarget(TargetTypes.TARGETTYPES_TARGET_ENEMY))
            {
                if (_unit.GetDistanceToObject(_unit.CbtInterface.GetCurrentTarget()) < BOSS_MELEE_RANGE
                ) // In melee range
                {
                    var blessing = Combat.CurrentTarget.BuffInterface.HasBuffOfType((byte) BuffTypes.Blessing);
                    return blessing;
                }

                return false;
            }

            return false;
        }

        public bool CanKnockDownTarget()
        {
            if (!TargetInMeleeRange())
                return false;
            if (TargetIsUnstoppable())
                return false;

            return true;
        }

        public bool CanPuntTarget()
        {
            if (!TargetInMeleeRange())
                return false;
            if (TargetIsUnstoppable())
                return false;

            return true;
        }

        public bool TwentyPercentHealth()
        {
            if (_unit.PctHealth <= 20)
                return true;
            return false;
        }

        public bool FourtyNinePercentHealth()
        {
            if (_unit.PctHealth <= 49)
                return true;
            return false;
        }

        public bool SeventyFivePercentHealth()
        {
            if (_unit.PctHealth <= 74)
                return true;
            return false;
        }

        public bool NinetyNinePercentHealth()
        {
            if (_unit.PctHealth <= 99)
                return true;
            return false;
        }

        public void IncrementPhase()
        {
            // Phases must be ints in ascending order.
            var currentPhase = CurrentPhase;
            if (Phases.Count == currentPhase)
                return;
            CurrentPhase = currentPhase + 1;

            SpeakYourMind($" using Increment Phase vs {currentPhase}=>{CurrentPhase}");
        }

        public void ShatterBlessing()
        {
            if (Combat.CurrentTarget != null)
            {
                SpeakYourMind($" using Shatter Confidence vs {(Combat.CurrentTarget as Player).Name}");
                SimpleCast(_unit, Combat.CurrentTarget, "Shatter Confidence", 8023);
            }
        }

        public void PrecisionStrike()
        {
            if (Combat.CurrentTarget != null)
            {
                SpeakYourMind($" using PrecisionStrike vs {(Combat.CurrentTarget as Player).Name}");
                SimpleCast(_unit, Combat.CurrentTarget, "PrecisionStrike", 8005);
            }
        }

        public void SeepingWound()
        {
            if (Combat.CurrentTarget != null)
            {
                SpeakYourMind($" using Seeping Wound vs {(Combat.CurrentTarget as Player).Name}");
                SimpleCast(_unit, Combat.CurrentTarget, "Seeping Wound", 8346);
            }
        }

        public bool TargetIsUnstoppable()
        {
            var buff = Combat.CurrentTarget.BuffInterface.GetBuff((ushort) GameBuffs.Unstoppable, Combat.CurrentTarget);
            return buff != null;
        }

        public void KnockDownTarget()
        {
            SpeakYourMind($" using Downfall vs {(Combat.CurrentTarget as Player).Name}");
            SimpleCast(_unit, Combat.CurrentTarget, "Downfall", 8346);
        }

        public void PuntTarget()
        {
            if (Combat.CurrentTarget != null)
            {
                SpeakYourMind($" using Repel vs {(Combat.CurrentTarget as Player).Name}");
                Combat.CurrentTarget.ApplyKnockback(_unit, AbilityMgr.GetKnockbackInfo(8329, 0));
            }
        }

        public void Corruption()
        {
            if (Combat.CurrentTarget != null)
            {
                SpeakYourMind($" using Corruption vs {(Combat.CurrentTarget as Player).Name}");
                SimpleCast(_unit, Combat.CurrentTarget, "Corruption", 8400);
            }
        }


        public void Stagger()
        {
            if (Combat.CurrentTarget != null)
            {
                SpeakYourMind($" using Quake vs {(Combat.CurrentTarget as Player).Name}");
                SimpleCast(_unit, Combat.CurrentTarget, "Quake", 8349);
            }
        }

        public void BestialFlurry()
        {
            if (Combat.CurrentTarget != null)
            {
                SpeakYourMind($" using BestialFlurry vs {(Combat.CurrentTarget as Player).Name}");
                SimpleCast(_unit, Combat.CurrentTarget, "BestialFlurry", 5347);
            }
        }

        public void Whirlwind()
        {
            SpeakYourMind(" using Whirlwind");
            SimpleCast(_unit, Combat.CurrentTarget, "Whirlwind", 5568);
        }

        public void EnfeeblingShout()
        {
            SpeakYourMind(" using Enfeebling Shout");
            SimpleCast(_unit, Combat.CurrentTarget, "Enfeebling Shout", 5575);
        }

        public void Cleave()
        {
            SpeakYourMind(" using Cleave");
            SimpleCast(_unit, Combat.CurrentTarget, "Cleave", 13626);
        }

        public void Stomp()
        {
            SpeakYourMind(" using Stomp");
            SimpleCast(_unit, Combat.CurrentTarget, "Stomp", 4811);
        }

        public void EnragedBlow()
        {
            SpeakYourMind(" using EnragedBlow");
            SimpleCast(_unit, Combat.CurrentTarget, "EnragedBlow", 8315);
        }

        
        public void FlingSpines()
        {
            var newTarget = SetRandomTarget();
            if (newTarget != null)
            {
                SpeakYourMind($" using FlingSpines {newTarget.Name}");
                Combat.SetTarget(newTarget, TargetTypes.TARGETTYPES_TARGET_ENEMY);
                SimpleCast(_unit, Combat.CurrentTarget, "FlingSpines", 13089);
            }
        }


        public void Terror()
        {
            SpeakYourMind(" using Terror");
            SimpleCast(_unit, Combat.CurrentTarget, "Terror", 5968);
        }


        public void PlagueAura()
        {
            SpeakYourMind(" using PlagueAura");
            SimpleCast(_unit, Combat.CurrentTarget, "PlagueAura", 13660);
        }

        /// <summary>
        /// Aslong as the Banner of Bloodlust i s up,Borzhar will charge a t a new target
        /// and should s tay locked on the target for a medium duration before charging
        /// at a new target. Players can destroy the banner which will prevent him f rom
        /// using this charge anymore.
        /// </summary>
        public void DeployBannerOfBloodlust()
        {
            SpeakYourMind(" using DeployBannerOfBloodlust");

            GameObject_proto proto = GameObjectService.GetGameObjectProto(3100412);
          
            GameObject_spawn spawn = new GameObject_spawn
            {
                Guid = (uint)GameObjectService.GenerateGameObjectSpawnGUID(),
                WorldX = _unit.WorldPosition.X + StaticRandom.Instance.Next(50),
                WorldY = _unit.WorldPosition.Y + StaticRandom.Instance.Next(50),
                WorldZ = _unit.WorldPosition.Z,
                WorldO = _unit.Heading,
                ZoneId = _unit.Zone.ZoneId
            };

            spawn.BuildFromProto(proto);
            proto.IsAttackable = 1;

            var go = _unit.Region.CreateGameObject(spawn);
            go.EvtInterface.AddEventNotify(EventName.OnDie, RemoveGOs); 

        }

        private bool RemoveGOs(Object obj, object args)
        {
            GameObject go = obj as GameObject;
            go.EvtInterface.AddEvent(go.Destroy, 2 * 1000, 1);
            return false;
        }


        /// <summary>
        /// Aslong as the Banner of the Bloodherdisup, Bloodherd Gors willrally to
        /// Borzhar’s side. To stopthe reinforcement, players must destroy the Banner of
        /// the Bloodherd.
        /// </summary>
        public void BannerOfTheBloodHerd()
        {
            // If the Banner exists within 150 feet, allow spawn adds
            var creatures = _unit.GetInRange<GameObject>(150);
            foreach (var creature in creatures)
            {
                if (creature.Entry == 3100412)
                {
                    SpawnAdds();
                    break;
                }
            }
        }

        public void SpawnAdds()
        {
            if (_unit is Boss)
            {
                var adds = (_unit as Boss).AddDictionary;
                
                foreach (var entry in adds)
                {
                    Spawn(entry);
                }

                // Force zones to update
                _unit.Region.Update();
            }
        }

        private void Spawn(BossSpawn entry)
        {
            ushort facing = 2093;

            var X = _unit.WorldPosition.X;
            var Y = _unit.WorldPosition.Y;
            var Z = _unit.WorldPosition.Z;


            var spawn = new Creature_spawn { Guid = (uint)CreatureService.GenerateCreatureSpawnGUID() };
            var proto = CreatureService.GetCreatureProto(entry.ProtoId);
            if (proto == null)
                return;
            spawn.BuildFromProto(proto);

            spawn.WorldO = facing;
            spawn.WorldX = X + StaticRandom.Instance.Next(500);
            spawn.WorldY = Y + StaticRandom.Instance.Next(500);
            spawn.WorldZ = Z;
            spawn.ZoneId = (ushort)_unit.ZoneId;


            var creature = _unit.Region.CreateCreature(spawn);
            creature.EvtInterface.AddEventNotify(EventName.OnDie, RemoveNPC);
            entry.Creature = creature;
            (_unit as Boss).SpawnDictionary.Add(entry);

            if (entry.Type == BrainType.AggressiveBrain)
                creature.AiInterface.SetBrain(new AggressiveBrain(creature));
            if (entry.Type == BrainType.HealerBrain)
                creature.AiInterface.SetBrain(new HealerBrain(creature));
            if (entry.Type == BrainType.PassiveBrain)
                creature.AiInterface.SetBrain(new PassiveBrain(creature));

        }

        private bool RemoveNPC(Object obj, object args)
        {
            Creature c = obj as Creature;
            if (c != null) c.EvtInterface.AddEvent(c.Destroy, 20000, 1);

            return false;
        }

        

        public void ExecuteStartUpAbilities()
        {
            var abilities = GetStartCombatAbilities();
            foreach (var startUpAbility in abilities)
            {
                _logger.Trace($"Executing Start Up : {startUpAbility.Name} ");
                var method = GetType().GetMethod(startUpAbility.Execution);
                method.Invoke(this, null);

                PerformSpeech(startUpAbility);

                    PerformSound(startUpAbility);
            }
        }
    }
}