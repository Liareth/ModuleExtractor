using System;
using System.IO;

namespace ModuleExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Expected three arguments - output_path path_to_module path_to_dialog_tlk_file.");
                return;
            }

            string outPath = args[0];
            string modulePath = args[1];
            string dialogPath = args[2];
            string scratchPath = Path.Combine(Path.GetTempPath(), "moduleextractorscratch");

            if (Directory.Exists(scratchPath))
            {
                Console.WriteLine("Clearing scratch path {0}.", scratchPath);
                Directory.Delete(scratchPath, true);
            }

            Directory.CreateDirectory(scratchPath);
            Directory.SetCurrentDirectory(scratchPath);

            int ret = Utility.RunShellCommand(string.Format("nwn_erf -xf {0}", modulePath));

            if (ret != 0)
            {
                Console.Error.WriteLine("Failed to extract the module. Error code: {0}", ret);
                return;
            }

            Module module = new Module(scratchPath);

            if (!module.SaveToFolder(outPath, scratchPath, dialogPath))
            {
                Console.Error.WriteLine("Failed to save the extracted module.");
                return;
            }
        }
    }
}
