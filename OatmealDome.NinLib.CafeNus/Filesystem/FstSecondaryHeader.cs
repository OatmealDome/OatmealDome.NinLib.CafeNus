using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.CafeNus.Filesystem
{
    internal class FstSecondaryHeader
    {
        public uint Offset
        {
            get;
            private set;
        }

        public uint Size
        {
            get;
            private set;
        }

        public ulong OwnerTitleId
        {
            get;
            private set;
        }

        public uint GroupId
        {
            get;
            private set;
        }

        public ushort Unknown
        {
            get;
            private set;
        }

        public FstSecondaryHeader(BinaryDataReader reader)
        {
            Offset = reader.ReadUInt32();
            Size = reader.ReadUInt32();
            OwnerTitleId = reader.ReadUInt64();
            GroupId = reader.ReadUInt32();
            Unknown = reader.ReadUInt16();

            reader.Seek(10); // padding?
        }
    }
}
