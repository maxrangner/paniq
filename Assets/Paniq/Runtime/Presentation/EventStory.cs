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

        public EventStory(FireReactionSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                personNumbers[snapshot.Agents[i].AgentId.Value] = i + 1;
            }

            for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
            {
                FireReactionPhysicsObjectSnapshot thing = snapshot.PhysicsObjects[i];
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
        public static bool IsBackground(FireReactionEventType type)
        {
            switch (type)
            {
                case FireReactionEventType.FireSpread:
                case FireReactionEventType.FireDoused:
                case FireReactionEventType.ExtinguisherSprayed:
                case FireReactionEventType.AgentYelled:
                case FireReactionEventType.AgentNoticedSound:
                case FireReactionEventType.AgentsCollided:
                case FireReactionEventType.BoxBumped:
                case FireReactionEventType.BoxesCollided:
                case FireReactionEventType.AlarmRang:
                case FireReactionEventType.ObjectBurntOut:
                case FireReactionEventType.PowerSparkArrived:
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
            int seconds = tick / FireReactionSimulation.TicksPerSecond;
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        /// <summary>What happened, in one sentence.</summary>
        public string Describe(CausalEvent record)
        {
            string who = Name(record.SourceId);
            string whom = Name(record.TargetId);
            switch (record.EventType)
            {
                case FireReactionEventType.RoundEventTriggered: return "you set it off";
                case FireReactionEventType.FireActivated: return "a fire took hold";
                case FireReactionEventType.FireSpread: return "the fire spread";
                case FireReactionEventType.FireDoused: return "a burning patch went out";

                case FireReactionEventType.AgentAlerted: return $"{who} noticed something was wrong";
                case FireReactionEventType.AgentYelled: return $"{who} shouted";
                case FireReactionEventType.AgentNoticedSound: return $"{who} heard something";
                case FireReactionEventType.AgentScared: return $"{who} panicked";
                case FireReactionEventType.AgentFroze: return $"{who} froze on the spot";
                case FireReactionEventType.AgentUnfroze: return $"{who} came out of it and moved";
                case FireReactionEventType.AgentCaughtFire: return $"{who} caught fire";
                case FireReactionEventType.AgentRolled: return $"{who} threw themselves down and rolled";
                case FireReactionEventType.AgentLost: return $"{who} did not make it";
                case FireReactionEventType.AgentEscaped: return $"{who} got out of the building";
                case FireReactionEventType.AgentSurvived: return $"{who} was still alive inside at the end";

                case FireReactionEventType.AgentsCollided: return $"{who} ran into {whom}";
                case FireReactionEventType.AgentKnockedDown: return $"{who} was knocked off their feet";
                case FireReactionEventType.AgentTripped: return $"{who} tripped";
                case FireReactionEventType.AgentGotUp: return $"{who} picked themselves up";
                case FireReactionEventType.AgentPassedOut: return $"{who} was knocked out cold";
                case FireReactionEventType.AgentCameTo: return $"{who} came round";
                case FireReactionEventType.AgentCrushed: return $"{who} was squeezed off their feet by the crush";
                case FireReactionEventType.AgentShoved: return $"{who} heaved {whom} out of the way";

                case FireReactionEventType.DoorUnlocked: return $"{who} was unlocked";
                case FireReactionEventType.DoorOpened: return $"{who} was opened";
                case FireReactionEventType.DoorClosed: return $"{who} shut {whom}";
                case FireReactionEventType.DoorLocked: return $"{who} locked {whom}";
                case FireReactionEventType.DoorBrokenDown: return $"{who} shouldered {whom} off its hinges";
                case FireReactionEventType.DoorBurntThrough: return $"{who} burnt through and the fire came on";
                case FireReactionEventType.DoorBlocked: return $"{who} came to rest in {whom} and jammed it";
                case FireReactionEventType.DoorUnblocked: return $"{whom} was clear again";
                case FireReactionEventType.AgentTriedDoor: return $"{who} tried a door and it would not open";
                case FireReactionEventType.AgentForcedDoor: return $"{who} threw a shoulder at a door";
                case FireReactionEventType.AgentGaveUpOnDoor: return $"{who} gave up on a door";
                case FireReactionEventType.AgentBarricadedDoor: return $"{who} wedged something against {whom}";
                case FireReactionEventType.AgentShovedObstruction: return $"{who} heaved {whom} out of a doorway";

                case FireReactionEventType.AgentTookExtinguisher: return $"{who} picked up {whom}";
                case FireReactionEventType.ExtinguisherSprayed: return "an extinguisher was sprayed at the fire";
                case FireReactionEventType.ExtinguisherEmptied: return $"{who} ran dry";
                case FireReactionEventType.AgentBlasted: return $"{whom} was knocked over by the jet";
                case FireReactionEventType.AgentDoused: return $"{whom} was hosed down and put out";

                case FireReactionEventType.LeaderCalledPeopleOn: return $"{who} called everybody on";
                case FireReactionEventType.LeaderOrderedDoorBroken: return $"{who} sent {whom} at a door";
                case FireReactionEventType.LeaderOrderedFireFought: return $"{who} sent {whom} for an extinguisher";

                case FireReactionEventType.AgentShookAwake: return $"{who} shook {whom} awake";
                case FireReactionEventType.AgentGrabbed: return $"{who} took hold of {whom}";
                case FireReactionEventType.AgentDropped: return $"{who} let go of {whom}";
                case FireReactionEventType.AgentRescued: return $"{who} dragged {whom} clear";

                case FireReactionEventType.ItemThrown: return $"{who} flung {whom} away from them";
                case FireReactionEventType.ItemDropped: return $"{who} dropped {whom}";
                case FireReactionEventType.BoxBumped: return $"{whom} was knocked about";
                case FireReactionEventType.BoxHitAgent: return $"{whom} was hit by something flying";
                case FireReactionEventType.BoxesCollided: return $"{who} knocked into {whom}";
                case FireReactionEventType.ObjectCaughtFire: return $"{who} caught fire";
                case FireReactionEventType.ObjectBurntOut: return $"{who} burnt out";
                case FireReactionEventType.ObjectBroke: return $"{who} broke";
                case FireReactionEventType.ObjectExploded: return $"{who} went off with a bang";

                case FireReactionEventType.AlarmPulled: return $"{who} hit a fire alarm";
                case FireReactionEventType.AlarmRang: return "the alarms rang out";

                case FireReactionEventType.PowerBeefcake: return $"you made {whom} as strong as anyone can be";
                case FireReactionEventType.PowerSpawnedFire: return "you started a fire of your own";
                case FireReactionEventType.PowerSpawnedExtinguisher: return "you stood an extinguisher on the floor";
                case FireReactionEventType.PowerBlastedWall: return "you blew a hole through a wall";
                case FireReactionEventType.PowerPoppedFuseBox: return "you popped the fuse box";

                case FireReactionEventType.PowerSparkStarted:
                    return $"a spark set off along the cable from {Name(record.SourceId)} " +
                           $"toward {Name(record.TargetId)}";
                case FireReactionEventType.PowerSparkArrived:
                    return $"the spark reached {Name(record.TargetId)}";

                case FireReactionEventType.RoundEnded: return $"the round ended with {record.Strength} saved";
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
                default: return "something";
            }
        }
    }
}
