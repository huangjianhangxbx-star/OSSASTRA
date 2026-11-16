using System;

namespace Ossastra.Characters
{
    public static class SideViewDiveRules
    {
        public static bool TryFreezeDirection(float x, float y, float minimumDown, out float horizontal, out float vertical)
        {
            horizontal = vertical = 0f;
            var length = Math.Sqrt((double)x * x + (double)y * y);
            if (double.IsNaN(length) || double.IsInfinity(length) || length <= .0001 || -y / length < minimumDown)
                return false;
            horizontal = (float)(x / length);
            vertical = (float)(y / length);
            return true;
        }

        public static bool IsBeforeObstruction(float contact, float obstruction) => contact + .002f < obstruction;
    }
}
