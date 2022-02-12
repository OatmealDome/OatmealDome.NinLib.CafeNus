using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.CafeNus
{
    class Tmd
    {
        public ulong TitleId
        {
            get;
            private set;
        }

        public ushort TitleVersion
        {
            get;
            set;
        }

        public List<TmdContent> Contents
        {
            get;
            private set;
        }

        // https://wiiubrew.org/wiki/Title_metadata
        public Tmd(Stream stream)
        {
            using BinaryDataReader reader = new BinaryDataReader(stream);

            reader.ByteOrder = ByteOrder.BigEndian;

            reader.Seek(0x18c, SeekOrigin.Begin);
            TitleId = reader.ReadUInt64();

            reader.Seek(0x1dc, SeekOrigin.Begin);
            TitleVersion = reader.ReadUInt16();

            reader.Seek(0x1de, SeekOrigin.Begin);
            int contentCount = reader.ReadUInt16();

            // WiiUBrew is actually wrong here, the content structs actually start
            // at 0xb04: https://github.com/ihaveamac/wiiu-things/blob/master/wiiu_decrypt.py
            reader.Seek(0xb04, SeekOrigin.Begin);

            Contents = new List<TmdContent>();
            for (int i = 0; i < contentCount; i++)
            {
                Contents.Add(new TmdContent(reader));
            }
        }
    }
}
