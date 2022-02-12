using System.Security.Cryptography;

namespace OatmealDome.NinLib.CafeNus
{
    class NusHashedContentStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        // In 0xFC00 blocks
        private long DataLength;

        public override long Length
        {
            get
            {
                return DataLength;
            }
        }

        // Within the "real data" space of 0xFC00 blocks
        private long DataPosition;

        public override long Position
        {
            get
            {
                return DataPosition;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        private Stream InnerStream;
        private bool KeepStreamOpen;
        private bool IsDisposed;

        private Aes Aes;
        private int CurrentBlock;
        private byte[] HashBlock;
        private byte[] DataBlock;

        public NusHashedContentStream(Stream stream, byte[] titleKey, bool keepOpen = false)
        {
            Aes = Aes.Create();
            Aes.Mode = CipherMode.CBC;
            Aes.Padding = PaddingMode.None;
            Aes.Key = titleKey;

            InnerStream = stream;
            KeepStreamOpen = keepOpen;
            IsDisposed = false;

            CurrentBlock = 0;
            HashBlock = new byte[0x400];
            DataBlock = new byte[0xFC00];

            long blockCount = InnerStream.Length / 0x10000;
            DataLength = blockCount * 0xFC00;

            DataPosition = 0;

            UpdateBlockData();
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }

            base.Dispose(disposing);

            if (!KeepStreamOpen)
            {
                InnerStream.Dispose();
            }

            IsDisposed = true;
        }

        private void AdvanceBlock()
        {
            SetBlock(CurrentBlock + 1);
        }

        private void SetBlock(int block)
        {
            CurrentBlock = block;

            UpdateBlockData();
        }

        private void UpdateBlockData()
        {
            long blockOffset = CurrentBlock * 0x10000;

            InnerStream.Seek(blockOffset, SeekOrigin.Begin);

            // Hash blocks use all zero IV
            byte[] iv = new byte[16];
            Aes.IV = iv;

            using (CryptoStream cryptoStream = new CryptoStream(InnerStream, Aes.CreateDecryptor(), CryptoStreamMode.Read, true))
            {
                cryptoStream.Read(HashBlock, 0, 0x400);
            }

            // h0 hash is the IV
            Array.Copy(HashBlock, 0x14 * (CurrentBlock % 16), iv, 0, 0x10);
            Aes.IV = iv;

            using (CryptoStream cryptoStream = new CryptoStream(InnerStream, Aes.CreateDecryptor(), CryptoStreamMode.Read, true))
            {
                cryptoStream.Read(DataBlock, 0, 0xFC00);
            }

            // Only hash levels 0 to 2 are verified, 3 isn't because often we have no h3 block
            for (int i = 0; i < 3; i++)
            {
                if (!VerifyBlock(i))
                {
                    throw new Exception($"h{i} mismatch");
                }
            }
        }

        private bool VerifyBlock(int hashLevel)
        {
            if (hashLevel < 0 || hashLevel > 3)
            {
                throw new Exception("Invalid hash level");
            }

            SHA1 sha1 = SHA1.Create();
            
            byte[] hash;
            if (hashLevel == 0)
            {
                hash = sha1.ComputeHash(DataBlock);
            }
            else
            {
                hash = sha1.ComputeHash(HashBlock, 0x140 * (hashLevel - 1), 0x140);
            }

            int hashNum = 0;
            switch (hashLevel)
            {
                case 0:
                    hashNum = CurrentBlock % 16;
                    break;
                case 1:
                    hashNum = (CurrentBlock / 16) % 16;
                    break;
                case 2:
                    hashNum = (CurrentBlock / 256) % 16;
                    break;
                case 3:
                    hashNum = (CurrentBlock / 4096) % 16;
                    break;
            }

            byte[] expectedHash = new byte[0x14];
            if (hashLevel == 3)
            {
                throw new Exception("Hash level 3 not supported");
            }
            else
            {
                Array.Copy(HashBlock, (0x140 * hashLevel) + (hashNum * 0x14), expectedHash, 0, 0x14);
            }

            return expectedHash.SequenceEqual(hash);
        }

        public override void Flush()
        {
            throw new NotSupportedException("Stream is read-only");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (DataPosition >= DataLength)
                {
                    return i;
                }

                long blockPosition = DataPosition % 0xFC00;

                buffer[offset + i] = DataBlock[blockPosition];

                DataPosition++;

                if ((blockPosition + 1) == 0xFC00)
                {
                    if (DataPosition >= DataLength)
                    {
                        return i;
                    }

                    AdvanceBlock();
                }
            }

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset < 0)
            {
                throw new NotImplementedException("Seeking backwards not implemented");
            }

            long newOffset;
            if (origin == SeekOrigin.Begin)
            {
                newOffset = offset;
            }
            else if (origin == SeekOrigin.Current)
            {
                newOffset = DataPosition + offset;
            }
            else
            {
                throw new NotImplementedException("SeekOrigin.End not implemented");
            }

            if (newOffset >= Length)
            {
                throw new Exception("Seek past end");
            }

            DataPosition = newOffset;

            long block = newOffset / 0xFC00;
            SetBlock((int)block);

            return newOffset;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Stream is read-only");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Stream is read-only");
        }
    }
}
