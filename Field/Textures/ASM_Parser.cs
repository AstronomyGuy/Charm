using Field.General;
using Field.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Field.Textures
{
    internal class ASM_Parser
    {
        private static Dictionary<string, string> nodeMap = new Dictionary<string, string>();
        private int var_index = 0;
        private string rawASM;
        Material material;
        public StringBuilder bpy = new StringBuilder();
        bool vertexShader;

        Dictionary<string, int> cbuffers = new Dictionary<string, int>();
        public ASM_Parser(string raw, Material material, bool vs) {
            rawASM = raw;
            this.material = material;
            this.vertexShader = vs;
            ensureNodeMap();
        }
        class Operation
        {
            public string operation;
            public string targetVar;
            public string[] parameters;
            public Operation(string line)
            {
                string[] split = splitParams(line);
                string[] splitFirst = splitParams(split[0], " ");
                this.operation = splitFirst[0];
                this.targetVar = splitFirst[1];
                this.parameters = split.Skip(1).ToArray();
            }
            private string handleSpecialOperations(string op) {
                if (op == "ld_indexable(buffer)(float,float,float,float)")
                {
                    return "ld";
                }
                else {
                    return op;
                }
            }
            override
            public string ToString()
            {
                string output = $"{operation} {targetVar}";
                foreach (string p in parameters)
                {
                    output += $" {p}";
                }
                return output;
            }
            public static string[] varType(string variable) {
                //LITERALLY JUST A NUMBER
                float _;
                if (float.TryParse(variable, out _))
                {
                    return new string[] { variable };
                }                
                // CBUFFERS
                // (\\+|-)?cb(\d+)\[(\d+)\]\.([xyzwrgb]{0,4})
                if (Regex.IsMatch(variable, "(\\+|-)?cb(\\d+)\\[(.+)\\]\\.([xyzwrgba]{0,4})"))
                {
                    Match match = Regex.Match(variable, "(\\+|-)?cb(\\d+)\\[(.+)\\]\\.([xyzwrgba]{0,4})");
                    int res;
                    if (int.TryParse(match.Groups[3].Value, out res)) {
                        //cbuffer with constant index
                        return new string[] { match.Groups[1].Value, $"cb{match.Groups[2].Value}[{res}]", match.Groups[4].Value };
                    }
                    else
                    {
                        //cbuffer with dynamic index
                        return new string[] { match.Groups[1].Value, $"cb{match.Groups[2]}", match.Groups[4].Value, match.Groups[3].Value };
                    }                    
                }
                else if (Regex.IsMatch(variable, "t(\\d)\\.([xyzwrgba]{0,4})"))
                {
                    Match match = Regex.Match(variable, "t(\\d)\\.([xyzwrgba]{0,4})");
                    return new string[] { match.Groups[1].Value, match.Groups[2].Value };
                }
                else if (Regex.IsMatch(variable, "(\\+|-)?((?:r|v|o)\\d+)\\.([xyzwrgba]{0,4})"))
                {
                    //float variable
                    Match match = Regex.Match(variable, "((?:r|v|o)\\d+)\\.([xyzwrgba]{0,4})");
                    return new string[] { match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value };
                }
                else if (Regex.IsMatch(variable, "l\\(((?:[+-]?\\d(?:\\.\\d+)?(?:, )?)+)\\)"))
                {
                    //float constant
                    Match match = Regex.Match(variable, "l\\(((?:[+-]?\\d(?:\\.\\d+)?(?:, )?)+)\\)");
                    return new string[] { match.Groups[1].Value, match.Groups[2].Value };
                }
                else
                {
                    return null;
                }
            }
        }
        private string createIdentifier(string line_id = "")
        {            
            var_index++;
            return $"NODE_{line_id}_{var_index}";
        }
        public string ToNodeScript() {
            ReadASM();
            return bpy.ToString();
        }
        private void ReadASM() { 
            StringReader reader = new StringReader(rawASM);
            string line = reader.ReadLine();
            //Skip comments
            while ((line != null && line.StartsWith("//")) || line == null) {
                if (line == null) { line = ""; }
                bpy.AppendLine($"#{line.Substring(2)}");
                line = reader.ReadLine();
            }
            line = reader.ReadLine(); //Skip shader type/version line
            ////First line will always be vs_x_y or ps_x_y
            //if (line.StartsWith("vs")) 
            //    vertexShader = true;
            //else
            //    vertexShader = false;

            Dictionary<string, string> inputs = new Dictionary<string, string>();
            Dictionary<string, string> outputs = new Dictionary<string, string>();
            while (line != null && line.StartsWith("dcl")) {
                if (line.StartsWith("dcl_constantbuffer")) {
                    Match match = Regex.Match(line, "[Cc][Bb](\\d+)\\[(\\d+)\\]");
                    cbuffers.Add($"cb{match.Groups[1].Value}", int.Parse(match.Groups[2].Value));
                }
                else if (line.StartsWith("dcl_input_sgv"))
                {
                    Match match = Regex.Match(line, "(v\\d).([xyzw]{0,4}), (\\S+)");
                    if (match.Groups[3].Value == "vertex_id" || match.Groups[3].Value == "instance_id")
                    {
                        inputs.Add(match.Groups[3].Value, $"{match.Groups[1].Value}.x");
                    }
                    else {
                        foreach (char d in match.Groups[2].Value)
                        {
                            inputs.Add($"{match.Groups[1].Value}.{d}", $"{match.Groups[1].Value}.{d}");
                        }
                    }
                }
                else if (line.StartsWith("dcl_input"))
                {
                    Match match = Regex.Match(line, "(v\\d).([xyzw]{0,4})");
                    foreach (char d in match.Groups[2].Value) {
                        inputs.Add($"{match.Groups[1].Value}.{d}", $"{match.Groups[1].Value}.{d}");
                    }
                }
                else if (line.StartsWith("dcl_output"))
                {
                    Match match = Regex.Match(line, "(o\\d).([xyzw]{0,4})");
                    foreach (char d in match.Groups[2].Value)
                    {
                        outputs.Add($"{match.Groups[1].Value}.{d}", $"{match.Groups[1].Value}.{d}");
                    }
                }                
                line = reader.ReadLine();
            }

            bpy.AppendLine($"SC_shadergroup = bpy.data.node_groups.new({(vertexShader ? "VS" : "PS")}_{material.Hash}, 'ShaderNodeTree')");
            bpy.AppendLine("link = SC_shadergroup.links.new");
            bpy.AppendLine("newNode = SC_shadergroup.nodes.new");
            bpy.AppendLine("variable_dict = {}\n");
            bpy.AppendLine("SC_group_in = SC_shadergroup.nodes.new('NodeGroupInput')");
            bpy.AppendLine("SC_group_in.location = (-200,0)");
            for (int i = 0; i < inputs.Count; i++)
            {
                var pair = inputs.ElementAt(i);
                bpy.AppendLine($"SC_shadergroup.inputs.new('NodeSocketFloat','{pair.Key}')");
                bpy.AppendLine($"variable_dict['{pair.Value}'] = SC_group_in.outputs[{i}]");
            }
            bpy.AppendLine("SC_group_out = SC_shadergroup.nodes.new('NodeGroupOutput')");
            bpy.AppendLine("SC_group_out.location = (400,0)");
            for (int i = 0; i < outputs.Count; i++)
            {
                var pair = outputs.ElementAt(i);
                bpy.AppendLine($"SC_shadergroup.inputs.new('NodeSocketFloat','{pair.Key}')");                
            }
            bpy.AppendLine();
            WriteCbuffers();

            while (line != null && !line.StartsWith("ret ")) { 
                Operation lineOp = new Operation(line);
                string id = createIdentifier();
                string header = "";
                string nodes = "";
                for (int i = 0; i < lineOp.targetVar.Split(".")[1].Length; i++)
                {
                    nodes += formatMap(lineOp, id, i, ref header);
                }
                bpy.AppendLine(header);
                bpy.AppendLine(nodes);
                line = reader.ReadLine();
            }
        }
        private void WriteCbuffers()
        {
            bpy.AppendLine("### CBUFFERS ###");
            // Try to find matches, pixel shader has Unk2D0 Unk2E0 Unk2F0 Unk300 available
            foreach (var cbuffer in cbuffers)
            {
                bpy.AppendLine($"#static float4 {cbuffer.Key}[{cbuffer.Value}]").AppendLine();
                bpy.Append($"{cbuffer.Key} = [");

                dynamic data = null;
                int data_dims = 4;
                if (vertexShader)
                {
                    if (cbuffer.Value == material.Header.Unk90.Count)
                    {
                        data = material.Header.Unk90;
                        data_dims = 1;
                    }
                    else if (cbuffer.Value == material.Header.UnkA0.Count)
                    {
                        data = material.Header.UnkA0;
                        data_dims = 4;
                    }
                    else if (cbuffer.Value == material.Header.UnkB0.Count)
                        data = material.Header.UnkB0;
                    else if (cbuffer.Value == material.Header.UnkC0.Count)
                    {
                        data = material.Header.UnkC0;
                        data_dims = 4;
                    }
                }
                else
                {
                    if (cbuffer.Value == material.Header.Unk2D0.Count)
                    {
                        data = material.Header.Unk2D0;
                        data_dims = 1;
                    }
                    else if (cbuffer.Value == material.Header.Unk2E0.Count)
                    {
                        data = material.Header.Unk2E0;
                        data_dims = 4;
                    }
                    else if (cbuffer.Value == material.Header.Unk2F0.Count)
                        data = material.Header.Unk2F0;
                    else if (cbuffer.Value == material.Header.Unk300.Count)
                    {
                        data = material.Header.Unk300;
                        data_dims = 4;
                    }
                    else
                    {
                        if (material.Header.PSVector4Container.Hash != 0xffff_ffff)
                        {
                            // Try the Vector4 storage file
                            var container = new DestinyFile(PackageHandler.GetEntryReference(material.Header.PSVector4Container));
                            var containerData = container.GetData();
                            var num = containerData.Length / 16;
                            if (cbuffer.Value == num)
                            {
                                var float4S = new List<Vector4>();
                                for (var i = 0; i < containerData.Length / 16; i++)
                                    float4S.Add(containerData.Skip(i * 16).Take(16).ToArray().ToStructure<Vector4>());
                                data = float4S;                                
                            }                            
                        }
                    }
                }


                for (var i = 0; i < cbuffer.Value; i++)
                {
                    switch (data_dims)
                    {
                        case 4:
                            if (data == null)
                                bpy.Append($"(0.0, 0.0, 0.0, 0.0)");
                            else
                            {
                                try
                                {
                                    if (data[i] is Vector4)
                                        bpy.Append($"({data[i].X}, {data[i].Y}, {data[i].Z}, {data[i].W})");
                                    else
                                    {
                                        var x = data[i].Unk00.X; // really bad but required
                                        bpy.Append($"({x}, {data[i].Unk00.Y}, {data[i].Unk00.Z}, {data[i].Unk00.W})");
                                    }
                                }
                                catch (Exception e)
                                { // figure out whats up here, taniks breaks it
                                    bpy.Append($"(0.0, 0.0, 0.0, 0.0)");
                                }
                            }
                            break;
                        case 3:
                            if (data == null)
                                bpy.Append($"(0.0, 0.0, 0.0)");
                            else
                                bpy.Append($"({data[i].Unk00.X}, {data[i].Unk00.Y}, {data[i].Unk00.Z})");
                            break;
                        case 1:
                            if (data == null)
                                bpy.Append($"(0.0)");
                            else
                                bpy.Append($"({data[i].Unk00})");
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    if (i < cbuffer.Value - 1)
                    {
                        bpy.Append(", ");
                    }
                }
                bpy.AppendLine("]");
            }
        }
        private static bool isOperation1D(string operation, int param)
        {
            if (operation.StartsWith("deriv_rtx") || operation.StartsWith("deriv_rty"))
            {
                return param switch
                {
                    -1 => true,
                    0 => false
                };
            }
            switch (operation)
            {
                //case "sample":
                //    return param switch
                //    {
                //        -1 => false,
                //        0 => true, //Should never actually be anything other than a float anyway
                //        1 => false
                //    };
                case "dp2":
                case "dp3":
                case "dp4":
                    return param switch
                    {
                        -1 => true,
                        0 => false,
                        1 => false
                    };
                case "crs":
                    return param switch
                    {
                        -1 => false,
                        0 => false,
                        1 => false
                    };
                case "dst":
                    return param switch
                    {
                        -1 => false,
                        0 => false,
                        1 => false
                    };
                case "length":
                    return param switch
                    {
                        -1 => true,
                        0 => false
                    };
                default:
                    return true;
            }
        }
        private static string[] splitParams(string ParamString, string splitBy = ", ")
        {
            string[] Params = ParamString.Split(splitBy);
            int paraCount = 0;
            List<string> output = new List<string>();
            string temp = "";
            foreach (string param in Params)
            {
                paraCount += param.Count(c => c == '(') - param.Count(c => c == ')');
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
        /// <summary>
        /// Reads the node mappings file and parses it to a set of formattable strings based on opcode
        /// </summary>
        public static void ensureNodeMap()
        {
            if (nodeMap.Count == 0) {
                try
                {
                    IEnumerable<string> maps = File.ReadAllText("nodemappings.py").Split("\n## ").Skip(1);
                    foreach (string map in maps)
                    {
                        string[] lines = map.Split("\n");
                        nodeMap.Add(lines[0], String.Join("\n", lines.Skip(1)));
                    }
                }
                catch (IOException) {
                    return; //File is being used by another thread doing the same thing (probably) (hopefully)
                }                
            }
            return;
        }
        private static string formatMap(Operation operation, string id, int dim, ref string headerRef) {
            if (id.Contains("38")) {
                Console.WriteLine("53!");
            }            
            Dictionary<char, int> dimMap = new Dictionary<char, int>() { { 'x', 0 }, { 'y', 1 }, { 'z', 2 }, { 'w', 3 }, { 'r', 0 }, { 'g', 1 }, { 'b', 2 }, { 'a', 3 } };
            string header = headerRef;
            bool generateHeader = header == "";
            string nodes = ""; //
            bool isTex = false;
            string[][] splitParameters = operation.parameters.Select(p => Operation.varType(p)).ToArray();
            if (operation.operation.StartsWith("ld") || operation.operation.StartsWith("sample"))
            {
                if (generateHeader)
                {
                    //Texture Sampler
                    StringBuilder headerBuilder = new StringBuilder();
                    headerBuilder.AppendLine($"{id}_H_Tex = matnodes.new(\"ShaderNodeTexImage\")");
                    headerBuilder.AppendLine($"{id}_H_Tex.interpolation = {(operation.operation.StartsWith("ld") ? "\'Closest\'" : "\'Linear\'")}");
                    headerBuilder.AppendLine($"{id}_H_Tex.image = §p1");
                    headerBuilder.AppendLine($"{id}_H_Vec = matnodes.new(\"ShaderNodeCombineXYZ\")");
                    headerBuilder.AppendLine($"{id}_H_Vec.inputs[0] = §0_0");
                    headerBuilder.AppendLine($"{id}_H_Vec.inputs[1] = §0_1");
                    headerBuilder.AppendLine($"{id}_H_Tex.inputs[0] = {id}_H_Vec.outputs[0]");
                    headerBuilder.AppendLine($"{id}_H_Split = matnodes.new(\"ShaderNodeSeparateXYZ\")");
                    headerBuilder.AppendLine($"{id}_H_Split.inputs[0] = {id}_H_Tex.outputs[0]");                    
                    header = headerBuilder.ToString();
                }
                isTex = true;
                nodes = nodeMap[operation.operation.StartsWith("ld") ? "ld\r" : "sample\r"];
            }
            else if (nodeMap.ContainsKey(operation.operation + "\r"))
            {
                nodes = nodeMap[operation.operation + "\r"];
            }
            else {
                Console.WriteLine($"Don't know operation {operation.operation + "\r"}");
                return $"#UNKNOWN OPERATION {operation.ToString()}";
            }
            nodes = $"#DIM {dim} OF {operation.ToString()}\n" + nodes;
            nodes = nodes.Replace("§name", $"{id}_{dim}");
            nodes = nodes.Replace("§var", $"variable_dict['{operation.targetVar.Split(".")[0]}.{operation.targetVar.Split(".")[1][dim]}']");                  
            
            for (int i = 0; i < splitParameters.Count(); i++)
            {                
                string VarToNode(string[] p, int ldim = -1)
                {
                    if (ldim == -1)
                        ldim = dim;                    
                    if (isTex)
                    {
                        if (i == 1)
                        {
                            int dimidx = p[1].Length == 1 ? dimMap[p[1][0]] : dimMap[p[1][dim]];
                            if (dimidx == 3) {
                                return $"{id}_H_Tex.outputs[1]";
                            }
                            else
                            {
                                return $"{id}_H_Split.outputs[{dimidx}]";
                            }
                        }
                    }
                    if (p == null) {
                        return $"UNKNOWN AT {i}";
                    }
                    if (p.Length == 1) {
                        //Literally just a float                        
                        return $"basic_val({p[0]})";
                    }
                    if (p.Length == 2)
                    {
                        int res;
                        if (int.TryParse(p[0], out res))
                        {
                            //Texture                            
                            return $"tex_dict[{p[0]}]";
                        }
                        //Float constant                    
                        string[] floats = p[0].Split(",").Select(s => s.Trim()).ToArray();
                        if (isOperation1D(operation.operation, i))
                        {
                            int nthDim = p[1].Trim() == "" ? ldim : dimMap[p[1][ldim]];
                            return $"basic_val({floats[nthDim]})";
                        }
                        else
                        {
                            string outStr = "combine_floats((";
                            for (int j = 0; j < p[1].Length - 1; j++)
                            {
                                outStr += floats[dimMap[p[1][j]]] + ",";
                            }
                            outStr += floats[dimMap[p[1].Last()]] + "))";
                            return outStr;
                        }
                    }
                    else if (p.Length == 3)
                    {
                        int offset = p[1].StartsWith("cb") ? 1 : 0;
                        //Generic Variable
                        string variableOut = $"{p[0+offset]}.{p[1+offset][ldim]}";
                        if (p[0] == "-")
                        {
                            return $"mulNegative(getVarDict('{variableOut}'))";
                        }
                        else
                        {
                            return $"getVarDict('{variableOut}')";
                        }                        
                    }
                    else
                    {
                        //Cbuffer with dynamic index
                        if (generateHeader)
                        {
                            StringBuilder headerAddition = new StringBuilder();
                            //Potentially dangerous assumption that dynamic indexes only use these 3 operators, and will only use one at a time
                            //I *really* don't feel like making a whole equation parser for this, and it'd be kinda stupid if there was some other operator here
                            if (p[3].Contains('+'))
                            {
                                string[] idxParams = p[3].Split('+').Select(s => VarToNode(Operation.varType(s.Trim()), 0)).ToArray();
                                
                                headerAddition.AppendLine($"{id}_H{i}_idx_op = matnodes.new(\"ShaderNodeMath\")");
                                headerAddition.AppendLine($"{id}_H{i}_idx_op.operation = \'ADD\'");
                                headerAddition.AppendLine($"link({idxParams[0]}, {id}_idx_op{i}.inputs[0])");
                                headerAddition.AppendLine($"link({idxParams[1]}, {id}_idx_op{i}.inputs[1])");

                                headerAddition.AppendLine($"{id}_H{i}_out = add_dynamic_cbuffer({id}_idx_op{i}.outputs[0], {p[1]})");
                            }
                            else if (p[3].Contains('-'))
                            {
                                string[] idxParams = p[3].Split('-').Select(s => VarToNode(Operation.varType(s.Trim()), 0)).ToArray();

                                headerAddition.AppendLine($"{id}_H{i}_idx_op = matnodes.new(\"ShaderNodeMath\")");
                                headerAddition.AppendLine($"{id}_H{i}_idx_op.operation = \'SUBTRACT\'");
                                headerAddition.AppendLine($"link({idxParams[0]}, {id}_idx_op{i}.inputs[0])");
                                headerAddition.AppendLine($"link({idxParams[1]}, {id}_idx_op{i}.inputs[1])");

                                headerAddition.AppendLine($"{id}_H{i}_out = add_dynamic_cbuffer({id}_idx_op{i}.outputs[0], {p[1]})");
                            }
                            else if (p[3].Contains('%'))
                            {
                                string[] idxParams = p[3].Split('%').Select(s => VarToNode(Operation.varType(s.Trim()), 0)).ToArray();
                                headerAddition.AppendLine($"{id}_H{i}_idx_op = matnodes.new(\"ShaderNodeMath\")");
                                headerAddition.AppendLine($"{id}_H{i}_idx_op.operation = \'ADD\'");
                                headerAddition.AppendLine($"link({idxParams[0]}, {id}_idx_op{i}.inputs[0])");
                                headerAddition.AppendLine($"link({idxParams[1]}, {id}_idx_op{i}.inputs[1])");

                                headerAddition.AppendLine($"{id}_H{i}_out = add_dynamic_cbuffer({id}_idx_op{i}.outputs[0], {p[1]})");                                
                            }
                            else
                            {
                                string parameter = VarToNode(Operation.varType(p[3].Trim()).ToArray(), 0);
                                headerAddition.AppendLine($"{id}_H{i}_out = add_dynamic_cbuffer({parameter}, {p[1]})");

                            }
                            headerAddition.AppendLine($"{id}_H{i}_out_split = matnodes.new(\"ShaderNodeSeparateXYZ\")");
                            headerAddition.AppendLine($"link({id}_H{i}_out[0], {id}_H{i}_out_split.inputs[0])");
                            header += headerAddition.ToString();
                        }
                        if (dimMap[p[2][ldim]] == 3) {
                            return $"{id}_H{i}_out[1]";
                        }
                        else
                        {
                            return $"{id}_H{i}_out_split.outputs[{dimMap[p[2][ldim]]}]";
                        }
                    }
                }
                string p = VarToNode(splitParameters[i]);
                nodes = nodes.Replace($"§p{i}", p);
                if (isTex)
                {
                    header = header.Replace($"§p{i}", p);
                    header = header.Replace($"§{i}_{dim}", p);
                }
            }
            if (nodes.Contains("§")) {
                throw new ArgumentException($"Insufficient parameters! This operation uses {nodes.Count(c => c == '§') + operation.parameters.Count()} parameters, was only provided {operation.parameters.Count()}");
            }
            headerRef = header;
            return nodes + "\n";
        }
    }
}
