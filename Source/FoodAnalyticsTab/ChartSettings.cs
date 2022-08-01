using System.Collections.Generic;
using Verse;

namespace FoodAnalyticsTab;

public class ChartSettings : MapComponent
{
    public Dictionary<string, bool> graphEnable = new Dictionary<string, bool>();
    public Predictor.ModelType predictorModel = Predictor.ModelType.iterative;

    public bool ShowDeficiency,
        DrawPoints,
        UseAntiAliasedLines,
        EnableLearning,
        EnableOutdoorAnimalDetection,
        EnableOutdoorNoGrowWinter;

    private string title = "";

    public ChartSettings(Map map) : base(map)
    {
        this.map = map;
        SetDefault();
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref title, "ChartTitle" + title);
    }

    public void SetDefault()
    {
        ShowDeficiency = false;
        DrawPoints = false;
        UseAntiAliasedLines = true;
        predictorModel = Predictor.ModelType.iterative;
        EnableLearning = false;
        EnableOutdoorAnimalDetection = true;
        EnableOutdoorNoGrowWinter = true;
    }
}