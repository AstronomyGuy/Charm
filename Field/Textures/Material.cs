using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Field.Entities;
using Field.General;
using Field.Utils;
using File = System.IO.File;

namespace Field;

public class Material : Tag
{
    public D2Class_AA6D8080 Header;
    public static object _lock = new object();

    
    public Material(TagHash hash) : base(hash)
    {
    }

    protected override void ParseStructs()
    {
        Header = ReadHeader<D2Class_AA6D8080>();
    }

    public void SaveAllTextures(string saveDirectory)
    {
        foreach (var e in Header.VSTextures)
        {
            if (e.Texture == null)
            {
                continue;
            }
            // todo change to 64 bit hash?
            string path = $"{saveDirectory}/VS_{e.TextureIndex}_{e.Texture.Hash}";
            if (!File.Exists(path))
            {
                e.Texture.SavetoFile(path); 
            }
        }
        foreach (var e in Header.PSTextures)
        {
            if (e.Texture == null)
            {
                continue;
            }
            // todo change to 64 bit hash?
            string path = $"{saveDirectory}/{e.Texture.Hash}";
            if (!File.Exists(path + ".dds") && !File.Exists(path + ".png") && !File.Exists(path + ".tga"))
            {
                e.Texture.SavetoFile(path); 
            }
        }
    }

    private byte[] GetBytecode(ShaderType type) {
        var path = GetTempPath(type);
        lock (Lock) {
            if (!File.Exists(path)) {
                var bytecode = type switch {
                    ShaderType.Pixel => Header.PixelShader.GetBytecode(),
                    ShaderType.Vertex => Header.VertexShader.GetBytecode(),
                    _ => Header.ComputeShader.GetBytecode()
                };
                Directory.GetParent(path)?.Create();
                File.WriteAllBytes(path, bytecode);
                return bytecode;
            }
            return File.ReadAllBytes(path);
        }
    }
    
    private string Disassemble(ShaderType type) {
        var fileName = GetTempPath(type, ".asm");
        if (!File.Exists(fileName)) {
            var data = DirectX.DisassembleDXBC(GetBytecode(type));
            File.WriteAllText(fileName, data);
            return data;
        }
        return File.ReadAllText(fileName);
    }
    
    // [DllImport("HLSLDecompiler.dll", EntryPoint = "DecompileHLSL", CallingConvention = CallingConvention.Cdecl)]
    // public static extern IntPtr DecompileHLSL(
    //     IntPtr pShaderBytecode,
    //     int BytecodeLength,
    //     out int pHlslTextLength
    // );

    public string Decompile(byte[] shaderBytecode, string? type = "ps")
    {
        // tried doing it via dll pinvoke but seemed to cause way too many problems so doing it via exe instead
        // string hlsl;
        // lock (_lock)
        // {
        //     GCHandle gcHandle = GCHandle.Alloc(shaderBytecode, GCHandleType.Pinned);
        //     IntPtr pShaderBytecode = gcHandle.AddrOfPinnedObject();
        //     IntPtr pHlslText = Marshal.AllocHGlobal(5000);
        //     int len;
        //     pHlslText = DecompileHLSL(pShaderBytecode, shaderBytecode.Length, out int pHlslTextLength);
        //     // len = Marshal.ReadInt32(pHlslTextLength);
        //     len = pHlslTextLength;
        //     hlsl = Marshal.PtrToStringUTF8(pHlslText);
        //     gcHandle.Free();
        // }
        // // Marshal.FreeHGlobal(pHlslText);
        // return hlsl;
    
        string directory = "hlsl_temp";
        string binPath = $"{directory}/{type}{Hash}.bin";
        string hlslPath = $"{directory}/{type}{Hash}.hlsl";

      

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory("hlsl_temp/");
        }

        lock (_lock)
        {
            if (!File.Exists(binPath))
            {
                File.WriteAllBytes(binPath, shaderBytecode);
            } 
        }

        if (!File.Exists(hlslPath))
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.FileName = "3dmigoto_shader_decomp.exe";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = $"-D {binPath}";
            using (Process exeProcess = Process.Start(startInfo))
            {
                exeProcess.WaitForExit();
            }

            if (!File.Exists(hlslPath))
            {
                throw new FileNotFoundException($"Decompilation failed for {Hash}");
            }
        }

        string hlsl = "";
        lock (_lock)
        {
            while (hlsl == "")
            {
                try  // needed for slow machines
                {
                    hlsl = File.ReadAllText(hlslPath);
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
            }
            return hlsl;
        }
    }
    
    public void SavePixelShader(string saveDirectory, bool isTerrain = false)
    {
        if (Header.PixelShader != null)
        {
            string hlsl = Decompile(Header.PixelShader.GetBytecode());
            string usf = new UsfConverter().HlslToUsf(this, hlsl, false);
            string vfx = new VfxConverter().HlslToVfx(this, hlsl, false, isTerrain);
            
            Directory.CreateDirectory($"{saveDirectory}/Source2");
            Directory.CreateDirectory($"{saveDirectory}/Source2/materials");
            StringBuilder vmat = new StringBuilder();
            if (usf != String.Empty || vfx != String.Empty)
            {
                try
                {
                    if(!File.Exists($"{saveDirectory}/PS_{Hash}.usf"))
                    {
                        File.WriteAllText($"{saveDirectory}/PS_{Hash}.usf", usf);
                    }
                    if (!File.Exists($"{saveDirectory}/Source2/PS_{Hash}.shader"))
                    {
                        File.WriteAllText($"{saveDirectory}/Source2/PS_{Hash}.shader", vfx);
                    }

                    Console.WriteLine($"Saved pixel shader {Hash}");
                }
                catch (IOException)  // threading error
                {
                }
            }
            
            vmat.AppendLine("Layer0 \n{");
            
            //If the shader doesnt exist, just use the default complex.shader
            if (!File.Exists($"{saveDirectory}/Source2/PS_{Hash}.shader"))
            {
                vmat.AppendLine($"  shader \"complex.shader\"");

                //Use just the first texture for the diffuse
                if (Header.PSTextures.Count > 0)
                {
                    vmat.AppendLine($"  TextureColor \"materials/Textures/{Header.PSTextures[0].Texture.Hash}.png\"");
                }
            }
            else
            {
                vmat.AppendLine($"  shader \"ps_{Hash}.shader\"");
                vmat.AppendLine("   F_ALPHA_TEST 1");
            }

            foreach (var e in Header.PSTextures)
            {
                if (e.Texture == null)
                {
                    continue;
                }
               
                vmat.AppendLine($"  TextureT{e.TextureIndex} \"materials/Textures/{e.Texture.Hash}.png\"");
            }

            // if(isTerrain)
            // {
            //     vmat.AppendLine($"  TextureT14 \"materials/Textures/{partEntry.Dyemap.Hash}.png\"");
            // }
            vmat.AppendLine("}");
            
            if(!File.Exists($"{saveDirectory}/Source2/materials/{Hash}.vmat"))
            {
                try
                {
                    File.WriteAllText($"{saveDirectory}/Source2/materials/{Hash}.vmat", vmat.ToString());
                }
                catch (IOException)  
                {
                }
            }
        }
    }
    
    public void SaveVertexShader(string saveDirectory)
    {
        Directory.CreateDirectory($"{saveDirectory}");
        if (Header.VertexShader != null && !File.Exists($"{saveDirectory}/VS_{Hash}.usf"))
        {
            string hlsl = Decompile(Header.VertexShader.GetBytecode(), "vs");
            string usf = new UsfConverter().HlslToUsf(this, hlsl, true);
            if (usf != String.Empty)
            {
                try
                {
                    File.WriteAllText($"{saveDirectory}/VS_{Hash}.usf", usf);
                    Console.WriteLine($"Saved vertex shader {Hash}");
                }
                catch (IOException)  // threading error
                {
                }
            }
        }
    }

    public void SaveComputeShader(string saveDirectory)
    {
        Directory.CreateDirectory($"{saveDirectory}");
        if (Header.ComputeShader != null && !File.Exists($"{saveDirectory}/CS_{Hash}.usf"))
        {
            string hlsl = Decompile(Header.ComputeShader.GetBytecode(), "cs");
            string usf = new UsfConverter().HlslToUsf(this, hlsl, false);
            if (usf != String.Empty)
            {
                try
                {
                    File.WriteAllText($"{saveDirectory}/CS_{Hash}.usf", usf);
                    Console.WriteLine($"Saved compute shader {Hash}");
                }
                catch (IOException)  // threading error
                {
                }
            }
        }
    }
    #region Texture Manifest

    private JsonObject CreateTextureManifest() {
        var root = new JsonObject();
        var textureMeta = new JsonObject();
        if(Header.PixelShader != null) {
            var i = CreateShaderIndices(Header.PSTextures);
            if(i.Count > 0)
                root["pixelShader"] = new JsonObject { ["indices"] = i };
            GetShaderTextureMeta(textureMeta, Header.PSTextures);
        }
        if(Header.VertexShader != null) {
            var i = CreateShaderIndices(Header.VSTextures);
            if(i.Count > 0)
                root["vertexShader"] = new JsonObject { ["indices"] = i };
            GetShaderTextureMeta(textureMeta, Header.VSTextures);
        }
        if(Header.ComputeShader != null) {
            var i = CreateShaderIndices(Header.CSTextures);
            if(i.Count > 0)
                root["computeShader"] = new JsonObject { ["indices"] = i };
            GetShaderTextureMeta(textureMeta, Header.CSTextures);
        }
        root["textures"] = textureMeta;
        root["format"] = GetTextureExtension(TextureExtractor.Format);
        return root;
    }
    
    private static void GetShaderTextureMeta(JsonObject table, List<D2Class_CF6D8080> textures) {
        foreach(var e in textures) {
            if(table.ContainsKey(e.Texture.Hash))
                continue;
            var meta = new JsonObject {
                ["srgb"] = e.Texture.IsSrgb(),
                ["volume"] = e.Texture.IsVolume(),
                ["cubemap"] = e.Texture.IsCubemap()
            };
            table[e.Texture.Hash] = meta;
        }
    }
    
    private static JsonObject CreateShaderIndices(List<D2Class_CF6D8080> textures) {
        var textureMeta = new JsonObject();
        foreach(var e in textures)
            textureMeta[e.TextureIndex.ToString()] = e.Texture.Hash.ToString();
        return textureMeta;
    }
    
    #endregion
    
    #region Platform-specific exports

    private void ExportMaterialRaw(string path) {
        if(Header.PixelShader != null) {
            try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Pixel)}_{Hash}.hlsl", Decompile(ShaderType.Pixel)); }
            catch (IOException) { }
            try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Pixel)}_{Hash}.asm", Disassemble(ShaderType.Pixel)); }
            catch (IOException) { }
            Console.WriteLine($"Exported raw PixelShader for Material {Hash}.");
        }
        if(Header.VertexShader != null) {
            try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Vertex)}_{Hash}.hlsl", Decompile(ShaderType.Vertex)); }
            catch (IOException) { }
            try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Vertex)}_{Hash}.asm", Disassemble(ShaderType.Vertex)); }
            catch (IOException) { }
            Console.WriteLine($"Exported raw VertexShader for Material {Hash}.");
        }
        if(Header.ComputeShader != null) {
            try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Compute)}_{Hash}.hlsl", Decompile(ShaderType.Compute)); }
            catch (IOException) { }
            try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Compute)}_{Hash}.asm", Disassemble(ShaderType.Compute)); }
            catch (IOException) { }
            Console.WriteLine($"Exported raw ComputeShader for Material {Hash}.");
        }
    }

    private void ExportMaterialBlender(string path) {
        // TODO: Merge Vertex and Pixel Shaders if applicable
        // TODO: Create a proper import script, assuming textures are bound and loaded
        if(Header.PixelShader != null) {
            var bpy = new NodeConverter().HlslToBpy(this, $"{path}/../..", Decompile(ShaderType.Pixel), false);
            if(bpy != string.Empty) {
                try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Pixel)}_{Hash}.py", bpy); }
                catch (IOException) { }
                Console.WriteLine($"Exported Blender PixelShader {Hash}.");
            }
        }
        if(Header.VertexShader != null) {
            var bpy = new NodeConverter().HlslToBpy(this, $"{path}/../..", Decompile(ShaderType.Vertex), true);
            if(bpy != string.Empty) {
                try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Vertex)}_{Hash}.py", bpy); }
                catch (IOException) { }
                Console.WriteLine($"Exported Blender VertexShader {Hash}.");
            }
        }
        // I don't think Blender can even handle compute shaders, so I'll leave that out. If it can, it's the same as above.
    }

    private void ExportMaterialUnreal(string path) {
        if(Header.PixelShader != null) {
            var usf = new UsfConverter().HlslToUsf(this, Decompile(ShaderType.Pixel), false);
            if(usf != string.Empty) {
                try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Pixel)}_{Hash}.usf", usf); }
                catch (IOException) { }
                Console.WriteLine($"Exported Unreal PixelShader {Hash}.");
            }
        }
        if(Header.VertexShader != null) {
            var usf = new UsfConverter().HlslToUsf(this, Decompile(ShaderType.Vertex), true);
            if(usf != string.Empty) {
                try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Vertex)}_{Hash}.usf", usf); }
                catch (IOException) { }
                Console.WriteLine($"Exported Unreal VertexShader {Hash}.");
            }
        }
    }

    private void ExportMaterialSource2(string path) {
        if(Header.PixelShader != null) {
            var vfx = new VfxConverter().HlslToVfx(this, Decompile(ShaderType.Pixel), false);
            if(vfx != string.Empty) {
                try { File.WriteAllText($"{path}/{GetShaderPrefix(ShaderType.Pixel)}_{Hash}.vfx", vfx); }
                catch (IOException) { }
                Console.WriteLine($"Exported Source 2 PixelShader {Hash}.");
            }
            var materialBuilder = new StringBuilder("Layer0 \n{");
            materialBuilder.AppendLine($"\n\tshader \"{GetShaderPrefix(ShaderType.Pixel)}_{Hash}.vfx\"");
            materialBuilder.AppendLine("\tF_ALPHA_TEST 1");
            foreach(var e in Header.PSTextures.Where(e => e.Texture != null))
                materialBuilder.AppendLine($"\tTextureT{e.TextureIndex} \"materials/Textures/{e.Texture.Hash}{GetTextureExtension(TextureExtractor.Format)}\"");
            materialBuilder.AppendLine("}");
            Directory.CreateDirectory($"{path}/materials");
            try { File.WriteAllText($"{path}/materials/{Hash}.vmat", materialBuilder.ToString()); }
            catch (IOException) { }
        }
    }
    
    #endregion

    #region Utils
    
    private static string GetTextureExtension(ETextureFormat format) {
        return format switch {
            ETextureFormat.PNG => ".png",
            ETextureFormat.TGA => ".tga",
            _ => ".dds"
        };
    }

    private static string GetShaderPrefix(ShaderType type) {
        return type switch {
            ShaderType.Pixel => "PS",
            ShaderType.Vertex => "VS",
            _ => "PS"
        };
    }
    
    private string GetTempPath(ShaderType type, string extension = ".bin") {
        var dir = $"{Path.GetTempPath()}/CharmCache/Shaders";
        var path = $"{dir}/{GetShaderPrefix(type)}_{Hash}{extension}";
        if(!File.Exists(dir))
            Directory.CreateDirectory(dir);
        return path;
    }

    private enum ShaderType { Pixel, Vertex, Compute }
    
    #endregion
}

[StructLayout(LayoutKind.Sequential, Size = 0x3D0)]
public struct D2Class_AA6D8080
{
    public long FileSize;
    public uint Unk08;
    public uint Unk0C;
    public uint Unk10;

    [DestinyOffset(0x70), DestinyField(FieldType.TagHash)]
    public ShaderHeader VertexShader;
    [DestinyOffset(0x78), DestinyField(FieldType.TablePointer)]
    public List<D2Class_CF6D8080> VSTextures;
    [DestinyOffset(0x90), DestinyField(FieldType.TablePointer)]
    public List<D2Class_09008080> Unk90;
    [DestinyField(FieldType.TablePointer)]
    public List<D2Class_90008080> UnkA0;
    [DestinyField(FieldType.TablePointer)]
    public List<D2Class_3F018080> UnkB0;
    [DestinyField(FieldType.TablePointer)]
    public List<D2Class_90008080> UnkC0;
    
    [DestinyOffset(0x2B0), DestinyField(FieldType.TagHash)]
    public ShaderHeader PixelShader;
    [DestinyOffset(0x2B8), DestinyField(FieldType.TablePointer)]
    public List<D2Class_CF6D8080> PSTextures;
    [DestinyOffset(0x2D0), DestinyField(FieldType.TablePointer)]
    public List<D2Class_09008080> Unk2D0;
    [DestinyField(FieldType.TablePointer)]
    public List<D2Class_90008080> Unk2E0;
    [DestinyField(FieldType.TablePointer)]
    public List<D2Class_3F018080> Unk2F0;
    [DestinyField(FieldType.TablePointer)]
    public List<D2Class_90008080> Unk300;
    [DestinyOffset(0x324)] 
    public TagHash PSVector4Container;
    
    [DestinyOffset(0x340), DestinyField(FieldType.TagHash)]
    public ShaderHeader ComputeShader;
    [DestinyOffset(0x348), DestinyField(FieldType.TablePointer)]
    public List<D2Class_CF6D8080> CSTextures;
    [DestinyOffset(0x360), DestinyField(FieldType.TablePointer)]
    public List<D2Class_09008080> Unk360;
    [DestinyField(FieldType.TablePointer)]
    public List<D2Class_90008080> Unk370;
    [DestinyField(FieldType.TablePointer)]
    public List<D2Class_3F018080> Unk380;
    [DestinyField(FieldType.TablePointer)]
    public List<D2Class_90008080> Unk390;
    
}

[StructLayout(LayoutKind.Sequential, Size = 0x18)]
public struct D2Class_CF6D8080
{
    public long TextureIndex;
    [DestinyField(FieldType.TagHash64)]
    public TextureHeader Texture;
}

[StructLayout(LayoutKind.Sequential, Size = 1)]
public struct D2Class_09008080
{
    public byte Value;
}

[StructLayout(LayoutKind.Sequential, Size = 0x10)]
public struct D2Class_3F018080
{
    [DestinyField(FieldType.TagHash64)]
    public Tag Unk00;
}
