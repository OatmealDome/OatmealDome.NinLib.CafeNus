# OatmealDome.NinLib.CafeNus

This library allows you to decrypt and read files from Wii U NUS files. The Wii U common key is required (not included).

## Usage

```csharp
byte[] commonKey = File.ReadAllBytes("/path/to/common_key.bin");
using NusFilesystem nusFilesystem = new NusFilesystem("/path/to/nus/files/base/", "/path/to/nus/files/update/", commonKey);
// the path to update files is optional, specify null if none exists

byte[] file = nusFilesystem.ReadFile("/content/path/to/file");
```
