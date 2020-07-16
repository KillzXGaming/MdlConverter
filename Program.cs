using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Toolbox.Core;
using Toolbox.Core.Collada;
using GCNLibrary.LM.MDL;
using Toolbox.Core.IO;
using System.Runtime.Remoting.Messaging;

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
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ModelConverter.exe (target .mdl file)");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("");
                Console.WriteLine("Create a new .mdl file:");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ModelConverter.exe (extracted .mdl folder or .dae) (originalFile.mdl)");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Optional Arguments:");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("-skel (Uses custom skeleton when creating new mdl)");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }

            string folder = "";
            string target = "";
            string originalMdlTarget = "";

            string appFolder = AppDomain.CurrentDomain.BaseDirectory;


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

            if (Utils.GetExtension(target) == ".dae")
            {
                if (args.Length > 1)
                    originalMdlTarget = args[1];
                else if (File.Exists($"{folder}.mdl"))
                    originalMdlTarget = $"{folder}.mdl";
                else if (File.Exists(target.Replace(".dae", ".mdl")))
                    originalMdlTarget = target.Replace(".dae", ".mdl");
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error! Make sure you input the original .mdl file.");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("");
                    Console.WriteLine("  ModelConverter.exe (extracted .mdl folder or .dae) (originalFile.mdl)");
                    Console.ForegroundColor = ConsoleColor.White;
                    return;
                }
            }

            var importedModel = (IModelFormat)STFileLoader.OpenFileFormat(target);
            if (importedModel is MDL)
                ExportModel((MDL)importedModel);
            else
            {
                var importModel = importedModel.ToGeneric();
                importModel.OrderBones(importModel.Skeleton.Bones.OrderBy(x => GetBoneIndex(x.Name)).ToList());
                //Load any extra texture maps not referenced by the model itself for texture animations
                foreach (var file in Directory.GetFiles(Path.GetDirectoryName(target)))
                {
                    if (Utils.GetExtension(file) == ".png") {
                        string texname = Path.GetFileNameWithoutExtension(file);
                        if (!importModel.Textures.Any(x => x.Name == texname))
                            importModel.Textures.Add(new GenericBitmapTexture(file));
                    }
                }

                if (originalMdlTarget != string.Empty && !args.Contains("-skel"))
                {
                    var mdl = (IModelFormat)STFileLoader.OpenFileFormat(originalMdlTarget);
                    importModel.Skeleton = mdl.ToGeneric().Skeleton;

                    //Recalculate the bone indices on the original skeleton
                    foreach (var mesh in importModel.Meshes)
                    {
                        for (int v = 0; v < mesh.Vertices.Count; v++) {
                            for (int j = 0; j < mesh.Vertices[v].BoneNames.Count; j++) {
                                var boneName = mesh.Vertices[v].BoneNames[j];
                                var boneIndex = importModel.Skeleton.Bones.FindIndex(x => x.Name == boneName);
                                mesh.Vertices[v].BoneIndices[j] = boneIndex;
                            }
                        }
                    }
                }

                var model = new MDL();
                model.FileInfo = new File_Info();
                model.Header = new MDL_Parser();

                if (File.Exists($"{folder}\\SamplerList.json"))
                {
                    string json = File.ReadAllText($"{folder}\\SamplerList.json");
                    model.ReplaceSamplers(json);
                }

                STGenericScene scene = new STGenericScene();
                scene.Models.Add(importModel);
                model.FromGeneric(scene);

                //Reset the indices and assign by json file
                foreach (var node in model.Header.Nodes)
                {
                    node.ShapeCount = 0;
                    node.ShapeIndex = 0;
                }

                Node[] nodeList = new Node[model.Header.Nodes.Length];
                for (int i = 0; i < nodeList.Length; i++)
                    nodeList[i] = new Node();

   
                var drawElements = model.Header.DrawElements;
                for (int i = 0; i < importModel.Meshes.Count; i++)
                {
                    ushort nodeIndex = 0;

                    //Check both the default and the imported mesh names. and inject data to these slots
                    if (File.Exists($"{folder}\\{importModel.Meshes[i].Name}.json"))
                    {
                        string json = File.ReadAllText($"{folder}\\{importModel.Meshes[i].Name}.json");
                        nodeIndex = model.ReplaceMaterial(json, drawElements[i]);
                    }
                    else if (File.Exists($"{appFolder}\\Defaults.json"))
                    {
                        string json = File.ReadAllText($"{appFolder}\\Defaults.json");
                        nodeIndex = model.ReplaceMaterial(json, drawElements[i]);
                    }

                    //Here we add our draw elements to the assigned node they use from the json files
                    if (nodeIndex < model.Header.Nodes.Length) {
                        nodeList[nodeIndex].DrawElements.Add(drawElements[i]);
                    }
                    else
                        nodeList[0].DrawElements.Add(drawElements[i]);
                }

                //Create a new draw list in proper order
                List<DrawElement> sortedElements = new List<DrawElement>();
                for (int i = 0; i < nodeList.Length; i++)
                    sortedElements.AddRange(nodeList[i].DrawElements);

                model.Header.DrawElements = sortedElements.ToArray();

                //Set the referenced draw elements/shapes used for the node lists
                ushort index = 0;
                for (int i = 0; i < nodeList.Length; i++) {
                    model.Header.Nodes[i].ShapeIndex = index;
                    model.Header.Nodes[i].ShapeCount = (ushort)nodeList[i].DrawElements.Count;

                    index += (ushort)nodeList[i].DrawElements.Count; 
                }

                Console.WriteLine("Saving file..");

                string name = Path.GetFileName(folder);
                STFileSaver.SaveFileFormat(model, $"{name}.new.mdl");

                Console.WriteLine($"Saved model " + $"{name}.new.mdl!");
            }
        }

        class Node
        {
            public List<DrawElement> DrawElements = new List<DrawElement>();
        }

        class Draw
        {
            public int Index;
            public DrawElement element;
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
            var drawElements = model.Header.DrawElements;
            //Export by draw element to export shape flags
            for (int i = 0; i < drawElements.Length; i++) {
                File.WriteAllText($"{folder}/Mesh{i}.json",
                    model.ExportMaterial(drawElements[i]));
            }

            File.WriteAllText($"{folder}/SamplerList.json", model.ExportSamplers());

            var settings = new DAE.ExportSettings();
            settings.ImageFolder = folder;
            DAE.Export(Path.Combine(folderDir, daePath), settings, model);

            Console.WriteLine($"Exported model {model.FileInfo.FileName}!");
        }
    }
}
