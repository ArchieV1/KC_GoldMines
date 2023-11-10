using System;
using System.Collections;
using UnityEngine;
using Harmony;
using Valve.VR.InteractionSystem;
using Object = System.Object;

public class GoldMines : MonoBehaviour
{
    public KCModHelper Helper;
    public static GoldMines Inst;

    public IMCPort port;
    
    ModConfigME registeredMod;

    public GoldMines()
    {
        Inst = this;
    }
    
    #region Startup
    public void Preload(KCModHelper helper)
    {
        Helper = helper;
        Debugging.Helper = Helper;
        Debugging.Active = true;
        
        Helper.Log($"Starting GoldMines at {DateTime.Now}");
        Helper.Log($"===============Preload===============");

        // Load harmony
        var harmony = HarmonyInstance.Create("uk.ArchieV.GoldMines");
        harmony.PatchAll();
    }
    
    public void PreScriptLoad(KCModHelper helper)
    {
        Helper.Log($"===============PreScriptLoad===============");
    }

    public void SceneLoaded(KCModHelper helper)
    {
        Helper.Log(($"===============SceneLoaded==============="));
    }

    private IEnumerator WaitSeconds(float time, ModConfigME goldMinesMod)
    {
        Helper.Log($"Starting wait for {time}s");
        yield return new WaitForSeconds(time);
        
        // List all gameobjects
        Object[] allMonobehvaiour = FindObjectsOfType(typeof(MonoBehaviour));
        foreach(Object obj in allMonobehvaiour)
        {
            Helper.Log(obj.ToString());
        }
        
        // Try and do RPC
        port.RPC<ModConfigME, ModConfigME>(ModdingFrameworkNames.Objects.ModdingFrameworkName,
            ModdingFrameworkNames.Methods.RegisterMod, goldMinesMod, 5f, (response) =>
            {
                Helper.Log(
                    $"{ModdingFrameworkNames.Objects.ModdingFrameworkName}:{ModdingFrameworkNames.Methods.RegisterMod} returned {response}!");
                registeredMod = response;
            }, (exception) =>
            {
                Helper.Log($"Failed to call method. {exception.Message}");
            });
        
        Helper.Log($"Finished {time}s wait");
    }
    
    public void Start()
    {
        Helper.Log($"===============Start===============");
        // BUNDLE NAME WILL BE LOWER CASE
        // ASSET NAME DEPENDS ON THE MODDER
        // In this case: GoldDeposit (Shader) and golddeposit1 (Model)

        // These two will be supplied by the 3rd party mod
        AssetBundle goldMinesAssetBundle = KCModHelper.LoadAssetBundle($"{Helper.modPath}", "golddeposit");
        //GameObject goldDepositModel = goldMinesAssetBundle.LoadAsset("assets/workspace/golddeposit1.prefab") as GameObject;
        //ResourceTypeBase goldDeposit = new ResourceTypeBase("GoldDeposit", goldDepositModel);
        ResourceTypeBase goldDeposit = new ResourceTypeBase("GoldDeposit", "assets/workspace/golddeposit1.prefab");

        Helper.Log("Creating goldMinesMod ModConfigME: (Registering Generators and ResourceTypeBases)");
        ModConfigME goldMinesMod = new ModConfigME
        {
            Author = "ArchieV1",
            ModName = "GoldMines",
            AssetBundle = goldMinesAssetBundle,
            Generators = new GeneratorBase[]
            {
                new GoldDepositGenerator(new[] {goldDeposit}),
            }
        };

        Helper.Log($"Gold mines asset bundle: {goldMinesAssetBundle}");
        Helper.Log(
            $"goldDeposit model exists: {goldMinesAssetBundle.LoadAsset("assets/workspace/golddeposit1.prefab") as GameObject != null}");
        Helper.Log($"goldDeposit model loaded: {goldDeposit.Model != null}");
        Helper.Log($"goldDeposit model uri: {goldDeposit.ModelURI}");

        Helper.Log("Serialisation testing stuff");
        Helper.Log($"IsEncodable {Tools.IsEncodable(goldMinesMod)}");
        Helper.Log($"ENCODED: {Tools.EncodeObject(goldMinesMod)}");
        Helper.Log($"ENCODED THEN DECODED THEN ENCODED");
        Helper.Log($"{Tools.EncodeObject(Tools.DecodeObject(Tools.EncodeObject(goldMinesMod)))}");
        Helper.Log($"JSONABLE: {Tools.IsJSONable(goldMinesMod)}");

        Helper.Log("Finished testing serialisation stuff");
        
        // Initiate port
        transform.name = "GoldMines";
        gameObject.name = "GoldMines";
        port = gameObject.AddComponent<IMCPort>();
        
        Helper.Log($"Registering GoldMines mod");

        Helper.Log($"Target: {ModdingFrameworkNames.Objects.ModdingFrameworkName}");
        Helper.Log($"Method: {ModdingFrameworkNames.Methods.RegisterMod}");
        
        // Sends the message after waiting a few secs
        StartCoroutine(WaitSeconds(5, goldMinesMod));
        


        // ModdingFrameworkBootstrapper.Register(goldMinesMod, (proxy, saved) =>
        // {
        //     //TODO Determine if proxy is needed. It has stuff to do with ports so maybe.
        //     OnModRegistered(proxy, saved);
        //     Helper.Log($"Proxy: {proxy.configMe}");
        //     Helper.Log($"Mod is registered: {goldMinesMod.Registered}");
        //     Helper.Log("Registered GoldMines mod");
        // }, (ex) =>
        // {
        //     ULogger.Log("GoldMines", $"Failed to register mod: {ex.Message}");
        //     ULogger.Log("GoldMines", ex.StackTrace);
        // });
    }

    private void OnModRegistered(ModdingFrameworkBootstrapper.ModdingFrameworkProxy proxy, ModConfigME saved)
    {
        // Do something when mod is installed with the bootstrapper
        // IDK get the ResourceTypeBase.ResourceType values and store them?
        Helper.Log("Starting OnModRegistered");
        
        Helper.Log("Done OnModRegistered");
        ULogger.Log("GoldMines", "OnModRegistered");
    }

    #endregion
    
    public void Update()
    {
        
    }
    
    public class GoldDepositGenerator : GeneratorBase
    {
        public GoldDepositGenerator(ResourceTypeBase[] resourceTypeBases) : base(resourceTypeBases)
        {
        }

        public override bool Generate(World world)
        {
            KCModHelper helper = Inst.Helper;
            Console.WriteLine("Generating GoldDeposit");
            Console.WriteLine($"{this}");
            Resources.ForEach(x => Console.WriteLine(x));
            System.Random randomStoneState = Tools.GetRandomStoneState(world);
        
            helper.Log("Populating list");
            // Populate list of cells to become GoldDeposits
            int numDeposits = world.GridWidth;
            Cell[] cells = new Cell[numDeposits];
        
            // for (int cell = 0; cell < cells.Length - 1; cell++)
            // {
            //     int x = SRand.Range(0, world.GridWidth, randomStoneState);
            //     int z = SRand.Range(0, world.GridHeight, randomStoneState);
            //     cells[cell] = world.GetCellData(x, z);
            // }

            for (int x = 0; x < world.GridWidth - 1; x++)
            {
                cells[x] = world.GetCellData(x, 0);
            }

            helper.Log("Applying to cells");
            for (int cell = 0; cell < cells.Length - 1; cell++)
            {
                Cell currentCell = cells[cell];
            
                ClearCell(currentCell, clearCave:true);
                try
                {
                    TryPlaceResource(currentCell, Resources[0], deleteTrees: true, storePostGenerationType: true);
                }
                catch (PlacementFailedException e)
                {
                    helper.Log($"Not placed");
                    helper.Log(e.ToString());
                }
            }
        
            helper.Log("Finished generating GoldDeposit");
            return true;
        }
    }
}