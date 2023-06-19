using Assimp;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static CATHODE.Models;

namespace Level2OBJ
{
    static class Program
    {
        static Commands commands;
        static RenderableElements reds;
        static Scene scene;

        const float PI = 3.14159274f;

        [STAThread]
        static void Main(string[] args)
        {
            string level = "G:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\DATA\\ENV\\PRODUCTION\\bsp_torrens";

            Console.WriteLine("Loading WORLD");
            commands = new Commands(level + "/WORLD/COMMANDS.PAK");
            reds = new RenderableElements(level + "/WORLD/REDS.BIN");

            Console.WriteLine("Loading RENDERABLE");
            {
                Models models = new Models(level + "/RENDERABLE/LEVEL_MODELS.PAK");

                scene = new Scene();
                scene.Materials.Add(new Assimp.Material());
                scene.RootNode = new Node(level);

                //Add all models from the level to the scene
                int maxIndex = 0;
                foreach (RenderableElements.Element element in reds.Entries)
                    if (element.ModelIndex > maxIndex) maxIndex = element.ModelIndex;
                for (int i = 0; i < maxIndex; i++)
                    scene.Meshes.Add(models?.GetAtWriteIndex(i)?.ToMesh());
            }

            Console.WriteLine("Parsing COMMANDS");
            ParseComposite(commands.EntryPoints[0], scene.RootNode, new List<OverrideEntity>());

            Console.WriteLine("Exporting");
            AssimpContext exp = new AssimpContext();
            exp.ExportFile(scene, "out.obj", "obj"); //TODO: this is super slow
            exp.Dispose();
        }

        static void ParseComposite(Composite composite, Node node, List<OverrideEntity> overrides)
        {
            if (composite == null) return;
            List<Entity> entities = composite.GetEntities();
            
            //Compile all appropriate overrides, and keep the hierarchies trimmed so that index zero is accurate to this composite
            List<OverrideEntity> trimmedOverrides = new List<OverrideEntity>();
            for (int i = 0; i < overrides.Count; i++)
            {
                overrides[i].connectedEntity.hierarchy.RemoveAt(0);
                if (overrides[i].connectedEntity.hierarchy.Count != 0)
                    trimmedOverrides.Add(overrides[i]);
            }
            trimmedOverrides.AddRange(composite.overrides);
            overrides = trimmedOverrides;

            //Parse all functions in this composite & handle them appropriately
            foreach (FunctionEntity function in composite.functions)
            {
                //Jump through to the next composite
                if (!CommandsUtils.FunctionTypeExists(function.function))
                {
                    Composite compositeNext = commands.GetComposite(function.function);
                    if (compositeNext != null)
                    {
                        //Find all overrides that are appropriate to take through to the next composite
                        List<OverrideEntity> overridesNext = trimmedOverrides.FindAll(o => o.connectedEntity.hierarchy[0] == function.shortGUID);

                        //Work out our position, accounting for overrides
                        OverrideEntity ovrride = trimmedOverrides.FirstOrDefault(o => o.connectedEntity.hierarchy.Count == 1 && o.connectedEntity.hierarchy[0] == function.shortGUID);
                        Matrix4x4 transform = GetEntityMatrix(ovrride);
                        if (transform == Matrix4x4.Identity) transform = GetEntityMatrix(function);

                        //Update scene & continue through to next composite
                        Node nodeNext = new Node(compositeNext.name);
                        nodeNext.Transform = transform;
                        node.Children.Add(nodeNext);
                        ParseComposite(compositeNext, nodeNext, overridesNext);
                    }
                }

                //Parse model data
                else if (CommandsUtils.GetFunctionType(function.function) == FunctionType.ModelReference)
                {
                    //TEMP: Instead of parsing all logic, lets just consider models that connect to other entities to be scripted.
                    if (function.childLinks.Count != 0) continue;
                    if (entities.FirstOrDefault(o => o.childLinks.FindAll(x => x.childID == function.shortGUID).Count != 0) != null) continue;

                    //Work out our position, accounting for overrides
                    OverrideEntity ovrride = trimmedOverrides.FirstOrDefault(o => o.connectedEntity.hierarchy.Count == 1 && o.connectedEntity.hierarchy[0] == function.shortGUID);
                    Matrix4x4 transform = GetEntityMatrix(ovrride);
                    if (transform == Matrix4x4.Identity) transform = GetEntityMatrix(function);

                    Node nodeModel = new Node();
                    nodeModel.Transform = transform;
                    node.Children.Add(nodeModel);

                    //TODO: do we want to consider resources attached to the entity too? (YES)
                    Parameter resourceParam = function.GetParameter("resource");
                    if (resourceParam != null && resourceParam.content != null)
                    {
                        switch (resourceParam.content.dataType)
                        {
                            case DataType.RESOURCE:
                                cResource resource = (cResource)resourceParam.content;

                                //TEMP: While we can't parse collision mappings, we rely on both bits of data to form a high-poly collision mesh.
                                if (resource.value.FirstOrDefault(o => o.entryType == ResourceType.COLLISION_MAPPING) == null ||
                                    resource.value.FirstOrDefault(o => o.entryType == ResourceType.RENDERABLE_INSTANCE) == null)
                                    continue;

                                foreach (ResourceReference resourceRef in resource.value)
                                {
                                    Node nodeModelPart = new Node();
                                    //nodeModelPart.Transform = ToMatrix(resourceRef.position, resourceRef.rotation);
                                    nodeModel.Children.Add(nodeModelPart);

                                    for (int i = 0; i < resourceRef.count; i++)
                                    {
                                        RenderableElements.Element renderable = reds.Entries[resourceRef.startIndex + i];

                                        switch (resourceRef.entryType)
                                        {
                                            case ResourceType.RENDERABLE_INSTANCE:
                                                nodeModelPart.MeshIndices.Add(renderable.ModelIndex);
                                                break;
                                            case ResourceType.COLLISION_MAPPING:
                                                //TODO: we should use this data rather than mesh data to reduce overhead
                                                break;
                                        }
                                    }
                                }
                                break;
                        }
                    }
                }
            }
        }

        static Matrix4x4 GetEntityMatrix(Entity entity)
        {
            if (entity == null) return Matrix4x4.Identity;

            Parameter positionParam = entity.GetParameter("position");
            if (positionParam != null && positionParam.content != null)
            {
                switch (positionParam.content.dataType)
                {
                    case DataType.TRANSFORM:
                        cTransform transform = (cTransform)positionParam.content;
                        return ToMatrix(transform.position, transform.rotation);
                }
            }

            return Matrix4x4.Identity;
        }

        static Matrix4x4 ToMatrix(System.Numerics.Vector3 position, System.Numerics.Vector3 rotation)
        {
            Matrix4x4 positionM = Matrix4x4.FromTranslation(new Vector3D(position.X, position.Y, position.Z));
            Matrix4x4 rotationM = Matrix4x4.FromEulerAnglesXYZ(PI * rotation.X / 180.0f * -1.0f, PI * rotation.Y / 180.0f * -1.0f, PI * rotation.Z / 180.0f * -1.0f);
            Matrix4x4 scaleM = Matrix4x4.FromScaling(new Vector3D(1, 1, 1));
            return scaleM * rotationM * positionM;
        }
    }
}
