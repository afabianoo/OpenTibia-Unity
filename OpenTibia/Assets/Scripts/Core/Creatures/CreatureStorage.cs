﻿using System.Collections.Generic;
using System.Linq;

namespace OpenTibiaUnity.Core.Creatures
{
    public class CreatureStorage {
        public class OpponentsChangeEvent : UnityEngine.Events.UnityEvent<List<Creature>> {}

        private int m_CreatureCount = 0;
        private OpponentStates m_OpponentState = OpponentStates.NoAction;
        private List<Creature> m_Opponents = new List<Creature>();
        private int m_CreatureIndex = 0;
        private readonly int m_MaxCreaturesCount = 1300;
        private List<Creature> m_Creatures = new List<Creature>();

        public Creature Aim { get; set; } = null;
        public Creature AttackTarget { get; set; } = null;
        public Creature FollowTarget { get; set; } = null;
        List<Creature> Trappers { get; set; } = null;
        public Player Player { get; set; }

        public OpponentsChangeEvent onOpponentsRefreshed {
            get; private set;
        } = new OpponentsChangeEvent();

        public OpponentsChangeEvent onOpponentsRebuilt {
            get; private set;
        } = new OpponentsChangeEvent();

        public CreatureStorage() {
            Player = new Player(0);
        }

        public void SetAim(Creature aim) {
            if (Aim != aim) {
                var creature = Aim;
                Aim = aim;
                UpdateExtendedMark(creature);
                UpdateExtendedMark(Aim);
            }
        }

        public Creature GetNextAttackTarget(int step) {
            step = step < 0 ? -1 : 1;

            int total = m_Opponents.Count;
            if (total < 1)
                return null;

            int attackedIndex = AttackTarget ? m_Opponents.FindIndex((x) => x == AttackTarget) : -1;
            
            for (int i = 0; i < total; i++) {
                attackedIndex += step;
                if (attackedIndex >= total)
                    attackedIndex = 0;

                if (attackedIndex < 0)
                    attackedIndex = total - 1;

                var creature = m_Opponents[attackedIndex];
                if (creature.Type != CreatureTypes.NPC)
                    return creature;
            }

            return null;
        }
        
        public void MarkAllOpponentsVisible(bool value) {
            foreach (var opponent in m_Opponents) {
                opponent.Visible = value;
            }

            if (m_Opponents.Count > 0)
                InvalidateOpponents();
        }

        public Creature ReplaceCreature(Creature creature, uint id = 0) {
            if (!creature)
                throw new System.ArgumentException("CreatureStorage.replaceCreature: Invalid creature.");
            
            if (id != 0)
                RemoveCreature(id);

            if (m_CreatureCount >= m_MaxCreaturesCount)
                throw new System.ArgumentException("CreatureStorage.replaceCreature: No space left to append " + creature.ID);
            
            int index = 0;
            int lastIndex = m_CreatureCount - 1;
            while (index <= lastIndex) {
                int tmpIndex = index + lastIndex >> 1;
                var foundCreature = m_Creatures[tmpIndex];
                if (foundCreature.ID < creature.ID)
                    index = tmpIndex + 1;
                else if (foundCreature.ID > creature.ID)
                    lastIndex = tmpIndex - 1;
                else
                    return foundCreature;
            }

            creature.KnownSince = ++m_CreatureIndex;
            m_Creatures.Insert(index, creature);
            m_CreatureCount++;
            m_OpponentState = OpponentStates.Rebuild;
            return creature;
        }

        public void ToggleFollowTarget(Creature follow, bool send) {
            if (follow == Player) {
                throw new System.ArgumentException("CreatureStorage.ToggleFollowTarget: Cannot follow player.");
            }

            var creature = FollowTarget;
            if (creature != follow)
                FollowTarget = follow;
            else
                FollowTarget = null;
            
            if (send) {
                var protocolGame = OpenTibiaUnity.ProtocolGame;
                if (protocolGame)
                    protocolGame.SendFollow(FollowTarget ? FollowTarget.ID : 0);
            }

            UpdateExtendedMark(creature);
            UpdateExtendedMark(FollowTarget);

            if (AttackTarget) {
                creature = AttackTarget;
                AttackTarget = null;
                UpdateExtendedMark(creature);
            }
        }

        public Creature GetCreatureByName(string name) {
            return m_Creatures.Find((x) => name == x.Name);
        }

        public void RefreshOpponents() {
            switch (m_OpponentState) {
                case OpponentStates.NoAction:
                    break;

                case OpponentStates.Refresh:
                case OpponentStates.Rebuild: {
                    m_Opponents.Clear();
                    for (int i = 0; i < m_CreatureCount; i++) {
                        Creature creature = m_Creatures[i];
                        if (IsOpponent(creature, true))
                            m_Opponents.Add(creature);
                    }
                    
                    m_Opponents.Sort(OpponentComparator);
                    onOpponentsRebuilt.Invoke(m_Opponents);
                    break;
                }
            }
            
            m_OpponentState = OpponentStates.NoAction;
        }

        protected int OpponentComparator(Creature a, Creature b) {
            if (a == null || b == null)
                return 0;
            
            var pos = 0;
            var sortType = OpenTibiaUnity.OptionStorage.OpponentSort;

            bool desc = false;
            if (sortType == OpponentSortTypes.SortDistanceDesc || sortType == OpponentSortTypes.SortHitpointsDesc || sortType == OpponentSortTypes.SortKnownSinceDesc || sortType == OpponentSortTypes.SortNameDesc)
                desc = true;

            switch (sortType) {
                case OpponentSortTypes.SortDistanceAsc:
                case OpponentSortTypes.SortDistanceDesc:
                    var myPosition = Player.Position;
                    var d1 = System.Math.Max(System.Math.Abs(myPosition.x - a.Position.x), System.Math.Abs(myPosition.y - a.Position.y));
                    var d2 = System.Math.Max(System.Math.Abs(myPosition.x - b.Position.x), System.Math.Abs(myPosition.y - b.Position.y));
                    if (d1 < d2)
                        pos = -1;
                    else if (d1 > d2)
                        pos = 1;

                    break;

                case OpponentSortTypes.SortHitpointsAsc:
                case OpponentSortTypes.SortHitpointsDesc:
                    if (a.HealthPercent < b.HealthPercent)
                        pos = -1;
                    else if (a.HealthPercent > b.HealthPercent)
                        pos = 1;

                    break;

                case OpponentSortTypes.SortNameAsc:
                case OpponentSortTypes.SortNameDesc:
                    pos = a.Name.CompareTo(b.Name);
                    break;

                case OpponentSortTypes.SortKnownSinceAsc:
                case OpponentSortTypes.SortKnownSinceDesc:
                    break;
            }

            if (pos == 0) {
                if (a.KnownSince < b.KnownSince)
                    pos = -1;
                else if (a.KnownSince > b.KnownSince)
                    pos = 1;
                else
                    return 0;
            }

            return pos * (desc ? -1 : 1);
        }

        public void SetFollowTarget(Creature follow, bool send) {
            if (follow == Player) {
                throw new System.ArgumentException("CreatureStorage.ToggleFollowTarget: Cannot follow player.");
            }

            Creature creature = FollowTarget;
            if (creature != follow) {
                FollowTarget = creature;

                if (send) {
                    Network.ProtocolGame protocolGame = OpenTibiaUnity.ProtocolGame;
                    if (protocolGame)
                        protocolGame.SendFollow(FollowTarget ? FollowTarget.ID : 0);
                }

                UpdateExtendedMark(creature);
                UpdateExtendedMark(FollowTarget);
            }

            if (AttackTarget) {
                creature = AttackTarget;
                AttackTarget = null;
                UpdateExtendedMark(creature);
            }
        }
        
        protected void UpdateExtendedMark(Creature creature) {
            if (!creature)
                return;

            // aim-attack: white outline & red border
            // aim-follow: white outline & green border
            // aim: white outline
            // attack: red border
            // follow: green border

            Appearances.Marks marks = creature.Marks;
            if (creature == Aim) {
                if (creature == AttackTarget) {
                    marks.SetMark(Appearances.Marks.MarkType_ClientMapWindow, Appearances.Marks.MarkAimAttack);
                    marks.SetMark(Appearances.Marks.MarkType_ClientBattleList, Appearances.Marks.MarkAimAttack);
                } else if (creature == FollowTarget) {
                    marks.SetMark(Appearances.Marks.MarkType_ClientMapWindow, Appearances.Marks.MarkAimFollow);
                    marks.SetMark(Appearances.Marks.MarkType_ClientBattleList, Appearances.Marks.MarkAimFollow);
                } else {
                    marks.SetMark(Appearances.Marks.MarkType_ClientMapWindow, Appearances.Marks.MarkAim);
                    marks.SetMark(Appearances.Marks.MarkType_ClientBattleList, Appearances.Marks.MarkAim);
                }
            } else if (creature == AttackTarget) {
                marks.SetMark(Appearances.Marks.MarkType_ClientMapWindow, Appearances.Marks.MarkAttack);
                marks.SetMark(Appearances.Marks.MarkType_ClientBattleList, Appearances.Marks.MarkAttack);
            } else if (creature == FollowTarget) {
                marks.SetMark(Appearances.Marks.MarkType_ClientMapWindow, Appearances.Marks.MarkFollow);
                marks.SetMark(Appearances.Marks.MarkType_ClientBattleList, Appearances.Marks.MarkFollow);
            } else {
                marks.SetMark(Appearances.Marks.MarkType_ClientMapWindow, Appearances.Marks.MarkUnmarked);
                marks.SetMark(Appearances.Marks.MarkType_ClientBattleList, Appearances.Marks.MarkUnmarked);
            }
        }

        public void SetTrappers(List<Creature> trappers) {
            // TODO
        }

        public Creature GetCreature(uint id) {
            int index = 0;
            int lastIndex = m_CreatureCount - 1;
            while (index <= lastIndex) {
                int tmpIndex = index + lastIndex >> 1;
                Creature foundCreature = m_Creatures[tmpIndex];
                if (foundCreature.ID < id)
                    index = tmpIndex + 1;
                else if (foundCreature.ID > id)
                    lastIndex = tmpIndex - 1;
                else
                    return foundCreature;
            }

            return null;
        }

        public void InvalidateOpponents() {
            if (m_OpponentState < OpponentStates.Refresh)
                m_OpponentState = OpponentStates.Refresh;
        }

        public void MarkOpponentVisible(object param, bool visible) {
            Creature creature;
            if (param is Creature) {
                creature = param as Creature;
            } else if (param is Appearances.ObjectInstance obj) {
                creature = GetCreature(obj.Data);
            } else if (param is uint || param is int) {
                creature = GetCreature((uint)param);
            } else {
                throw new System.ArgumentException("CreatureStorage.MarkOpponentVisible: Invalid overload.");
            }

            if (creature) {
                creature.Visible = visible;
                InvalidateOpponents();
            }
        }

        public bool IsOpponent(Creature creature) {
            return IsOpponent(creature, false);
        }

        protected bool IsOpponent(Creature creature, bool deepCheck) {
            if (!creature || creature is Player)
                return false;

            var creaturePosition = creature.Position;
            var myPosition = Player.Position;

            if (creaturePosition.z != myPosition.z || System.Math.Abs(creaturePosition.x - myPosition.x) > Constants.MapWidth / 2 || System.Math.Abs(creaturePosition.y - myPosition.y) > Constants.MapHeight / 2)
                return false;

            var filter = OpenTibiaUnity.OptionStorage.OpponentFilter;
            if (!deepCheck || filter == OpponentFilters.None)
                return true;

            if ((filter & OpponentFilters.Players) > 0 && creature.Type == CreatureTypes.Player)
                return false;

            if ((filter & OpponentFilters.NPCs) > 0 && creature.Type == CreatureTypes.NPC)
                return false;

            if ((filter & OpponentFilters.Monsters) > 0 && creature.Type == CreatureTypes.Monster)
                return false;

            if ((filter & OpponentFilters.NonSkulled) > 0 && creature.Type == CreatureTypes.Player && creature.PKFlag == PKFlags.None)
                return false;

            if ((filter & OpponentFilters.Party) > 0 && creature.PartyFlag != PartyFlags.None)
                return false;

            if ((filter & OpponentFilters.Summons) > 0 && creature.SummonTypeFlag != SummonTypeFlags.None)
                return false;
            
            return true;
        }

        public void Reset(bool resetPlayer = true) {
            if (resetPlayer)
                Player.Reset();

            m_Creatures.ForEach(creature => creature.Reset());

            m_Creatures.Clear();
            m_CreatureCount = 0;
            m_CreatureIndex = 0;

            if (!resetPlayer)
                ReplaceCreature(Player);

            m_Opponents.Clear();
            m_OpponentState = OpponentStates.NoAction;
            Aim = null;
            AttackTarget = null;
            FollowTarget = null;
            Trappers = null;
        }

        public void RemoveCreature(uint id) {
            int currentIndex = 0;
            int lastIndex = m_CreatureCount - 1;

            int foundIndex = -1;
            Creature foundCreature = null;
            while (currentIndex <= lastIndex) {
                int tmpIndex = currentIndex + lastIndex >> 1;
                foundCreature = m_Creatures[tmpIndex];
                if (foundCreature.ID < id) {
                    currentIndex = tmpIndex + 1;
                } else if (foundCreature.ID > id) {
                    lastIndex = tmpIndex - 1;
                } else {
                    foundIndex = tmpIndex;
                    break;
                }
            }

            if (!foundCreature || foundIndex < 0)
                throw new System.ArgumentException("CreatureStorage.RemoveCreature: creature " + id + " not found");
            else if (foundCreature == Player)
                throw new System.Exception("CreatureStorage.RemoveCreature: cannot remove player.");

            if (foundCreature == Aim) {
                Aim = null;
                UpdateExtendedMark(foundCreature);
            }

            if (foundCreature == AttackTarget) {
                AttackTarget = null;
                UpdateExtendedMark(foundCreature);
            }

            if (foundCreature == FollowTarget) {
                FollowTarget = null;
                UpdateExtendedMark(foundCreature);
            }

            if (Trappers != null) {
                int index = Trappers.FindIndex((x) => x == foundCreature);
                if (index > 0) {
                    Trappers[index].Trapper = false;
                    Trappers.RemoveAt(index);
                }
            }

            foundCreature.Reset();
            m_Creatures.RemoveAt(foundIndex);
            m_CreatureCount--;
            m_OpponentState = OpponentStates.Rebuild;
        }

        public void Animate() {
            int ticks = OpenTibiaUnity.TicksMillis;

            foreach (var creature in m_Creatures) {
                if (creature) {
                    creature.AnimateMovement(ticks);
                    creature.AnimateOutfit(ticks);
                }
            }
        }

        public void ToggleAttackTarget(Creature attack, bool send) {
            if (attack == Player) {
                throw new System.ArgumentException("CreatureStorage.ToggleFollowTarget: Cannot follow player.");
            }

            var creature = FollowTarget;
            if (creature != attack)
                AttackTarget = attack;
            else
                AttackTarget = null;

            if (send) {
                var protocolGame = OpenTibiaUnity.ProtocolGame;
                if (protocolGame)
                    protocolGame.SendAttack(AttackTarget ? AttackTarget.ID : 0);
            }

            UpdateExtendedMark(creature);
            UpdateExtendedMark(AttackTarget);
            
            if (FollowTarget) {
                creature = FollowTarget;
                FollowTarget = null;
                UpdateExtendedMark(creature);
            }
        }

        public void ClearTargets() {
            var optionStorage = OpenTibiaUnity.OptionStorage;
            if (AttackTarget != null && optionStorage.AutoChaseOff && optionStorage.CombatChaseMode != CombatChaseModes.Off) {
                optionStorage.CombatChaseMode = CombatChaseModes.Off;
                var protocolGame = OpenTibiaUnity.ProtocolGame;
                if (protocolGame != null && protocolGame.IsGameRunning)
                    protocolGame.SendSetTactics();
            }

            if (FollowTarget != null)
                SetFollowTarget(null, true);
        }

        public void SetAttackTarget(Creature attack, bool send) {
            if (attack == Player)
                throw new System.ArgumentException("CreatureStorage.ToggleFollowTarget: Cannot follow player.");

            var creature = AttackTarget;
            if (creature != attack) {
                AttackTarget = creature;

                if (send) {
                    var protocolGame = OpenTibiaUnity.ProtocolGame;
                    if (protocolGame)
                        protocolGame.SendAttack(AttackTarget ? AttackTarget.ID : 0);
                }

                UpdateExtendedMark(creature);
                UpdateExtendedMark(AttackTarget);
            }

            if (FollowTarget) {
                creature = FollowTarget;
                FollowTarget = null;
                UpdateExtendedMark(creature);
            }
        }
    }
}
