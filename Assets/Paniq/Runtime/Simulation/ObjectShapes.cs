namespace Paniq.Simulation
{
    /// <summary>
    /// The solid shape of each kind of loose thing, built from boxes and
    /// spheres, in millimetres, standing on its own bottom centre and facing
    /// +Z. These are the shapes the physics engine collides, so they follow
    /// what the display draws (see BoxViews) closely enough that nothing is
    /// seen to float or sink: a chair is a block of legs, a seat and a back;
    /// a laptop a base and an open screen. Round things are near-square
    /// boxes, because a cylinder standing on its end is not a shape the
    /// engine has, and a capsule on its end falls over at a breath.
    /// </summary>
    internal static class ObjectShapes
    {
        internal enum PartShape
        {
            Box,
            Sphere
        }

        /// <summary>One solid piece: its centre above the thing's bottom centre, and its full size.</summary>
        internal readonly struct Part
        {
            public Part(PartShape shape, int centreX, int centreY, int centreZ, int sizeX, int sizeY, int sizeZ)
            {
                Shape = shape;
                CentreX = centreX;
                CentreY = centreY;
                CentreZ = centreZ;
                SizeX = sizeX;
                SizeY = sizeY;
                SizeZ = sizeZ;
            }

            public PartShape Shape { get; }
            public int CentreX { get; }
            public int CentreY { get; }
            public int CentreZ { get; }

            /// <summary>A box's width, height and depth; a sphere's diameter is <see cref="SizeX"/>.</summary>
            public int SizeX { get; }

            public int SizeY { get; }
            public int SizeZ { get; }

            public Part Scaled(int percent) => new Part(Shape,
                CentreX * percent / 100, CentreY * percent / 100, CentreZ * percent / 100,
                System.Math.Max(10, SizeX * percent / 100), System.Math.Max(10, SizeY * percent / 100),
                System.Math.Max(10, SizeZ * percent / 100));
        }

        /// <summary>The seat of every chair is this high, as drawn.</summary>
        public const int SeatHeightMillimetres = 450;

        private static Part Box(int x, int y, int z, int width, int height, int depth) =>
            new Part(PartShape.Box, x, y, z, width, height, depth);

        /// <summary>The solid pieces of a thing of this kind and size (its authored width).</summary>
        public static Part[] For(PhysicsObjectKind kind, int size)
        {
            switch (kind)
            {
                case PhysicsObjectKind.Chair:
                    return new[]
                    {
                        Box(0, SeatHeightMillimetres / 2 - 5, 0, size * 8 / 10, SeatHeightMillimetres - 10, size * 8 / 10),
                        Box(0, SeatHeightMillimetres, 0, size, 50, size),
                        Box(0, SeatHeightMillimetres + 220, -size / 2 + 25, size, 420, 50)
                    };

                case PhysicsObjectKind.OfficeChair:
                    return new[]
                    {
                        Box(0, 40, 0, size * 9 / 10, 80, size * 9 / 10),
                        Box(0, 265, 0, size * 3 / 10, 370, size * 3 / 10),
                        Box(0, SeatHeightMillimetres, 0, size, 50, size),
                        Box(0, SeatHeightMillimetres + 220, -size / 2 + 25, size, 420, 50)
                    };

                case PhysicsObjectKind.WasteBin:
                    return new[] { Box(0, size * 11 / 20, 0, size * 9 / 10, size * 11 / 10, size * 9 / 10) };

                case PhysicsObjectKind.PottedPlant:
                    return new[]
                    {
                        Box(0, size / 4, 0, size * 9 / 10, size / 2, size * 9 / 10),
                        new Part(PartShape.Sphere, 0, size * 112 / 100, 0, size * 11 / 10, 0, 0)
                    };

                case PhysicsObjectKind.Extinguisher:
                    return new[] { Box(0, size, 0, size * 85 / 100, size * 2, size * 85 / 100) };

                case PhysicsObjectKind.Bag:
                    return new[] { Box(0, size * 35 / 100, 0, size, size * 7 / 10, size * 3 / 4) };

                case PhysicsObjectKind.Microwave:
                    return new[] { Box(0, size * 3 / 10, 0, size, size * 6 / 10, size * 8 / 10) };

                case PhysicsObjectKind.WallSocket:
                    return new[] { Box(0, size / 4, 0, size, size / 2, 30) };

                case PhysicsObjectKind.Briefcase:
                    return new[] { Box(0, size * 4 / 10, 0, size, size * 8 / 10, size * 3 / 10) };

                case PhysicsObjectKind.Laptop:
                    return new[]
                    {
                        Box(0, 15, 0, size, 30, size * 7 / 10),
                        Box(0, size / 4, -size * 3 / 10, size, size / 2, 30)
                    };

                default:
                    return new[] { Box(0, size * 3 / 8, 0, size, size * 3 / 4, size) };
            }
        }

        /// <summary>
        /// Half the width of what a thing of this kind stands on, in
        /// millimetres: the broadest of its pieces that reach the floor,
        /// measured across its narrower side. A chair's seat overhangs its
        /// legs, so a chair can be pushed up against a table further than its
        /// seat is wide.
        /// </summary>
        public static int FootprintHalfWidth(PhysicsObjectKind kind, int size)
        {
            int broadest = 0;
            foreach (Part part in For(kind, size))
            {
                if (part.Shape != PartShape.Box || part.CentreY - part.SizeY / 2 > 10)
                {
                    continue;
                }

                broadest = System.Math.Max(broadest, System.Math.Min(part.SizeX, part.SizeZ) / 2);
            }

            return broadest == 0 ? size / 2 : broadest;
        }

        /// <summary>
        /// How high the top of a thing of this kind stands, in millimetres:
        /// what a thing stacked on it rests on. A chair's is its seat.
        /// </summary>
        public static int TopHeight(PhysicsObjectKind kind, int size)
        {
            switch (kind)
            {
                case PhysicsObjectKind.Chair:
                case PhysicsObjectKind.OfficeChair:
                    return SeatHeightMillimetres + 25;
                case PhysicsObjectKind.WasteBin:
                    return size * 11 / 10;
                case PhysicsObjectKind.PottedPlant:
                    return size / 2;
                case PhysicsObjectKind.Extinguisher:
                    return size * 2;
                case PhysicsObjectKind.Bag:
                    return size * 7 / 10;
                case PhysicsObjectKind.Microwave:
                    return size * 6 / 10;
                case PhysicsObjectKind.WallSocket:
                    return size / 2;
                case PhysicsObjectKind.Briefcase:
                    return size * 8 / 10;
                case PhysicsObjectKind.Laptop:
                    return 30;
                default:
                    return size * 3 / 4;
            }
        }
    }
}
