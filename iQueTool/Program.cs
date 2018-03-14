﻿using NDesk.Options;
using System;
using System.Collections.Generic;
using System.IO;
using iQueTool.Structs;
using iQueTool.Files;

namespace iQueTool
{
    class Program
    {
        // parameters
        static bool printHelp = false;
        static bool printInfo = false;
        static bool writeInfo = false;

        static string outputFile = String.Empty;

        static bool extractAll = false;
        static string extractContentIds = String.Empty;
        static string extractTIDs = String.Empty;
        static string extractIDs = String.Empty;

        static bool extractKernel = false;
        
        static bool writeNewTicketFile = false;

        static bool skipVerifyChecksums = false;
        static bool isBadDump = false;

        static bool showAllFsInfo = false;

        static string generateSparePath = String.Empty;
        static bool generateFullSpare = false;

        static string filePath = String.Empty;
        static string extraPath = String.Empty;

        static void Main(string[] args)
        {
            if (File.Exists("cert.sys"))
                iQueCertCollection.MainCollection = new iQueCertCollection(File.ReadAllBytes("cert.sys"));
            else if(File.Exists(@"D:\cert.sys")) // try reading from root of a drive, so we don't have to copy cert.sys with us everywhere
                iQueCertCollection.MainCollection = new iQueCertCollection(File.ReadAllBytes(@"D:\cert.sys"));

            const string fmt = "   ";

            var p = new OptionSet {
                { "h|?|help", v => printHelp = v != null },
                { "i|info", v => printInfo = v != null },
                { "wi|writeinfo", v => writeInfo = v != null },
                { "o|output=", v => outputFile = v },

                { "x|extract", v => extractAll = v != null },
                { "xc|extractcids=", v => extractContentIds = v },
                { "xt|extracttids=", v => extractTIDs = v },
                { "xi|extractids=", v => extractIDs = v },

                { "xk|extractkernel", v => extractKernel = v != null },
                { "fs|showallfs", v => showAllFsInfo = v != null },

                { "gs|genspare=", v => generateSparePath = v },
                { "gp|fullspare", v => generateFullSpare = v != null },

                { "sc|skipchecksums", v => skipVerifyChecksums = v != null },
                { "bd|baddump", v => isBadDump = v != null },

                { "n|newfile", v => writeNewTicketFile = v != null }
            };

            var extraArgs = p.Parse(args);

            Console.WriteLine("iQueTool 0.2a: iQue Player file manipulator");

            if (printHelp || extraArgs.Count <= 1)
            {
                Console.WriteLine("Usage  : iquetool.exe [mode] [parameters] [filepath]");
                Console.WriteLine();
                Console.WriteLine("Valid modes: nand / tickets / certs / crl / sparefix"); // / privdata / kernel");
                Console.WriteLine();
                Console.WriteLine("General parameters:");
                Console.WriteLine(fmt + "-h (-help) - print iquetool usage");
                Console.WriteLine(fmt + "-i (-info) - print basic info about file");
                Console.WriteLine(fmt + "-wi (-writeinfo) - write detailed info about file to [filepath].txt");
                Console.WriteLine(fmt + "-o (-output) <output-path> - specify output filename/directory");

                Console.WriteLine();
                Console.WriteLine("Mode \"tickets\" / \"certs\" / \"crl\":");
                Console.WriteLine();
                Console.WriteLine(fmt + "-x - extracts all entries from file");
                Console.WriteLine(fmt + "-xi (-extractids) <comma-delimited-ids> - extract entries with these indexes");
                Console.WriteLine(fmt + "-xc (-extractcids) <comma-delimited-cids> - extract entries with these content ids");
                Console.WriteLine(fmt + "-xt (-extracttids) <comma-delimited-tids> - extract entries with these ticket ids");
                Console.WriteLine();
                Console.WriteLine(fmt + "-n - writes extracted entries into a single array file");

                Console.WriteLine();
                Console.WriteLine("Note that by default the extract commands above will extract tickets as seperate files");
                Console.WriteLine("with the format <output-dir>\\ticket-<bbid>-<contentid>-<tid>.dat");

                Console.WriteLine();
                Console.WriteLine("Mode \"nand\":");
                Console.WriteLine();
                Console.WriteLine(fmt + "-x - extracts all files from NAND");
                Console.WriteLine(fmt + "-xi (-extractids) <comma-delimited-ids> - extract inodes with these indexes");
                Console.WriteLine(fmt + "-xk (-extractkernel) - extract secure-kernel from NAND");
                Console.WriteLine(fmt + "-fs (-showallfs) - shows info about all found FS blocks");
                Console.WriteLine();
                Console.WriteLine(fmt + "-gs (-genspare) <dest-spare.bin-path> - generates block-spare/ECC data for this NAND");
                Console.WriteLine(fmt + "-gp (-fullspare) - will generate page-spare/ECC data (0x20 pages per block) instead");
                Console.WriteLine();
                Console.WriteLine(fmt + "-sc (-skipchecksums) - skip verifying FS checksums");
                Console.WriteLine(fmt + "-bd (-baddump) - will try reading inodes with a 0x10 byte offset");
                Console.WriteLine();
                Console.WriteLine("Mode \"sparefix\":");
                Console.WriteLine(fmt + "usage: sparefix [spare.bin path] <nand.bin path>");
                Console.WriteLine(fmt + "fixes overdump / raw page-spare dumps to match BB block-spare dumps");
                Console.WriteLine(fmt + "if nand.bin path is specified, will correct the spare data using that nand");
                Console.WriteLine();
                Console.WriteLine(fmt + "-o (-output) <output-path> - specify output filename (default: [input]_fixed)");
                Console.WriteLine(fmt + "-gp (-fullspare) - disables reducing page-spare data to block-spare");
                Console.WriteLine();

                Console.WriteLine("iQue signature verification: " + (iQueCertCollection.MainCollection != null ? "enabled" : "disabled"));
                if (iQueCertCollection.MainCollection == null)
                {
                    Console.WriteLine(fmt + "To enable, drop a cert.sys file (taken from an iQue NAND) next to the iQueTool exe");
                    Console.WriteLine(fmt + "Alternatively you can put it at the root of your D: drive");
                    Console.WriteLine(fmt + "Also when opening a NAND image the cert.sys will automatically be loaded from it, if not already found locally");
                }
                Console.WriteLine();
                return;
            }
            Console.WriteLine();

            string mode = extraArgs[0].ToLower();
            filePath = extraArgs[1];
            if (extraArgs.Count > 2)
                extraPath = extraArgs[2];

            if (mode == "tickets")
                ModeArrayFile<iQueTitleData>();
            else if (mode == "certs")
                ModeArrayFile<iQueCertificate>();
            else if (mode == "crl")
                ModeArrayFile<iQueCertificateRevocation>();
            else if (mode == "nand")
                ModeNAND();
            else if (mode == "sparefix")
                ModeSpareFix();
            else
            {
                Console.WriteLine($"Invalid mode \"{mode}\".");
                Console.WriteLine("Valid modes are: nand / tickets / certs / crl / sparefix"); // / privdata / kernel");
                return;
            }
        }

        static void ModeSpareFix()
        {
            if (string.IsNullOrEmpty(outputFile))
                outputFile = filePath + "_fixed";
            Console.WriteLine($"Reading spare from {filePath}...");

            using (var fixedStream = new MemoryStream())
            {
                using (var reader = new BinaryReader(File.OpenRead(filePath)))
                {
                    bool isBlockSpare = true;
                    if (reader.BaseStream.Length == 64 * 1024)
                    {
                        // must be a block-spare already, just copy it all to the fixed stream so we can fix up the ECC
                        byte[] spare = reader.ReadBytes(64 * 1024);
                        fixedStream.Write(spare, 0, 64 * 1024);
                    }
                    else
                    {
                        int numSkip = 0;
                        if (reader.BaseStream.Length == 1024 * 1024) // overdump, just skip 0xF0 after each read
                            numSkip = 0xF0;
                        else if (reader.BaseStream.Length == 1024 * 1024 * 2) // page-spare dump, we only want the spare of the last page in each block
                        {
                            numSkip = 0x1F0;
                            reader.BaseStream.Position = 0x1F0;
                        }
                        else
                        {
                            Console.WriteLine("Spare isn't overdump/page-spare/block-spare dump?");
                            return;
                        }

                        if (generateFullSpare)
                        {
                            if (numSkip != 0x1F0)
                            {
                                Console.WriteLine("Invalid spare used with -gp (-fullspare)");
                                Console.WriteLine("This switch only works with page/block-spares, not overdumps!");
                                return;
                            }

                            // user gave -gp switch, so just copy the whole page-spare instead of reducing to block-spare
                            reader.BaseStream.Position = 0;
                            byte[] spare = reader.ReadBytes(1024 * 1024 * 2);
                            fixedStream.Write(spare, 0, 1024 * 1024 * 2);

                            isBlockSpare = false;
                        }
                        else
                            while (reader.BaseStream.Position < reader.BaseStream.Length)
                            {
                                byte[] spare = reader.ReadBytes(0x10);
                                spare[6] = 0; // fix page-spare dump to match format of block-spare dump
                                fixedStream.Write(spare, 0, 0x10);
                                if (reader.BaseStream.Position < reader.BaseStream.Length) // with raw page-spare dumps we're at the end here, so check for that
                                    reader.BaseStream.Position += numSkip;
                            }
                    }

                    // if nand.bin is specified, read from it and correct the spare data
                    if(!string.IsNullOrEmpty(extraPath))
                    {
                        var nand = new iQueNand(extraPath);
                        nand.Read();
                        var fixedSpare = nand.GenerateSpareData(isBlockSpare, fixedStream.ToArray()); // todo: make a stream-based GenerateSpareData method?
                        fixedStream.Position = 0;
                        fixedStream.Write(fixedSpare, 0, fixedSpare.Length);
                    }
                }
                File.WriteAllBytes(outputFile, fixedStream.ToArray());
            }
            Console.WriteLine($"Saved fixed spare to {outputFile}!");
        }

        static void ModeNAND()
        {
            Console.WriteLine($"Opening NAND image from {filePath}...");
            Console.WriteLine();

            var nandFile = new iQueNand(filePath) {
                SkipVerifyFsChecksums = skipVerifyChecksums,
                InodesOffset = isBadDump ? 0x10 : 0
            };

            if (!nandFile.Read())
            {
                Console.WriteLine($"[!] Failed to read NAND image!");
                return;
            }

            if(printInfo)
                Console.WriteLine(nandFile.ToString(true, true, showAllFsInfo));

            if(writeInfo)
            {
                File.WriteAllText(filePath + ".txt", nandFile.ToString(true, false, showAllFsInfo));
                Console.WriteLine($"Wrote detailed NAND info to {filePath}.txt");
            }

            if(extractAll)
            {
                if (string.IsNullOrEmpty(outputFile))
                {
                    outputFile = filePath + "_ext";
                    Console.WriteLine("[!] No output path (-o) given, set path to:");
                    Console.WriteLine($"- {outputFile}");
                    Console.WriteLine();
                }

                if (!Directory.Exists(outputFile))
                    Directory.CreateDirectory(outputFile);

                int count = 0;
                foreach (var file in nandFile.MainFsInodes)
                {
                    if (!file.IsValid)
                        continue;

                    var extPath = Path.Combine(outputFile, file.NameString);
                    Console.WriteLine($"Writing file to {extPath}");
                    File.WriteAllBytes(extPath, nandFile.GetInodeData(file));
                    count++;
                }

                Console.WriteLine($"Extracted {count} files to {outputFile}");
            }

            if(extractKernel)
            {
                if (string.IsNullOrEmpty(outputFile))
                {
                    outputFile = filePath + ".sksa.bin";
                    Console.WriteLine("[!] No output path (-o) given, set path to:");
                    Console.WriteLine($"- {outputFile}");
                    Console.WriteLine();
                }
                if (extractAll) // if we just extracted the fs files we'll extract SKSA into the same folder
                    outputFile = Path.Combine(outputFile, "sksa.bin");

                File.WriteAllBytes(outputFile, nandFile.GetSKSAData());

                Console.WriteLine($"Extracted SKSA to {outputFile}");
            }

            if(!string.IsNullOrEmpty(generateSparePath))
            {
                File.WriteAllBytes(generateSparePath, nandFile.GenerateSpareData(!generateFullSpare));
                Console.WriteLine($"Wrote generated {(generateFullSpare ? "page" : "block")}-spare data to {generateSparePath}");
            }
        }

        // warning: stinky code
        static void ModeArrayFile<T>()
        {
            var type = typeof(T);

            Console.WriteLine($"Opening {type.Name} file {filePath}...");
            Console.WriteLine();

            var arrayFile = new iQueArrayFile<T>(filePath);

            for(int i = 0; i < arrayFile.Count; i++)
            {
                if (type == typeof(iQueTitleData))
                    arrayFile[i] = (T)(object)((iQueTitleData)(object)arrayFile[i]).EndianSwap();
                else if (type == typeof(iQueCertificate))
                    arrayFile[i] = (T)(object)((iQueCertificate)(object)arrayFile[i]).EndianSwap();
                else if (type == typeof(iQueCertificateRevocation))
                    arrayFile[i] = (T)(object)((iQueCertificateRevocation)(object)arrayFile[i]).EndianSwap();
            }

            var info = arrayFile.ToString(true);

            if (printInfo)
                Console.WriteLine(info);

            if (writeInfo)
            {
                File.WriteAllText(filePath + ".txt", info);
                Console.WriteLine($"Wrote {type.Name} info to {filePath}.txt");
            }
            
            if((!String.IsNullOrEmpty(extractContentIds) || !String.IsNullOrEmpty(extractTIDs)) && type != typeof(iQueTitleData))
            {
                Console.WriteLine("Warning: using -xc or -xt in wrong mode");
                Console.WriteLine("(those params are only valid for the \"ticket\" mode)");
                extractContentIds = String.Empty;
                extractTIDs = String.Empty;
            }

            if (extractAll || !String.IsNullOrEmpty(extractIDs) || !String.IsNullOrEmpty(extractContentIds) || !String.IsNullOrEmpty(extractTIDs))
            {
                if (String.IsNullOrEmpty(outputFile))
                {
                    outputFile = filePath + "_ext";
                    if (writeNewTicketFile)
                        outputFile += ".dat";
                    Console.WriteLine("[!] No output path (-o) given, set path to:");
                    Console.WriteLine($"- {outputFile}");
                    Console.WriteLine();
                }

                var extractEntries = new List<T>();
                if (extractAll)
                    extractEntries = arrayFile;
                else
                {
                    if (!String.IsNullOrEmpty(extractContentIds) && type == typeof(iQueTitleData))
                    {
                        var ids = extractContentIds.Split(',');
                        foreach (var id in ids)
                        {
                            uint intID = 0xFFFFFFFF;
                            if (!uint.TryParse(id, out intID))
                            {
                                Console.WriteLine($"extractContentIds: couldn't parse content ID {id}!");
                                continue;
                            }

                            var tickets = arrayFile.FindAll(t => ((iQueTitleData)(object)t).Ticket.ContentId == intID);
                            if (tickets.Count <= 0)
                            {
                                Console.WriteLine($"extractContentIds: failed to find ticket with content ID {id}!");
                                continue;
                            }

                            if (!extractEntries.Contains(tickets[0]))
                                extractEntries.Add(tickets[0]);
                        }
                    }

                    if (!String.IsNullOrEmpty(extractTIDs) && type == typeof(iQueTitleData))
                    {
                        var ids = extractTIDs.Split(',');
                        foreach (var id in ids)
                        {
                            ushort intID = 0xFFFF;
                            if (!ushort.TryParse(id, out intID))
                            {
                                Console.WriteLine($"extractTIDs: couldn't parse TID {id}!");
                                continue;
                            }

                            var tickets = arrayFile.FindAll(t => ((iQueTitleData)(object)t).TitleId == intID);
                            if (tickets.Count <= 0)
                            {
                                Console.WriteLine($"extractTIDs: failed to find ticket with TID {id}!");
                                continue;
                            }

                            if (!extractEntries.Contains(tickets[0]))
                                extractEntries.Add(tickets[0]);
                        }
                    }

                    if (!String.IsNullOrEmpty(extractIDs))
                    {
                        var ids = extractIDs.Split(',');
                        foreach (var id in ids)
                        {
                            int intID = -1;
                            if (!int.TryParse(id, out intID))
                            {
                                Console.WriteLine($"extractIDs: couldn't parse ID {id}!");
                                continue;
                            }

                            if (intID >= arrayFile.Count || intID < 0)
                            {
                                Console.WriteLine($"extractIDs: couldn't find ID {id} as its out of bounds!");
                                continue;
                            }

                            if (!extractEntries.Contains(arrayFile[intID]))
                                extractEntries.Add(arrayFile[intID]);
                        }
                    }
                }

                if (extractEntries.Count <= 0)
                {
                    Console.WriteLine("Extract failed: none of the specified entries were found?");
                    return;
                }

                Console.WriteLine($"Extracting entries to {outputFile}");
                if (!writeNewTicketFile)
                {
                    Console.WriteLine($"^ as seperate raw files!");
                    if (!Directory.Exists(outputFile))
                        Directory.CreateDirectory(outputFile);

                    for(int i = 0; i < extractEntries.Count; i++)
                    {
                        var entry = extractEntries[i];
                        var entryType = "unk";
                        var name = i.ToString();
                        byte[] data = null;
                        if (type == typeof(iQueTitleData))
                        {
                            entryType = "ticket";
                            name = ((iQueTitleData)(object)entry).TicketUID;
                            data = ((iQueTitleData)(object)entry).GetBytes();
                        }
                        else if (type == typeof(iQueCertificate))
                        {
                            entryType = "cert";
                            name = ((iQueCertificate)(object)entry).CertNameString;
                            data = ((iQueCertificate)(object)entry).GetBytes();
                        }
                        else if (type == typeof(iQueCertificateRevocation))
                        {
                            entryType = "crl";
                            name = ((iQueCertificateRevocation)(object)entry).CertNameString;
                            data = ((iQueCertificateRevocation)(object)entry).GetBytes();
                        }

                        var outputPath = $"{entryType}-{name}.dat";
                        outputPath = Path.Combine(outputFile, outputPath);
                        if (File.Exists(outputPath))
                            File.Delete(outputPath);

                        Console.WriteLine($"Writing entry to {outputPath}");
                        File.WriteAllBytes(outputPath, data);
                    }
                }
                else
                {
                    Console.WriteLine($"^ into a single new array file!");
                    using (var outputIO = new IO(outputFile, FileMode.Create))
                    {
                        outputIO.Writer.Write(((uint)extractEntries.Count).EndianSwap());
                        for (int i = 0; i < extractEntries.Count; i++)
                        {
                            var entry = extractEntries[i];
                            var name = i.ToString();
                            byte[] data = null;
                            if (type == typeof(iQueTitleData))
                            {
                                name = ((iQueTitleData)(object)entry).TicketUID;
                                data = ((iQueTitleData)(object)entry).GetBytes();
                            }
                            else if (type == typeof(iQueCertificate))
                            {
                                name = ((iQueCertificate)(object)entry).CertNameString;
                                data = ((iQueCertificate)(object)entry).GetBytes();
                            }
                            else if (type == typeof(iQueCertificateRevocation))
                            {
                                name = ((iQueCertificateRevocation)(object)entry).CertNameString;
                                data = ((iQueCertificateRevocation)(object)entry).GetBytes();
                            }
                            Console.WriteLine($"Writing entry {name}");
                            outputIO.Writer.Write(data);
                        }
                    }
                }
                Console.WriteLine($"Extraction complete, wrote {extractEntries.Count} entries!");
            }

            Console.WriteLine();
        }
    }
}
