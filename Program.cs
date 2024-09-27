using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using LZ4;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MetaphorAPKPack
{
    internal class Program
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TextureEntry
        {
            public string filename;
            public int fileSize;
            public int field04;
            public int field08;
            public int field0c;
            public int field10;
            public int field14;
            public int offset;
            public int field1c;
        }

        public class TextureEntryCompressed
        {
            public string filename;
            public int fileSizeDec;
            public int fileSizeComp;
            public int pointer;
            public byte[] compFileData;
        }

        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Drag and drop an APK file or folder full of .dds files onto the application.\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            string input = args[0];

            if (File.Exists(input) && Path.GetExtension(input).Equals(".apk", StringComparison.OrdinalIgnoreCase))
            {
                List<TextureEntry> structList = await ProcessAPK(input);
                await DumpDDSFiles(structList, input);
            }
            else if (Directory.Exists(input))
            {
                string outputApkName = Path.Combine(Path.GetDirectoryName(input), Path.GetFileName(input) + ".apk");
                List<TextureEntryCompressed> structList = ProcessDDSFolder(input);
                WriteDDSToAPK(structList, outputApkName);
            }
            else
            {
                Console.WriteLine("Invalid input. Provide either a .apk file or a folder with dds files and a filelist.txt\nPress any key to exit...");
                Console.ReadKey();
                return;
            }
        }

        public static async Task<List<TextureEntry>> ProcessAPK(string apkPath)
        {
            List<TextureEntry> TextureEntries = new List<TextureEntry>();

            using (FileStream fs = new FileStream(apkPath, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    fs.Seek(0x8, SeekOrigin.Begin);

                    var numOfBlocks = reader.ReadInt32();
                    var dummy = reader.ReadInt32();

                    for (int i = 0; i < numOfBlocks; i++)
                    {
                        TextureEntry data = new TextureEntry();

                        // Read name as byte array and convert to string
                        byte[] nameBytes = reader.ReadBytes(0x100);
                        data.filename = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                        // Read remaining fields
                        data.fileSize = reader.ReadInt32();
                        data.field04 = reader.ReadInt32();
                        data.field08 = reader.ReadInt32();
                        data.field0c = reader.ReadInt32();
                        data.field10 = reader.ReadInt32();
                        data.field14 = reader.ReadInt32();
                        data.offset = reader.ReadInt32();
                        data.field1c = reader.ReadInt32();

                        // Add to the list
                        TextureEntries.Add(data);
                    }
                }
            }

            foreach (var data in TextureEntries)
            {
                Console.WriteLine($"Name: {data.filename}, FileSize: 0x{data.fileSize:X8}, Offset: 0x{data.offset:X8}");
            }

            return TextureEntries;
        }

        public static async Task DumpDDSFiles(List<TextureEntry> TextureEntries, string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                List<string> FileNames = new List<string>();

                foreach (var data in TextureEntries)
                {
                    FileNames.Add(data.filename);

                    fs.Seek(data.offset, SeekOrigin.Begin);

                    byte[] compressedFile = new byte[data.fileSize];
                    await fs.ReadAsync(compressedFile, 0, data.fileSize);

                    string outputFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath), data.filename.TrimEnd('\0'));

                    Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));

                    int decompressedSize = BitConverter.ToInt32(compressedFile, 0x0C);
                    int compressedSize = BitConverter.ToInt32(compressedFile, 0x20);

                    // Extract the compressed data starting from 0x30
                    byte[] compressedData = new byte[compressedSize];
                    Array.Copy(compressedFile, 0x30, compressedData, 0, compressedSize);

                    byte[] decompressedData = LZ4Codec.Decode(compressedData, 0, compressedSize, decompressedSize);

                    await File.WriteAllBytesAsync(outputFilePath, decompressedData);
                    Console.WriteLine($"Saved {data.filename.TrimEnd('\0')} to {outputFilePath}");
                }

                string txtPath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath), "FileList.txt");
                File.WriteAllLines(txtPath, FileNames);
            }
        }

        public static List<TextureEntryCompressed> ProcessDDSFolder(string folderPath)
        {
            List<TextureEntryCompressed> structList = new List<TextureEntryCompressed>();

            // Get all .dds files in the folder
            string[] ddsFiles = Directory.GetFiles(folderPath, "*.dds");

            List<string> FileList = new List<string>(File.ReadAllLines(Path.Combine(folderPath, "FileList.txt")));

            foreach (var ddsFile in ddsFiles)
            {
                FileInfo fileInfo = new FileInfo(ddsFile);
                byte[] fileData = File.ReadAllBytes(ddsFile);

                TextureEntryCompressed data = new TextureEntryCompressed
                {
                    filename = fileInfo.Name,
                    fileSizeDec = fileData.Length,
                    compFileData = fileData,
                };

                structList.Add(data);
            }

            structList.Sort((x, y) => FileList.IndexOf(x.filename).CompareTo(FileList.IndexOf(y.filename)));

            return structList;
        }


        public static void WriteDDSToAPK(List<TextureEntryCompressed> structList, string outputFilePath)
        {
            Console.WriteLine($"Creating Directory {Path.GetDirectoryName(outputFilePath)}");
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));

            List<TextureEntryCompressed> CompressedDataHolder = new List<TextureEntryCompressed>();

            foreach (var data in structList)
            {
                Console.WriteLine($"Compressing file {data.filename}");

                byte[] compressedData = LZ4Codec.EncodeHC(data.compFileData, 0, data.fileSizeDec);

                int paddingSize = (0x10 - (compressedData.Length % 0x10)) % 0x10;
                byte[] paddedCompressedData = new byte[compressedData.Length + paddingSize];

                Buffer.BlockCopy(compressedData, 0, paddedCompressedData, 0, compressedData.Length);

                byte[] header = new byte[0x30];
                Buffer.BlockCopy(BitConverter.GetBytes(0x305A5A5A), 0, header, 0x0, 4);  // ZZZ0 Magic
                Buffer.BlockCopy(BitConverter.GetBytes(0x010001), 0, header, 0x4, 4);    // Unknown, Bitfield?
                Buffer.BlockCopy(BitConverter.GetBytes(data.fileSizeDec), 0, header, 0xC, 4);  // Decompressed size
                Buffer.BlockCopy(BitConverter.GetBytes(compressedData.Length + 0x30), 0, header, 0x10, 4);  // 0x10
                Buffer.BlockCopy(BitConverter.GetBytes(compressedData.Length), 0, header, 0x20, 4);  // Compressed size
                Buffer.BlockCopy(BitConverter.GetBytes(0x30), 0, header, 0x24, 4);  // Header size

                byte[] outputData = new byte[header.Length + paddedCompressedData.Length];
                Buffer.BlockCopy(header, 0, outputData, 0, header.Length);
                Buffer.BlockCopy(paddedCompressedData, 0, outputData, header.Length, paddedCompressedData.Length);

                CompressedDataHolder.Add(new TextureEntryCompressed
                {
                    filename = data.filename,
                    fileSizeDec = data.fileSizeDec,
                    fileSizeComp = paddedCompressedData.Length,
                    compFileData = outputData,
                });
            }

            using (FileStream fs = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                writer.Write(0x4B434150);
                writer.Write(0x10000);
                writer.Write(CompressedDataHolder.Count);
                writer.Write(0);

                foreach (var data in CompressedDataHolder)
                {
                    writer.Write(new byte[0x120]); // fill dummy headers for pointers
                }

                for (int i = 0; i < CompressedDataHolder.Count; i++)
                {
                    CompressedDataHolder[i].pointer = (int)writer.BaseStream.Position;
                    writer.Write(CompressedDataHolder[i].compFileData);
                }

                writer.Seek(0x10, SeekOrigin.Begin);

                foreach (var data in CompressedDataHolder) // write headers now that we have pointers
                {
                    byte[] nameBytes = Encoding.ASCII.GetBytes(data.filename.PadRight(0x100, '\0'));
                    writer.Write(nameBytes);

                    writer.Write(data.compFileData.Length);
                    writer.Write(0); // 0x04
                    writer.Write(0); // 0x08
                    writer.Write(0); // 0x0C
                    writer.Write(0); // 0x10
                    writer.Write(0); // 0x14
                    writer.Write(data.pointer); // 0x18
                    writer.Write(0); // 0x1C
                }

                Console.WriteLine($"APK created: {outputFilePath}");
            }
        }

    }
}