using Assimp;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using static CATHODE.Models;

namespace Level2OBJ
{
    public static class CathodeLibExtensions
    {
        public static Mesh ToMesh(this CS2.Component.LOD.Submesh submesh)
        {
            Mesh mesh = new Mesh();
            mesh.MaterialIndex = 0; //todo

            List<int> indices = new List<int>();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector4> tangents = new List<Vector4>();
            List<Vector2> uv0 = new List<Vector2>();
            List<Vector2> uv1 = new List<Vector2>();
            List<Vector2> uv2 = new List<Vector2>();
            List<Vector2> uv3 = new List<Vector2>();
            List<Vector2> uv7 = new List<Vector2>();

            //TODO: implement skeleton lookup for the indexes
            List<Vector4> boneIndex = new List<Vector4>(); //The indexes of 4 bones that affect each vertex
            List<Vector4> boneWeight = new List<Vector4>(); //The weights for each bone

            if (submesh.content.Length == 0) return mesh;

            using (BinaryReader reader = new BinaryReader(new MemoryStream(submesh.content)))
            {
                for (int i = 0; i < submesh.VertexFormat.Elements.Count; ++i)
                {
                    if (i == submesh.VertexFormat.Elements.Count - 1)
                    {
                        for (int x = 0; x < submesh.IndexCount; x++)
                            indices.Add(reader.ReadUInt16());

                        continue;
                    }

                    for (int x = 0; x < submesh.VertexCount; ++x)
                    {
                        for (int y = 0; y < submesh.VertexFormat.Elements[i].Count; ++y)
                        {
                            AlienVBF.Element format = submesh.VertexFormat.Elements[i][y];
                            switch (format.VariableType)
                            {
                                case VBFE_InputType.VECTOR3:
                                    {
                                        Vector3 v = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                                        switch (format.ShaderSlot)
                                        {
                                            case VBFE_InputSlot.NORMAL:
                                                normals.Add(v);
                                                break;
                                            case VBFE_InputSlot.TANGENT:
                                                tangents.Add(new Vector4((float)v.X, (float)v.Y, (float)v.Z, 0));
                                                break;
                                            case VBFE_InputSlot.UV:
                                                //TODO: 3D UVW
                                                break;
                                        };
                                        break;
                                    }
                                case VBFE_InputType.INT32:
                                    {
                                        int v = reader.ReadInt32();
                                        switch (format.ShaderSlot)
                                        {
                                            case VBFE_InputSlot.COLOUR:
                                                //??
                                                break;
                                        }
                                        break;
                                    }
                                case VBFE_InputType.VECTOR4_BYTE:
                                    {
                                        Vector4 v = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                                        switch (format.ShaderSlot)
                                        {
                                            case VBFE_InputSlot.BONE_INDICES:
                                                boneIndex.Add(v);
                                                break;
                                        }
                                        break;
                                    }
                                case VBFE_InputType.VECTOR4_BYTE_DIV255:
                                    {
                                        Vector4 v = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                                        v /= 255.0f;
                                        switch (format.ShaderSlot)
                                        {
                                            case VBFE_InputSlot.BONE_WEIGHTS:
                                                boneWeight.Add(v / (v.X + v.Y + v.Z + v.W));
                                                break;
                                            case VBFE_InputSlot.UV:
                                                uv2.Add(new Vector2(v.X, v.Y));
                                                uv3.Add(new Vector2(v.Z, v.W));
                                                break;
                                        }
                                        break;
                                    }
                                case VBFE_InputType.VECTOR2_INT16_DIV2048:
                                    {
                                        Vector2 v = new Vector2(reader.ReadInt16() / 2048.0f, reader.ReadInt16() / 2048.0f);
                                        switch (format.ShaderSlot)
                                        {
                                            case VBFE_InputSlot.UV:
                                                if (format.VariantIndex == 0) uv0.Add(v);
                                                else if (format.VariantIndex == 1)
                                                {
                                                    // TODO: We can figure this out based on AlienVBFE.
                                                    //Material->Material.Flags |= Material_HasTexCoord1;
                                                    uv1.Add(v);
                                                }
                                                else if (format.VariantIndex == 2) uv2.Add(v);
                                                else if (format.VariantIndex == 3) uv3.Add(v);
                                                else if (format.VariantIndex == 7) uv7.Add(v);
                                                break;
                                        }
                                        break;
                                    }
                                case VBFE_InputType.VECTOR4_INT16_DIVMAX:
                                    {
                                        Vector4 v = new Vector4(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
                                        v /= (float)Int16.MaxValue;
                                        if (v.W != 0 && v.W != -1 && v.W != 1) throw new Exception("Unexpected vert W");
                                        v *= submesh.ScaleFactor; //Account for scale
                                        switch (format.ShaderSlot)
                                        {
                                            case VBFE_InputSlot.VERTEX:
                                                vertices.Add(new Vector3(v.X, v.Y, v.Z));
                                                break;
                                        }
                                        break;
                                    }
                                case VBFE_InputType.VECTOR4_BYTE_NORM:
                                    {
                                        Vector4 v = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                                        v /= (float)byte.MaxValue - 0.5f;
                                        v = Vector4.Normalize(v);
                                        switch (format.ShaderSlot)
                                        {
                                            case VBFE_InputSlot.NORMAL:
                                                normals.Add(new Vector3(v.X, v.Y, v.Z));
                                                break;
                                            case VBFE_InputSlot.TANGENT:
                                                break;
                                            case VBFE_InputSlot.BITANGENT:
                                                break;
                                        }
                                        break;
                                    }
                            }
                        }
                    }
                    Utilities.Align(reader, 16);
                }
            }

            if (vertices.Count == 0) return mesh;

            for (int i = 0; i < vertices.Count; i++)
                mesh.Vertices.Add(new Assimp.Vector3D((float)vertices[i].X, (float)vertices[i].Y, (float)vertices[i].Z));
            mesh.HasTextureCoords(0);
            for (int i = 0; i < uv0.Count; i++)
                mesh.TextureCoordinateChannels[0].Add(new Assimp.Vector3D((float)uv0[i].X, (float)uv0[i].Y, 0));
            for (int i = 0; i < normals.Count; i++)
                mesh.Normals.Add(new Assimp.Vector3D((float)normals[i].X, (float)normals[i].Y, (float)normals[i].Z));
            bool worked = mesh.SetIndices(indices.ToArray(), 3);
            if (!worked) throw new Exception("oops");
            return mesh;
        }
    }
}
