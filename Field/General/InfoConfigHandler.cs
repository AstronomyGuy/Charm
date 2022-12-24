using System.Text.Json;
using System.Text.Json.Nodes;
using Field.Models;
using Field.Statics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Field.Textures;

namespace Field.General;

public class InfoConfigHandler {
    
    private readonly JsonObject _config = new JsonObject();
    private static List<TagHash> existingHashes = new List<TagHash>();
    public void AddMaterial(Material material) {
        if(!material.Hash.IsValid() && !existingHashes.Contains(material.Hash))
            return;
        existingHashes.Add(material.Hash);

        var materialNode = new JsonObject();
        var shaderInfoTable = new JsonObject();
        
        if(material.Header.PixelShader != null) {
            var shaderData = new JsonObject();
            if(CreateShaderData(shaderData, material, Material.ShaderType.Pixel))
                WriteTextureMeta(material.GetTextures(Material.ShaderType.Pixel));
            shaderInfoTable["pixelShader"] = shaderData;
        }
        if(material.Header.VertexShader != null) {
            var shaderData = new JsonObject();
            if(CreateShaderData(shaderData, material, Material.ShaderType.Vertex))
                WriteTextureMeta(material.GetTextures(Material.ShaderType.Vertex));
            shaderInfoTable["vertexShader"] = shaderData;
        }
        if(material.Header.ComputeShader != null) {
            var shaderData = new JsonObject();
            if(CreateShaderData(shaderData, material, Material.ShaderType.Compute))
                WriteTextureMeta(material.GetTextures(Material.ShaderType.Compute));
            shaderInfoTable["computeShader"] = shaderData;
        }

        materialNode["textureFormat"] = Material.GetTextureExtension();
        
        if (shaderInfoTable.Count > 0)
            materialNode["shaders"] = shaderInfoTable;

        GetJsonObject(_config, "materials")[material.Hash] = materialNode;
    }
    
    public void AddPart(Part part, string partName) {
        GetJsonObject(_config, "parts")[partName] = part.Material.Hash.GetHashString();
    }

    public void AddType(string type)
    {
        _config["Type"] = type;
    }

    public void SetMeshName(string meshName)
    {
        _config["MeshName"] = meshName;
    }

    public void SetUnrealInteropPath(string interopPath) {
        var value = new string(interopPath.Split("\\Content").Last().ToArray()).TrimStart('\\');
        _config["unrealPath"] = value == "" ? "Content" : value;
    }

    public void AddInstance(string modelHash, float scale, Vector4 quatRotation, Vector3 translation) {
        if (!GetJsonObject(_config, "instances").ContainsKey(modelHash))
            GetJsonObject(_config, "instances")[modelHash] = new JsonArray();
        GetJsonObject(_config, "instances", modelHash).AsArray().Add(new JsonObject {
            ["translation"] = new JsonArray(translation.X, translation.Y, translation.Z),
            ["rotation"] = new JsonArray(quatRotation.X, quatRotation.Y, quatRotation.Z, quatRotation.W),
            ["scale"] = scale
        });
    }

    public void AddStaticInstances(List<D2Class_406D8080> instances, string staticMesh) {
        foreach (var instance in instances)
            AddInstance(staticMesh, instance.Scale.X, instance.Rotation, instance.Position);
    }
    
    public void AddCustomTexture(string material, int index, TextureHeader texture) {
        GetJsonObject(_config, "materials", material, "shaders", "pixelShader", "indices")[index] = texture.Hash.GetHashString();
        GetJsonObject(_config, "materials", material, "textures")[texture.Hash] = new JsonObject {
            ["srgb"] = texture.IsSrgb(),
            ["volume"] = texture.IsVolume(),
            ["cubemap"] = texture.IsCubemap()
        };
    }
    
    public void WriteToFile(string path)
    {
        // If theres only 1 part, we need to rename it + the instance to the name of the mesh (unreal imports to fbx name if only 1 mesh inside)
        if (_config.ContainsKey("parts") && _config["parts"]!.AsObject().Count == 1) {
            var part = GetJsonObject(_config, "parts")[0];
            var meshName = _config["assetHash"]!.ToString();
            //I'm not sure what to do if it's 0, so I guess I'll leave that to fix it in the future if something breakes.
            if (_config.ContainsKey("instances") && _config["instances"]!.AsArray().Count > 0) {
                var instance = GetJsonObject(_config, "instances")[0];
                GetJsonObject(_config, "instances")[meshName] = instance;
            }
            GetJsonObject(_config, "parts")[meshName] = part;
        }

        
        //im not smart enough to have done this, so i made an ai do it lol
        //this just sorts the "instances" part of the cfg so its ordered by scale
        //makes it easier for instancing models in Hammer/S&Box

        var sortedDict = new ConcurrentDictionary<string, ConcurrentBag<JsonInstance>>();

        // Use LINQ's OrderBy method to sort the values in each array
        // based on the "Scale" key. The lambda expression specifies that
        // the "Scale" property should be used as the key for the order.
        foreach (var keyValuePair in (ConcurrentDictionary<string, ConcurrentBag<JsonInstance>>)_config["Instances"])
        {
            var array = keyValuePair.Value;
            var sortedArray = array.OrderBy(x => x.Scale);

            // Convert the sorted array to a ConcurrentBag
            var sortedBag = new ConcurrentBag<JsonInstance>(sortedArray);

            // Add the sorted bag to the dictionary
            sortedDict.TryAdd(keyValuePair.Key, sortedBag);
        }

        // Finally, update the _config["Instances"] object with the sorted values
        _config["Instances"] = sortedDict;

        
        string s = JsonConvert.SerializeObject(_config, Formatting.Indented);
        if (_config.ContainsKey("MeshName"))
        {
            File.WriteAllText($"{path}/{_config["MeshName"]}_info.cfg", s);
        }
    }
    
    private void WriteTextureMeta(List<D2Class_CF6D8080> textures) {
        var rootNode = GetJsonObject(_config, "textures")?? new JsonObject();
        foreach (var e in textures) {
            if (ContainsKey(rootNode, e.Texture.Hash))
                continue;
            var meta = new JsonObject {
                ["srgb"] = e.Texture.IsSrgb(),
                ["volume"] = e.Texture.IsVolume(),
                ["cubemap"] = e.Texture.IsCubemap()
            };
            rootNode[e.Texture.Hash] = meta;
        }
    }
    
    // Write any relevant properties for this shader. Texture indices, whether it uses vertex color, perhaps eventually even cbuffer info.
    private static bool CreateShaderData(JsonNode shaderNode, Material mat, Material.ShaderType type) {
        if(type == Material.ShaderType.Vertex)
            shaderNode["useVertexData"] = true; // Obviously figure this out properly, but will come in handy in the long run.
        List<D2Class_CF6D8080> textures = mat.GetTextures(type);
        if (textures.Count > 0) {
            var textureMeta = new JsonObject();
            foreach(var e in textures)
                textureMeta[e.TextureIndex.ToString()] = e.Texture.Hash.ToString();
            shaderNode["indices"] = textureMeta;
            return true;
        }
        return false;
    }

    private static JsonObject GetJsonObject(JsonObject root, params dynamic[] keys) {
        var current = root;
        foreach (var key in keys) {
            if (!ContainsKey(current, key))
                current[key] = new JsonObject();
            current = current[key];
        }
        return current;
    }
    private static bool ContainsKey(JsonObject json, string key)
    {
        //Have to do this stupid stuff because the Json object is a dictionary and apparently it's being modified concurrently by *something*
        try
        {
            var temp = json[key];
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
        return true;
    }
}
