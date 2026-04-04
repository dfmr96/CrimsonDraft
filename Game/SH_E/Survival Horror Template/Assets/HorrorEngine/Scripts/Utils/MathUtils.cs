namespace HorrorEngine
{
    public static class MathUtils
    {
        public static float Map(float x, float minFrom, float maxFrom, float minTo, float maxTo)
        {
            float denom = (maxFrom - minFrom);
            if (denom == 0)
            {
                return (minTo + maxTo) / 2;
            }
            var m = (maxTo - minTo) / denom;
            var c = minTo - m * minFrom; // point of interest: c is also equal to y2 - m * x2, though float math might lead to slightly different results.

            return m * x + c;
        }

        // Wraps x betwen min and max (both inclusive)
        public static float Wrap(float x, float min, float max)
        {
            if (min == max) return min;

            if (x < min)
                x = max - (min - x) % (max - min);
            else
                x = min + (x - min) % (max - min);

            return x;
        }

        // Wraps x betwen min and max (both inclusive)
        public static int Wrap(int x, int min, int max)
        {
            int range_size = max - min + 1;

            if (x < min)
                x += range_size * ((min - x) / range_size + 1);

            return min + (x - min) % range_size;
        }
    }
}
