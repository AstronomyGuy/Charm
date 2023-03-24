using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Field.General;
using Field.Models;
using Internal.Fbx;

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

    private Dictionary<string, string> variableTypes = new Dictionary<string, string>();

    
    private string convertComponent(char component, string type = "unknown")
    {
        Dictionary<char, string> componentConversionRGBA = new Dictionary<char, string>()
        {
            { 'x', "rgb[0]" },
            { 'y', "rgb[1]" },
            { 'z', "rgb[2]" },
            { 'w', "w" },
            { 'r', "rgb[0]" },
            { 'g', "rgb[1]" },
            { 'b', "rgb[2]" },
            { 'a', "w" }
        };
        Dictionary<char, int> componentIndex = new Dictionary<char, int>()
        {
            { 'x', 0 },
            { 'y', 1 },
            { 'z', 2 },
            { 'w', 3 },
            { 'r', 0 },
            { 'g', 1 },
            { 'b', 2 },
            { 'a', 3 }
        };
        Dictionary<char, string> componentConversionColor = new Dictionary<char, string>()
        {
            { 'x', "r" },
            { 'y', "g" },
            { 'z', "b" },
            { 'w', "a" },
            { 'r', "r" },
            { 'g', "g" },
            { 'b', "b" },
            { 'a', "a" }
        };
        switch (type)
        {
            case "RGBA":
                return '.' + componentConversionRGBA[component];
            case "index":
                return componentIndex[component].ToString();
            case "vector":
                return $"[{componentIndex[component]}]";
            case "int":
            case "float":
                return "";
            case "texture": //Convenience alias
            case "color":
                return '.' + componentConversionColor[component];
        }
        return componentConversionRGBA[component]; //When in doubt, RGBA
    }

    private string getVariableType(string type) { 
        if (type.Contains("["))
        {
            //Array reference
            return variableTypes[type.Split('[')[0]];
        }
        else
        {
            //Direct variable
            return variableTypes[type];
        }
    }

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
        //WriteCbuffers(material, bIsVertexShader); Done during function definition
        WriteFunctionDefinition(bIsVertexShader, material);
        hlsl = new StringReader(hlslText);
        bool success = ConvertInstructions(material);
        if (!success)
        {
            return "";
        }

        //if (!bIsVertexShader)
        //{
        //    AddOutputs();
        //}

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
            return "RGBA";
        }
        else if (type.EndsWith("2")) {
            return "RGBA";
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
            //case "DUAL":
            //    return $"{{{val}, {val}}}";
            //case "vector":
            //    return $"vector({val}, {val}, {val})";
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
            "\tfloat y;\n" +
            "};");
        osl.AppendLine("#include \"stdosl.h\"\r\nvector saturate (vector x)\r\n{\r\n    return clamp(x, 0, 1);\r\n}\r\n\r\nfloat saturate (float x)\r\n{\r\n    return clamp(x, 0, 1);\r\n}\r\n\r\nfloat frac(float x) {\r\n    return x - floor(x);\r\n}\r\n");
    }
    private void WriteCbuffers(Material material, bool bIsVertexShader)
    {
        // Try to find matches, pixel shader has Unk2D0 Unk2E0 Unk2F0 Unk300 available
        foreach (var cbuffer in cbuffers)
        {
            if(bIsVertexShader)
                osl.AppendLine($"\t {cbuffer.Type} {cbuffer.Variable}[{cbuffer.Count}] = ").AppendLine("\t{");
            else
                osl.AppendLine($"\t {cbuffer.Type} {cbuffer.Variable}[{cbuffer.Count}] = ").AppendLine("\t{");
            variableTypes.Add(cbuffer.Variable, cbuffer.Type);
            
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
                                 osl.AppendLine($"\t    {GetDefaultValue("RGBA")},");
                            }
                            break;        
                        }
                        
                        if (data == null)
                        {
                            osl.AppendLine($"\t    {GetDefaultValue("RGBA")},");
                        }
                        else
                        {
                            try
                            {
                                if (data[i] is Vector4)
                                {
                                    osl.AppendLine($"\t    {{vector({data[i].X}, {data[i].Y}, {data[i].Z}), {data[i].W}}},");
                                }
                                else
                                {
                                    var x = data[i].Unk00.X; // really bad but required
                                    osl.AppendLine($"\t    {{vector({x}, {data[i].Unk00.Y}, {data[i].Unk00.Z}), {data[i].Unk00.W}}},");
                                }
                            }
                            catch (Exception e)  // figure out whats up here, taniks breaks it
                            {
                                if(bIsVertexShader)
                                {
                                    osl.AppendLine($"\t    {GetDefaultValue("RGBA")},");
                                }
                                else
                                    osl.AppendLine($"\t    {GetDefaultValue("RGBA")},");
                            }
                        }
                        break;
                    case "vector":
                        if(bIsVertexShader)
                        {
                            if (data == null)
                            {
                                 osl.AppendLine("\t    vector(1.0, 1.0, 1.0),");
                            }
                            break;        
                        }
                        if (data == null) osl.AppendLine("    vector(0.0, 0.0, 0.0),");
                        else osl.AppendLine($"\t    vector({data[i].Unk00.X}, {data[i].Unk00.Y}, {data[i].Unk00.Z}),");
                        break;
                    case "float":
                        if(bIsVertexShader)
                        {
                            if (data == null)
                            {
                                 osl.AppendLine("\t    1.0,");
                            }
                            break;        
                        }
                        if (data == null) osl.AppendLine("    0.0,");
                        else osl.AppendLine($"\t    {data[i].Unk00},");
                        break;
                    default:
                        throw new NotImplementedException();
                }  
            }
            osl.Remove(osl.ToString().LastIndexOf(','), 1);
            osl.AppendLine("\t};");
        }
    }
    
    private void WriteFunctionDefinition(bool bIsVertexShader, Material material)
    {
        osl.AppendLine("#define cmp -");
        osl.AppendLine("#define rsqrt inversesqrt");
        osl.AppendLine("shader main(");
        foreach (var i in inputs)
        {
            osl.AppendLine($"\t{i.Type} {i.Variable} = {GetDefaultValue(i.Type, 1)}, // {i.Semantic}");
            variableTypes.Add(i.Variable, i.Type);
        }
        foreach (var texture in textures)
        {
            osl.AppendLine($"\tstring {texture.Variable} = \"\",");            
        }

        //osl.AppendLine($"\tDUAL tx = {GetDefaultValue("DUAL", 1)},");
        foreach (var output in outputs)
        {
            osl.Append($"\toutput {output.Type} {output.Variable} = {GetDefaultValue(output.Type)}");
            variableTypes.Add(output.Variable, output.Type);
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
        WriteCbuffers(material, bIsVertexShader);
    }    
    private string[] splitVariable(string variable)
    {
        //TODO: Reorder vector by components if they exist
        if (Regex.IsMatch(variable, "\\{([0-9\\.]+), ?([0-9\\.]+), ?([0-9\\.]+), ?([0-9\\.]+)\\}"))
        {
            Match match = Regex.Match(variable, "\\{([0-9\\.]+), ([0-9\\.]+), ([0-9\\.]+), ([0-9\\.]+)\\}");
            return new string[] { match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value };
        }
        else if (variable.Contains("vector"))
        {
            if (variable.Contains("{"))
            {
                //RGBA definition
                Match match = Regex.Match(variable, "{vector\\(([0-9\\.]*), ?([0-9\\.]*), ?([0-9\\.]*)\\), ([0-9\\.]*)}");
                return new string[] { match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value };
            }
            else
            {
                //3-dim Vector definition
                Match match = Regex.Match(variable, "vector\\(([0-9\\.]*), ?([0-9\\.]*), ?([0-9\\.]*)\\)");
                return new string[] { match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value };
            }
        }
        else if (!variable.Contains('.'))
        {
            // Does not have dimension modifier; must be int or other primitive type
            return new string[] { variable };
        }
        else
        {
            //var.[xyzrgba]

            //Will not work if variable is not RGBA
            //All variables *should* be RGBA though so it should be fine
            string[] split = variable.Split(".");
            List<string> result = new List<string>();
            foreach (char c in split[1])
            {
                result.Add($"{split[0]}{convertComponent(c, "RGBA")}");
            }
            return result.ToArray();
        }
    }
    private string solveIndex(string line, int i)
    {

        string body = line;

        string query = "(?:\\{([0-9\\.]+), ([0-9\\.]+), ([0-9\\.]+), ([0-9\\.]+)\\}|(vector)\\(([0-9\\.]+), ?([0-9\\.]+), ?([0-9\\.]+)\\))\\.([xyzwrgba]{0,4})";

        Match dim4 = Regex.Match(body, query);
        while (dim4.Success)
        {
            int index = int.Parse(convertComponent(dim4.Groups[9].Value[i % dim4.Groups[9].Length], "index"))+1; //Should never return a non-int type
            if (dim4.Groups[5].Length > 0) { index += 5; }

            string replacement = dim4.Groups[index].Value;
            body = body.Remove(dim4.Index, dim4.Length);
            body = body.Insert(dim4.Index, replacement);

            dim4 = Regex.Match(body, query);
        }

        return body;
    }
    private bool ConvertInstructions(Material material)
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

        //Insert w_dummy variable
        //The sole reason that this exists is to allow us to get the alpha channel from a given texture without overriding other data        
        osl.AppendLine("\tint w_dummy = 0;");

        hlsl.ReadLine();
        do
        {
            line = hlsl.ReadLine();            

            if (line != null)
            {
                ///Pre-processing

                // Partial Derivative functions
                line = Regex.Replace(line, "dd([xyz])(?:_coarse|_fine)", "D$1");
                // Manual vector declarations
                line = Regex.Replace(line, "(float|uint|int|double)2\\(([0-9\\., ]*)\\)", "vector($2,0).xy");
                line = Regex.Replace(line, "(float|uint|int|double)3\\(([0-9\\., ]*)\\)", "vector($2).xyz");
                line = Regex.Replace(line, "(float|uint|int|double)4\\(([0-9\\.]+, ?[0-9\\.]+, ?[0-9\\.]+), ?([0-9.]+)\\)", "{$2, $3}");
                // Texture Sampling
                line = Regex.Replace(line, "t(\\d+).Sample\\(s1_s, ([A-Za-z_][A-Za-z_0-9\\\\.]*)\\)\\.?([xyzwrgba]{0,4})", "texture(t$1, $2).$3");


                if (line.Contains("return;"))
                {
                    break;
                }
                //if (line.Contains("Sample"))
                //{
                //    var equal = line.Split("=")[0];
                //    var texIndex = Int32.Parse(line.Split(".Sample")[0].Split("t")[1]);
                //    var sampleIndex = Int32.Parse(line.Split("(s")[1].Split("_s,")[0]);
                //    var sampleUv = line.Split(", ")[1].Split(")")[0];
                //    var dotAfter = line.Split(").")[1];
                //    // todo add dimension
                //    osl.AppendLine($"\t{equal}= texture(t{sortedIndices.IndexOf(texIndex)}, {sampleUv}).{dotAfter}");
                //}
                // todo add load, levelofdetail, o0.w, discard
                else if (line.Contains("discard"))
                {
                    //StringBuilder discard = new StringBuilder();
                    //discard.AppendLine("{");
                    //foreach (Output output in outputs)
                    //{
                    //    discard.AppendLine($"\t\t{output.Variable} = {GetDefaultValue(output.Type)};");
                    //}
                    //discard.AppendLine("\t\treturn;\n\t}");
                    //osl.AppendLine(line.Replace("discard;", discard.ToString()));
                }                
                else
                {
                    if (line.Contains(" = "))
                    {
                        //Assignment
                        
                        string[] eq_sides = line.Split(" = ");
                        Match set_var = Regex.Match(eq_sides[0], "([A-Za-z_][A-Za-z_0-9\\[\\]]+)\\.([xyzwrgba]{0,4})");

                        string placeholder = Regex.Replace(eq_sides[1], "([A-Za-z_][A-Za-z_0-9\\.]*)\\((.*)\\)\\.?([xyzwrgba]{0,4})", "ø");
                        List<Tuple<string, string>> funcList = new List<Tuple<string, string>>();
                        int match_idx = 0;

                        /// Escape for special functions   
                        foreach (Match func in Regex.Matches(line, "([A-Za-z_][A-Za-z_0-9\\.]*)\\((.*)\\)\\.?([xyzwrgba]{0,4})"))
                        {
                            //Match func = Regex.Match(line, "([A-Za-z_][A-Za-z_0-9\\.]*)\\((.*)\\)\\.?([xyzwrgba]{0,4})");
                            string method = func.Groups[1].Value.ToLower().Trim();
                            string[] mparams = splitParams(func.Groups[2].Value).Select(s => s.Trim()).ToArray();

                            if (method == "texture" || Regex.IsMatch(method, "t\\d+.sample")) //tX.Sample gets replaced by texture in pre-processing
                            {
                                //Split input coord into separate coordinates
                                string[] split = splitVariable(mparams[1]);

                                //Get Texture Index
                                //Index is long to match format of D2Class_CF6D8080, realistically it'll never get close to using that
                                long index = long.Parse(mparams[0].Substring(1));

                                /// --- NOTES FOR ALPHA HANDLING ---
                                /// The alpha channel needs to be referenced with an additional parameter: ..., "alpha", <variable>
                                /// This requires a dummy variable to dump the alpha value into and a bunch of other special handling
                                ///  - OSL Specification pg.66
                                ///  Example: https://github.com/AcademySoftwareFoundation/OpenShadingLanguage/blob/main/testsuite/texture-alpha/test.osl

                                if (split.Length == 2)
                                {                                    
                                    //2D lookup
                                    funcList.Add(new Tuple<string, string>(
                                        $"texture({mparams[0]}, {split[0]}, {split[1]}, \"wrap\", \"periodic\")||{index}",
                                        func.Groups[3].Value)
                                    );
                                }
                                else if (split.Length == 3)
                                {
                                    //3D lookup
                                    funcList.Add(new Tuple<string, string>(
                                        $"texture3d({mparams[0]}, {split[0]}, {split[1]}, {split[2]})||{index}",
                                        func.Groups[3].Value)
                                    );
                                }
                            }
                            else if (method == "dot" || method == "cross")
                            {
                                //Convert inputs into vectors
                                string[] vec1 = splitVariable(mparams[0]);
                                string[] vec2 = splitVariable(mparams[1]);

                                //Assume vectors are same length
                                if (vec1.Length == 2)
                                {
                                    funcList.Add(new Tuple<string, string>(
                                        $"{method}(vector({vec1[0]}, {vec1[1]}, 0), vector({vec2[0]}, {vec2[1]}, 0))",
                                        func.Groups[3].Value
                                    ));
                                }
                                else if (vec1.Length == 3)
                                {
                                    funcList.Add(new Tuple<string, string>(
                                        $"{method}(vector({vec1[0]}, {vec1[1]}, {vec1[2]}), vector({vec2[0]}, {vec2[1]}, {vec2[2]}))",
                                        func.Groups[3].Value
                                    ));
                                }                         
                                else if (method == "dot") //Cross is undefined unless length is 2 or 3, only have to worry abt dot
                                {
                                    string equation = "(";
                                    for (int i = 0; i < vec1.Length; i++)
                                    {
                                        equation += $"{vec1[i]}*{vec2[i]} + ";
                                    }
                                    equation += $"0); // Dot Product for {vec1.Length} elements";
                                    funcList.Add(new Tuple<string, string>(
                                        equation,
                                        func.Groups[3].Value
                                    ));
                                }
                            }                        
                            else if (method == "length" || method == "normalize")
                            {
                                //Convert input into vector
                                string[] vec1 = splitVariable(mparams[0]);
                                funcList.Add(new Tuple<string, string>(
                                    $"{method}(vector({vec1[0]}, {vec1[1]}, {vec1[2]}))",
                                    func.Groups[3].Value
                                ));
                            }
                            else if (method == "distance")
                            {
                                //Convert inputs into points
                                string[] vec1 = splitVariable(mparams[0]);
                                string[] vec2 = splitVariable(mparams[1]);
                                //Assume vectors are same length
                                if (vec1.Length == 2)
                                {
                                    funcList.Add(new Tuple<string, string>(
                                        $"{method}(point({vec1[0]}, {vec1[1]}, 0), point({vec2[0]}, {vec2[1]}, 0))",
                                        func.Groups[3].Value
                                    ));
                                } 
                                else
                                {
                                    funcList.Add(new Tuple<string, string>(
                                        $"{method}(point({vec1[0]}, {vec1[1]}, {vec1[2]}), point({vec2[0]}, {vec2[1]}, {vec2[2]}))",
                                        func.Groups[3].Value
                                    ));
                                }
                                    
                            }
                            else
                            {
                                //If function has no special properties, put it back and allow it to be split
                                
                                int last_index = placeholder.IndexOf("ø");
                                for (int i = 0; i < match_idx; i++)
                                {
                                    last_index = placeholder.IndexOf("ø", last_index);                                    
                                }
                                if (last_index == -1)
                                {
                                    //Ran out of ø chars even though they should be identical
                                    throw new Exception("Number of Function placeholders does not match number of Functions.");
                                }

                                placeholder = placeholder.Remove(last_index, 1);
                                placeholder = placeholder.Insert(last_index, func.Value);
                            }
                            match_idx++;
                            /// Remaining Functions: faceforward, reflect, refract, fresnel, rotate
                            /// Not included because I don't think they're used in HLSL
                        }
                        
                        placeholder = Regex.Replace(placeholder, "([A-Za-z_][A-Za-z_0-9\\[\\]]+)\\.([xyzwrgba]{1,4})", "§");


                        MatchCollection vars = Regex.Matches(eq_sides[1], "([A-Za-z_][A-Za-z_0-9\\[\\]]+)\\.([xyzwrgba]{0,4})");

                        osl.AppendLine($"\t// {line}");

                        //Getting a value from Regex groups is expensive, so we reduce calls as much as possible
                        string base_components = set_var.Groups[2].Value;
                        string line_starter = $"{set_var.Groups[1]}";

                        for (int i = 0; i < base_components.Length; i++)
                        {
                            string new_line = line_starter + $"{convertComponent(base_components[i], "RGBA")} = {placeholder}";

                            if (new_line.Contains("vector") || (new_line.Contains("{") && new_line.Contains("}")))
                            {                                
                                new_line = solveIndex(new_line, i);
                            }

                            int placeholder_index = new_line.IndexOf('§');
                            int placeholders_iter = 0;

                            // Construct loose variables
                            while (placeholder_index != -1)
                            {
                                //Variable Handler
                                Match current_var = vars[placeholders_iter];
                                string components = current_var.Groups[2].Value;
                                string body = current_var.Groups[1].Value;

                                new_line = new_line.Remove(placeholder_index, 1);
                                new_line = new_line.Insert(placeholder_index, $"{current_var.Groups[1].Value}{convertComponent(components[i % components.Length], getVariableType(current_var.Groups[1].Value))}");

                                placeholders_iter++;
                                placeholder_index = new_line.IndexOf('§');
                            }

                            placeholder_index = new_line.IndexOf('ø');
                            placeholders_iter = 0;

                            //Construct functions
                            while (placeholder_index != -1)
                            {
                                //Variable Handler
                                Tuple<string, string> current_func = funcList[placeholders_iter];
                                string components = current_func.Item2;
                                string body = current_func.Item1;                                

                                new_line = new_line.Remove(placeholder_index, 1);
                                if (components.Length > 0)
                                {
                                    if (body.Contains("texture")) {
                                        if (convertComponent(components[i % components.Length], "index") == "3")
                                        {
                                            //Alpha channel weirdness

                                            //Can be optimized by cutting out w_dummy and inserting the alpha call when one of the color channels are set
                                            //However then you have to deal with the possibility of there *not* being a call to other channels, etc etc
                                            new_line = $"w_dummy = {body.Split("||")[0].Substring(0, body.Length-1)}, \"firstchannel\", 0, \"alpha\", {line_starter}{convertComponent(base_components[i], getVariableType(set_var.Groups[1].Value))});";
                                        }
                                        else
                                        {
                                            body = $"{body.Split("||")[0].Substring(0, body.Length - 1)}, \"firstchannel\", {convertComponent(components[i % components.Length], "index")})";
                                            if (material.isTexSRGB(long.Parse(body.Split("||")[1]))) {
                                                body = $"pow({body}, 2.33333333)";
                                            }
                                            new_line = new_line.Insert(placeholder_index, $"{body}");
                                        }
                                    }
                                    else
                                    {
                                        new_line = new_line.Insert(placeholder_index, $"{body}{convertComponent(components[i % components.Length], "RGBA")}");
                                    }
                                }
                                else
                                {
                                    new_line = new_line.Insert(placeholder_index, $"{body}");
                                }

                                placeholders_iter++;
                                placeholder_index = new_line.IndexOf('ø');
                            }

                            osl.AppendLine("\t" + new_line);
                        }                       
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
                                    variableTypes.Add(parts[i], type);
                                    osl.AppendLine($"\t{type} {parts[i].Trim()} = {GetDefaultValue(type)};");
                                }
                            }
                        }
                    }
                    else
                    {
                        //This should pretty much only be flow control and other miscellaneous things, which are in OSL
                        osl.AppendLine(line);
                    }
                }                
            }
        } while (line != null);

        return true;
    }

    private static string[] splitParams(string ParamString, string splitBy = ",")
    {
        string[] Params = ParamString.Split(splitBy);
        int paraCount = 0;
        List<string> output = new List<string>();
        string temp = "";
        foreach (string param in Params)
        {
            paraCount += param.Count(c => "({[\"".Contains(c)) - param.Count(c => ")}]\"".Contains(c));
            if (paraCount == 0)
            {
                temp += param;
                output.Add(temp);
                temp = "";
            }
            else
            {
                //Parantheses unbalanced
                temp += param + splitBy;
            }
        }
        return output.ToArray();
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
        osl.AppendLine("}");
    }
}