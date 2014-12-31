using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


using UnityEngine;
using KSPPluginFramework;

using TestFlightAPI;

namespace TestFlightCore
{
    public class PartFlightData : IConfigNode
    {
        private List<TestFlightData> flightData = null;
        private string partName = "";

        public void AddFlightData(string name, TestFlightData data)
        {
            if (flightData == null)
            {
                flightData = new List<TestFlightData>();
                partName = name;
                // add new entry for this scope
                TestFlightData newData = new TestFlightData();
                newData.scope = data.scope;
                newData.flightData = data.flightData;
                newData.flightTime = 0;
                flightData.Add(newData);
            }
            else
            {
                int dataIndex = flightData.FindIndex(s => s.scope == data.scope);
                if (dataIndex >= 0)
                {
                    TestFlightData currentData = flightData[dataIndex];
                    // We only update the data if its higher than what we already have
                    if (data.flightData > currentData.flightData)
                    {
                        currentData.flightData = data.flightData;
                        flightData[dataIndex] = currentData;
                    }
                    // We don't care about flightTime, so set it to 0
                    currentData.flightTime = 0;
                }
                else
                {
                    // add new entry for this scope
                    TestFlightData newData = new TestFlightData();
                    newData.scope = data.scope;
                    newData.flightData = data.flightData;
                    newData.flightTime = 0;
                    flightData.Add(newData);
                }
            }
        }

        public List<TestFlightData> GetFlightData()
        {
//            Debug.Log("PartFlightData: GetFlightData()");
            return flightData;
        }

        public string GetPartName()
        {
//            Debug.Log("PartFlightData: GetPartName()");
            return partName;
        }

        public override string ToString()
        {
//            Debug.Log("PartFlightData: ToString()");
            string baseString = partName + ":";
            foreach (TestFlightData data in flightData)
            {
                string dataString = String.Format("{0},{1},0", data.scope, data.flightData);
                baseString = baseString + dataString + " ";
            }

            return baseString;
        }

        public static PartFlightData FromString(string str)
        {
//            Debug.Log("PartFlightData: FromString()");
            // String format is
            // partName:scope,data,0 scope,data scope,data,0 scope,data,0 
            PartFlightData newData = null;
            if (str.IndexOf(':') > -1)
            {
                newData = new PartFlightData();
                string[] baseString = str.Split(new char[1]{ ':' });
                newData.partName = baseString[0];
                string[] dataStrings = baseString[1].Split(new char[1]{ ' ' });
                foreach (string dataString in dataStrings)
                {
                    if (newData.flightData == null)
                        newData.flightData = new List<TestFlightData>();

                    if (dataString.Trim().Length > 0)
                    {
                        string[] dataMembers = dataString.Split(new char[1]{ ',' });
                        if (dataMembers.Length == 3)
                        {
                            TestFlightData tfData = new TestFlightData();
                            tfData.scope = dataMembers[0];;
                            tfData.flightData = float.Parse(dataMembers[1]);
                            tfData.flightTime = 0;
                            newData.flightData.Add(tfData);
                        }
                    }
                }
            }
            return newData;
        }

        public void Load(ConfigNode node)
        {
            partName = node.GetValue("partName");
            if (node.HasNode("FLIGHTDATA"))
            {
                flightData = new List<TestFlightData>();
                foreach (ConfigNode dataNode in node.GetNodes("FLIGHTDATA"))
                {
                    TestFlightData newData = new TestFlightData();
                    newData.scope = dataNode.GetValue("scope");
                    if (dataNode.HasValue("flightData"))
                        newData.flightData = float.Parse(dataNode.GetValue("flightData"));
                    Debug.Log("SCOPE: " + newData.scope);
                    Debug.Log("DATA: " + newData.flightData);
                    flightData.Add(newData);
                }
            }
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("partName", partName);
            foreach (TestFlightData data in flightData)
            {
                ConfigNode dataNode = node.AddNode("FLIGHTDATA");
                Debug.Log("SCOPE: " + data.scope);
                dataNode.AddValue("scope", data.scope);
                Debug.Log("DATA: " + data.flightData);
                dataNode.AddValue("flightData", data.flightData);
            }
        }


    }

    internal struct PartStatus
    {
        internal string partName;
        internal uint partID;
        internal int partStatus;
        internal int flightTime;
        internal double flightData;
        internal double reliability;
        internal ITestFlightCore flightCore;
        internal ITestFlightFailure activeFailure;
        internal bool highlightPart;
        internal string repairRequirements;
    }

    internal struct MasterStatusItem
    {
        internal Guid vesselID;
        internal string vesselName;
        internal List<PartStatus> allPartsStatus;
    }

    internal struct PartWindowSettings
    {
        internal bool partHighlight;
    }

    internal struct WindowSettings
    {
        internal Dictionary<uint, PartWindowSettings> partWindowSettings;
        internal int activePage;
        internal Guid selectedVessel;
        internal bool showSelectedVessel;
        internal Vector2 vesselSelectionScroll;
    }


    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
	public class TestFlightManager : MonoBehaviourWindow
	{
        public TestFlightManagerScenario tsm;

		internal override void Start()
		{
			var game = HighLogic.CurrentGame;
			ProtoScenarioModule psm = game.scenarios.Find(s => s.moduleName == typeof(TestFlightManagerScenario).Name);
			if (psm == null)
			{
				GameScenes[] desiredScenes = new GameScenes[4] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER };
				psm = game.AddProtoScenarioModule(typeof(TestFlightManagerScenario), desiredScenes);
			}
            psm.Load(ScenarioRunner.fetch);
            tsm = game.scenarios.Select(s => s.moduleRef).OfType<TestFlightManagerScenario>().SingleOrDefault();
            base.Start();
		}

        internal override void Awake()
        {
            Visible = false;
            base.Awake();
        }

        internal override void DrawWindow(Int32 id)
        {
        }
	}

    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    internal class MasterStatusDisplay : MonoBehaviourWindow
    {
        private TestFlightManagerScenario tsm;
        private WindowSettings winSettings;
        private PartWindowSettings partSettings;
        private ApplicationLauncherButton appLauncherButton;
        private bool sticky = false;

        internal override void Start()
        {
            var game = HighLogic.CurrentGame;
            ProtoScenarioModule psm = game.scenarios.Find(s => s.moduleName == typeof(TestFlightManagerScenario).Name);
            if (psm == null)
            {
                GameScenes[] desiredScenes = new GameScenes[4] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER };
                psm = game.AddProtoScenarioModule(typeof(TestFlightManagerScenario), desiredScenes);
            }
            psm.Load(ScenarioRunner.fetch);
            tsm = game.scenarios.Select(s => s.moduleRef).OfType<TestFlightManagerScenario>().SingleOrDefault();
            base.Start();
        }
        
        internal override void Awake()
        {
            if (winSettings.partWindowSettings == null)
            {
                winSettings.partWindowSettings = new Dictionary<uint, PartWindowSettings>();
            }
            StartCoroutine("AddToToolbar");
            base.Awake();
        }

        internal override void OnGUIOnceOnly()
        {
            Styles.Init();
            SkinsLibrary.SetCurrent("Default");
            // Default position and size -- will get proper bounds calculated when needed
            WindowRect = new Rect(0, 50, 500, 50);
            DragEnabled = true;
            ClampToScreen = true;
            TooltipsEnabled = true;
            WindowCaption = "TestFlight Master Status Display";
        }

        internal void CalculateWindowBounds()
        {
            if (appLauncherButton == null)
                return;
            if (tsm == null)
                return;
            float windowWidth = 650f;
            float left = Screen.width - windowWidth;
            float windowHeight = 10f;
            float top = 40f;

            // Calculate height based on amount of parts
            Dictionary<Guid, MasterStatusItem> masterStatus = tsm.GetMasterStatus();

            if (masterStatus != null && masterStatus.Count() > 0)
            {
                Guid currentVessel = masterStatus.First().Key;
                windowHeight = masterStatus[currentVessel].allPartsStatus.Count() * 20f;
            }
            if (!ApplicationLauncher.Instance.IsPositionedAtTop)
            {
                top = Screen.height - windowHeight - 40f;
            }

            WindowRect = new Rect(left, top, windowWidth, windowHeight);
        }

        IEnumerator AddToToolbar()
        {
            while (!ApplicationLauncher.Ready)
            {
                yield return null;
            }
            try
            {
                // Load the icon for the button
                Debug.Log("TestFlight MasterStatusDisplay: Loading icon texture");
                Texture iconTexture = GameDatabase.Instance.GetTexture("TestFlight/Resources/AppLauncherIcon", false);
                if (iconTexture == null)
                {
                    throw new Exception("TestFlight MasterStatusDisplay: Failed to load icon texture");
                }
                Debug.Log("TestFlight MasterStatusDisplay: Creating icon on toolbar");
                appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
                    OpenWindow,
                    CloseWindow,
                    HoverInButton,
                    HoverOutButton,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.ALWAYS,
                    iconTexture);
                ApplicationLauncher.Instance.AddOnHideCallback(HideButton);
                ApplicationLauncher.Instance.AddOnRepositionCallback(RepostionWindow);
            }
            catch (Exception e)
            {
                Debug.Log("TestFlight MasterStatusDisplay: Unable to add button to application launcher: " + e.Message);
                throw e;
            }
        }
        void OpenWindow()
        {
            CalculateWindowBounds();
            Visible = true;
            sticky = true;
        }
        void CloseWindow()
        {
            Visible = false;
            sticky = false;
        }
        void HideButton()
        {
            ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
        }
        void RepostionWindow()
        {
            CalculateWindowBounds();
            Debug.Log("TestFlight MasterStatusDisplay: RepositionWindow");
        }
        void HoverInButton()
        {
            CalculateWindowBounds();
            Visible = true;
        }
        void HoverOutButton()
        {
            if (!sticky)
                Visible = false;
        }
        internal override void DrawWindow(Int32 id)
        {
            CalculateWindowBounds();

            GUILayout.BeginVertical();
            Dictionary<Guid, MasterStatusItem> masterStatus = tsm.GetMasterStatus();
            if (masterStatus == null)
                GUILayout.Label("TestFlight is starting up...");
            else if (masterStatus.Count() <= 0)
                GUILayout.Label("TestFlight is not currently tracking any vessels");
            else
            {
                GUILayout.Label("Select Vessel to Display");
                // Set up button to select vessel for display
                string selectionButtonText = "Active Vessel";
                if (winSettings.showSelectedVessel)
                    selectionButtonText = masterStatus[winSettings.selectedVessel].vesselName;
                if (GUILayout.Button(selectionButtonText))
                {
                    // If selection button is pressed, we open a scrollview containing buttons for all the vessels we track
                    winSettings.vesselSelectionScroll = GUILayout.BeginScrollView(winSettings.vesselSelectionScroll);
                    foreach (var entry in masterStatus)
                    {
                        GUILayout.Button(entry.Value.vesselName);
                    }
                    GUILayout.EndScrollView();
                }

                // Determine which vessel is our focused one
                if (winSettings.showSelectedVessel)
                {
                    if (!masterStatus.ContainsKey(winSettings.selectedVessel))
                        winSettings.selectedVessel = masterStatus.First().Key;
                }
                else
                {
                    winSettings.selectedVessel = masterStatus[FlightGlobals.ActiveVessel.id];
                }

                // Display information on selected vessel
                GUILayout.Label("MSD for " + masterStatus[winSettings.selectedVessel].vesselName);
                foreach (PartStatus status in masterStatus[winSettings.selectedVessel].allPartsStatus)
                {
                    // load window settings for this part
                    if (!winSettings.partWindowSettings.ContainsKey(status.partID))
                    {
                        winSettings.partWindowSettings.Add(status.partID, new PartWindowSettings());
                    }
                    partSettings = winSettings.partWindowSettings[status.partID];

                    // Display part data
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(String.Format("{0,50}", status.partName));
                    GUILayout.Label(String.Format("{0,7:F2}du", status.flightData));
                    GUILayout.Label(String.Format("{0,7:F2}%", status.reliability));
                    string goNoGo;
                    GUIStyle useStyle;
                    if (status.activeFailure != null)
                    {
                        if (status.activeFailure.GetFailureDetails().severity == "major")
                            useStyle = Styles.textStyleCritical;
                        else
                            useStyle = Styles.textStyleWarning;
                        goNoGo = String.Format("{0,-25}", status.activeFailure.GetFailureDetails().failureTitle);
                    }
                    else
                    {
                        useStyle = Styles.textStyleSafe;
                        goNoGo = String.Format("{0,-25}", "Status OK");
                    }
                    string tooltip = status.repairRequirements;
                    GUILayout.Label(new GUIContent(goNoGo, tooltip), useStyle);
                    if (GUILayout.Button("H"))
                    {
                        // Highlight part
                        partSettings.partHighlight = !partSettings.partHighlight;
                        status.flightCore.HighlightPart(partSettings.partHighlight);
                    }
                    if (status.activeFailure != null)
                    {
                        if (GUILayout.Button("R"))
                        {
                            // attempt repair
                            bool repairSuccess = status.flightCore.AttemptRepair();
                        }
                    }

                    GUILayout.EndHorizontal();
                    winSettings.partWindowSettings[status.partID] = partSettings;
                }
            }

            GUILayout.EndVertical();
        }
    }


	public class TestFlightManagerScenario : ScenarioModule
	{
        public List<PartFlightData> partsFlightData;
        public List<String> partsPackedStrings;
        public Dictionary<Guid, double> knownVessels;

        Settings settings = null;
        public double pollingInterval = 5.0f;
        public bool processInactiveVessels = true;

//        private bool havePartsBeenInitialized = false;

        private Dictionary<Guid, MasterStatusItem> masterStatus = null;

        double currentUTC = 0.0;
        double lastDataPoll = 0.0;
        double lastFailurePoll = 0.0;
        double lastMasterStatusUpdate = 0.0;

        public override void OnAwake()
		{
            if (partsFlightData == null)
            {
                partsFlightData = new List<PartFlightData>();
                if (partsPackedStrings != null)
                {
                    foreach (string packedString in partsPackedStrings)
                    {
                        Debug.Log(packedString);
                        PartFlightData data = PartFlightData.FromString(packedString);
                        partsFlightData.Add(data);
                    }
                }
            }
            if (partsPackedStrings == null)
            {
                partsPackedStrings = new List<string>();
            }
            if (settings == null)
            {
                settings = new Settings("../settings.cfg");
            }
            string assemblyPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string filePath = System.IO.Path.Combine(assemblyPath, "../settings.cfg").Replace("\\","/");
            Debug.Log("Settings stored in " + filePath);
            if (!System.IO.File.Exists(filePath))
            {
                settings.flightDataEngineerMultiplier = 1.0;
                settings.flightDataMultiplier = 1.0;
                settings.globalReliabilityModifier = 1.0;
                settings.minTimeBetweenDataPoll = 0.5;
                settings.minTimeBetweenFailurePoll = 60;
                settings.processAllVessels = false;
                settings.masterStatusUpdateFrequency = 10;
                settings.Save();
            }
            settings.Load();
			base.OnAwake();
		}

        internal Dictionary<Guid, MasterStatusItem> GetMasterStatus()
        {
            return masterStatus;
        }

        private PartFlightData GetFlightDataForPartName(string partName)
        {
            foreach (PartFlightData data in partsFlightData)
            {
                if (data.GetPartName() == partName)
                    return data;
            }
            return null;
        }

        private void InitializeParts(Vessel vessel)
        {
            Debug.Log("TestFlightManagerScenario: Initializing parts for vessel " + vessel.GetName());
            foreach (Part part in vessel.parts)
            {
                foreach (PartModule pm in part.Modules)
                {
                    ITestFlightCore core = pm as ITestFlightCore;
                    if (core != null)
                    {
                        PartFlightData partData = GetFlightDataForPartName(pm.part.name);
                        if (partData == null)
                        {
                            partData = new PartFlightData();
                        }

                        if (partData != null)
                        {
                            core.InitializeFlightData(partData.GetFlightData(), settings.globalReliabilityModifier);
                        }
                    }
                }
            }
        }

        // This method simply scans through the Master Status list every now and then and removes vessels and parts that no longer exist
        public void VerifyMasterStatus()
        {
            // iterate through our cached vessels and delete ones that are no longer valid
            List<Guid> vesselsToDelete = new List<Guid>();
            foreach(var entry in masterStatus)
            {
                Debug.Log("TestFlightManagerScenario: Checking if vessel(" + entry.Key + ") in Master Status is still valid");
                Vessel vessel = FlightGlobals.Vessels.Find(v => v.id == entry.Key);
                if (vessel == null)
                {
                    Debug.Log("TestFlightManagerScenario: Vessel no longer exists. Marking it for deletion.");
                    vesselsToDelete.Add(entry.Key);
                }
                else
                {
                    if (vessel.vesselType == VesselType.Debris)
                    {
                        Debug.Log("TestFlightManagerScenario: Vessel appears to be debris now. Marking it for deletion.");
                        vesselsToDelete.Add(entry.Key);
                    }
                }
            }
            Debug.Log("TestFlightManagerScenario: Removing " + vesselsToDelete.Count() + " vessels from Master Status");
            foreach (Guid id in vesselsToDelete)
            {
                masterStatus.Remove(id);
            }
            // iterate through the remaining vessels and check for parts that no longer exist
            List<PartStatus> partsToDelete = new List<PartStatus>();
            foreach (var entry in masterStatus)
            {
                partsToDelete.Clear();
                Vessel vessel = FlightGlobals.Vessels.Find(v => v.id == entry.Key);
                Debug.Log("TestFlightManagerScenario: Scanning parts on vessel" + vessel.GetName() + " for master status update");
                foreach (PartStatus partStatus in masterStatus[entry.Key].allPartsStatus)
                {
                    Debug.Log("TestFlightManagerScenario: Looking to see if part with flightID " + partStatus.partID + " still exists");
                    Part part = vessel.Parts.Find(p => p.flightID == partStatus.partID);
                    if (part == null)
                    {
                        Debug.Log("TestFlightManagerScenario: Could not find part.  Marking it for deletion.");
                        partsToDelete.Add(partStatus);
                    }
                }
                Debug.Log("TestFlightManagerScenario: Deleting " + partsToDelete.Count() + " parts from vessel " + vessel.GetName());
                foreach (PartStatus oldPartStatus in partsToDelete)
                {
                    masterStatus[entry.Key].allPartsStatus.Remove(oldPartStatus);
                }
            }
        }

        public void CacheVessels()
        {
            // build a list of vessels to process based on setting
            if (knownVessels == null)
                knownVessels = new Dictionary<Guid, double>();

            // iterate through our cached vessels and delete ones that are no longer valid
            List<Guid> vesselsToDelete = new List<Guid>();
            foreach(var entry in knownVessels)
            {
                Vessel vessel = FlightGlobals.Vessels.Find(v => v.id == entry.Key);
                if (vessel == null)
                    vesselsToDelete.Add(entry.Key);
                else
                {
                    if (vessel.vesselType == VesselType.Debris)
                        vesselsToDelete.Add(entry.Key);
                }
            }
            if (vesselsToDelete.Count() > 0)
                Debug.Log("TestFlightManagerScenario: Deleting " + vesselsToDelete.Count() + " vessels from cached vessels");
            foreach (Guid id in vesselsToDelete)
            {
                knownVessels.Remove(id);
            }

            // Build our cached list of vessels.  The reason we do this is so that we can store an internal "missionStartTime" for each vessel because the game
            // doesn't consider a vessel launched, and does not start the mission clock, until the player activates the first stage.  This is fine except it
            // makes things like engine test stands impossible, so we instead cache the vessel the first time we see it and use that time as the missionStartTime

            if (!settings.processAllVessels)
            {
                if (FlightGlobals.ActiveVessel != null && !knownVessels.ContainsKey(FlightGlobals.ActiveVessel.id))
                {
                    Debug.Log("TestFlightManagerScenario: Adding new vessel " + FlightGlobals.ActiveVessel.GetName() + " with launch time " + Planetarium.GetUniversalTime());
                    knownVessels.Add(FlightGlobals.ActiveVessel.id, Planetarium.GetUniversalTime());
                    InitializeParts(FlightGlobals.ActiveVessel);
                }
            }
            else
            {
                foreach (Vessel vessel in FlightGlobals.Vessels)
                {
                    if (vessel.vesselType == VesselType.Lander || vessel.vesselType == VesselType.Probe || vessel.vesselType == VesselType.Rover || vessel.vesselType == VesselType.Ship || vessel.vesselType == VesselType.Station)
                    {
                        if ( !knownVessels.ContainsKey(vessel.id) )
                        {
                            Debug.Log("TestFlightManagerScenario: Adding new vessel " + vessel.GetName() + " with launch time " + Planetarium.GetUniversalTime());
                            knownVessels.Add(vessel.id, Planetarium.GetUniversalTime());
                            InitializeParts(vessel);
                        }
                    }
                }
            }
        }

        public void DoFlightUpdate(ITestFlightCore core, double launchTime)
        {
            // Tell the core to do a flight update
            core.DoFlightUpdate(launchTime, settings.flightDataMultiplier, settings.flightDataEngineerMultiplier, settings.globalReliabilityModifier);
        }

        public TestFlightData DoDataUpdate(ITestFlightCore core, Part part)
        {
            // Then grab its flight data
            return core.GetCurrentFlightData();
        }

        public void DoFailureUpdate(ITestFlightCore core, double launchTime)
        {
            Debug.Log("TestFlightManagerScenario: Doing Failure Update");
            core.DoFailureCheck(launchTime, settings.globalReliabilityModifier);
        }

		public void Update()
		{
            if (masterStatus == null)
                masterStatus = new Dictionary<Guid, MasterStatusItem>();

            currentUTC = Planetarium.GetUniversalTime();
            // ensure out vessel list is up to date
            CacheVessels();
            if (currentUTC >= lastMasterStatusUpdate + settings.masterStatusUpdateFrequency)
            {
                lastMasterStatusUpdate = currentUTC;
                VerifyMasterStatus();
            }
            // process vessels
            foreach (var entry in knownVessels)
            {
                Vessel vessel = FlightGlobals.Vessels.Find(v => v.id == entry.Key);
                foreach (Part part in vessel.parts)
                {
                    foreach (PartModule pm in part.Modules)
                    {
                        ITestFlightCore core = pm as ITestFlightCore;
                        if (core != null)
                        {
                            // Poll for flight data and part status
                            if (currentUTC >= lastDataPoll + settings.minTimeBetweenDataPoll)
                            {
                                Debug.Log("TestFlightManagerScenario: Processing Part " + part.name + "(" + part.flightID + ")");
                                DoFlightUpdate(core, entry.Value);
                                TestFlightData currentFlightData = DoDataUpdate(core, part);

                                PartStatus partStatus = new PartStatus();
                                partStatus.flightCore = core;
                                partStatus.partName = part.partInfo.title;
                                partStatus.partID = part.flightID;
                                partStatus.flightData = currentFlightData.flightData;
                                partStatus.flightTime = currentFlightData.flightTime;
                                partStatus.partStatus = core.GetPartStatus();
                                partStatus.reliability = core.GetCurrentReliability(settings.globalReliabilityModifier);
                                partStatus.repairRequirements = core.GetRequirementsTooltip();
                                if (core.GetPartStatus() > 0)
                                {
                                    partStatus.activeFailure = core.GetFailureModule();
                                }
                                else
                                {
                                    partStatus.activeFailure = null;
                                }

                                // Update or Add part status in Master Status
                                if (masterStatus.ContainsKey(vessel.id))
                                {
                                    // Vessel is already in the Master Status, so check if part is in there as well
                                    int numItems = masterStatus[vessel.id].allPartsStatus.Count(p => p.partID == part.flightID);
                                    int existingPartIndex;
                                    if (numItems == 1)
                                    {
                                        existingPartIndex = masterStatus[vessel.id].allPartsStatus.FindIndex(p => p.partID == part.flightID);
                                        masterStatus[vessel.id].allPartsStatus[existingPartIndex] = partStatus;
                                    }
                                    else if (numItems == 0)
                                    {
                                        masterStatus[vessel.id].allPartsStatus.Add(partStatus);
                                    }
                                    else
                                    {
                                        existingPartIndex = masterStatus[vessel.id].allPartsStatus.FindIndex(p => p.partID == part.flightID);
                                        masterStatus[vessel.id].allPartsStatus[existingPartIndex] = partStatus;
                                        Debug.Log("[ERROR] TestFlightManagerScenario: Found " + numItems + " matching parts in Master Status Display!");
                                    }
                                }
                                else
                                {
                                    // Vessel is not in the Master Status so create a new entry for it and add this part
                                    MasterStatusItem masterStatusItem = new MasterStatusItem();
                                    masterStatusItem.vesselID = vessel.id;
                                    masterStatusItem.vesselName = vessel.GetName();
                                    masterStatusItem.allPartsStatus = new List<PartStatus>();
                                    masterStatusItem.allPartsStatus.Add(partStatus);
                                    masterStatus.Add(vessel.id, masterStatusItem);
                                }

                                PartFlightData data = GetFlightDataForPartName(part.name);
                                if (data != null)
                                {
                                    data.AddFlightData(part.name, currentFlightData);
                                }
                                else
                                {
                                    data = new PartFlightData();
                                    data.AddFlightData(part.name, currentFlightData);
                                    partsFlightData.Add(data);
                                    partsPackedStrings.Add(data.ToString());
                                }
                            }
                            // Poll for failures
                            if (currentUTC >= lastFailurePoll + settings.minTimeBetweenFailurePoll)
                            {
                                DoFailureUpdate(core, entry.Value);
                            }
                        }
                    }
                }
                if (currentUTC >= lastDataPoll + settings.minTimeBetweenDataPoll)
                {
                    lastDataPoll = currentUTC;
                }
                if (currentUTC >= lastFailurePoll + settings.minTimeBetweenFailurePoll)
                {
                    lastFailurePoll = currentUTC;
                }
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            Debug.Log("TestFlightManagerScenario: OnLoad()");
            Debug.Log(node);
            if (node.HasNode("FLIGHTDATA_PART"))
            {
                if (partsFlightData == null)
                    partsFlightData = new List<PartFlightData>();

                foreach (ConfigNode partNode in node.GetNodes("FLIGHTDATA_PART"))
                {
                    Debug.Log("TestFlightManagerScenario: Loading Flight Data");
                    PartFlightData partData = new PartFlightData();
                    partData.Load(partNode);
                    partsFlightData.Add(partData);
                    partsPackedStrings.Add(partData.ToString());
                    Debug.Log(partData.ToString());
                }
            }
        }

		public override void OnSave(ConfigNode node)
		{
            Debug.Log("TestFlightManagerScenario: OnSave()");
            if (HighLogic.LoadedSceneIsFlight)
            {
                Debug.Log("TestFlightManagerScenario: Saving in FLIGHT scene");
                foreach (PartFlightData partData in partsFlightData)
                {
                    ConfigNode partNode = node.AddNode("FLIGHTDATA_PART");
                    partData.Save(partNode);
                }
            }
            else
            {
                Debug.Log("TestFlightManagerScenario: Saving in NON-FLIGHT scene");
                foreach (PartFlightData partData in partsFlightData)
                {
                    ConfigNode partNode = node.AddNode("FLIGHTDATA_PART");
                    partData.Save(partNode);
                }
            }
            Debug.Log(node);
			Debug.Log("TestFlight: Scenario Saved");
		}

	}
}