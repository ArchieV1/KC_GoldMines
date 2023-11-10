using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Harmony;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

public class ModdingFrameworkNames
{
    public static class Methods
    {
        /// <summary>
        /// Send by mods to register themselves, menu returns a ModConfig with all values assigned (The ResourceType they have been assigned):
        /// Parameter: ModConfigME
        /// Return value: ModConfigME
        /// </summary>
        public const string RegisterMod = "RegisterMod";
    }
    public static class Objects
    {
        /// <summary>
        /// The name of the gameobject the modding engine is attached to
        /// </summary>
        public const string ModdingFrameworkName = "ModdingFramework";
    }
}

public class ModdingFrameworkBootstrapper : MonoBehaviour
{
    // Code modified from Zat.ModMenu.API
    // Have a copy of this file in your mod's files

    public IMCPort port;
    private static string cat = "ModdingFrameworkBootstrapper";

    public static void Register(ModConfigME configMe, UnityAction<ModdingFrameworkProxy, ModConfigME> onRegistered,
        UnityAction<Exception> onError,
        int retries = 30, float delay = 1f)
    {
        ULogger.Log(cat, "Register");
        var host = new GameObject($"ModdingFramework::Client::{Guid.NewGuid().ToString()}");
        ULogger.Log(cat, host);
        
        var bootstrapper = host.AddComponent<ModdingFrameworkBootstrapper>();

        bootstrapper.InitPort(configMe, onRegistered, onError, retries, delay);
    }

    private void InitPort(ModConfigME configMe, UnityAction<ModdingFrameworkProxy, ModConfigME> onRegistered,
        UnityAction<Exception> onError,
        int retries = 30, float delay = 1f)
    {
        ULogger.Log(cat, "InitPort");
        StartCoroutine(WaitForTarget(configMe, onRegistered, onError, retries, delay));
    }

    private IEnumerator WaitForTarget(ModConfigME configMe,
        UnityAction<ModdingFrameworkProxy, ModConfigME> onRegistered,
        UnityAction<Exception> onError,
        int retries = 30, float delay = 1f)
    {
        ULogger.Log(cat, $"WaitForTarget with {retries} retries");
        port = gameObject.AddComponent<IMCPort>();
        int _retries = retries;
        GameObject ModdingFramework = null;
        do
        {
            ULogger.Log(cat, $"Waiting for {delay/Time.timeScale} seconds"); // If div0 the wait will not work anyway
            yield return new WaitForSeconds(delay);
            ULogger.Log(cat, $"Waited {delay/Time.timeScale} seconds");
            ULogger.Log(cat, "Starting `Find` operation");
            ModdingFramework = GameObject.Find(ModdingFrameworkNames.Objects.ModdingFrameworkName);
            ULogger.Log(cat, "Finished `Find` operation");
            ULogger.Log(cat, $"ModdingFramework found: {ModdingFramework != null}");
        } while (_retries-- > 0 && ModdingFramework == null);

        ULogger.Log(cat, $"Finished do/while with {_retries} retires left");
        if (ModdingFramework == null)
        {
            onError(new Exception(
                $"WaitForTarget: Missing ModdingFramework GameObject \"{ModdingFrameworkNames.Objects.ModdingFrameworkName}\"!"));
            ULogger.Log(cat, "ModdingFramework is still null. Failed");
            yield break;
        }

        yield return RegisterOnTarget(configMe, onRegistered, onError, retries, delay);
    }

    private IEnumerator RegisterOnTarget(ModConfigME configMe,
        UnityAction<ModdingFrameworkProxy, ModConfigME> onRegistered,
        UnityAction<Exception> onError, int retries = 30, float delay = 1f)
    {
        ULogger.Log(cat, "RegisterOnTarget");
        yield return new WaitForSeconds(delay);
        try
        {
            ULogger.Log(cat, $"Trying RPC. Reties = {retries}");
            port.RPC<ModConfigME, ModConfigME>(ModdingFrameworkNames.Objects.ModdingFrameworkName,
                ModdingFrameworkNames.Methods.RegisterMod, configMe, 3f, (modConfig) =>
                {
                    ULogger.Log(cat, $"Start of callback");
                    var proxy = gameObject.AddComponent<ModdingFrameworkProxy>();
                    proxy.port = port;
                    proxy.configMe = configMe;
                    proxy.Setup();
                    onRegistered?.Invoke(proxy, modConfig);
                    ULogger.Log(cat, $"Finished callback. About to destroy {this}");
                    Destroy(this);
                },
                (ex) =>
                {
                    ULogger.Log(cat, $"EXCEPTION in {ModdingFrameworkNames.Methods.RegisterMod}. Retries = {retries}");
                    ULogger.Log(cat, $"Retries = {retries}");
                    ULogger.Log(cat, ex);
                    if (retries > 0)
                    {
                        ULogger.Log(cat, $"As retries ({retries}) > 0 trying again");
                        StartCoroutine(RegisterOnTarget(configMe, onRegistered, onError, retries - 1, delay));
                    }
                    else
                    {
                        onError?.Invoke(ex);
                    }
                }
            );
        }
        catch (Exception ex)
        {
            ULogger.Log(cat, "RPC failed with exception:");
            ULogger.Log(cat, ex);

            if (retries > 0)
            {
                ULogger.Log(cat, $"As retries ({retries}) > 0 trying again");
                StartCoroutine(RegisterOnTarget(configMe, onRegistered, onError, retries - 1, delay));

            }
            else
            {
                onError?.Invoke(ex);
            }
        }
    }

    /// <summary>
    /// The ModMenu API client used to interact with the ModMenu
    /// </summary>
    public class ModdingFrameworkProxy : MonoBehaviour
    {
        /// <summary>
        /// IMCPort to communicate through; set by ModdingFrameworkBootstrapper
        /// </summary>
        public IMCPort port;

        /// <summary>
        /// ModConfigME associated with this proxy, mirrors the state of the configMe in the central ModMenu
        /// </summary>
        public ModConfigME configMe;

        /// <summary>
        /// Called once by ModSettingsBootstrapper to set up various listeners on the port, effectively registering callbacks for events raised by the ModMenu
        /// </summary>
        internal void Setup()
        {

        }
    }
}

#region Useful classes needed for the methods

public class ModConfigME
{
    public string ModName;
    public string Author;
    public GeneratorBase[] Generators;
    public AssetBundle AssetBundle;
    public bool Registered = false;
    
    public override string ToString()
    {
        string generatorNames = Generators.Aggregate("", (current, generator) => current + generator);

        return $"{ModName} by {Author}. Generators included are {generatorNames}";
    }
}

public class ResourceTypeBase
{
    public string Name;
    public GameObject Model;
    public string ModelURI;
    public ResourceType ResourceType; // Main.RegisterResource() assigns this
    public bool ModelLoaded => Model != null;
    // TODO: CanBeDestroyedBy (Eg Rock destroyer, axe tool)
    
    /// <summary>
    /// The ResourceTypeBase to appear on the map.
    /// Generated using a Generator which is part of a ModConfigME
    /// </summary>
    /// <param name="name">The name of the ResourceTypeBase</param>
    /// <param name="model">The model to be used on the map</param>
    public ResourceTypeBase(string name, GameObject model)
    {
        Name = name;
        Model = model;
        Debugging.Log($"ResourceTypeBase", $"Model for {Name} loaded: {ModelLoaded}");
    }

    public ResourceTypeBase(string name, string modelURI)
    {
        Name = name;
        ModelURI = modelURI;
        Debugging.Log($"ResourceTypeBase", $"Model for {Name} loaded: {ModelLoaded}");
    }

    public override string ToString()
    {
        return $"{Name}\n" +
               $"Model loaded: {ModelLoaded}\n" +
               $"ModelURI: {ModelURI}\n" +
               $"ResourceType = {ResourceType}";
    }
}

public class GeneratorBase
{
    public readonly string Name; // The name of this generator
    public readonly ResourceTypeBase[] Resources; // The resources to be used in .Generate()

    /// <summary>
    /// Create a ResourceTypeBase with multiple resources contained within it.
    /// Use this if generation code required multiple 
    /// </summary>
    /// <param name="resourceTypeBases">The list of resources this generatorBase will use</param>
    public GeneratorBase(ResourceTypeBase[] resourceTypeBases)
    {
        Debugging.Log($"Generation",$"Creating {GetType()}.\n" +
                                    $"It contains the resources:\n\t{resourceTypeBases.ToList().Join(null, "\n\t")}");

        Name = GetType().ToString(); // The name of the class derived from this
        Resources = resourceTypeBases;

        Debugging.Log($"Generation",$"Created {Name}");
    }
    
    /// <summary>
    /// Generates the resources in this generatorBase into the, already generated, world.
    /// Look at World.GenLand() for or http://www.ArchieV.uk/GoldMines for examples
    /// Please use World.inst.seed, SRand, and this.RandomStoneState for randomness.
    /// </summary>
    /// <param name="world"></param>
    /// <returns>Returns true if implemented</returns>
    public virtual bool Generate(World world)
    {
        return false;
    }

    public override string ToString()
    {
        return $"{Name}";
    }

    public string AllInfo()
    {
        StringBuilder str = new StringBuilder($"Generator: {Name}");
        str.Append("Resource Types:");
        foreach (ResourceTypeBase x in Resources)
        {
            str.Append($"{x}");
        }
        
        return str.ToString();
    }

    public static void TryPlaceResource(Cell cell, ResourceTypeBase resourceTypeBase,
        bool storePostGenerationType = false,
        bool deleteTrees = false,
        Vector3 localScale = new Vector3(),
        Vector3 rotate = new Vector3())
    {
        try
        {
            Debugging.Log($"Generation", $"Placing {resourceTypeBase} at {cell.x}, {cell.z}");;
            cell.Type = resourceTypeBase.ResourceType;
            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(resourceTypeBase.Model);

            if (storePostGenerationType) cell.StorePostGenerationType();
            if (deleteTrees) TreeSystem.inst.DeleteTreesAt(cell);

            gameObject.transform.position = cell.Position;
            if (localScale != new Vector3()) gameObject.transform.localScale = localScale;
            if (rotate != new Vector3()) gameObject.transform.Rotate(rotate); // 0 or 180 for Z axis

            if (cell.Models == null)
            {
                cell.Models = new List<GameObject>();
            }

            cell.Models.Add(gameObject);
            Debugging.Log($"Generation","Placed");
        }
        catch (Exception e)
        {
            throw new PlacementFailedException(e.ToString());
        }
    }

    protected static bool ClearCell(Cell cell, bool clearCave = true)
    {
        if (cell.Models != null)
        {
            foreach (GameObject model in cell.Models)
            {
                UnityEngine.Object.Destroy(model.gameObject);
            }

            cell.Models.Clear();
        }
        
        if (clearCave) ClearCaveAtCell(cell);
        
        return true;
    }

    protected static bool ClearCaveAtCell(Cell cell)
    {
        GameObject caveAt = World.inst.GetCaveAt(cell);
        if (caveAt != null)
        {
            UnityEngine.Object.Destroy(caveAt);
        }

        return true;
    }
    
    #region Useful Generation Methods
    // Places a small amount of "type" at x/y with some unusable stone around it
    // PlaceSmallStoneFeature(int x, int y, ResourceType type)

    // Places a large amount of "type" at x/y with some unusable stone around it
    // PlaceLargeStoneFeature(int x, int y, ResourceType type)

    // Sets cell at x/y's type to be "type".
    // Deleted the trees at that location
    // Calls SetupStoneForCell(Cell cell) to instantiate the model
    // PlaceStone(int x, int y, ResourceType type)

    // Instantiates the model and 
    // gameObject = UnityEngine.Object.Instantiate<GameObject>(CORRECT_MODEL); // Instantiate the mode
    // gameObject.GetComponent<MeshRenderer>().sharedMaterial = this.uniMaterial[0]; // Apply the mesh. Always [0]. What is uniMaterial
    // gameObject.transform.parent = this.resourceContainer.transform; // Smth to do with moving it relative to the parent but not in the world
    // SetupStoneForCell(Cell cell)
    #endregion
}

//TODO (NOT IMPLEMENTED)
#region Default generators 
/// <summary>
/// Use this Generator if you wish your modded resource to spawn like vanilla Stone does. Requires two resourceTypes
/// </summary>
public class StoneLikeGenerator : GeneratorBase
{
    public StoneLikeGenerator(ResourceTypeBase[] resourceTypeBases) : base(resourceTypeBases)
    {
    }

    /// <summary>
    /// Stone generation requires two resources.
    /// A good resource and a dud resource. Can use vanilla resources for either. Just use their data in your ResourceTypeBase implementation
    /// </summary>
    /// <param name="world"></param>
    /// <returns></returns>
    public override bool Generate(World world)
    {
        if (Resources.Length <= 2)
        {
            return false;
        }
        // Do StoneLike generation


        return base.Generate(world);
    }
}

public class WoodLikeGenerator : GeneratorBase
{
    public WoodLikeGenerator(ResourceTypeBase[] resourceTypeBases) : base(resourceTypeBases)
    {
    }

    public override bool Generate(World world)
    {
        return base.Generate(world);
    }
}

public class IronLikeGenerator : GeneratorBase
{
    public IronLikeGenerator(ResourceTypeBase[] resourceTypeBases) : base(resourceTypeBases)
    {
    }

    public override bool Generate(World world)
    {
        return base.Generate(world);
    }
}

public class FishLikeGenerator : GeneratorBase
{
    public FishLikeGenerator(ResourceTypeBase[] resourceTypeBases) : base(resourceTypeBases)
    {
    }

    public override bool Generate(World world)
    {
        return base.Generate(world);
    }
}

public class EmptyCaveLikeGenerator : GeneratorBase
{
    public EmptyCaveLikeGenerator(ResourceTypeBase[] resourceTypeBases) : base(resourceTypeBases)
    {
    }

    public override bool Generate(World world)
    {
        return base.Generate(world);
    }
}

public class WitchHutLikeGenerator : GeneratorBase
{
    public WitchHutLikeGenerator(ResourceTypeBase[] resourceTypeBases) : base(resourceTypeBases)
    {
    }

    public override bool Generate(World world)
    {
        return base.Generate(world);
    }
}

#endregion
#endregion

public class PlacementFailedException : Exception
{
    public PlacementFailedException()
    {
    }

    public PlacementFailedException(string message) : base(message)
    {
    }

    public PlacementFailedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected PlacementFailedException([NotNull] SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}

public static class Tools
{
    public static Cell[] GetCellData(World world)
    {
        return (Cell[]) GetPrivateField(world, "cellData");
    }

    public static void SetCellData(World world, Cell cell, int i)
    {
        // Get a copy of cellData and edit it
        Cell[] newCellData = GetCellData(world);
        newCellData[i] = cell;
        
        SetPrivateField(world, "cellData", newCellData);
    }

    public static void SetCellData(World world, Cell[] newCellData)
    {
        SetPrivateField(world, "cellData", newCellData);
    }

    /// <summary>
    /// Sets a private field using reflection
    /// </summary>
    /// <param name="instance">The object containing the private field</param>
    /// <param name="fieldName">The name of the private field</param>
    /// <param name="newValue">The new value the field will hold</param>
    public static void SetPrivateField(object instance, string fieldName, object newValue)
    {
        Type type = instance.GetType();
        PropertyInfo cellDataProperty = type.GetProperty(fieldName);
        
        // Set fieldName's value to the NewValue
        cellDataProperty?.SetValue(type, newValue, null);
    }

    /// <summary>
    /// Uses GetPrivateWorldField to get randomStoneState
    /// </summary>
    /// <param name="world">The instance of World randomStoneState will be read from</param>
    /// <returns>A copy of randomStoneState from the given world</returns>
    public static System.Random GetRandomStoneState(World world)
    {
        return (System.Random) GetPrivateWorldField(world, "randomStoneState");
    }

    /// <summary>
    /// Gets a private field from a given World instance with the given FieldName
    /// </summary>
    /// <param name="world"></param>
    /// <param name="fieldName"></param>
    /// <param name="fieldIsStatic"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Thrown if fieldName does not exist in the given context (Static/Instance)</exception>
    public static object GetPrivateWorldField(World world, string fieldName, bool fieldIsStatic = false)
    {
        return GetPrivateField(world, fieldName, fieldIsStatic);
    }

    /// <summary>
    /// Get value of a private field
    /// </summary>
    /// <param name="instance">The instance that contains the private field</param>
    /// <param name="fieldName">The private field name</param>
    /// <param name="fieldIsStatic">Is the field static</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static object GetPrivateField(object instance, string fieldName, bool fieldIsStatic = false)
    {
        string exceptionString =
            $"{fieldName} does not correspond to a private {(fieldIsStatic ? "static" : "instance")} field in {instance}";
        object result;
        try
        {
            Type type = instance.GetType();

            FieldInfo fieldInfo = fieldIsStatic ? type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic) : type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            
            if (fieldInfo == null) throw new ArgumentException(exceptionString);
            
            result = fieldInfo.GetValue(instance);
        }
        catch (Exception e)
        {
            ULogger.Log(e);
            throw new ArgumentException(exceptionString);
        }

        return result;
    }
    
    /// <summary>
    /// Returns an array of Methods above the currently called method in the method calling stack
    /// </summary>
    /// <returns></returns>
    [STAThread]
    public static IEnumerable<string> GetCallingMethods()
    {
        StackTrace stackTrace = new StackTrace();
        StackFrame[] stackFrames = stackTrace.GetFrames();

        var list = new List<string>();
        if (stackFrames == null) return list;
        list.AddRange(stackFrames.Select(frame => frame.GetMethod().Name));
        list.Remove(stackFrames[0].GetMethod().Name);
        
        return list;
    }

    public static string GetCallingMethodsAsString()
    {
        return string.Join(", ", GetCallingMethods().Where(u => u != "GetCallingMethodsAsString"));
    }

    public static bool IsEncodable(object obj)
    {
        try
        {
            JsonConvert.SerializeObject(obj, IMCPort.serializerSettings);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsDecodable(string str)
    {
        if (str == null) return false;
        
        try
        {
            DecodeObject(str);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsJSONable(object obj)
    {
        try
        {
            // If Encode/Decode/Encode == Encode then it can be sent and received without issue
            return EncodeObject( DecodeObject(EncodeObject(obj))) ==
                   EncodeObject(obj);
        }
        catch
        {
            return false;
        }
    }
    
    public static string EncodeObject(object obj, JsonSerializerSettings settings = null)
    {
        if (settings == null)
        {
            settings = IMCPort.serializerSettings;
        }

        return JsonConvert.SerializeObject(obj, settings);
    }

    public static object DecodeObject(string str, JsonSerializerSettings settings = null)
    {
        if (settings == null)
        {
            settings = IMCPort.serializerSettings;
        }

        return JsonConvert.DeserializeObject(str, settings);
    }
}

public static class ULogger
{
    public static void Log(string category, string message)
    {
        Console.WriteLine("[ULOGGER|{2}] {0, 25} | {1}", category, message, DateTime.Now);
    }
    
    public static void Log(string message)
    {
        Log("", message);
    }
    
    public static void Log(object category, object message)
    {
        Log(category.ToString(), message.ToString());
    }

    public static void Log(object message)
    {
        Log("", message);
    }
}