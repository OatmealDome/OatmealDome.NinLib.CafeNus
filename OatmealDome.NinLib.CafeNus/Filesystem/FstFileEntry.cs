namespace OatmealDome.NinLib.CafeNus.Filesystem
{
    internal class FstFileEntry
    {
        public uint Offset
        {
            get;
            set;
        }

        public uint Size
        {
            get;
            set;
        }

        public ushort Flags
        {
            get;
            set;
        }

        public int ContentIdx
        {
            get;
            set;
        }

        public bool IsNotInPackage
        {
            get;
            set;
        }
    }
}
