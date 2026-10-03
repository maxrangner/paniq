namespace Paniq.Simulation
{
    /// <summary>
    /// What a person can see of their situation this tick, worked out once
    /// before anything is asked of them (2026-10-03, the one task model).
    /// </summary>
    internal readonly struct Situation
    {
        public Situation(bool choosing, bool inDanger, bool eager, int roll = 0, bool justMoved = false)
        {
            Choosing = choosing;
            InDanger = inDanger;
            Eager = eager;
            Roll = roll;
            JustMoved = justMoved;
        }

        /// <summary>
        /// The one dice roll of a calm decision, nought to ninety-nine: the
        /// calm options take bands of it, so one roll decides between them
        /// (tidying the lowest, then sitting, then going back to a desk).
        /// </summary>
        public readonly int Roll;

        /// <summary>The calm activity that just ended was a walk somewhere: people usually stop a moment.</summary>
        public readonly bool JustMoved;

        /// <summary>
        /// A decision moment: whatever else they might take up is weighed now.
        /// In between, only what is under way carries on.
        /// </summary>
        public readonly bool Choosing;

        /// <summary>The flames are inside their danger distance.</summary>
        public readonly bool InDanger;

        /// <summary>Set on a way out they can see standing open, right now.</summary>
        public readonly bool Eager;
    }

    /// <summary>
    /// One thing a person might take up instead of the default of their state
    /// (calm: their own day; frightened: running). Every option is split the
    /// same way (2026-10-03, the audit of 2026-10-02, A1 and D): carrying on
    /// with it, every tick, and taking it up, only at a decision moment. The
    /// options used to be asked to take themselves up every tick, building
    /// routes as they did, so the first eight or so frightened people used up
    /// the tick's routes and everybody after them walked blind.
    /// </summary>
    internal interface ITaskOption
    {
        /// <summary>Whether this person is on this option's task now.</summary>
        bool IsDoing(Agent agent);

        /// <summary>
        /// One tick of the task they are on. No intent: nothing for this tick
        /// (a follower close behind their leader runs their own way), or the
        /// task has just ended, through <see cref="Tasks.End"/>.
        /// </summary>
        MotorIntent? Continue(Agent agent, in Situation situation);

        /// <summary>
        /// Whether this is worth weighing for them at all: cheap, with no
        /// routes and no random draws. Only a yes is asked to begin.
        /// </summary>
        bool Wants(Agent agent, in Situation situation);

        /// <summary>
        /// Takes the task up if it can: true when they have, with the first
        /// tick of it (or none, when the task's own step gives the next one).
        /// False when it fell through, or when taking it up is a deed done at
        /// once (a leader's shout) and they go on as they were.
        /// </summary>
        bool TryBegin(Agent agent, in Situation situation, out MotorIntent? first);
    }

    /// <summary>
    /// The one chooser (2026-10-03): the options in the order a person weighs
    /// them. What is under way carries on every tick; at a decision moment
    /// every option they are not already doing is weighed, in order, and the
    /// first that takes itself up wins. The order is the priority and is
    /// written down only where the chooser is built.
    /// </summary>
    internal sealed class TaskChooser
    {
        private readonly ITaskOption[] options;

        public TaskChooser(params ITaskOption[] options)
        {
            this.options = options;
        }

        /// <summary>What they do this tick instead of the default of their state, or nothing.</summary>
        public MotorIntent? Step(Agent agent, in Situation situation)
        {
            for (int i = 0; i < options.Length; i++)
            {
                ITaskOption option = options[i];
                if (option.IsDoing(agent))
                {
                    MotorIntent? move = option.Continue(agent, situation);
                    if (move.HasValue)
                    {
                        return move;
                    }

                    continue;
                }

                if (situation.Choosing && option.Wants(agent, situation) && option.TryBegin(agent, situation, out MotorIntent? first))
                {
                    return first;
                }
            }

            return null;
        }

        /// <summary>
        /// A decision moment for somebody with nothing under way (the calm,
        /// whose day steps each activity itself): the first option, in order,
        /// that takes itself up. False when none did.
        /// </summary>
        public bool Choose(Agent agent, in Situation situation)
        {
            for (int i = 0; i < options.Length; i++)
            {
                ITaskOption option = options[i];
                if (option.Wants(agent, situation) && option.TryBegin(agent, situation, out _))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>How a task came to an end.</summary>
    internal enum TaskEnd
    {
        /// <summary>It was done: the alarm pulled, the person dragged clear, the crate shifted.</summary>
        Done,

        /// <summary>It came to nothing: no way there, hemmed in too long, out of time.</summary>
        GaveUp,

        /// <summary>Something bigger took them off it: the flames, a fright, calming down.</summary>
        Interrupted
    }

    internal static partial class Tasks
    {
        /// <summary>
        /// The one way a task ends (2026-10-03, the audit's D: twelve copies of
        /// "give up and think again" disagreed about what the hand made of it,
        /// so the hand behaved differently depending on which task failed).
        /// What the hand makes of it, the same whatever the task was: done,
        /// the goal is over; given up, a task taken for the hand costs
        /// conviction and a beat, and one of their own costs nothing; cut
        /// short, the goal stays for the other side of them to answer. Then
        /// the route is forgotten and they go back to the default of their
        /// state -- running, thinking again a beat later, or standing about a
        /// beat before choosing their next thing.
        /// </summary>
        public static void End(Agent agent, TaskEnd how, SimulationContext context, InfluenceSystem influence,
            FrightenedWalk walk)
        {
            bool forTheHand = agent.Hand.Acting || (agent.Carry.ForTheHand && agent.Carry.Holding);
            switch (how)
            {
                case TaskEnd.Done:
                    if (forTheHand)
                    {
                        InfluenceSystem.Done(agent);
                    }

                    break;
                case TaskEnd.GaveUp:
                    if (forTheHand && influence != null)
                    {
                        influence.GiveUp(agent);
                    }

                    break;
                default:
                    InfluenceSystem.Interrupted(agent);
                    break;
            }

            walk?.Forget(agent);
            agent.Body.BlockedTicks = 0;
            if (agent.Fear.State == AgentFearState.Calm)
            {
                agent.Intent.Activity = AgentActivityState.Standing;
                agent.Intent.ActivityEndTick = checked(context.Tick + context.ReactionLag());
                return;
            }

            agent.Intent.Activity = AgentActivityState.Fleeing;
            context.ThinkAgainSoon(agent.Intent);
        }
    }
}
