using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModuleExtractor
{
    public enum ResourceType
    {
        // Paletteable types
        UTC, // Creature
        UTD, // Door
        UTE, // Encounter
        UTI, // Item
        UTM, // Store
        UTP, // Placeable
        UTS, // Sound
        UTT, // Trigger
        UTW, // Waypoint

        // Non paletteable types
        ARE, // Area
        GIC, // Some area header
        GIT, // Some area header
        DLG, // Dialogue
        FAC, // Factions
        ITP, // Palette
        JRL, // Journal
        NSS, // Script
        NCS, // Compiled script

        ERROR, // Invalid
    };

    public class Module
    {
        public Dictionary<ResourceType, List<string>> Resources { get; set; } = new Dictionary<ResourceType, List<string>>();

        public Module(string scratchPath)
        {
            foreach (string file in Directory.GetFiles(scratchPath))
            {
                string extension = Path.GetExtension(file);
                extension = extension.Substring(1, extension.Length - 1); // Remove dot

                if (Enum.TryParse(extension.ToUpper(), out ResourceType type))
                {
                    if (Resources.ContainsKey(type))
                    {
                        Resources[type].Add(file);
                    }
                    else
                    {
                        List<string> data = new List<string>();
                        data.Add(file);
                        Resources.Add(type, data);
                    }
                }
                else
                {
                    Console.WriteLine("Skipping resource {0}", file);
                }
            }
        }

        public bool SaveToFolder(string folder, string scratchPath, string dialogDotTlkPath)
        {
            if (!CreatePalette(folder, scratchPath, dialogDotTlkPath))
            {
                Console.Error.WriteLine("Failed to extract the palette.");
                return false;
            }

            if (!CreateOtherFiles(folder, scratchPath))
            {
                Console.Error.WriteLine("Failed to extract the other files.");
                return false;
            }

            return true;
        }

        private static Dictionary<int, string> LoadDialogDotTlkFile(string scratchPath, string dialogDotTlkPath)
        {
            Dictionary<int, string> dialogDotTlkMap = new Dictionary<int, string>();

            string tempJsonPath = Path.Combine(scratchPath, "dialogDotTlk.json");
        
            if (Utility.RunShellCommand(string.Format("nwn_tlk -i {0} -o {1}", dialogDotTlkPath, tempJsonPath)) == 0)
            {
                using (var streamReader = new StreamReader(tempJsonPath))
                {
                    JsonTextReader reader = new JsonTextReader(streamReader);
                    JObject jObject = JObject.Load(reader);

                    foreach (JObject entry in jObject["entries"])
                    {
                        int id = entry["id"].Value<int>();
                        string text = entry["text"].Value<string>();
                        dialogDotTlkMap.Add(id, text);
                    }
                }
            }

            return dialogDotTlkMap;
        }

        private static List<string> CreatePalette_r(Dictionary<int, string> tlkTable,
            ResourceType type, JObject jToken,
            string name, string cumulativeName,
            string outPath, string scratchPath)
        {
            List<string> palette = new List<string>();
            JObject jResref = (JObject)jToken["RESREF"];

            if (jResref != null)
            {
                // This means we're processing entries in the palette here.


                if (jResref != null)
                {
                    string resref = jResref["value"].Value<string>();
                    string pathToResource = Path.Combine(scratchPath, resref + "." + type.ToString().ToLower());

                    if (File.Exists(pathToResource))
                    {
                        string src = pathToResource;
                        string dst = Path.Combine(Path.Combine(outPath, cumulativeName), Path.GetFileName(pathToResource));

                        if (File.Exists(dst))
                        {
                            Console.Error.WriteLine("Resource {0} already exists, replacing it.", dst);
                            File.Delete(dst);
                        }

                        File.Copy(src, dst);
                    }
                    else
                    {
                        Console.Error.WriteLine("Failed to find file {0} when constructing the palette for type {1}.", pathToResource, type);
                    }
                }

                return palette;
            }

            // We're processing the raw palette info here.
            JObject jId = (JObject)jToken["ID"];
            JObject jStrref = (JObject)jToken["STRREF"];

            int strref = jStrref["value"].Value<int>();
            string tlkTableEntry = tlkTable[strref];

            if (name.Length != 0)
            {
                cumulativeName += "/";
            }

            cumulativeName += tlkTableEntry;
            name = tlkTableEntry;

            Directory.CreateDirectory(Path.Combine(outPath, cumulativeName));

            string formattedLine = strref.ToString();

            if (jId != null)
            {
                int id = jId["value"].Value<int>();
                formattedLine += "." + id.ToString();
            }

            formattedLine += " " + cumulativeName;

            palette.Add(formattedLine);

            JToken jListToken = jToken["LIST"];
            if (jListToken != null)
            {
                foreach (JObject jListElement in jListToken["value"])
                {
                    palette.AddRange(CreatePalette_r(tlkTable, type, jListElement, name, cumulativeName, outPath, scratchPath));
                }
            }

            return palette;
        }

        private static ResourceType ItpFileToResType(string file)
        {
            ResourceType type;

            if (file.EndsWith("creaturepalcus.itp"))
            {
                type = ResourceType.UTC;
            }
            else if (file.EndsWith("doorpalcus.itp"))
            {
                type = ResourceType.UTD;
            }
            else if (file.EndsWith("encounterpalcus.itp"))
            {
                type = ResourceType.UTE;
            }
            else if (file.EndsWith("itempalcus.itp"))
            {
                type = ResourceType.UTI;
            }
            else if (file.EndsWith("placeablepalcus.itp"))
            {
                type = ResourceType.UTP;
            }
            else if (file.EndsWith("soundpalcus.itp"))
            {
                type = ResourceType.UTS;
            }
            else if (file.EndsWith("storepalcus.itp"))
            {
                type = ResourceType.UTM;
            }
            else if (file.EndsWith("triggerpalcus.itp"))
            {
                type = ResourceType.UTT;
            }
            else if (file.EndsWith("waypointpalcus.itp"))
            {
                type = ResourceType.UTW;
            }
            else
            {
                Console.Error.WriteLine("Failed to determine resource type for {0]", file);
                return ResourceType.ERROR;
            }

            return type;
        }

        string ResourceTypeToString(ResourceType type)
        {
            string name;

            switch (type)
            {
                case ResourceType.UTC: name = "creature"; break;
                case ResourceType.UTD: name = "door"; break;
                case ResourceType.UTE: name = "encounter"; break;
                case ResourceType.UTI: name = "item"; break;
                case ResourceType.UTM: name = "store"; break;
                case ResourceType.UTP: name = "placeable"; break;
                case ResourceType.UTS: name = "sound"; break;
                case ResourceType.UTT: name = "trigger"; break;
                case ResourceType.UTW: name = "waypoint"; break;
                default: name = "bad_type"; break;
            }

            return name;
        }
        
        private bool CreatePalette(string folder, string scratchPath, string dialogDotTlkPath)
        {
            Dictionary<int, string> dialogDotTlkMap = LoadDialogDotTlkFile(scratchPath, dialogDotTlkPath);

            List<string> paletteFiles;
            if (!Resources.TryGetValue(ResourceType.ITP, out paletteFiles))
            {
                return false;
            }

            foreach (string file in paletteFiles)
            {
                string fullPathToItp = file;
                string fulLPathToItpJson = Path.ChangeExtension(fullPathToItp, "json");

                if (Utility.RunShellCommand(string.Format("nwn_gff -i {0} -o {1}", fullPathToItp, fulLPathToItpJson)) != 0)
                {
                    return false;
                }

                using (var streamReader = new StreamReader(fulLPathToItpJson))
                {
                    JsonTextReader reader = new JsonTextReader(streamReader);
                    JObject jObject = JObject.Load(reader);

                    ResourceType type = ItpFileToResType(file);
                    string name = ResourceTypeToString(type);

                    List<string> paletteContents = new List<string>();

                    string paletteOutputPath = Path.Combine(folder, name);
                    Directory.CreateDirectory(paletteOutputPath);

                    foreach (JObject jEntry in jObject["MAIN"]["value"])
                    {
                        paletteContents.AddRange(CreatePalette_r(dialogDotTlkMap, type, jEntry, "", "", paletteOutputPath, scratchPath));
                    }

                    File.WriteAllLines(Path.Combine(paletteOutputPath, "palette.txt"), paletteContents);
                }
            }

            return true;
        }

        private bool CreateOtherFiles(string folder, string scratchPath)
        {
            // Folder -> list of files.
            Dictionary<string, List<string>> filesToCopy = new Dictionary<string, List<string>>();

            // Areas take ARE, GIC, and GIT. This is safe to do because every module must have one or more areas.
            string areasFolder = Path.Combine(folder, "areas");
            Directory.CreateDirectory(areasFolder);
            filesToCopy[areasFolder] = Resources[ResourceType.ARE].Concat(Resources[ResourceType.GIC]).Concat(Resources[ResourceType.GIT]).ToList();

            string dialogueFolder = Path.Combine(folder, "dlg");
            Directory.CreateDirectory(dialogueFolder);

            // Dialogues are DLG.

            List<string> dlg;
            if (Resources.TryGetValue(ResourceType.DLG, out dlg))
            {
                filesToCopy[dialogueFolder] = dlg;
            }

            // Scripts are either NSS or NCS.

            List<string> scripts = new List<string>();

            List<string> ncs;
            if (Resources.TryGetValue(ResourceType.NCS, out ncs))
            {
                scripts = scripts.Concat(ncs).ToList();
            }

            List<string> nss;
            if (Resources.TryGetValue(ResourceType.NSS, out nss))
            {
                scripts = scripts.Concat(nss).ToList();
            }

            string scriptsFolder = Path.Combine(folder, "scripts");
            Directory.CreateDirectory(scriptsFolder);
            filesToCopy[scriptsFolder] = scripts;

            // Copy all the files ...
            foreach (KeyValuePair<string, List<string>> kvp in filesToCopy)
            {
                foreach (string file in kvp.Value)
                {
                    string folderPath = Path.Combine(folder, kvp.Key);
                    File.Copy(file, Path.Combine(folderPath, Path.GetFileName(file)));
                }
            }

            // Every module must have a faction file, FAC.
            File.Copy(Resources[ResourceType.FAC][0], Path.Combine(folder, "repute.fac"));

            // Journal file is optional, JRL.
            List<string> jrl;
            if (Resources.TryGetValue(ResourceType.JRL, out jrl))
            {
                File.Copy(jrl[0], Path.Combine(folder, "module.jrl"));
            }

            return true;
        }
    }
}
