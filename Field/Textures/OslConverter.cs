using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Field.General;
using Field.Models;

namespace Field;

public class OslConverter
{
    private StringReader hlsl;
    private StringBuilder osl;
    private bool bOpacityEnabled = false;
    private List<Texture> textures = new List<Texture>();
    private List<int> samplers = new List<int>();
    private List<Cbuffer> cbuffers = new List<Cbuffer>();
    private List<Input> inputs = new List<Input>();
    private List<Output> outputs = new List<Output>();

    public string HlslToOsl(Material material, string hlslText, bool bIsVertexShader)
    {
        hlsl = new StringReader(hlslText);
        osl = new StringBuilder();
        bOpacityEnabled = false;
        ProcessHlslData();
        if (bOpacityEnabled)
        {
            osl.AppendLine("// masked");
        }
        // WriteTextureComments(material, bIsVertexShader);
        WriteStructs();
        WriteCbuffers(material, bIsVertexShader);
        WriteFunctionDefinition(bIsVertexShader);
        hlsl = new StringReader(hlslText);
        bool success = ConvertInstructions();
        if (!success)
        {
            return "";
        }

        if (!bIsVertexShader)
        {
            AddOutputs();
        }

        WriteFooter(bIsVertexShader);
        return osl.ToString();
    }

    private void ProcessHlslData()
    {
        string line = string.Empty;
        bool bFindOpacity = false;
        do
        {
            line = hlsl.ReadLine();
            if (line != null)
            {
                if (line.Contains("r0,r1")) // at end of function definition
                {
                    bFindOpacity = true;
                }

                if (bFindOpacity)
                {
                    if (line.Contains("discard"))
                    {
                        bOpacityEnabled = true;
                        break;
                    }
                    continue;
                }

                if (line.Contains("Texture"))
                {
                    Texture texture = new Texture();
                    texture.Dimension = line.Split("<")[0];
                    texture.Type = TypeConversion(line.Split("<")[1].Split(">")[0]);
                    texture.Variable = line.Split("> ")[1].Split(" :")[0];
                    texture.Index = Int32.TryParse(new string(texture.Variable.Skip(1).ToArray()), out int index) ? index : -1;
                    textures.Add(texture);
                }
                else if (line.Contains("SamplerState"))
                {
                    samplers.Add(line.Split("(")[1].Split(")")[0].Last() - 48);
                }
                else if (line.Contains("cbuffer"))
                {
                    hlsl.ReadLine();
                    line = hlsl.ReadLine();
                    Cbuffer cbuffer = new Cbuffer();
                    cbuffer.Variable = "cb" + line.Split("cb")[1].Split("[")[0];
                    cbuffer.Index = Int32.TryParse(new string(cbuffer.Variable.Skip(2).ToArray()), out int index) ? index : -1;
                    cbuffer.Count = Int32.TryParse(new string(line.Split("[")[1].Split("]")[0]), out int count) ? count : -1;
                    cbuffer.Type = TypeConversion(line.Split("cb")[0].Trim());
                    cbuffers.Add(cbuffer);
                }
                else if (line.Contains(" v") && line.Contains(" : ") && !line.Contains("?"))
                {
                    Input input = new Input();
                    input.Variable = "v" + line.Split("v")[1].Split(" : ")[0];
                    input.Index = Int32.TryParse(new string(input.Variable.Skip(1).ToArray()), out int index) ? index : -1;
                    input.Semantic = line.Split(" : ")[1].Split(",")[0];
                    input.Type = TypeConversion(line.Split(" v")[0].Trim());
                    inputs.Add(input);
                }
                else if (line.Contains("out") && line.Contains(" : "))
                {
                    Output output = new Output();
                    output.Variable = "o" + line.Split(" o")[2].Split(" : ")[0];
                    output.Index = Int32.TryParse(new string(output.Variable.Skip(1).ToArray()), out int index) ? index : -1;
                    output.Semantic = line.Split(" : ")[1].Split(",")[0];
                    output.Type = TypeConversion(line.Split("out ")[1].Split(" o")[0]);
                    outputs.Add(output);
                }
            }

        } while (line != null);
    }

    private string TypeConversion(string type)
    {
        if (type.Length >= 2 && type[type.Length - 2] == 'x')
        {
            // Might not work, will have to find a shader using matrices to test
            return "matrix";
        }

        if (type.EndsWith("4"))
        {
            return "RGBA";
        }
        else if (type.EndsWith("3"))
        {
            return "vector";
        }
        else if (type.EndsWith("2")) {
            return "DUAL";
        }
        else if (type.ToLower().StartsWith("min"))
        {
            //Handles minimum bit types (e.g. min16float, min12int, etc.)
            return TypeConversion(type.Substring(5));
        }
        switch (type.ToLower())
        {
            case "uint":
            case "dword":
            case "half":
                return "int"; //Might cause problems down the line, but OSL doesn't support unsigned ints
            case "bool":
                return "int";
            case "double":
                return "float"; //Also may cause issues, but I doubt any shader is using more than 32 bits
            default:
                return type;

        }
    }

    private string GetDefaultValue(string type, int val = 0, bool force = true)
    {
        switch (type)
        {
            case "RGBA":
                return $"{{vector({val}, {val}, {val}), {val}}}";
            case "DUAL":
                return $"{{{val}, {val}}}";
            case "vector":
                return $"vector({val}, {val}, {val})";
            case "float":
            case "int":
                return $"{val}";
            case "matrix":
                return val == 0 ? "1" : val.ToString(); //Page 29 of OSL specification; setting to 1 will create an identity matrix

        }
        if (force)
        {
            //Will attempt to convert type before throwing error
            //force variable exists to prevent this from infinitely looping
            return GetDefaultValue(TypeConversion(type), force: false);
        }
        //All types returned from TypeConversion should be represented here
        throw new NotImplementedException();
    }
    private void WriteStructs()
    {
        osl.AppendLine("struct RGBA {\n" +
            "\tvector rgb;\n" +
            "\tfloat w;\n" +
            "};");
        osl.AppendLine("struct Dual {\n" +
            "\tfloat x;\n" +
            "\tfloat y\n" +
            "};");
    }
    private void WriteCbuffers(Material material, bool bIsVertexShader)
    {
        // Try to find matches, pixel shader has Unk2D0 Unk2E0 Unk2F0 Unk300 available
        foreach (var cbuffer in cbuffers)
        {
            if(bIsVertexShader)
                osl.AppendLine($"static {cbuffer.Type} {cbuffer.Variable}[{cbuffer.Count}] = ").AppendLine("{");
            else
                osl.AppendLine($"static {cbuffer.Type} {cbuffer.Variable}[{cbuffer.Count}] = ").AppendLine("{");
            
            dynamic data = null;
            if (bIsVertexShader)
            {
                if (cbuffer.Count == material.Header.Unk90.Count)
                {
                    data = material.Header.Unk90;
                }
                else if (cbuffer.Count == material.Header.UnkA0.Count)
                {
                    data = material.Header.UnkA0;
                }
                else if (cbuffer.Count == material.Header.UnkB0.Count)
                {
                    data = material.Header.UnkB0;
                }
                else if (cbuffer.Count == material.Header.UnkC0.Count)
                {
                    data = material.Header.UnkC0;
                }
                else
                {
                    
                    // if (material.Header.VSVector4Container.Hash != 0xffff_ffff)
                    // {
                    //     // Try the Vector4 storage file
                    //     DestinyFile container = new DestinyFile(PackageHandler.GetEntryReference(material.Header.VSVector4Container));
                    //     byte[] containerData = container.GetData();
                    //     int num = containerData.Length / 16;
                    //     if (cbuffer.Count == num)
                    //     {
                    //         List<Vector4> float4s = new List<Vector4>();
                    //         for (int i = 0; i < containerData.Length / 16; i++)
                    //         {
                    //             float4s.Add(StructConverter.ToStructure<Vector4>(containerData.Skip(i*16).Take(16).ToArray()));
                    //         }

                    //         data = float4s;
                    //     }                        
                    // }
                }
            }
            else
            {
                if (cbuffer.Count == material.Header.Unk2D0.Count)
                {
                    data = material.Header.Unk2D0;
                }
                else if (cbuffer.Count == material.Header.Unk2E0.Count)
                {
                    data = material.Header.Unk2E0;
                }
                else if (cbuffer.Count == material.Header.Unk2F0.Count)
                {
                    data = material.Header.Unk2F0;
                }
                else if (cbuffer.Count == material.Header.Unk300.Count)
                {
                    data = material.Header.Unk300;
                }
                else
                {
                    if (material.Header.PSVector4Container.Hash != 0xffff_ffff)
                    {
                        // Try the Vector4 storage file
                        DestinyFile container = new DestinyFile(PackageHandler.GetEntryReference(material.Header.PSVector4Container));
                        byte[] containerData = container.GetData();
                        int num = containerData.Length / 16;
                        if (cbuffer.Count == num)
                        {
                            List<Vector4> float4s = new List<Vector4>();
                            for (int i = 0; i < containerData.Length / 16; i++)
                            {
                                float4s.Add(StructConverter.ToStructure<Vector4>(containerData.Skip(i*16).Take(16).ToArray()));
                            }

                            data = float4s;
                        }                        
                    }

                }
            }


            for (int i = 0; i < cbuffer.Count; i++)
            {
                switch (cbuffer.Type)
                {
                    case "RGBA":
                        if(bIsVertexShader)
                        {
                            if (data == null)
                            {
                                 osl.AppendLine("    {color(1.0, 1.0, 1.0), 1.0},");
                            }
                            break;        
                        }
                        
                        if (data == null)
                        { 
                            osl.AppendLine("    {color(0.0, 0.0, 0.0), 0.0},");
                        }
                        else
                        {
                            try
                            {
                                if (data[i] is Vector4)
                                {
                                    osl.AppendLine($"    {{color({data[i].X}, {data[i].Y}, {data[i].Z}), {data[i].W}}},");
                                }
                                else
                                {
                                    var x = data[i].Unk00.X; // really bad but required
                                    osl.AppendLine($"    {{color({x}, {data[i].Unk00.Y}, {data[i].Unk00.Z}), {data[i].Unk00.W}}},");
                                }
                            }
                            catch (Exception e)  // figure out whats up here, taniks breaks it
                            {
                                if(bIsVertexShader)
                                {
                                    osl.AppendLine("    {color(1.0, 1.0, 1.0), 1.0},");
                                }
                                else
                                    osl.AppendLine("    {color(0.0, 0.0, 0.0), 0.0},");
                            }
                        }
                        break;
                    case "vector":
                        if(bIsVertexShader)
                        {
                            if (data == null)
                            {
                                 osl.AppendLine("    vector(1.0, 1.0, 1.0),");
                            }
                            break;        
                        }
                        if (data == null) osl.AppendLine("    vector(0.0, 0.0, 0.0),");
                        else osl.AppendLine($"    vector({data[i].Unk00.X}, {data[i].Unk00.Y}, {data[i].Unk00.Z}),");
                        break;
                    case "float":
                        if(bIsVertexShader)
                        {
                            if (data == null)
                            {
                                 osl.AppendLine("    1.0,");
                            }
                            break;        
                        }
                        if (data == null) osl.AppendLine("    0.0,");
                        else osl.AppendLine($"    {data[i].Unk00},");
                        break;
                    default:
                        throw new NotImplementedException();
                }  
            }

            osl.AppendLine("};");
        }
    }
    
    private void WriteFunctionDefinition(bool bIsVertexShader)
    {
        osl.AppendLine("#define cmp -");
        if (bIsVertexShader)
        {          
            osl.AppendLine().AppendLine("shader main(");
            foreach (var texture in textures)
            {
                osl.AppendLine($"   {texture.Type} {texture.Variable},");
            }
            for (var i = 0; i < inputs.Count; i++)
            {
                osl.AppendLine($"\t{inputs[i].Type} {inputs[i].Variable}, // {inputs[i].Semantic}");
            }
            foreach (var output in outputs)
            {
                osl.AppendLine($"output {output.Type} {output.Variable} = {GetDefaultValue(output.Type)};");
            }
        }
        else
        {
            osl.AppendLine("shader main(");
            foreach (var i in inputs)
            {
                osl.AppendLine($"\t{i.Type} {i.Variable} = {GetDefaultValue(i.Type, 1)}, // {i.Semantic}");
            }
            foreach (var texture in textures)
            {
                osl.AppendLine($"\tstring {texture.Variable} = \"\",");
            }

            //osl.AppendLine($"\tDUAL tx = {GetDefaultValue("DUAL", 1)},");
            foreach (var output in outputs)
            {
                osl.Append($"\toutput {output.Type} {output.Variable} = {GetDefaultValue(output.Type)}");
                if (outputs.IndexOf(output) != outputs.Count - 1)
                {
                    osl.Append(",\n");
                }
                else
                {
                    osl.Append(")\n");
                }
            }
            osl.AppendLine(" {");
        }
    }

    private bool ConvertInstructions()
    {
        Dictionary<int, Texture> texDict = new Dictionary<int, Texture>();
        foreach (var texture in textures)
        {
            texDict.Add(texture.Index, texture);
        }
        List<int> sortedIndices = texDict.Keys.OrderBy(x => x).ToList();
        string line = hlsl.ReadLine();
        if (line == null)
        {
            // its a broken pixel shader that uses some kind of memory textures
            return false;
        }
        while (!line.Contains("SV_TARGET2"))
        {
            line = hlsl.ReadLine();
            if (line == null)
            {
                // its a broken pixel shader that uses some kind of memory textures
                return false;
            }
        }
        hlsl.ReadLine();
        do
        {
            line = hlsl.ReadLine();
            if (line != null)
            {
                if (line.Contains("return;"))
                {
                    break;
                }
                if (line.Contains("Sample"))
                {
                    var equal = line.Split("=")[0];
                    var texIndex = Int32.Parse(line.Split(".Sample")[0].Split("t")[1]);
                    var sampleIndex = Int32.Parse(line.Split("(s")[1].Split("_s,")[0]);
                    var sampleUv = line.Split(", ")[1].Split(")")[0];
                    var dotAfter = line.Split(").")[1];
                    // todo add dimension
                    osl.AppendLine($"\t{equal}= texture(t{sortedIndices.IndexOf(texIndex)}, {sampleUv}).{dotAfter}");
                }
                // todo add load, levelofdetail, o0.w, discard
                else if (line.Contains("discard"))
                {
                    foreach (Output output in outputs)
                    {
                        osl.AppendLine($"\toutput {output.Type} {output.Variable} = {GetDefaultValue(output.Type)};");
                    }
                    osl.AppendLine("return;");
                }
                else
                {
                    if (line.Contains("="))
                    {
                        //Assignment                        
                        string[] eq_sides = line.Split("=");
                        Match set_var = Regex.Match(eq_sides[0], "([A-Za-z_][A-Za-z_0-9\\[\\]]+)\\.([xyzwrgba]{0,4})");
                        
                        ///TODO: Escape for special functions (dot, texture, cross, etc)
                        
                        Dictionary<char, string> componentConversion= new Dictionary<char, string>()
                        {
                            { 'x', "rgb.x" },
                            { 'y', "rgb.y" },
                            { 'z', "rgb.z" },
                            { 'w', "w" },
                            { 'r', "rgb.x" },
                            { 'g', "rgb.y" },
                            { 'b', "rgb.z" },
                            { 'a', "w" }
                        };

                        string placeholder = Regex.Replace(eq_sides[1], "([A-Za-z_][A-Za-z_0-9\\[\\]]+)\\.([xyzwrgba]{1,4})", "§");
                        MatchCollection vars = Regex.Matches(eq_sides[1], "([A-Za-z_][A-Za-z_0-9\\[\\]]+)\\.([xyzwrgba]{0,4})");

                        osl.AppendLine($"// {line}");

                        //Getting a value from Regex groups is expensive, so we reduce calls as much as possible
                        string base_components = set_var.Groups[2].Value;
                        string line_starter = $"{set_var.Groups[1]}.";

                        for (int i = 0; i < base_components.Length; i++)
                        {                            
                            int placeholder_index = 0;

                            string new_line = line_starter + $"{base_components[i]} = ";
                            
                            // Construct other side of assignment
                            foreach (char c in placeholder)
                            {
                                if (c == '§')
                                {
                                    //Variable Handler
                                    Match current_var = vars[placeholder_index];
                                    string components = current_var.Groups[2].Value;
                                    new_line += $"{current_var.Groups[1].Value}.{componentConversion[components[i % components.Length]]}";
                                    placeholder_index++;
                                }
                                else
                                {
                                    new_line += c;
                                }
                            }
                            osl.AppendLine(new_line);
                        }
                        osl.AppendLine();
                    }
                    else if (line.EndsWith(";"))
                    {
                        //Declaration
                        string[] parts = line.Split(new char[] {' ', ',', ';'});
                        string type = "";
                        for (int i = 1; i < parts.Count(); i++)
                        {
                            if (parts[i].Trim().Length > 0) {
                                if (type.Length == 0)
                                {
                                    type = TypeConversion(parts[i].Trim());
                                }
                                else
                                {
                                    osl.AppendLine($"{type} {parts[i].Trim()} = {GetDefaultValue(type)}");
                                }
                            }
                        }
                    }
                    osl.AppendLine(line);
                }
            }
        } while (line != null);

        return true;
    }

    private void AddOutputs()
    {
        string outputString = @"
        ///RT0
        output.BaseColor = o0.xyz; // Albedo
        
        ///RT1

        // Normal
        float3 biased_normal = o1.xyz - vector(0.5, 0.5, 0.5);
        float normal_length = length(biased_normal);
        float3 normal_in_world_space = biased_normal / normal_length;
        normal_in_world_space.z = sqrt(1.0 - saturate(dot(normal_in_world_space.xy, normal_in_world_space.xy)));
        output.Normal = normalize((normal_in_world_space * 2 - 1.35)*0.5 + 0.5);

        // Roughness
        float smoothness = saturate(8 * (normal_length - 0.375));
        output.Roughness = 1 - smoothness;
 
        ///RT2
        output.Metallic = saturate(o2.x);
        output.EmissiveColor = clamp((o2.y - 0.5) * 2 * 5 * output.BaseColor, 0, 100);  // the *5 is a scale to make it look good
        output.AmbientOcclusion = saturate(o2.y * 2); // Texture AO

        output.OpacityMask = 1;

        return output;
        ";
        osl.AppendLine(outputString);
    }

    private void WriteFooter(bool bIsVertexShader)
    {
        osl.AppendLine("};");
    }
}