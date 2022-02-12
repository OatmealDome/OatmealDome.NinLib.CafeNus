using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.CafeNus
{
    internal class TmdContent
    {
        public uint Id
        {
            get;
            private set;
        }

        public ushort Idx
        {
            get;
            private set;
        }

        public ushort Type
        {
            get;
            private set;
        }

        public ulong Size
        {
            get;
            private set;
        }

        public byte[] Hash
        {
            get;
            private set;
        }

        public TmdContent(BinaryDataReader reader)
        {
            Id = reader.ReadUInt32();
            Idx = reader.ReadUInt16();
            Type = reader.ReadUInt16();
            Size = reader.ReadUInt64();
            Hash = reader.ReadBytes(20);
            reader.Seek(12); // padding?
        }
    }
}
