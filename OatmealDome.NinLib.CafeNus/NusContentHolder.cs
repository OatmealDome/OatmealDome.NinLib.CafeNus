using System.Security.Cryptography;
using OatmealDome.NinLib.CafeNus.Filesystem;

namespace OatmealDome.NinLib.CafeNus
{
    class NusContentHolder
    {
        public List<string> Files
        {
            get;
            private set;
        }

        private readonly Tmd _tmd;
        private readonly byte[] _titleKey;
        private readonly List<Stream> _contentStreams;
        private readonly List<object> _streamLocks;
        private readonly Fst _fst;

        public NusContentHolder(string basePath, byte[] commonKey)
        {
            _contentStreams = new List<Stream>();
            _streamLocks = new List<object>();

            // Search for the TMD by looking for files that begin with "tm"
            // Some might be "tmd", and some might have the version appended, like "tmd.16"
            string? tmdPath = Directory.EnumerateFiles(basePath, "tm*").FirstOrDefault();
            if (tmdPath == null)
            {
                throw new Exception("Could not find tmd");
            }

            using (FileStream stream = File.OpenRead(tmdPath))
            {
                _tmd = new Tmd(stream);
            }

            using (FileStream stream = File.OpenRead(Path.Combine(basePath, "cetk")))
            {
                Ticket ticket = new Ticket(stream);

                using (MemoryStream inputStream = new MemoryStream(ticket.EncryptedTitleKey))
                using (MemoryStream outputStream = new MemoryStream())
                {
                    byte[] titleKeyIv = new byte[16];
                    Array.Copy(BitConverter.GetBytes(_tmd.TitleId).Reverse().ToArray(), titleKeyIv, 8);

                    DecryptAesCbc(inputStream, outputStream, commonKey, titleKeyIv);

                    _titleKey = outputStream.ToArray();
                }
            }
            
            foreach (TmdContent content in _tmd.Contents)
            {
                FileStream fileStream = File.OpenRead(Path.Combine(basePath, content.Id.ToString("x8")));

                if ((content.Type & 2) != 0) // has hash tree
                {
                    _contentStreams.Add(new NusHashedContentStream(fileStream, _titleKey));
                }
                else // normal, just AES-CBC encrypted
                {
                    MemoryStream memoryStream = new MemoryStream();
                    
                    DecryptAesCbc(fileStream, memoryStream, _titleKey, new byte[16]);

                    memoryStream.Seek(0, SeekOrigin.Begin);

                    _contentStreams.Add(memoryStream);

                    fileStream.Dispose();
                }

                _streamLocks.Add(new object());
            }

            // The FST is always at content idx 0
            _fst = new Fst(_contentStreams[0]);

            Files = new List<string>();

            foreach (string path in _fst.GetAllFilePaths())
            {
                FstFileEntry entry = _fst.GetEntry(path);

                if (!entry.IsNotInPackage)
                {
                    Files.Add(path);
                }
            }
        }

        public void Dispose()
        {
            foreach (Stream stream in _contentStreams)
            {
                stream.Dispose();
            }
        }

        public byte[] GetFile(string path)
        {
            FstFileEntry entry = _fst.GetEntry(path);
            
            if (entry.IsNotInPackage)
            {
                throw new FileNotFoundException("Can't access deleted file");
            }
            
            lock (_streamLocks[entry.ContentIdx])
            {
                Stream contentStream = _contentStreams[entry.ContentIdx];
                
                contentStream.Seek(entry.Offset * _fst.OffsetFactor, SeekOrigin.Begin);

                byte[] file = new byte[entry.Size];
                contentStream.Read(file, 0, (int)entry.Size);

                return file;
            }
        }

        private void DecryptAesCbc(Stream inStream, Stream outStream, byte[] key, byte[] iv)
        {
            using Aes aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.Key = key;
            aes.IV = iv;

            ICryptoTransform decryptor = aes.CreateDecryptor(key, iv);

            using CryptoStream cryptoStream = new CryptoStream(inStream, decryptor, CryptoStreamMode.Read);
            cryptoStream.CopyTo(outStream);
        }
    }
}
