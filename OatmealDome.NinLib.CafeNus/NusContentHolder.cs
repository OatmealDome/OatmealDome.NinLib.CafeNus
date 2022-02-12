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

        private Tmd Tmd;
        private byte[] TitleKey;
        private List<Stream> ContentStreams;
        private List<object> StreamLocks;
        private Fst Fst;

        public NusContentHolder(string basePath, byte[] commonKey)
        {
            ContentStreams = new List<Stream>();
            StreamLocks = new List<object>();

            // Search for the TMD by looking for files that begin with "tm"
            // Some might be "tmd", and some might have the version appended, like "tmd.16"
            string tmdPath = Directory.EnumerateFiles(basePath, "tm*").FirstOrDefault();
            if (tmdPath == null)
            {
                throw new Exception("Could not find tmd");
            }

            using (FileStream stream = File.OpenRead(tmdPath))
            {
                Tmd = new Tmd(stream);
            }

            using (FileStream stream = File.OpenRead(Path.Combine(basePath, "cetk")))
            {
                Ticket ticket = new Ticket(stream);

                using (MemoryStream inputStream = new MemoryStream(ticket.EncryptedTitleKey))
                using (MemoryStream outputStream = new MemoryStream())
                {
                    byte[] titleKeyIv = new byte[16];
                    Array.Copy(BitConverter.GetBytes(Tmd.TitleId).Reverse().ToArray(), titleKeyIv, 8);

                    DecryptAesCbc(inputStream, outputStream, commonKey, titleKeyIv);

                    TitleKey = outputStream.ToArray();
                }
            }
            
            foreach (TmdContent content in Tmd.Contents)
            {
                FileStream fileStream = File.OpenRead(Path.Combine(basePath, content.Id.ToString("x8")));

                if ((content.Type & 2) != 0) // has hash tree
                {
                    ContentStreams.Add(new NusHashedContentStream(fileStream, TitleKey));
                }
                else // normal, just AES-CBC encrypted
                {
                    MemoryStream memoryStream = new MemoryStream();
                    
                    DecryptAesCbc(fileStream, memoryStream, TitleKey, new byte[16]);

                    memoryStream.Seek(0, SeekOrigin.Begin);

                    ContentStreams.Add(memoryStream);

                    fileStream.Dispose();
                }

                StreamLocks.Add(new object());
            }

            // The FST is always at content idx 0
            Fst = new Fst(ContentStreams[0]);

            Files = new List<string>();

            foreach (string path in Fst.GetAllFilePaths())
            {
                FstFileEntry entry = Fst.GetEntry(path);

                if (!entry.IsNotInPackage)
                {
                    Files.Add(path);
                }
            }
        }

        public void Dispose()
        {
            foreach (Stream stream in ContentStreams)
            {
                stream.Dispose();
            }
        }

        public Stream GetFile(string path)
        {
            FstFileEntry entry = Fst.GetEntry(path);
            
            if (entry.IsNotInPackage)
            {
                throw new FileNotFoundException("Can't access deleted file");
            }
            
            lock (StreamLocks[entry.ContentIdx])
            {
                Stream contentStream = ContentStreams[entry.ContentIdx];
                
                contentStream.Seek(entry.Offset * Fst.OffsetFactor, SeekOrigin.Begin);

                byte[] file = new byte[entry.Size];
                contentStream.Read(file, 0, (int)entry.Size);

                return new MemoryStream(file);
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

            using (CryptoStream cryptoStream = new CryptoStream(inStream, decryptor, CryptoStreamMode.Read))
            {
                cryptoStream.CopyTo(outStream);
            }
        }
    }
}
