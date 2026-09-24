using System.Collections.Generic;
using Paniq.Simulation;

namespace Paniq.Presentation
{
    /// <summary>
    /// The run's causal log, said out loud. Every event the simulation records
    /// already knows who did it, to whom, when and why; this turns one of them
    /// into a sentence somebody can read.
    /// <para>
    /// People are named by the number drawn over their head and shown in the
    /// stats table, so a line in the log points at somebody the player watched.
    /// Doors are named by the number the hover hint uses. Things are named by
    /// what they are, because "the microwave" is worth more than an ID.
    /// </para>
    /// <para>
    /// This reads the log and nothing else. It decides no outcome and changes
    /// nothing, which is the rule for anything on this side of the line.
    /// </para>
    /// </summary>
    internal sealed class EventStory
    {
        /// <summary>Person ID to the number over their head, which starts at one.</summary>
        private readonly Dictionary<ulong, int> personNumbers = new Dictionary<ulong, int>();

        /// <summary>Thing ID to what it is, so a line can say "a chair" rather than "3104".</summary>
        private readonly Dictionary<ulong, string> thingNames = new Dictionary<ulong, string>();

        private readonly HashSet<ulong> doorIds = new HashSet<ulong>();

        public EventStory(RunSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                personNumbers[snapshot.Agents[i].AgentId.Value] = i + 1;
            }

            for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
            {
                PhysicsObjectSnapshot thing = snapshot.PhysicsObjects[i];
                thingNames[thing.ObjectId.Value] = NameOfKind(thing.Kind);
            }

            for (int i = 0; i < snapshot.Doors.Count; i++)
            {
                doorIds.Add(snapshot.Doors[i].DoorId.Value);
            }
        }

        /// <summary>
        /// The chatter. These happen dozens or hundreds of times in a round --
        /// the fire creeping one square, an extinguisher hissing, people
        /// bumping shoulders -- and printed one to a line they bury everything
        /// worth reading. The log folds runs of them into a single line.
        /// </summary>
        public static bool IsBackground(CausalEventType type)
        {
            switch (type)
            {
                case CausalEventType.FireSpread:
                case CausalEventType.FireDoused:
                case CausalEventType.ExtinguisherSprayed:
                case CausalEventType.AgentYelled:
                case CausalEventType.AgentNoticedSound:
                case CausalEventType.AgentsCollided:
                case CausalEventType.BoxBumped:
                case CausalEventType.BoxesCollided:
                case CausalEventType.AlarmRang:
                case CausalEventType.ObjectBurntOut:
                case CausalEventType.PowerSparkArrived:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The number drawn over this person's head, or nothing if the ID is
        /// not a person's. Shared with anything else that names people, so the
        /// "3" on a pop-up sign and the "person 3" in the log are the same
        /// person the player has been watching.
        /// </summary>
        public int? NumberOf(SimulationId id)
        {
            return personNumbers.TryGetValue(id.Value, out int number) ? number : (int?)null;
        }

        /// <summary>The tick as a clock reading, counted from the start of the run.</summary>
        /// <remarks>Kept beside the other formatting helpers.</remarks>
        public static string TimeOf(int tick)
        {
            int seconds = tick / Run.TicksPerSecond;
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        /// <summary>What happened, in one sentence.</summary>
        public string Describe(CausalEvent record)
        {
            string who = Name(record.SourceId);
            string whom = Name(record.TargetId);
            switch (record.EventType)
            {
                case CausalEventType.RoundEventTriggered: return "you set it off";
                case CausalEventType.FireActivated: return "a fire took hold";
                case CausalEventType.FireSpread: return "the fire spread";
                case CausalEventType.FireDoused: return "a burning patch went out";

                case CausalEventType.AgentAlerted: return $"{who} noticed something was wrong";
                case CausalEventType.AgentYelled: return $"{who} shouted";
                case CausalEventType.AgentNoticedSound: return $"{who} heard something";
                case CausalEventType.AgentScared: return $"{who} panicked";
                case CausalEventType.AgentFroze: return $"{who} froze on the spot";
                case CausalEventType.AgentUnfroze: return $"{who} came out of it and moved";
                case CausalEventType.AgentCaughtFire: return $"{who} caught fire";
                case CausalEventType.AgentRolled: return $"{who} threw themselves down and rolled";
                case CausalEventType.AgentLost: return $"{who} did not make it";
                case CausalEventType.AgentEscaped: return $"{who} got out of the building";
                case CausalEventType.AgentSurvived: return $"{who} was still alive inside at the end";

                case CausalEventType.AgentsCollided: return $"{who} ran into {whom}";
                case CausalEventType.AgentKnockedDown: return $"{who} was knocked off their feet";
                case CausalEventType.AgentTripped: return $"{who} tripped";
                case CausalEventType.AgentGotUp: return $"{who} picked themselves up";
                case CausalEventType.AgentPassedOut: return $"{who} was knocked out cold";
                case CausalEventType.AgentCameTo: return $"{who} came round";
                case CausalEventType.AgentCrushed: return $"{who} was squeezed off their feet by the crush";
                case CausalEventType.AgentShoved: return $"{who} heaved {whom} out of the way";

                case CausalEventType.AgentLookedForAWayOut: return $"{who} did not know the way out and went looking";
                case CausalEventType.AgentFoundADeadEnd: return $"{who} found only a dead end";
                case CausalEventType.AgentFoundTheWayOut:
                    switch ((WayLearned)record.Strength)
                    {
                        case WayLearned.Sign: return $"{who} read a sign and knew the way out";
                        case WayLearned.SawItOpen: return $"{who} saw a way out open up";
                        case WayLearned.Told: return $"{who} was shown the way out";
                        default: return $"{who} spotted the way out";
                    }

                case CausalEventType.DoorUnlocked: return $"{who} was unlocked";
                case CausalEventType.DoorOpened: return $"{who} was opened";
                case CausalEventType.DoorClosed: return $"{who} shut {whom}";
                case CausalEventType.DoorLocked: return $"{who} locked {whom}";
                case CausalEventType.DoorBrokenDown: return $"{who} shouldered {whom} off its hinges";
                case CausalEventType.DoorBurntThrough: return $"{who} burnt through and the fire came on";
                case CausalEventType.DoorBlocked: return $"{who} came to rest in {whom} and jammed it";
                case CausalEventType.DoorUnblocked: return $"{whom} was clear again";
                case CausalEventType.AgentTriedDoor: return $"{who} tried a door and it would not open";
                case CausalEventType.AgentForcedDoor: return $"{who} threw a shoulder at a door";
                case CausalEventType.AgentGaveUpOnDoor: return $"{who} gave up on a door";
                case CausalEventType.AgentBarricadedDoor: return $"{who} wedged something against {whom}";
                case CausalEventType.AgentShovedObstruction: return $"{who} heaved {whom} out of a doorway";

                case CausalEventType.AgentTookExtinguisher: return $"{who} picked up {whom}";
                case CausalEventType.ExtinguisherSprayed: return "an extinguisher was sprayed at the fire";
                case CausalEventType.ExtinguisherEmptied: return $"{who} ran dry";
                case CausalEventType.AgentBlasted: return $"{whom} was knocked over by the jet";
                case CausalEventType.AgentDoused: return $"{whom} was hosed down and put out";

                case CausalEventType.LeaderCalledPeopleOn: return $"{who} called everybody on";
                case CausalEventType.LeaderOrderedDoorBroken: return $"{who} sent {whom} at a door";
                case CausalEventType.LeaderOrderedFireFought: return $"{who} sent {whom} for an extinguisher";

                case CausalEventType.AgentShookAwake: return $"{who} shook {whom} awake";
                case CausalEventType.AgentGrabbed: return $"{who} took hold of {whom}";
                case CausalEventType.AgentDropped: return $"{who} let go of {whom}";
                case CausalEventType.AgentRescued: return $"{who} dragged {whom} clear";

                case CausalEventType.ItemThrown: return $"{who} flung {whom} away from them";
                case CausalEventType.TableHeaved: return $"{who} heaved a table out of the way";
                case CausalEventType.ObjectPopped: return $"{who} went over and its bulb popped";
                case CausalEventType.ItemDropped: return $"{who} dropped {whom}";
                case CausalEventType.BoxBumped: return $"{whom} was knocked about";
                case CausalEventType.BoxHitAgent: return $"{whom} was hit by something flying";
                case CausalEventType.BoxesCollided: return $"{who} knocked into {whom}";
                case CausalEventType.ObjectCaughtFire: return $"{who} caught fire";
                case CausalEventType.ObjectBurntOut: return $"{who} burnt out";
                case CausalEventType.ObjectBroke: return $"{who} broke";
                case CausalEventType.ObjectExploded: return $"{who} went off with a bang";

                case CausalEventType.AlarmPulled: return $"{who} hit a fire alarm";
                case CausalEventType.AlarmRang: return "the alarms rang out";

                case CausalEventType.PowerBeefcake: return $"you made {whom} as strong as anyone can be";
                case CausalEventType.PowerCourage: return $"you made {whom} fearless";
                case CausalEventType.PowerTerror: return $"you put the fear of God into {whom}";
                case CausalEventType.PowerBastard: return $"you turned {whom} nasty";
                case CausalEventType.PowerColdHeart: return $"you stopped {whom} caring what happened to anybody";
                case CausalEventType.PowerSpawnedFire: return "you started a fire of your own";
                case CausalEventType.PowerSpawnedExtinguisher: return "you stood an extinguisher on the floor";
                case CausalEventType.PowerBlastedWall: return "you blew a hole through a wall";
                case CausalEventType.PowerPoppedFuseBox: return "you popped the fuse box";

                case CausalEventType.PowerSparkStarted:
                    return $"a spark set off along the cable from {Name(record.SourceId)} " +
                           $"toward {Name(record.TargetId)}";
                case CausalEventType.PowerSparkArrived:
                    return $"the spark reached {Name(record.TargetId)}";

                case CausalEventType.CardDealt:
                    return $"{who} died, and dealt you {PlayerInput.NameOf((PlayerCommandType)record.Strength)}";

                case CausalEventType.RoundEnded: return $"the round ended with {record.Strength} saved";
                default: return record.EventType.ToString();
            }
        }

        /// <summary>
        /// Whatever this ID belongs to, said the way the player already sees it:
        /// a person by their number, a door by the number the hover hint uses,
        /// a thing by what it is.
        /// </summary>
        private string Name(SimulationId id)
        {
            if (id.Value == 0UL)
            {
                return "somebody";
            }

            if (personNumbers.TryGetValue(id.Value, out int number))
            {
                return $"person {number}";
            }

            if (doorIds.Contains(id.Value))
            {
                return $"door {id.Value}";
            }

            return thingNames.TryGetValue(id.Value, out string thing) ? thing : "something";
        }

        private static string NameOfKind(PhysicsObjectKind kind)
        {
            switch (kind)
            {
                case PhysicsObjectKind.Box: return "a cardboard box";
                case PhysicsObjectKind.Chair: return "a chair";
                case PhysicsObjectKind.OfficeChair: return "an office chair";
                case PhysicsObjectKind.WasteBin: return "a waste bin";
                case PhysicsObjectKind.PottedPlant: return "a potted plant";
                case PhysicsObjectKind.Bag: return "a bag";
                case PhysicsObjectKind.Laptop: return "a laptop";
                case PhysicsObjectKind.Extinguisher: return "an extinguisher";
                case PhysicsObjectKind.Briefcase: return "a briefcase";
                case PhysicsObjectKind.Microwave: return "the microwave";
                case PhysicsObjectKind.WallSocket: return "a wall socket";
                case PhysicsObjectKind.FuseBox: return "the fuse box";
                case PhysicsObjectKind.TableWreck: return "a heap of broken boards";
                case PhysicsObjectKind.VendingMachine: return "the vending machine";
                case PhysicsObjectKind.Cabinet: return "a filing cabinet";
                case PhysicsObjectKind.Shelves: return "a set of shelves";
                case PhysicsObjectKind.CopyMachine: return "the copier";
                case PhysicsObjectKind.Whiteboard: return "a whiteboard";
                case PhysicsObjectKind.StandingLamp: return "a standing lamp";
                case PhysicsObjectKind.LampShade: return "a lamp shade";
                case PhysicsObjectKind.RobotVacuum: return "the robot vacuum";
                default: return "something";
            }
        }
    }
}
