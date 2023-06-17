using Assimp;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.IO;
using static CATHODE.Models;

namespace Level2OBJ
{
    static class Program
    {
        static Commands commands;
        static RenderableElements reds;
        static Models models;

        static Scene scene;

        [STAThread]
        static void Main(string[] args)
        {
            string level = "G:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\DATA\\ENV\\PRODUCTION\\bsp_torrens";

            commands = new Commands(level + "/WORLD/COMMANDS.PAK");
            reds = new RenderableElements(level + "/WORLD/REDS.BIN");
            models = new Models(level + "/RENDERABLE/LEVEL_MODELS.PAK");

            scene = new Scene();
            scene.Materials.Add(new Assimp.Material());
            scene.RootNode = new Node(level);

            ParseComposite(commands.EntryPoints[0], scene.RootNode);

            AssimpContext exp = new AssimpContext();
            exp.ExportFile(scene, "out.obj", "obj");
            exp.Dispose();

            Console.WriteLine("Done!");
            Console.ReadLine();
        }

        static void ParseComposite(Composite composite, Node node)
        {
            if (composite == null) return;

            foreach (OverrideEntity ovrride in composite.overrides)
            {
                //Store all overrides here so that we respect them as we continue down the hierarchy
            }

            foreach (FunctionEntity function in composite.functions)
            {
                if (!CommandsUtils.FunctionTypeExists(function.function))
                {
                    Composite compositeNext = commands.GetComposite(function.function);
                    if (compositeNext != null)
                    {
                        Node nodeNext = new Node(compositeNext.name);
                        nodeNext.Transform = GetMatrix(function); //need to respect overrides here
                        node.Children.Add(nodeNext);
                        ParseComposite(compositeNext, nodeNext);
                    }
                }
                else if (CommandsUtils.GetFunctionType(function.function) == FunctionType.ModelReference)
                {
                    Node nodeModel = new Node();
                    nodeModel.Transform = GetMatrix(function); //need to respect overrides here
                    node.Children.Add(nodeModel);

                    Parameter resourceParam = function.GetParameter("resource");
                    if (resourceParam != null && resourceParam.content != null)
                    {
                        switch (resourceParam.content.dataType)
                        {
                            case DataType.RESOURCE:
                                cResource resource = (cResource)resourceParam.content;
                                foreach (ResourceReference resourceRef in resource.value)
                                {
                                    if (resourceRef.entryType != ResourceType.RENDERABLE_INSTANCE) continue;
                                    for (int i = 0; i < resourceRef.count; i++)
                                    {
                                        RenderableElements.Element renderable = reds.Entries[resourceRef.startIndex + i];
                                        Models.CS2.Component.LOD.Submesh submesh = models.GetAtWriteIndex(renderable.ModelIndex);

                                        Mesh mesh = submesh.ToMesh();
                                        scene.Meshes.Add(mesh);
                                        nodeModel.MeshIndices.Add(scene.Meshes.Count - 1);
                                    }
                                }
                                break;
                        }
                    }
                }
            }
        }

        static Matrix4x4 GetMatrix(Entity entity)
        {
            Parameter positionParam = entity.GetParameter("position");
            if (positionParam != null && positionParam.content != null)
            {
                switch (positionParam.content.dataType)
                {
                    case DataType.TRANSFORM:
                        cTransform transform = (cTransform)positionParam.content;
                        Matrix4x4 position = Matrix4x4.FromTranslation(new Vector3D(transform.position.X, transform.position.Y, transform.position.Z));
                        Matrix4x4 rotation = Matrix4x4.FromEulerAnglesXYZ(new Vector3D(transform.rotation.X, transform.rotation.Y, transform.rotation.Z));
                        Matrix4x4 scale = Matrix4x4.FromScaling(new Vector3D(1, 1, 1));
                        return position * rotation * scale;
                }
            }
            return Matrix4x4.Identity;
        }
    }
}
