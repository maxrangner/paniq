using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The cable running from socket to socket and back to the fuse box, and
    /// the spark that runs along it.
    /// <para>
    /// When the flames reach a socket it pops, and the cable lights at that
    /// socket like a fuse on a stick of dynamite. A spark races along the wall
    /// to whatever is at the far end, which pops in turn, and so on down the
    /// line. Every run of cable ends at the fuse box, and when the spark gets
    /// there the box goes off harder than anything else in the building.
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
            public int ExtraDelay;
            public ulong CauseEventId;

            /// <summary>Lit once and finished with: a cable never carries a second spark.</summary>
            public bool Spent;
        }

        /// <summary>Every socket and the fuse box, in ascending ID order.</summary>
        private readonly SimulationId[] nodeIds;
        private readonly int[] nodeObjectIndex;
        private readonly bool[] nodeIsFuseBox;
        private readonly bool[] nodeBlown;

        private readonly Line[] lines;

        /// <summary>Which lines touch each node, so a pop can light all of them.</summary>
        private readonly int[][] linesAtNode;

        /// <summary>Scratch for the cascade, so a tick allocates nothing.</summary>
        private readonly int[] worklist;
        private int worklistCount;

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
                if (!lines[i].Live || lines[i].ExtraDelay > 0)
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
        /// Something electrical has gone off. Every run of cable touching it
        /// lights, unless it is already lit or already spent, and unless
        /// whatever is at the far end has gone off too -- a spark crawling
        /// toward a socket that is already wreckage has nothing to do when it
        /// arrives.
        /// </summary>
        public void SomethingPopped(SimulationId id, ulong causeEventId)
        {
            int node = NodeOf(id);
            if (node < 0 || nodeBlown[node])
            {
                return;
            }

            nodeBlown[node] = true;
            LightTheCableAt(node, causeEventId);
        }

        /// <summary>
        /// The player's card: pop the fuse box by hand. The spark then runs the
        /// other way, out of the maintenance room and along the line of sockets,
        /// which costs no extra code because the cable has no direction of its
        /// own. Refused, with nothing spent and nothing written down, if there
        /// is no fuse box within reach or it has already gone.
        /// </summary>
        public bool CanPopTheFuseBoxNear(LogicalPosition where) => FuseBoxNear(where) >= 0;

        /// <summary>
        /// Pops it. Only called once <see cref="CanPopTheFuseBoxNear"/> has
        /// said there is one there.
        /// </summary>
        public void PopTheFuseBoxNear(LogicalPosition where, ulong causeEventId)
        {
            int node = FuseBoxNear(where);
            if (node < 0)
            {
                return;
            }

            nodeBlown[node] = true;
            ulong bang = objects.Detonate(nodeObjectIndex[node], nodeIds[node], causeEventId);
            LightTheCableAt(node, bang == 0UL ? causeEventId : bang);
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
        /// Phase 2, beside the fire: every live spark crawls on, and anything a
        /// spark reaches goes off.
        /// <para>
        /// Arrivals are resolved in ascending cable order, and the pops they
        /// cause light their own cables afterwards through a worklist rather
        /// than by calling back into this, so two sparks arriving on the same
        /// tick always work out the same way round.
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

                if (lines[i].ExtraDelay > 0)
                {
                    lines[i].ExtraDelay--;
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

                // Already wreckage: the spark got there, and there is nothing
                // left for it to set off. It does not carry on past, either --
                // the chain beyond was lit when that one went.
                if (nodeBlown[arrivedAt] || nodeObjectIndex[arrivedAt] < 0)
                {
                    continue;
                }

                nodeBlown[arrivedAt] = true;
                ulong bang = objects.Detonate(nodeObjectIndex[arrivedAt], nodeIds[arrivedAt], lines[i].CauseEventId);
                worklist[worklistCount++] = arrivedAt;
                lines[i].CauseEventId = bang == 0UL ? lines[i].CauseEventId : bang;
            }

            for (int w = 0; w < worklistCount; w++)
            {
                LightTheCableAt(worklist[w], CauseAt(worklist[w]));
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

        /// <summary>Lights every run of cable leaving this node that is not already lit or finished with.</summary>
        private void LightTheCableAt(int node, ulong causeEventId)
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

                lines[line].Live = true;
                lines[line].FromTheFromEnd = fromTheFromEnd;
                lines[line].Travelled = 0;
                lines[line].CauseEventId = causeEventId;

                // A beat of silence before the big one.
                lines[line].ExtraDelay = nodeIsFuseBox[farEnd] ? settings.FuseBoxExtraDelayTicks : 0;

                context.Events.Append(context.Tick, nodeIds[node], CausalEventType.PowerSparkStarted,
                    PositionOfNode(node), lines[line].LengthMillimetres,
                    TicksToCross(line), causeEventId, nodeIds[farEnd]);
            }
        }

        private int TicksToCross(int line)
        {
            int speed = settings.SparkSpeedMillimetresPerTick;
            return lines[line].ExtraDelay + (lines[line].LengthMillimetres + speed - 1) / speed;
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
