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
                var importModel = importedModel.ToGeneric();
                importModel.OrderBones(importModel.Skeleton.Bones.OrderBy(x => GetBoneIndex(x.Name)).ToList());
                if (args.Length > 1)
                {
                    var mdl = (IModelFormat)STFileLoader.OpenFileFormat(args[1]);
                    importModel.Skeleton = mdl.ToGeneric().Skeleton;

                    //Recalculate the bone indices on the original skeleton
                    foreach (var mesh in importModel.Meshes)
                    {
                        for (int v = 0; v < mesh.Vertices.Count; v++)
                        {
                            for (int j = 0; j < mesh.Vertices[v].BoneNames.Count; j++)
                            {
                                var boneName = mesh.Vertices[v].BoneNames[j];
                                var boneIndex = importModel.Skeleton.Bones.FindIndex(x => x.Name == boneName);
                                mesh.Vertices[v].BoneIndices[j] = boneIndex;
                            }
                        }
                    }
                }

                STGenericScene scene = new STGenericScene();
                scene.Models.Add(importModel);

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

        static int GetBoneIndex(string name)
        {
            int index = 0;
            string value = name.Replace("Bone", string.Empty).Replace("Mesh", string.Empty);
            int.TryParse(value, out index);
            return index;
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
