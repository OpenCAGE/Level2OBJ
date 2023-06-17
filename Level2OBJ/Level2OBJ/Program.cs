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
            string level = "G:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\DATA\\ENV\\PRODUCTION\\FRONTEND";

            commands = new Commands(level + "/WORLD/COMMANDS.PAK");
            reds = new RenderableElements(level + "/WORLD/REDS.BIN");
            models = new Models(level + "/RENDERABLE/LEVEL_MODELS.PAK");

            ParseComposite(commands.EntryPoints[0], Matrix4x4.Identity);
        }

        static void ParseComposite(Composite composite, Matrix4x4 stackedTransform)
        {
            if (composite == null) return;

            foreach (OverrideEntity ovrride in composite.overrides)
            {
                //Store all overrides here so that we respect them as we continue down the hierarchy
            }
            foreach (FunctionEntity function in composite.functions)
            {
                if (CommandsUtils.GetFunctionType(function.function) == FunctionType.ModelReference)
                {
                    //Store model info
                    Matrix4x4 transform = Matrix4x4.Add(stackedTransform, GetMatrix(function)); //need to respect overrides here
                    foreach (ResourceReference resource in function.resources)
                    {
                        if (resource.entryType != ResourceType.RENDERABLE_INSTANCE) continue;

                    }
                }
                else if (!CommandsUtils.FunctionTypeExists(function.function))
                {
                    //Continue down the hierarchy
                    Matrix4x4 transform = Matrix4x4.Add(stackedTransform, GetMatrix(function)); //need to respect overrides here
                    ParseComposite(commands.GetComposite(function.function), transform);
                }
            }
        }

        static Matrix4x4 GetMatrix(Entity entity)
        {
            Parameter positionParam = entity.GetParameter("position");
            switch (positionParam.content.dataType)
            {
                case DataType.TRANSFORM:
                    cTransform transformParam = (cTransform)positionParam.content;
                    Matrix4x4 position = Matrix4x4.CreateTranslation(transformParam.position);
                    Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(transformParam.rotation.X, transformParam.rotation.Y, transformParam.rotation.Z); //todo: this may be flipped
                    return Matrix4x4.Add(position, rotation);
            }
            return Matrix4x4.Identity;
        }
    }
}
