using System;
using System.Net.Http;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

[System.Serializable]
public class Agent
{
    public int id { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public int z { get; set; }
    public int is_active { get; set; }
}

[System.Serializable]
public class Position
{
    public int id { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public int z { get; set; }
}

[System.Serializable]
public class SimulationStep
{
    public int step { get; set; }
    public List<Agent> agents { get; set; }
}

[System.Serializable]
public class SimulationData
{
    public int grid_size { get; set; }
    public int shelters_n { get; set; }
    public int explorers_n { get; set; }
    public int oxygen_endpoints_n { get; set; }
    public int simulation_steps { get; set; }
    public List<SimulationStep> explorers_steps { get; set; }
    public List<Position> oxygen_positions { get; set; }
    public List<Position> domes_positions { get; set; }
}


public class DataFetcher : MonoBehaviour
{
    // Keep a single static instance of HttpClient around.
    private static readonly HttpClient httpClient = new HttpClient();
    
    // Sample async method that fetches the data from an endpoint.
    // Provide your endpoint URL and API key as parameters.
    public async Task<SimulationData> FetchDataAsync(string endpoint, string apiKey)
    {
        // Clear any existing headers (optional step).
        httpClient.DefaultRequestHeaders.Clear();

        // Replace "X-API-KEY" with exactly the header name from your Python code
        httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);

        // Make the GET request and read the response as a string.
        string jsonResponse = await httpClient.GetStringAsync(endpoint);
        Debug.Log("Received JSON: " + jsonResponse);
        // Deserialize the JSON into our RootObject model.
        SimulationData result = JsonConvert.DeserializeObject<SimulationData>(jsonResponse);

        return result;
    }
}

public class SimulationControler : MonoBehaviour
{
    public SimulationData simulationData;
    public DataFetcher dataFetcher;
    [SerializeField] public GameObject explorerPrefab;
    [SerializeField] public GameObject domePrefab;
    [SerializeField] public GameObject oxigenEndPointsPrefab;
    
    // This dictionary maps each 'agent.id' to the GameObject we instantiate once.
    private Dictionary<int, GameObject> explorersDict = new Dictionary<int, GameObject>();

    public int currentStep = 1;
    public const  int ofsetForPositioning = 4;
    private int getNumberOfSimulationSteps()
    {
        int numberOfSimulationSteps = simulationData.simulation_steps;
        return numberOfSimulationSteps;
    }

    private void InitializeExplorersAtFirstStep()
    {
        if (simulationData == null || simulationData.explorers_steps == null)
        {
            Debug.LogWarning("No simulation data to initialize explorers.");
            return;
        }

        // If step 0 is out of range, just bail
        if (simulationData.explorers_steps.Count == 0)
        {
            Debug.LogWarning("explorers_steps is empty.");
            return;
        }

        // We'll read step=0’s agent list
        SimulationStep firstStep = simulationData.explorers_steps[0];
        foreach (Agent agent in firstStep.agents)
        {
            // Instantiate once
            GameObject newExplorer = Instantiate(explorerPrefab);
            newExplorer.name = $"Explorer_{agent.id}_Initial";

            // Position for step 0
            newExplorer.transform.position = new Vector3(agent.x, agent.y , agent.z);

            // Store in dictionary
            explorersDict[agent.id] = newExplorer;
        }
    }

    private void UpdateExplorersPositions(int stepIndex)
    {
        // Safety checks
        if (simulationData == null 
            || simulationData.explorers_steps == null 
            || stepIndex < 0 
            || stepIndex >= simulationData.explorers_steps.Count)
        {
            Debug.LogWarning($"Step index {stepIndex} invalid, or no data loaded.");
            return;
        }

        // Grab the step's agent list
        SimulationStep stepData = simulationData.explorers_steps[stepIndex];

        // 1) Build a set of agent IDs for the new step
        HashSet<int> currentStepAgentIDs = new HashSet<int>();
        foreach (Agent agent in stepData.agents)
        {
            currentStepAgentIDs.Add(agent.id);
        }

        // 2) Destroy any explorers *not* in this step
        //    We'll collect them first, then remove from dictionary
        List<int> agentsToRemove = new List<int>();
        foreach (var kvp in explorersDict)
        {
            int agentID = kvp.Key;
            if (!currentStepAgentIDs.Contains(agentID))
            {
                // This agent is *not* in the current step, so remove it
                GameObject obj = kvp.Value;
                Destroy(obj);             // This removes it from the scene
                agentsToRemove.Add(agentID); 
            }
        }

        // Remove them from the dictionary so we don't track them anymore
        foreach (int agentID in agentsToRemove)
        {
            explorersDict.Remove(agentID);
        }

        // 3) Now create or move the ones that *are* in this step
        foreach (Agent agent in stepData.agents)
        {
            // If we never saw this agent before, instantiate a new one
            if (!explorersDict.ContainsKey(agent.id))
            {
                GameObject newExplorer = Instantiate(explorerPrefab);
                newExplorer.name = $"Explorer_{agent.id}";
                explorersDict[agent.id] = newExplorer;
            }

            // Update the position
            GameObject explorerObj = explorersDict[agent.id];
            explorerObj.transform.position = new Vector3(agent.x*ofsetForPositioning , agent.y * ofsetForPositioning , agent.z * ofsetForPositioning);
        }
    }
    private void InstantiateDomesAndOxygen()
    {
        // Create domes
        foreach (Position domePos in simulationData.domes_positions)
        {
            GameObject domeObj = Instantiate(domePrefab);
            domeObj.transform.position = new Vector3(domePos.x * ofsetForPositioning, domePos.y * ofsetForPositioning, domePos.z * ofsetForPositioning);
        }

        // Create oxygen endpoints
        foreach (Position oxyPos in simulationData.oxygen_positions)
        {
            GameObject oxyObj = Instantiate(oxigenEndPointsPrefab);
            oxyObj.transform.position = new Vector3(oxyPos.x * ofsetForPositioning, oxyPos.y * ofsetForPositioning, oxyPos.z * ofsetForPositioning);
        }
    }

    private void RenderControls()
    {
        int numberOfSimulationSteps = getNumberOfSimulationSteps();

        var root = GetComponent<UIDocument>().rootVisualElement;
        VisualElement sliderContainer = root.Q<VisualElement>("sliderContainer");
        VisualElement sliderLabelContainer = root.Q<VisualElement>("labelContainer");
        Label labelStep = new Label();
        labelStep.text = "Step: 1";
        sliderLabelContainer.Add(labelStep);

        SliderInt slider = new SliderInt("", 0, numberOfSimulationSteps);
        slider.value = 1;
        slider.RegisterValueChangedCallback(evt =>
        {
            currentStep = evt.newValue;
            labelStep.text = $"Step: {currentStep}";
            UpdateExplorersPositions(currentStep);

        });
        Console.Out.WriteLine("Waaa");
        slider.AddToClassList("simSlider");
        sliderContainer.Add(slider);
    }

    private async void Start()
    
    {
        if (dataFetcher == null)
        {
            dataFetcher = gameObject.AddComponent<DataFetcher>();
        }

        SimulationData data = await dataFetcher.FetchDataAsync("https://tc2008b-rest-api.onrender.com/simulation_data", "Kohk:~qSSX'zjYBkQ1z}Z2+^g3Ri%J/s");
        simulationData = data;
        InitializeExplorersAtFirstStep();
        InstantiateDomesAndOxygen();
        RenderControls();

    }

private void Update()
    {
        
    }
}
