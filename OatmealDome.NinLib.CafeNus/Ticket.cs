using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.CafeNus
{
    internal class Ticket
    {
        public byte[] EncryptedTitleKey
        {
            get;
            private set;
        }

        // https://wiiubrew.org/wiki/Ticket
        public Ticket(Stream stream)
        {
            using BinaryDataReader reader = new BinaryDataReader(stream);

            reader.ByteOrder = ByteOrder.BigEndian;

            reader.Seek(0x1bf, SeekOrigin.Begin);

            EncryptedTitleKey = reader.ReadBytes(16);
        }
    }
}
