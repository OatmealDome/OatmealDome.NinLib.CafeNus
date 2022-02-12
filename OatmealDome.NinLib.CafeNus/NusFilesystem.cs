namespace OatmealDome.NinLib.CafeNus;

public sealed class NusFilesystem
{
    private readonly NusContentHolder _baseContentHolder;
    private readonly NusContentHolder? _updateContentHolder;

    private readonly Dictionary<string, NusSource> _files = new Dictionary<string, NusSource>();

    public NusFilesystem(string basePath, string? updatePath, byte[] commonKey)
    {
        if (updatePath != null)
        {
            _baseContentHolder = new NusContentHolder(basePath, commonKey);
            _updateContentHolder = new NusContentHolder(updatePath, commonKey);
        }
        else
        {
            _baseContentHolder = new NusContentHolder(basePath, commonKey);
            _updateContentHolder = null;
        }

        foreach (string filePath in _baseContentHolder.Files)
        {
            _files.Add(filePath, NusSource.Base);
        }

        if (_updateContentHolder != null)
        {
            foreach (string filePath in _updateContentHolder.Files)
            {
                _files[filePath] = NusSource.Update;
            }
        }
    }
    
    public byte[] GetFile(string path)
    {
        if (_files[path] == NusSource.Base)
        {
            return _baseContentHolder.GetFile(path);
        }
        else
        {
            return _updateContentHolder!.GetFile(path);
        }
    }

    public IEnumerable<string> GetAllFiles()
    {
        return _files.Keys;
    }
}
