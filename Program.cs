using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Toolbox.Core;
using Toolbox.Core.Collada;
using GCNLibrary.LM.MDL;
using Toolbox.Core.IO;

namespace ModelConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("");
                Console.WriteLine("Tool made by KillzXGaming.");
                Console.WriteLine("Mdl research thanks to opeyx and SpaceCats.");
                Console.WriteLine("");
                Console.WriteLine("Arguments:");
                Console.WriteLine("");
                Console.WriteLine("Convert .mdl to .dae/.png/.json formats:");
                Console.WriteLine("  ModelConverter.exe (target .mdl file)");
                Console.WriteLine("");
                Console.WriteLine("Create a new .mdl file:");
                Console.WriteLine("  ModelConverter.exe (extracted .mdl folder or .dae)");
                return;
            }

            string folder = "";
            string target = "";

            //Input is a folder
            if (Directory.Exists(args[0]))
            {
                folder = args[0];

                var daeFiles = Directory
                        .EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                        .Where(s => Path.GetExtension(s).ToLowerInvariant() == ".dae").ToList();
                if (daeFiles.Count == 0)
                    throw new Exception("No .dae files found in folder!");

                target = daeFiles[0];
            }
            else
            {
                folder = Path.GetDirectoryName(args[0]);
                target = args[0];
            }

            var importedModel = (IModelFormat)STFileLoader.OpenFileFormat(target);
            if (importedModel is MDL)
                ExportModel((MDL)importedModel);
            else
            {
                STGenericScene scene = new STGenericScene();
                scene.Models.Add(importedModel.ToGeneric());

                var model = new MDL();
                model.FileInfo = new File_Info();
                model.FromGeneric(scene);

                var newmaterials = model.Header.Materials;
                for (int i = 0; i < newmaterials.Length; i++)
                {
                    if (File.Exists($"{folder}\\Material{i}.json")) {
                         string json = File.ReadAllText($"{folder}\\Material{i}.json");
                         model.ReplaceMaterial(json, i);
                    }
                }

                string name = Path.GetFileName(folder);

                Console.WriteLine("Saving file..");
                STFileSaver.SaveFileFormat(model, $"{name}.new.mdl");
            }
        }

        static void ExportModel(MDL model)
        {
            IFileFormat fileFormat = (IFileFormat)model;
            string daePath = fileFormat.FileInfo.FileName.Replace(".mdl", ".dae");
            string folder = Path.GetFileNameWithoutExtension(fileFormat.FileInfo.FileName);
            string folderDir = Path.Combine(Path.GetDirectoryName(fileFormat.FileInfo.FilePath), folder);

            if (!Directory.Exists($"{folder}"))
                Directory.CreateDirectory($"{folder}");

            var materials = model.Header.Materials;
            for (int i = 0; i < materials.Length; i++) {
                File.WriteAllText($"{folder}/Material{i}.json", model.ExportMaterial(i));
            }

            var settings = new DAE.ExportSettings();
            settings.ImageFolder = folder;
            DAE.Export(Path.Combine(folderDir, daePath), settings, model);
        }
    }
}
