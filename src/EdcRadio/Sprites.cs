using System.Drawing;
using System.IO;
using System.Reflection;

namespace EdcRadio
{
    internal static class Sprites
    {
        public static readonly Rectangle TruckA = new Rectangle(0, 0, 22, 14);
        public static readonly Rectangle TruckB = new Rectangle(23, 0, 22, 14);
        public static readonly Rectangle Tower = new Rectangle(46, 0, 11, 28);
        public static readonly Rectangle PineL = new Rectangle(58, 0, 11, 13);
        public static readonly Rectangle PineS = new Rectangle(70, 0, 7, 9);
        public static readonly Rectangle Drift = new Rectangle(78, 0, 17, 3);
        public static readonly Rectangle CatA = new Rectangle(96, 0, 12, 11);
        public static readonly Rectangle CatB = new Rectangle(109, 0, 12, 11);
        public static readonly Rectangle KitSleep = new Rectangle(0, 29, 11, 9);
        public static readonly Rectangle Radio = new Rectangle(12, 29, 14, 9);
        public static readonly Rectangle Heart = new Rectangle(27, 29, 7, 6);
        public static readonly Rectangle NoteA = new Rectangle(35, 29, 6, 7);
        public static readonly Rectangle NoteB = new Rectangle(42, 29, 6, 7);
        public static readonly Rectangle Moon = new Rectangle(49, 29, 9, 10);
        public static readonly Rectangle Zz = new Rectangle(59, 29, 4, 4);
        public static readonly Rectangle Blink = new Rectangle(64, 29, 3, 3);
        public static readonly Rectangle Flake = new Rectangle(68, 29, 3, 3);

        private static Bitmap _pack;

        public static Bitmap Pack
        {
            get
            {
                if (_pack != null) return _pack;
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("EdcRadio.Assets.sprites.png"))
                    _pack = new Bitmap(s);
                return _pack;
            }
        }
    }
}
