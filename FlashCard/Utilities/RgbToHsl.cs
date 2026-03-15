namespace FlashCard.Utilities
{
    public static class RgbToHslTools
    {
        public static (double H, double S, double L) RgbToHsl(int r, int g, int b)
        {
            double rf = r / 255.0;
            double gf = g / 255.0;
            double bf = b / 255.0;

            double max = Math.Max(rf, Math.Max(gf, bf));
            double min = Math.Min(rf, Math.Min(gf, bf));
            double l = (max + min) / 2.0;
            double h, s;

            if (max == min)
            {
                h = s = 0; // achromatic
            }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

                if (max == rf) h = (gf - bf) / d + (gf < bf ? 6 : 0);
                else if (max == gf) h = (bf - rf) / d + 2;
                else h = (rf - gf) / d + 4;

                h /= 6.0;
            }

            return (h * 360, s, l);
        }

        public static (int R, int G, int B) HslToRgb(double h, double s, double l)
        {
            double r, g, b;
            h /= 360.0;

            if (s == 0)
            {
                r = g = b = l; // achromatic
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h + 1.0 / 3.0);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1.0 / 3.0);
            }

            return ((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }
}
