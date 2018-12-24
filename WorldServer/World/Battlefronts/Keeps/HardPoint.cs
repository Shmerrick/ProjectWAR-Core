﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldServer.World.Battlefronts.Keeps
{
    public class Hardpoint : Point3D
    {
        public readonly SiegeType SiegeType;
        public readonly ushort Heading;
        public Siege CurrentWeapon;
        public KeepMessage SiegeRequirement = KeepMessage.Safe;

        public Hardpoint(SiegeType type, int x, int y, int z, int heading)
        {
            SiegeType = type;
            X = x;
            Y = y;
            Z = z;
            Heading = (ushort)heading;
        }
    }
}
