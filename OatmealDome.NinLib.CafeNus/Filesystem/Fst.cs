using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.CafeNus.Filesystem
{
    internal class Fst
    {
        private static readonly uint Magic = 0x46535400; // "FST\0"

        public uint OffsetFactor
        {
            get;
            private set;
        }

        private readonly List<FstSecondaryHeader> _secondaryHeaders;
        private readonly long _nameTableOffset;
        private readonly Dictionary<string, FstFileEntry> _fileEntries;

        public Fst(Stream stream)
        {
            using BinaryDataReader reader = new BinaryDataReader(stream);

            reader.ByteOrder = ByteOrder.BigEndian;

            if (reader.ReadUInt32() != Magic)
            {
                throw new Exception("Invalid FST magic");
            }

            OffsetFactor = reader.ReadUInt32();
            int secondaryHeaderCount = (int)reader.ReadUInt32();
            ushort unk = reader.ReadUInt16();
            
            reader.Seek(18); // padding?

            _secondaryHeaders = new List<FstSecondaryHeader>();
            for (int i = 0; i < secondaryHeaderCount; i++)
            {
                _secondaryHeaders.Add(new FstSecondaryHeader(reader));
            }

            long fileEntriesOffset = reader.Position;

            reader.Seek(8); // ???

            int totalEntries = (int)reader.ReadUInt32();

            reader.Seek(4); // ???

            _nameTableOffset = fileEntriesOffset + (totalEntries * 0x10);

            _fileEntries = new Dictionary<string, FstFileEntry>();

            LoadDirectory(reader, 1, totalEntries, "/");
        }

        // Thanks to CarlKenner for directory iteration code:
        // https://github.com/CarlKenner/dolphin/blob/WiiU/Source/Core/DiscIO/FileSystemWiiU.cpp
        private int LoadDirectory(BinaryDataReader reader, int startIdx, int size, string currentPath)
        {
            int currentIdx = startIdx;

            while (currentIdx < size)
            {
                uint typeAndName = reader.ReadUInt32();
                byte type = (byte)(typeAndName >> 24);
                uint nameOffset = typeAndName & 0xFFFFFF;

                string path;
                using (reader.TemporarySeek(_nameTableOffset + (long)nameOffset, SeekOrigin.Begin))
                {
                    path = currentPath + reader.ReadString(StringDataFormat.ZeroTerminated);
                }

                uint fileOffset = reader.ReadUInt32();
                uint fileSize = reader.ReadUInt32();
                ushort flags = reader.ReadUInt16();
                int contentIdx = (int)reader.ReadUInt16();

                if ((type & 1) != 0) // dir
                {
                    currentIdx = LoadDirectory(reader, currentIdx + 1, (int)fileSize, path + "/");

                }
                else // file
                {
                    _fileEntries.Add(path, new FstFileEntry()
                    {
                        Offset = fileOffset,
                        Size = fileSize,
                        Flags = flags,
                        ContentIdx = contentIdx,
                        IsNotInPackage = (type & 0x80) != 0
                    });

                    currentIdx++;
                }
            }

            return currentIdx;
        }

        public int GetEntryCount()
        {
            return _fileEntries.Count;
        }

        public IEnumerable<string> GetAllFilePaths()
        {
            return _fileEntries.Keys;
        }

        public FstFileEntry GetEntry(string path)
        {
            return _fileEntries[path];
        }
    }
}
