/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    public struct FoliageCell
    {
        private static readonly int prime1 = 0xAB1D261;
        private static readonly int prime2 = 0x16447CD5;
        private static readonly int prime3 = 0x4BBF17D;

        public int x;
        public int y;
        public int z;

        public FoliageCell(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        
        public FoliageCell(Vector3 pos, bool subdivided)
        {
            if(subdivided == false)
            {
                x = Mathf.FloorToInt(pos.x / FoliageGlobals.CELL_SIZE);
                y = Mathf.FloorToInt(pos.y / FoliageGlobals.CELL_SIZE);
                z = Mathf.FloorToInt(pos.z / FoliageGlobals.CELL_SIZE);
            }
            else
            {
                x = Mathf.FloorToInt(pos.x / FoliageGlobals.CELL_SUBDIVIDED_SIZE);
                y = Mathf.FloorToInt(pos.y / FoliageGlobals.CELL_SUBDIVIDED_SIZE);
                z = Mathf.FloorToInt(pos.z / FoliageGlobals.CELL_SUBDIVIDED_SIZE);
            }
        }

        public void Set(Vector3 pos)
        {
            x = Mathf.FloorToInt(pos.x / FoliageGlobals.CELL_SIZE);
            y = Mathf.FloorToInt(pos.y / FoliageGlobals.CELL_SIZE);
            z = Mathf.FloorToInt(pos.z / FoliageGlobals.CELL_SIZE);
        }

        public void SetSubdivided(Vector3 pos)
        {
            x = Mathf.FloorToInt(pos.x / FoliageGlobals.CELL_SUBDIVIDED_SIZE);
            y = Mathf.FloorToInt(pos.y / FoliageGlobals.CELL_SUBDIVIDED_SIZE);
            z = Mathf.FloorToInt(pos.z / FoliageGlobals.CELL_SUBDIVIDED_SIZE);
        }

        public Vector3 GetCenter()
        {
            return new Vector3(
                FoliageGlobals.CELL_SIZE * x + FoliageGlobals.CELL_SIZE_HALF, 
                FoliageGlobals.CELL_SIZE * y + FoliageGlobals.CELL_SIZE_HALF, 
                FoliageGlobals.CELL_SIZE * z + FoliageGlobals.CELL_SIZE_HALF);
        }

        public Vector3 GetCenterSubdivided()
        {
            return new Vector3(
                FoliageGlobals.CELL_SUBDIVIDED_SIZE * x + FoliageGlobals.CELL_SUBDIVIDED_SIZE_HALF,
                FoliageGlobals.CELL_SUBDIVIDED_SIZE * y + FoliageGlobals.CELL_SUBDIVIDED_SIZE_HALF,
                FoliageGlobals.CELL_SUBDIVIDED_SIZE * z + FoliageGlobals.CELL_SUBDIVIDED_SIZE_HALF);
        }

        public Bounds GetBounds()
        {
            return new Bounds(GetCenter(), FoliageGlobals.CELL_SIZE3);
        }

        public Bounds GetBoundsSubdivided()
        {
            return new Bounds(GetCenterSubdivided(), FoliageGlobals.CELL_SUBDIVIDED_SIZE3);
        }

        public override int GetHashCode()
        {            
            return prime1 * x + prime2 * y + prime3 * z;
        }

        public override bool Equals(object obj)
        {
            FoliageCell other = (FoliageCell)obj;

            if (other.x != x)
                return false;
            if (other.y != y)
                return false;
            if (other.z != z)
                return false;

            return true;
        }

        public override string ToString()
        {
            return string.Format("({0}, {1}, {2})", x, y, z);
        }

        /** 
         * The position in here must be in defined as a local cell boundng box position 
         */
        public static int MakeHashSubdivided(Vector3 pos)
        {           
            int x = Mathf.FloorToInt(pos.x / FoliageGlobals.CELL_SUBDIVIDED_SIZE);
            int y = Mathf.FloorToInt(pos.y / FoliageGlobals.CELL_SUBDIVIDED_SIZE);
            int z = Mathf.FloorToInt(pos.z / FoliageGlobals.CELL_SUBDIVIDED_SIZE);

            return prime1 * x + prime2 * y + prime3 * z;
        }

        /** 
         * The position in here must be in defined as a world space position 
         */
        public static int MakeHash(Vector3 pos)
        {            
            int x = Mathf.FloorToInt(pos.x / FoliageGlobals.CELL_SIZE);
            int y = Mathf.FloorToInt(pos.y / FoliageGlobals.CELL_SIZE);
            int z = Mathf.FloorToInt(pos.z / FoliageGlobals.CELL_SIZE);

            return prime1 * x + prime2 * y + prime3 * z;
        }

        public static int MakeHash(int x, int y, int z)
        {
            return prime1 * x + prime2 * y + prime3 * z;
        }

        public delegate void FoliageIterationAction(int hash);

        public static void IterateMinMax(Vector3 min, Vector3 max, bool subdivided, FoliageIterationAction action)
        {
            FoliageCell cMin = new FoliageCell(min, subdivided);
            FoliageCell cMax = new FoliageCell(max, subdivided);

            for(int x = cMin.x; x <= cMax.x; x++)
            {
                for(int y = cMin.y; y <= cMax.y; y++)
                {
                    for(int z = cMin.z; z <= cMax.z; z++)
                    {
                        action(MakeHash(x, y, z));
                    }
                }
            }            
        }        

        /**
         * The depth should be around 1 or maximum 2 at runtime.
         */
        public static void IterateNeighboring(FoliageCell cell, int depth, FoliageIterationAction action)
        {
            int minX = cell.x - depth;
            int maxX = cell.x + depth;

            int minY = cell.y - depth;
            int maxY = cell.y + depth;

            int minZ = cell.z - depth;
            int maxZ = cell.z + depth;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        action(MakeHash(x, y, z));
                    }
                }
            }
        }
    }
}

