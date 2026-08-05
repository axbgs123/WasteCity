using System;
using UnityEngine;

namespace WasteCity.Graybox3D
{
    public sealed class PlanarCoordinateMapper3D
    {
        public int Width { get; }
        public int Height { get; }

        public PlanarCoordinateMapper3D(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
        }

        public bool TryCellToWorld(
            int cellX,
            int cellY,
            float visualY,
            out Vector3 world)
        {
            if (!ContainsCell(cellX, cellY))
            {
                world = default;
                return false;
            }

            world = new Vector3(
                cellX - Width * .5f,
                visualY,
                cellY - Height * .5f);
            return true;
        }

        public bool TryWorldToCell(
            Vector3 world,
            out int cellX,
            out int cellY)
        {
            cellX = Mathf.FloorToInt(world.x + Width * .5f);
            cellY = Mathf.FloorToInt(world.z + Height * .5f);
            return ContainsCell(cellX, cellY);
        }

        public Vector3 PlaneToWorld(Vector2 plane, float visualY)
        {
            return new Vector3(plane.x, visualY, plane.y);
        }

        public Vector2 WorldToPlane(Vector3 world)
        {
            return new Vector2(world.x, world.z);
        }

        public bool ContainsCell(int cellX, int cellY)
        {
            return cellX >= 0 &&
                   cellY >= 0 &&
                   cellX < Width &&
                   cellY < Height;
        }
    }
}
