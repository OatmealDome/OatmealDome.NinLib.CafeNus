using System.Security.Cryptography;

namespace OatmealDome.NinLib.CafeNus
{
    internal class NusHashedContentStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        // In 0xFC00 blocks
        private readonly long _dataLength;

        public override long Length
        {
            get
            {
                return _dataLength;
            }
        }

        // Within the "real data" space of 0xFC00 blocks
        private long _dataPosition;

        public override long Position
        {
            get
            {
                return _dataPosition;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        private readonly Stream _innerStream;
        private readonly bool _keepStreamOpen;
        private bool _isDisposed;

        private readonly Aes _aes;
        private int _currentBlock;
        private readonly byte[] _hashBlock;
        private readonly byte[] _dataBlock;

        public NusHashedContentStream(Stream stream, byte[] titleKey, bool keepOpen = false)
        {
            _aes = Aes.Create();
            _aes.Mode = CipherMode.CBC;
            _aes.Padding = PaddingMode.None;
            _aes.Key = titleKey;

            _innerStream = stream;
            _keepStreamOpen = keepOpen;
            _isDisposed = false;

            _currentBlock = 0;
            _hashBlock = new byte[0x400];
            _dataBlock = new byte[0xFC00];

            long blockCount = _innerStream.Length / 0x10000;
            _dataLength = blockCount * 0xFC00;

            _dataPosition = 0;

            UpdateBlockData();
        }

        protected override void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            base.Dispose(disposing);

            if (!_keepStreamOpen)
            {
                _innerStream.Dispose();
            }

            _isDisposed = true;
        }

        private void AdvanceBlock()
        {
            SetBlock(_currentBlock + 1);
        }

        private void SetBlock(int block)
        {
            _currentBlock = block;

            UpdateBlockData();
        }

        private void UpdateBlockData()
        {
            long blockOffset = _currentBlock * 0x10000;

            _innerStream.Seek(blockOffset, SeekOrigin.Begin);

            // Hash blocks use all zero IV
            byte[] iv = new byte[16];
            _aes.IV = iv;

            using (CryptoStream cryptoStream = new CryptoStream(_innerStream, _aes.CreateDecryptor(), CryptoStreamMode.Read, true))
            {
                cryptoStream.Read(_hashBlock, 0, 0x400);
            }

            // h0 hash is the IV
            Array.Copy(_hashBlock, 0x14 * (_currentBlock % 16), iv, 0, 0x10);
            _aes.IV = iv;

            using (CryptoStream cryptoStream = new CryptoStream(_innerStream, _aes.CreateDecryptor(), CryptoStreamMode.Read, true))
            {
                cryptoStream.Read(_dataBlock, 0, 0xFC00);
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
                hash = sha1.ComputeHash(_dataBlock);
            }
            else
            {
                hash = sha1.ComputeHash(_hashBlock, 0x140 * (hashLevel - 1), 0x140);
            }

            int hashNum = 0;
            switch (hashLevel)
            {
                case 0:
                    hashNum = _currentBlock % 16;
                    break;
                case 1:
                    hashNum = (_currentBlock / 16) % 16;
                    break;
                case 2:
                    hashNum = (_currentBlock / 256) % 16;
                    break;
                case 3:
                    hashNum = (_currentBlock / 4096) % 16;
                    break;
            }

            byte[] expectedHash = new byte[0x14];
            if (hashLevel == 3)
            {
                throw new Exception("Hash level 3 not supported");
            }
            else
            {
                Array.Copy(_hashBlock, (0x140 * hashLevel) + (hashNum * 0x14), expectedHash, 0, 0x14);
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
                if (_dataPosition >= _dataLength)
                {
                    return i;
                }

                long blockPosition = _dataPosition % 0xFC00;

                buffer[offset + i] = _dataBlock[blockPosition];

                _dataPosition++;

                if ((blockPosition + 1) == 0xFC00)
                {
                    if (_dataPosition >= _dataLength)
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
                newOffset = _dataPosition + offset;
            }
            else
            {
                throw new NotImplementedException("SeekOrigin.End not implemented");
            }

            if (newOffset >= Length)
            {
                throw new Exception("Seek past end");
            }

            _dataPosition = newOffset;

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
