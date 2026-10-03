namespace Paniq.Simulation
{
    /// <summary>
    /// What a person is doing, one level above how their body moves
    /// (2026-10-03, the one task model): the task an activity belongs to.
    /// Calm or frightened, everybody is doing exactly one of these.
    /// </summary>
    internal enum TaskKind
    {
        /// <summary>Standing about, glancing round, strolling: nothing in particular.</summary>
        Idle,

        /// <summary>Turned to see what a noise was.</summary>
        Noticing,

        /// <summary>Going to a chair, sat in it, or getting up out of it.</summary>
        Sitting,

        /// <summary>Tidying a thing away, or carrying one off for the hand.</summary>
        Tidying,

        /// <summary>A cue's script: the meeting ending, home time, a chat, going to look.</summary>
        Errand,

        /// <summary>Running for a way out, or for somewhere away from the danger, and the doors on the way.</summary>
        Escaping,

        /// <summary>Rooted to the spot with fear.</summary>
        Frozen,

        /// <summary>Alight.</summary>
        Burning,

        /// <summary>Behind somebody who is taking charge.</summary>
        Following,

        /// <summary>A bottle to the flames, or to somebody alight.</summary>
        FightingFire,

        /// <summary>Shaking somebody frozen awake, or dragging somebody out cold clear.</summary>
        Helping,

        /// <summary>To a pull station, and pulling it.</summary>
        RaisingTheAlarm,

        /// <summary>Wedging a door against the flames.</summary>
        Barricading,

        /// <summary>Doing what the player's hand asks: coming to it, or heaving a crate for it.</summary>
        ForTheHand
    }

    /// <summary>
    /// The one table of what each activity is (2026-10-03, the audit of
    /// 2026-10-02, section D): the task it belongs to, and the questions other
    /// systems ask about it. Six hand-kept lists used to answer these
    /// questions, each in its own file, so adding one activity meant finding
    /// all six; now it means one row here.
    /// </summary>
    internal static partial class Tasks
    {
        [System.Flags]
        private enum Is
        {
            Nothing = 0,

            /// <summary>Doing nothing in particular: a nudge makes them look round for whoever did it.</summary>
            Loitering = 1,

            /// <summary>A calm idle a cue (the meeting ending, home time) may cut short.</summary>
            Interruptible = 2,

            /// <summary>On the way to something in particular: any open doorway is theirs to walk through.</summary>
            OnTheWay = 4,

            /// <summary>Hands-on at something this moment: the round waits for them before calling them stuck.</summary>
            HandsOn = 8,

            /// <summary>Doing nothing a calming-down has to wait for.</summary>
            FreeToCalm = 16,

            /// <summary>Still held by a chair they are sat in, or lowering themselves into.</summary>
            InTheirSeat = 32
        }

        private readonly struct Row
        {
            public Row(TaskKind kind, Is answers)
            {
                Kind = kind;
                Answers = answers;
            }

            public readonly TaskKind Kind;
            public readonly Is Answers;
        }

        /// <summary>One row per <see cref="AgentActivityState"/>, in its order. A test checks the two stay in step.</summary>
        private static readonly Row[] Rows =
        {
            /* Standing */             new Row(TaskKind.Idle, Is.Loitering | Is.Interruptible | Is.FreeToCalm),
            /* LookingAround */        new Row(TaskKind.Idle, Is.Loitering | Is.Interruptible),
            /* Strolling */            new Row(TaskKind.Idle, Is.Loitering | Is.Interruptible),
            /* Socialising */          new Row(TaskKind.Idle, Is.Loitering | Is.Interruptible),
            /* Reacting */             new Row(TaskKind.Idle, Is.InTheirSeat),
            /* Fleeing */              new Row(TaskKind.Escaping, Is.FreeToCalm),
            /* Hesitating */           new Row(TaskKind.Escaping, Is.FreeToCalm),
            /* Investigating */        new Row(TaskKind.Noticing, Is.Loitering | Is.InTheirSeat),
            /* Frozen */               new Row(TaskKind.Frozen, Is.FreeToCalm),
            /* OpeningDoor */          new Row(TaskKind.Escaping, Is.HandsOn),
            /* TryingDoor */           new Row(TaskKind.Escaping, Is.HandsOn),
            /* ForcingDoor */          new Row(TaskKind.Escaping, Is.HandsOn),
            /* Burning */              new Row(TaskKind.Burning, Is.Nothing),
            /* Following */            new Row(TaskKind.Following, Is.OnTheWay),
            /* FetchingExtinguisher */ new Row(TaskKind.FightingFire, Is.OnTheWay),
            /* Spraying */             new Row(TaskKind.FightingFire, Is.OnTheWay | Is.HandsOn),
            /* GoingToSit */           new Row(TaskKind.Sitting, Is.OnTheWay | Is.InTheirSeat),
            /* Sitting */              new Row(TaskKind.Sitting, Is.Interruptible | Is.InTheirSeat),
            /* StandingUp */           new Row(TaskKind.Sitting, Is.HandsOn | Is.InTheirSeat),
            /* FetchingItem */         new Row(TaskKind.Tidying, Is.OnTheWay),
            /* PickingUp */            new Row(TaskKind.Tidying, Is.Nothing),
            /* CarryingItem */         new Row(TaskKind.Tidying, Is.OnTheWay),
            /* SettingDown */          new Row(TaskKind.Tidying, Is.Nothing),
            /* ShakingAwake */         new Row(TaskKind.Helping, Is.OnTheWay | Is.HandsOn),
            /* Grabbing */             new Row(TaskKind.Helping, Is.OnTheWay | Is.HandsOn),
            /* Dragging */             new Row(TaskKind.Helping, Is.OnTheWay | Is.HandsOn),
            /* GoingToAlarm */         new Row(TaskKind.RaisingTheAlarm, Is.OnTheWay),
            /* PullingAlarm */         new Row(TaskKind.RaisingTheAlarm, Is.HandsOn),
            /* FetchingBarricade */    new Row(TaskKind.Barricading, Is.OnTheWay),
            /* CarryingBarricade */    new Row(TaskKind.Barricading, Is.OnTheWay),
            /* Barricading */          new Row(TaskKind.Barricading, Is.Nothing),
            /* ShovingObstruction */   new Row(TaskKind.Escaping, Is.Nothing),
            /* RunningAnErrand */      new Row(TaskKind.Errand, Is.OnTheWay),
            /* Chatting */             new Row(TaskKind.Errand, Is.Nothing),
            /* HeavingForTheHand */    new Row(TaskKind.ForTheHand, Is.OnTheWay),
            /* AnsweringTheHand */     new Row(TaskKind.ForTheHand, Is.Nothing)
        };

        private static Row RowOf(AgentActivityState activity)
        {
            int i = (int)activity;
            return i >= 0 && i < Rows.Length ? Rows[i] : new Row(TaskKind.Idle, Is.Nothing);
        }

        /// <summary>How many rows the table has: one per activity, which a test checks.</summary>
        internal static int RowCount => Rows.Length;

        /// <summary>The task this activity belongs to.</summary>
        public static TaskKind KindOf(AgentActivityState activity) => RowOf(activity).Kind;

        /// <summary>The task this person is on.</summary>
        public static TaskKind KindOf(Agent agent) => KindOf(agent.Intent.Activity);

        /// <summary>Doing nothing in particular: a nudge makes them look round for whoever did it.</summary>
        public static bool IsLoitering(AgentActivityState activity) => (RowOf(activity).Answers & Is.Loitering) != 0;

        /// <summary>A calm idle, or sitting on purpose, that a cue may cut short.</summary>
        public static bool IsInterruptible(AgentActivityState activity) => (RowOf(activity).Answers & Is.Interruptible) != 0;

        /// <summary>On the way to something in particular, rather than standing about or milling around.</summary>
        public static bool IsOnTheWay(AgentActivityState activity) => (RowOf(activity).Answers & Is.OnTheWay) != 0;

        /// <summary>Hands-on at something this moment: a door, a body, a bottle, an alarm, a chair.</summary>
        public static bool IsHandsOn(AgentActivityState activity) => (RowOf(activity).Answers & Is.HandsOn) != 0;

        /// <summary>Doing nothing that calming down has to wait for.</summary>
        public static bool IsFreeToCalm(AgentActivityState activity) => (RowOf(activity).Answers & Is.FreeToCalm) != 0;

        /// <summary>Still held by the chair they sit in, or are lowering themselves into, or have turned in to look.</summary>
        public static bool KeepsTheirSeat(AgentActivityState activity) => (RowOf(activity).Answers & Is.InTheirSeat) != 0;
    }
}
