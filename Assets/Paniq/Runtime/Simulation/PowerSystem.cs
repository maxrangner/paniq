using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The cable running from the fuse box down to the sockets, and the spark
    /// that runs along it.
    /// <para>
    /// The cable runs one way (the owner's rule, 2026-09-26: "only if the
    /// fusebox goes, it should quickly cascade down to all outlets, but not
    /// the other way around"). When the fuse box goes off -- the flames reach
    /// it, the Director sets it off, or the player's card does -- a spark
    /// races out along every run of cable leaving it, and every socket it
    /// reaches pops in turn and passes it on further down the line. A socket
    /// popping by itself, in the flames or because the Director chose it, is
    /// a bang and nothing more: it lights no cable, so nothing climbs back up
    /// to the fuse box. "Down" is measured along the cable: each socket's
    /// distance from the fuse box, worked out once, and a spark only ever
    /// travels to something further away than where it set out from.
    /// </para>
    /// <para>
    /// A socket that has already gone does not stop the spark: it arrives,
    /// finds wreckage, and carries on down the line. (While the cable ran both
    /// ways the chain beyond a wrecked socket had already been lit from it;
    /// with one way, it has not.)
    /// </para>
    /// <para>
    /// The blast itself is not this system's: it calls the same
    /// <see cref="PhysicsObjectSystem.Detonate"/> the flames do, so a socket
    /// popped by the cable and one popped by the fire are the same event with
    /// the same consequences.
    /// </para>
    /// <para>
    /// It draws no random numbers at all. The spark is arithmetic -- a fixed
    /// speed along an authored length -- and every loop runs in a fixed order,
    /// so nothing here can move a replay by itself.
    /// </para>
    /// </summary>
    internal sealed class PowerSystem
    {
        private readonly SimulationContext context;
        private readonly PhysicsObjectSystem objects;
        private readonly PowerSettings settings;

        /// <summary>One run of cable, and how far the spark along it has got.</summary>
        private struct Line
        {
            public int FromNode;
            public int ToNode;
            public int LengthMillimetres;

            /// <summary>A spark is on its way, and travelling from this end.</summary>
            public bool Live;
            public bool FromTheFromEnd;
            public int Travelled;
            public ulong CauseEventId;

            /// <summary>Lit once and finished with: a cable never carries a second spark.</summary>
            public bool Spent;
        }

        /// <summary>Every socket and the fuse box, in ascending ID order.</summary>
        private readonly SimulationId[] nodeIds;
        private readonly int[] nodeObjectIndex;
        private readonly bool[] nodeIsFuseBox;
        private readonly bool[] nodeBlown;

        /// <summary>
        /// How far along the cable each node is from the nearest fuse box, in
        /// millimetres; a fuse box is 0, and a node no cable joins to a fuse
        /// box is <see cref="int.MaxValue"/>. A spark only travels to a node
        /// further away than the one it set out from: that is what "down"
        /// means.
        /// </summary>
        private readonly int[] nodeDistance;

        private readonly Line[] lines;

        /// <summary>Which lines touch each node, so a pop can light all of them.</summary>
        private readonly int[][] linesAtNode;

        /// <summary>Scratch for the cascade, so a tick allocates nothing.</summary>
        private readonly int[] worklist;
        private int worklistCount;

        /// <summary>Whether a node is already on this tick's worklist: two sparks arriving at one node queue it once.</summary>
        private readonly bool[] queued;

        public PowerSystem(SimulationContext context, PhysicsObjectSystem objects)
        {
            this.context = context;
            this.objects = objects;
            settings = context.Scenario.Power;

            PowerLineDefinition[] authored =
                context.Scenario.PowerLines ?? Array.Empty<PowerLineDefinition>();

            // Only the runs whose ends are really in this building. Cable to
            // something that is not here is not here either.
            var present = new System.Collections.Generic.List<PowerLineDefinition>();
            for (int i = 0; i < authored.Length; i++)
            {
                if (objects.IndexOf(authored[i].FromObjectId) >= 0 && objects.IndexOf(authored[i].ToObjectId) >= 0)
                {
                    present.Add(authored[i]);
                }
            }

            authored = present.ToArray();

            // The nodes, in ascending ID order, so nothing about this depends
            // on the order the cable happened to be written down in.
            var ids = new System.Collections.Generic.List<SimulationId>();
            for (int i = 0; i < authored.Length; i++)
            {
                if (!ids.Contains(authored[i].FromObjectId))
                {
                    ids.Add(authored[i].FromObjectId);
                }

                if (!ids.Contains(authored[i].ToObjectId))
                {
                    ids.Add(authored[i].ToObjectId);
                }
            }

            ids.Sort((left, right) => left.CompareTo(right));
            nodeIds = ids.ToArray();
            nodeObjectIndex = new int[nodeIds.Length];
            nodeIsFuseBox = new bool[nodeIds.Length];
            nodeBlown = new bool[nodeIds.Length];
            for (int i = 0; i < nodeIds.Length; i++)
            {
                nodeObjectIndex[i] = objects.IndexOf(nodeIds[i]);
                nodeIsFuseBox[i] = nodeObjectIndex[i] >= 0 &&
                                   objects.KindOf(nodeObjectIndex[i]) == PhysicsObjectKind.FuseBox;
            }

            lines = new Line[authored.Length];
            var counts = new int[nodeIds.Length];
            for (int i = 0; i < authored.Length; i++)
            {
                lines[i].FromNode = NodeOf(authored[i].FromObjectId);
                lines[i].ToNode = NodeOf(authored[i].ToObjectId);
                lines[i].LengthMillimetres = authored[i].LengthMillimetres;
                counts[lines[i].FromNode]++;
                counts[lines[i].ToNode]++;
            }

            linesAtNode = new int[nodeIds.Length][];
            for (int n = 0; n < nodeIds.Length; n++)
            {
                linesAtNode[n] = new int[counts[n]];
                counts[n] = 0;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                linesAtNode[lines[i].FromNode][counts[lines[i].FromNode]++] = i;
                linesAtNode[lines[i].ToNode][counts[lines[i].ToNode]++] = i;
            }

            worklist = new int[nodeIds.Length];
            queued = new bool[nodeIds.Length];
            nodeDistance = MeasureFromTheFuseBox();
        }

        /// <summary>
        /// Each node's distance along the cable from the nearest fuse box: the
        /// shortest way round, found by always settling the nearest node not
        /// yet settled. Ties are settled in ascending node order, so the answer
        /// never depends on the order the cable was written down in.
        /// </summary>
        private int[] MeasureFromTheFuseBox()
        {
            var distance = new int[nodeIds.Length];
            var settled = new bool[nodeIds.Length];
            for (int n = 0; n < nodeIds.Length; n++)
            {
                distance[n] = nodeIsFuseBox[n] ? 0 : int.MaxValue;
            }

            for (int round = 0; round < nodeIds.Length; round++)
            {
                int next = -1;
                for (int n = 0; n < nodeIds.Length; n++)
                {
                    if (!settled[n] && distance[n] != int.MaxValue && (next < 0 || distance[n] < distance[next]))
                    {
                        next = n;
                    }
                }

                if (next < 0)
                {
                    break;
                }

                settled[next] = true;
                int[] touching = linesAtNode[next];
                for (int i = 0; i < touching.Length; i++)
                {
                    Line line = lines[touching[i]];
                    int other = line.FromNode == next ? line.ToNode : line.FromNode;
                    long through = (long)distance[next] + line.LengthMillimetres;
                    if (!settled[other] && through < distance[other])
                    {
                        distance[other] = (int)through;
                    }
                }
            }

            return distance;
        }

        /// <summary>
        /// Every spark on its way right now, for the snapshot. Built fresh
        /// only when something is actually lit, which is most of a round not
        /// at all.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<PowerSparkSnapshot> Sparks()
        {
            System.Collections.Generic.List<PowerSparkSnapshot> live = null;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Live)
                {
                    continue;
                }

                live ??= new System.Collections.Generic.List<PowerSparkSnapshot>();
                live.Add(new PowerSparkSnapshot(i, lines[i].Travelled, lines[i].FromTheFromEnd));
            }

            return (System.Collections.Generic.IReadOnlyList<PowerSparkSnapshot>)live ??
                   System.Array.Empty<PowerSparkSnapshot>();
        }

        /// <summary>How many runs of cable there are, for the display and the tests.</summary>
        public int LineCount => lines.Length;

        /// <summary>Whether a spark is on its way along this run right now.</summary>
        public bool IsLive(int line) => lines[line].Live;

        /// <summary>How far along a live spark has got, in millimetres from the end it started at.</summary>
        public int TravelledOn(int line) => lines[line].Travelled;

        /// <summary>Whether the spark on this run is travelling from its first end toward its second.</summary>
        public bool RunsForward(int line) => lines[line].FromTheFromEnd;

        /// <summary>Whether the fuse box has already gone off.</summary>
        public bool FuseBoxHasBlown
        {
            get
            {
                for (int n = 0; n < nodeIds.Length; n++)
                {
                    if (nodeIsFuseBox[n] && nodeBlown[n])
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Something electrical has gone off: it is wreckage now. A fuse box
        /// going off lights every run of cable leading down from it; a socket
        /// lights nothing, because the cable only runs down.
        /// </summary>
        public void SomethingPopped(SimulationId id, ulong causeEventId)
        {
            int node = NodeOf(id);
            if (node < 0 || nodeBlown[node])
            {
                return;
            }

            nodeBlown[node] = true;
            if (nodeIsFuseBox[node])
            {
                LightDownstream(node, causeEventId);
            }
        }

        /// <summary>
        /// The player's card: pop the fuse box by hand. Refused, with nothing
        /// spent and nothing written down, if there is no fuse box within reach
        /// or it has already gone.
        /// </summary>
        public bool CanPopTheFuseBoxNear(LogicalPosition where) => FuseBoxNear(where) >= 0;

        /// <summary>
        /// Pops it. Only called once <see cref="CanPopTheFuseBoxNear"/> has
        /// said there is one there.
        /// </summary>
        public void PopTheFuseBoxNear(LogicalPosition where, ulong causeEventId)
        {
            int node = FuseBoxNear(where);
            if (node >= 0)
            {
                PopNode(node, causeEventId);
            }
        }

        /// <summary>
        /// The Director's last rung: the fuse box goes, and every socket after
        /// it. Returns the bang, or 0 when there is no fuse box left to go.
        /// </summary>
        public ulong PopTheFuseBox(ulong causeEventId)
        {
            int node = FuseBoxNode();
            return node < 0 ? 0UL : PopNode(node, causeEventId);
        }

        /// <summary>The Director's socket: it goes off, and nothing else does. Returns the bang, or 0.</summary>
        public ulong PopSocket(int node, ulong causeEventId)
        {
            return nodeIsFuseBox[node] ? 0UL : PopNode(node, causeEventId);
        }

        /// <summary>
        /// Sets one node off through the ordinary blast, which tells this
        /// system it popped (<see cref="SomethingPopped"/>); a thing whose blast
        /// is somehow nothing is still marked as gone, and a fuse box still
        /// sends its sparks down, so the chain cannot stall on it.
        /// </summary>
        private ulong PopNode(int node, ulong causeEventId)
        {
            if (nodeBlown[node] || nodeObjectIndex[node] < 0)
            {
                return 0UL;
            }

            ulong bang = objects.Detonate(nodeObjectIndex[node], nodeIds[node], causeEventId);
            if (!nodeBlown[node])
            {
                SomethingPopped(nodeIds[node], causeEventId);
            }

            return bang;
        }

        /// <summary>The first fuse box that has not gone yet, or -1.</summary>
        private int FuseBoxNode()
        {
            for (int n = 0; n < nodeIds.Length; n++)
            {
                if (nodeIsFuseBox[n] && !nodeBlown[n] && nodeObjectIndex[n] >= 0)
                {
                    return n;
                }
            }

            return -1;
        }

        /// <summary>How many sockets and fuse boxes the cable joins, for the Director to choose among.</summary>
        public int NodeCount => nodeIds.Length;

        /// <summary>Whether this node is a socket (not a fuse box) that is still in one piece.</summary>
        public bool IsSocketStillWhole(int node) => !nodeIsFuseBox[node] && !nodeBlown[node] && nodeObjectIndex[node] >= 0;

        /// <summary>Whether this node is a fuse box that is still in one piece.</summary>
        public bool IsFuseBoxStillWhole(int node) => nodeIsFuseBox[node] && !nodeBlown[node] && nodeObjectIndex[node] >= 0;

        /// <summary>The thing on the wall at this node.</summary>
        public SimulationId NodeId(int node) => nodeIds[node];

        /// <summary>Where this node is.</summary>
        public LogicalPosition NodePosition(int node) => PositionOfNode(node);

        /// <summary>The fuse box's node, whole or not, or -1 when the building has none.</summary>
        public int FuseBoxNodeIndex
        {
            get
            {
                for (int n = 0; n < nodeIds.Length; n++)
                {
                    if (nodeIsFuseBox[n])
                    {
                        return n;
                    }
                }

                return -1;
            }
        }

        /// <summary>A fuse box within the card's reach that has not already gone, or -1.</summary>
        private int FuseBoxNear(LogicalPosition where)
        {
            long reach = settings.CardReachMillimetres;
            for (int n = 0; n < nodeIds.Length; n++)
            {
                if (!nodeIsFuseBox[n] || nodeBlown[n] || nodeObjectIndex[n] < 0)
                {
                    continue;
                }

                LogicalPosition at = objects.PositionOf(nodeObjectIndex[n]);
                if (LogicalPosition.DistanceSquared(at, where) <= reach * reach)
                {
                    return n;
                }
            }

            return -1;
        }

        /// <summary>Where the fuse box is, for the card to aim at. Zero-zero if there is none.</summary>
        public bool TryFindTheFuseBox(out LogicalPosition where)
        {
            for (int n = 0; n < nodeIds.Length; n++)
            {
                if (nodeIsFuseBox[n] && nodeObjectIndex[n] >= 0)
                {
                    where = objects.PositionOf(nodeObjectIndex[n]);
                    return true;
                }
            }

            where = default;
            return false;
        }

        /// <summary>
        /// Phase 2, beside the fire: every live spark runs on, and anything a
        /// spark reaches goes off and passes it on down the line.
        /// <para>
        /// Arrivals are resolved in ascending cable order, and the cables
        /// further down are lit afterwards through a worklist rather than by
        /// calling back into this, so two sparks arriving on the same tick
        /// always work out the same way round.
        /// </para>
        /// </summary>
        public void Advance()
        {
            worklistCount = 0;
            int speed = settings.SparkSpeedMillimetresPerTick;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Live)
                {
                    continue;
                }

                lines[i].Travelled += speed;
                if (lines[i].Travelled < lines[i].LengthMillimetres)
                {
                    continue;
                }

                lines[i].Live = false;
                lines[i].Spent = true;
                lines[i].Travelled = lines[i].LengthMillimetres;

                int arrivedAt = lines[i].FromTheFromEnd ? lines[i].ToNode : lines[i].FromNode;
                context.Events.Append(context.Tick, nodeIds[arrivedAt],
                    CausalEventType.PowerSparkArrived, PositionOfNode(arrivedAt), 0, 0,
                    lines[i].CauseEventId, nodeIds[arrivedAt]);

                // Whole: it goes off. Already wreckage -- the flames or the
                // Director got to it first -- it has nothing left to set off,
                // but the spark carries on past it all the same.
                if (!nodeBlown[arrivedAt] && nodeObjectIndex[arrivedAt] >= 0)
                {
                    ulong bang = PopNode(arrivedAt, lines[i].CauseEventId);
                    lines[i].CauseEventId = bang == 0UL ? lines[i].CauseEventId : bang;
                }

                if (!queued[arrivedAt])
                {
                    queued[arrivedAt] = true;
                    worklist[worklistCount++] = arrivedAt;
                }
            }

            for (int w = 0; w < worklistCount; w++)
            {
                queued[worklist[w]] = false;
                LightDownstream(worklist[w], CauseAt(worklist[w]));
            }
        }

        /// <summary>The event that best explains why this node went off, for the next spark to point back at.</summary>
        private ulong CauseAt(int node)
        {
            int[] touching = linesAtNode[node];
            for (int i = 0; i < touching.Length; i++)
            {
                if (lines[touching[i]].Spent && lines[touching[i]].CauseEventId != 0UL)
                {
                    return lines[touching[i]].CauseEventId;
                }
            }

            return 0UL;
        }

        /// <summary>
        /// Lights every run of cable leading down from this node -- to
        /// something further from the fuse box than this -- that is not
        /// already lit or finished with.
        /// </summary>
        private void LightDownstream(int node, ulong causeEventId)
        {
            int[] touching = linesAtNode[node];
            for (int i = 0; i < touching.Length; i++)
            {
                int line = touching[i];
                if (lines[line].Live || lines[line].Spent)
                {
                    continue;
                }

                bool fromTheFromEnd = lines[line].FromNode == node;
                int farEnd = fromTheFromEnd ? lines[line].ToNode : lines[line].FromNode;
                if (nodeDistance[farEnd] <= nodeDistance[node])
                {
                    // Up the cable, or across to something no nearer the
                    // end of the line: the spark never goes that way.
                    continue;
                }

                lines[line].Live = true;
                lines[line].FromTheFromEnd = fromTheFromEnd;
                lines[line].Travelled = 0;
                lines[line].CauseEventId = causeEventId;

                context.Events.Append(context.Tick, nodeIds[node], CausalEventType.PowerSparkStarted,
                    PositionOfNode(node), lines[line].LengthMillimetres,
                    TicksToCross(line), causeEventId, nodeIds[farEnd]);
            }
        }

        private int TicksToCross(int line)
        {
            int speed = settings.SparkSpeedMillimetresPerTick;
            return (lines[line].LengthMillimetres + speed - 1) / speed;
        }

        private LogicalPosition PositionOfNode(int node)
        {
            return nodeObjectIndex[node] >= 0 ? objects.PositionOf(nodeObjectIndex[node]) : default;
        }

        private int NodeOf(SimulationId id)
        {
            for (int i = 0; i < nodeIds.Length; i++)
            {
                if (nodeIds[i] == id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
