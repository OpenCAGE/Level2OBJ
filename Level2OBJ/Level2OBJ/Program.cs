using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Numerics;

namespace Level2OBJ
{
    static class Program
    {
        static Commands commands;
        static RenderableElements reds;
        static Models models;

        [STAThread]
        static void Main(string[] args)
        {
            string level = "G:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\DATA\\ENV\\PRODUCTION\\bsp_torrens";

            commands = new Commands(level + "/WORLD/COMMANDS.PAK");
            reds = new RenderableElements(level + "/WORLD/REDS.BIN");
            models = new Models(level + "/RENDERABLE/LEVEL_MODELS.PAK");

            ParseComposite(commands.EntryPoints[0], Matrix4x4.Identity);

            Console.WriteLine("Done!");
            Console.ReadLine();
        }

        static void ParseComposite(Composite composite, Matrix4x4 stackedTransform)
        {
            if (composite == null) return;
            Console.WriteLine("Parsing: " + composite.name);

            foreach (OverrideEntity ovrride in composite.overrides)
            {
                //Store all overrides here so that we respect them as we continue down the hierarchy
            }
            foreach (FunctionEntity function in composite.functions)
            {
                if (!CommandsUtils.FunctionTypeExists(function.function))
                {
                    //Continue down the hierarchy
                    Matrix4x4 transform = Matrix4x4.Add(stackedTransform, GetMatrix(function)); //need to respect overrides here
                    ParseComposite(commands.GetComposite(function.function), transform);
                }
                else if (CommandsUtils.GetFunctionType(function.function) == FunctionType.ModelReference)
                {
                    //Store model info
                    Matrix4x4 transform = Matrix4x4.Add(stackedTransform, GetMatrix(function)); //need to respect overrides here
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
                                        Models.CS2.Component.LOD.Submesh mesh = models.GetAtWriteIndex(renderable.ModelIndex);

                                        Console.WriteLine("\tFound model: " + models.FindModelForSubmesh(mesh)?.Name + " (" + transform.ToString() + ")");
                                        break;
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
                        Matrix4x4 position = Matrix4x4.CreateTranslation(transform.position);
                        Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(transform.rotation.X, transform.rotation.Y, transform.rotation.Z); //todo: this may be flipped
                        return Matrix4x4.Add(position, rotation);
                }
            }
            return Matrix4x4.Identity;
        }
    }
}
