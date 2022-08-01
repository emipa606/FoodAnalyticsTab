/*
The MIT License (MIT)

Copyright (c) 2016 

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodAnalyticsTab;

[StaticConstructorOnStartup]
public class MainTabWindow_Estimator : MainTabWindow
{
    public enum SourceOptions
    {
        Plants,
        Animals
    }

    private static readonly int nextNDays = 60; // default display 25 days

    public static float[] debug_val = new float[10];


    public static Predictor predictor = new Predictor();
    public static bool IsDirty;
    private readonly List<LineChart> chartList = new List<LineChart>();

    private readonly List<Consumer> consList = new List<Consumer>();

    private readonly NoteType note;

    private readonly Vector2[]
        scrollPos = { Vector2.zero, Vector2.zero, Vector2.zero }; // for scrolling view

    private TabType curTab = TabType.Graph, prevTab = TabType.Analytics;

    // analytics
    public float dailyHayConsumptionIndoorAnimals,
        dailyKibbleConsumption,
        dailyHayConsumption,
        dailyHayConsumptionRoughAnimals;

    private List<DataPoint> dpList = new List<DataPoint>();

    protected float lastUpdate = -1f;
    private int lastUpdateTick;

    private int numAnimals,
        numRoughAnimals,
        numKibbleAnimals,
        numHaygrass,
        numHay,
        numMeat,
        numEgg,
        numHen,
        numColonist,
        numHerbivoreIndoor,
        numHerbivoreOutdoor;

    public SourceOptions Source = SourceOptions.Animals;

    // functions
    public MainTabWindow_Estimator()
    {
        doCloseX = true;
        doCloseButton = false;
        closeOnClickedOutside = false;
        forcePause = false;


        predictor.allPredType["Haygrass"].enabled = true;
        chartList.Add(new LineChart(nextNDays, ref predictor, Find.CurrentMap));

        var getComponent = Find.CurrentMap.components.OfType<NoteType>().FirstOrDefault();
        if (getComponent == null)
        {
            getComponent = new NoteType(Find.CurrentMap);
            Find.CurrentMap.components.Add(getComponent);
        }

        note = getComponent;
        //dpList.Add(new DataPoint(GenDate.DateFullStringAt(GenTicks.TicksAbs)));
        //ML.WriteToXmlFile<List<DataPoint>>("C://datapoint.xml", dpList);
    }

    public override Vector2 RequestedTabSize => new Vector2(1010f, Screen.height);

    public override void PreOpen()
    {
        base.PreOpen();
        if (!Find.TickManager.Paused)
        {
            return;
        }

        GetInGameData();
        predictor.MakePrediction(0);
        predictor.allPredType["Haygrass"].GenerateAnalysis();

        //dpList.Add(new DataPoint(GenDate.DateFullStringAt(GenTicks.TicksAbs)));

        //ML.WriteToBinaryFile<List<DataPoint>>("datapoint.dat", dpList);// save file under main dir
    }

    private void GetInGameData()
    {
        var currentMap = Find.CurrentMap;
        var pawns = currentMap.mapPawns.PawnsInFaction(Faction.OfPlayer);
        var allHaygrass = currentMap.listerThings.AllThings.Where(x => x.Label == "haygrass");
        numColonist = pawns.Count(x => x.RaceProps.Humanlike);
        numMeat = currentMap.resourceCounter.GetCountIn(ThingCategoryDefOf.MeatRaw);
        numEgg = currentMap.listerThings.AllThings.Count(x => x.def.defName == "EggChickenUnfertilized");
        numAnimals = pawns.Count(x => x.RaceProps.Animal);

        var roughAnimals = pawns
            .Where(x => x.RaceProps.Animal && x.RaceProps.Eats(FoodTypeFlags.Plant));
        numRoughAnimals = roughAnimals.Count();
        numHerbivoreOutdoor = roughAnimals
            .Count(a => a.Position.GetRoomOrAdjacent(currentMap).UsesOutdoorTemperature);
        numHerbivoreIndoor = numRoughAnimals - numHerbivoreOutdoor;

        numHen = roughAnimals
            .Count(x => x.def.defName == "Chicken" && x.gender == Gender.Female &&
                        x.ageTracker.CurLifeStage.defName ==
                        "AnimalAdult"); // x.def.defName == "Chicken" worked, but x.Label == "chicken" didn't work

        ThingDef first = null;
        foreach (var x in DefDatabase<ThingDef>.AllDefs)
        {
            if (x.ingestible == null || x.defName != "Hay")
            {
                continue;
            }

            first = x;
            break;
        }

        if (first == null)
        {
            return;
        }

        var hayNut = first.ingestible.CachedNutrition;
        dailyHayConsumptionIndoorAnimals = roughAnimals
            .Where(a => !a.Position.GetRoomOrAdjacent(currentMap).UsesOutdoorTemperature)
            .Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay / hayNut;
        dailyHayConsumptionRoughAnimals =
            roughAnimals.Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay / hayNut;
        //TODO: find animals' hunger rate when they are full instead whatever state they are in now.

        var kibbleAnimals = pawns.Where(x =>
            x.RaceProps.Animal && !x.RaceProps.Eats(FoodTypeFlags.Plant) && x.RaceProps.Eats(FoodTypeFlags.Kibble));
        numKibbleAnimals = kibbleAnimals.Count();
        dailyKibbleConsumption = kibbleAnimals
            .Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay / hayNut;


        numHaygrass = allHaygrass.Count();
        numHay = currentMap.listerThings.AllThings.Where(x => x.def.label == "hay").Sum(x => x.stackCount);


        //*
        if (Predictor.daysUntilGrowingPeriodOver > 0)
        {
            predictor.allPredType["Haygrass"].SetUpdateRule(
                (int)((1 - GenLocalDate.DayPercent(currentMap)) *
                      (dailyHayConsumptionRoughAnimals + (dailyKibbleConsumption * 2f / 5f))),
                (int)(dailyHayConsumptionRoughAnimals + (dailyKibbleConsumption * 2f / 5f)));
        }
        else
        {
            predictor.allPredType["Haygrass"].SetUpdateRule(
                (int)((1 - GenLocalDate.DayPercent(currentMap)) *
                      (dailyHayConsumptionRoughAnimals + (dailyKibbleConsumption * 2f / 5f))),
                (int)(dailyHayConsumptionRoughAnimals + (dailyKibbleConsumption * 2f / 5f)));
        }

        //*/
        consList.Clear();
        consList.Add(new Consumer(
            "Human",
            DefDatabase<ThingDef>.AllDefs.FirstOrDefault(x => x.race != null && x.defName == "Human")?.race,
            pawns.Count(x => x.RaceProps.Humanlike && x.ageTracker.CurLifeStage.defName == "HumanlikeChild"),
            pawns.Count(x => x.RaceProps.Humanlike && x.ageTracker.CurLifeStage.defName == "HumanlikeTeenager"),
            pawns.Count(x => x.RaceProps.Humanlike && x.ageTracker.CurLifeStage.defName == "HumanlikeAdult"),
            pawns.Where(x => x.RaceProps.Humanlike).Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay,
            pawns.Where(x => x.RaceProps.Humanlike).Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay /
            DefDatabase<ThingDef>.AllDefs.FirstOrDefault(x => x.ingestible != null && x.defName == "MealSimple")!
                .ingestible.CachedNutrition
        ));
        foreach (var a in DefDatabase<ThingDef>.AllDefs.Where(x => x.race is { Animal: true })
                     .OrderBy(x => x.defName))
        {
            var type = pawns.Where(x => x.RaceProps.Animal && x.def.defName == a.defName);
            var totalNut = type.Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay;
            consList.Add(new Consumer(
                a.defName,
                a.race,
                type.Count(x => x.ageTracker.CurLifeStage.defName == "AnimalBaby"),
                type.Count(x => x.ageTracker.CurLifeStage.defName == "AnimalJuvenile"),
                type.Count(x => x.ageTracker.CurLifeStage.defName == "AnimalAdult"),
                totalNut,
                totalNut / hayNut
            ));
        }

        // TODO: Find.ZoneManager.AllZones, potentially look at unplannted cells in growing zones and predict work amount and complete time.
        // look at Zone_Growing class , ZoneManager class
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (GenTicks.TicksAbs - lastUpdateTick >= GenDate.TicksPerHour / 6)
        {
            lastUpdateTick = GenTicks.TicksAbs;
            predictor.MakePrediction(0);
        }


        var rect2 = inRect;
        rect2.yMin += 45f; // move top border downs
        var list = new List<TabRecord>
        {
            new TabRecord("Graph".Translate(), delegate { curTab = TabType.Graph; }, curTab == TabType.Graph),
            new TabRecord("Analytics", delegate { curTab = TabType.Analytics; }, curTab == TabType.Analytics),
            new TabRecord("Detailed List", delegate { curTab = TabType.DetailedList; },
                curTab == TabType.DetailedList),
            new TabRecord("Help", delegate { curTab = TabType.Help; }, curTab == TabType.Help),
            new TabRecord("Note", delegate { curTab = TabType.Note; }, curTab == TabType.Note)
        };

        TabDrawer.DrawTabs(rect2, list);

        if (curTab == TabType.Graph)
        {
            DisplayGraphPage(rect2);
        }
        else if (curTab == TabType.Analytics)
        {
            DisplayAnalyticsPage(rect2);
        }
        else if (curTab == TabType.Help)
        {
            DisplayHelpPage(rect2);
        }
        else if (curTab == TabType.DetailedList)
        {
            DisplayDetailedListPage(rect2);
        }
        else if (curTab == TabType.Note)
        {
            DisplayNotePage(rect2);
        }
    }

    private void DisplayNotePage(Rect rect)
    {
        rect.y += 6;
        note.text = GUI.TextArea(new Rect(rect.x, rect.y, rect.width * 0.85f, rect.height), note.text);
    }

    private void DisplayAnalyticsPage(Rect rect)
    {
        // constructing string
        var analysis =
            "Dates:" +
            "\nDays Until Winter = " + Predictor.daysUntilWinter +
            "\nDays until growing period over = " + Predictor.daysUntilGrowingPeriodOver +
            "\nDays until the end of winter = " + Predictor.daysUntilEndofWinter +
            "\nDays until next harvest season = " + Predictor.daysUntilNextHarvestSeason +
            "\n\nGeneric Stats:" +
            "\nNumber of animals eating hay = " + numRoughAnimals + ", Number of animals eating kibble = " +
            numKibbleAnimals +
            "\nNumber of meat = " + numMeat + ", number of egg = " + numEgg +
            "\nNumber of hen = " + numHen +
            "\nEstimated number of kibble needed for all kibble-eaters = " + (int)dailyKibbleConsumption +
            ", meat =" + (dailyKibbleConsumption * 2 / 5) +
            ", egg=" + (dailyKibbleConsumption * 4 / 50) +
            predictor.allPredType["Haygrass"].analysis;


        /*
        foreach (var v in debug_val)
        {
            analysis += v + ",";
        }

        //*/

        /*
        foreach (string s in predictor.allPredType.Keys)
        {
            analysis += s + ",";
        }
        foreach (ThingDef x in plantDef)
        {
            analysis += x.defName + ",";
        }
        
        analysis += ",\n" + GenDate.CurrentMonth + "," + GenDate.CurrentSeason + "," +
                                    GenDate.DayOfMonth + "," + GenDate.DayOfYear + "," +
                                    GenTicks.TicksAbs + "," + Find.TickManager.TicksAbs + ",";
        //*/

        // draw text
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        var rect2 = new Rect(0, 0, rect.width, analysis.Split('\n').Length * 20);
        Widgets.BeginScrollView(rect, ref scrollPos[(int)TabType.Analytics], rect2);
        Widgets.Label(rect2, analysis);
        Widgets.EndScrollView();
    }

    private void DisplayGraphPage(Rect rect)
    {
        var currentMap = Find.CurrentMap;
        var btn = new Rect(0, rect.yMin + 6, 110f, 40f);
        if (Widgets.ButtonText(btn, "New Chart", true, false))
        {
            chartList.Add(new LineChart(60, ref predictor, currentMap));
        }


        if (chartList.NullOrEmpty())
        {
            return;
        }

        // update curves in each chart
        foreach (var c in chartList)
        {
            c.UpdateData(ref predictor);
        }

        // start drawing all charts
        rect.yMin = btn.yMax;
        Widgets.BeginScrollView(rect, ref scrollPos[(int)TabType.Graph],
            new Rect(rect.x, rect.yMin, chartList[0].rect.width, chartList[0].rect.height * chartList.Count));

        var newRect = new Rect(rect.xMin, btn.yMax, rect.width, rect.height);
        foreach (var g in chartList)
        {
            g.Draw(newRect);
            newRect = g.remove
                ? new Rect(g.rect.x, g.rect.yMin, rect.width, rect.height)
                : new Rect(g.rect.x, g.rect.yMax, rect.width, rect.height);
        }

        chartList.RemoveAll(g => g.remove);

        predictor.EnablePrediction(chartList);

        Widgets.EndScrollView();
    }

    private void DisplayHelpPage(Rect rect)
    {
    }

    private void DisplayDetailedListPage(Rect rect)
    {
        GUI.BeginGroup(rect); // TODO: figure out how to do scroll view below titles
        var x = 0;
        var sourceButton = new Rect(0f, 6f, 200f, 35f);
        if (Widgets.ButtonText(sourceButton, Source.ToString().Translate()))
        {
            var options = new List<FloatMenuOption>();
            if (Source != SourceOptions.Plants)
            {
                options.Add(new FloatMenuOption("Plants", delegate
                {
                    Source = SourceOptions.Plants;
                    IsDirty = true;
                }));
            }

            if (Source != SourceOptions.Animals)
            {
                options.Add(new FloatMenuOption("Animals", delegate
                {
                    Source = SourceOptions.Animals;
                    IsDirty = true;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        var offset = true;
        var pawnTypeList = DefDatabase<PawnKindDef>.AllDefs.Where(a => a.RaceProps.Animal).ToList();

        var nameRect = new Rect(x, sourceButton.height + 50f, 175f, 30f);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(nameRect, "Name");

        TooltipHandler.TipRegion(nameRect, "ClickToSortByName");
        Widgets.DrawHighlightIfMouseover(nameRect);

        var listing = new Listing_Standard();
        listing.Begin(new Rect(nameRect.x, nameRect.yMax, nameRect.width, consList.Count * 30));
        foreach (var c in consList.Where(cc => cc.numTotal > 0))
        {
            listing.Label(c.label);
        }

        listing.Label("Total");
        listing.End();

        x += 175;
        var headerNames = new List<string>
            { "Total", "Adult", "Teen", "Baby", "Daily Consumption[nut]", "Daily Meals/Hay/Kibbles" };
        var colWidth = (rect.width - x) / headerNames.Count;

        // loop from left column to rightmost column
        for (var i = 0; i < headerNames.Count; i++)
        {
            // draw title in each column
            var labelRect = new Rect(x + (colWidth * i) - (colWidth / 2),
                sourceButton.height + 10 + (offset ? 10f : 40f), colWidth * 2, 30f);
            Widgets.DrawLine(
                new Vector2(x + (colWidth * (i + 1)) - (colWidth / 2), sourceButton.height + 40f + (offset ? 5f : 35f)),
                new Vector2(x + (colWidth * (i + 1)) - (colWidth / 2), sourceButton.height + 80f), Color.gray, 1);

            Widgets.Label(labelRect, headerNames[i]);

            listing = new Listing_Standard();
            // setting drawing rect for each column
            //listing = new Listing_Standard(new Rect(labelRect.x, nameRect.yMax, labelRect.width, consList.Count() * (Text.LineHeight + listing.verticalSpacing)));
            switch (i)
            {
                case 0:
                    ///listing.End();
                    listing.Begin(new Rect(
                        labelRect.x + (labelRect.width / 2f),
                        nameRect.yMax,
                        consList.Max(ccc => GUI.skin.label.CalcSize(new GUIContent(ccc.numTotal.ToString())).x) +
                        30, // 24 + 6
                        consList.Count * (Text.LineHeight + listing.verticalSpacing)));
                    break;
                case 1:
                    //listing.Label(c.numAdult.ToString());
                    listing.Begin(new Rect(
                        labelRect.x + (labelRect.width / 2f),
                        nameRect.yMax,
                        consList.Max(ccc => GUI.skin.label.CalcSize(new GUIContent(ccc.numAdult.ToString())).x) +
                        30, // 24 + 6
                        consList.Count * (Text.LineHeight + listing.verticalSpacing)));
                    break;
                case 2:
                    //listing.Label(c.numTeen.ToString());
                    listing.Begin(new Rect(
                        labelRect.x + (labelRect.width / 2f),
                        nameRect.yMax,
                        consList.Max(ccc => GUI.skin.label.CalcSize(new GUIContent(ccc.numTeen.ToString())).x) +
                        30, // 24 + 6
                        consList.Count * (Text.LineHeight + listing.verticalSpacing)));
                    break;
                case 3:
                    //listing.Label(c.numInfant.ToString());
                    listing.Begin(new Rect(
                        labelRect.x + (labelRect.width / 2f),
                        nameRect.yMax,
                        consList.Max(ccc => GUI.skin.label.CalcSize(new GUIContent(ccc.numInfant.ToString())).x) +
                        30, // 24 + 6
                        consList.Count * (Text.LineHeight + listing.verticalSpacing)));
                    break;
                case 4:
                    //listing.Label(String.Format("{0:0.00}", c.totalNutr));
                    listing.Begin(new Rect(
                        labelRect.x + (labelRect.width / 2f),
                        nameRect.yMax,
                        consList.Max(ccc =>
                            GUI.skin.label.CalcSize(new GUIContent($"{ccc.numAdult:0.00}")).x) +
                        30, // 24 + 6
                        consList.Count * (Text.LineHeight + listing.verticalSpacing)));
                    break;
                case 5:
                    //listing.Label(String.Format("{0:0.0}", c.numFood));
                    listing.Begin(new Rect(
                        labelRect.x + (labelRect.width / 2f),
                        nameRect.yMax,
                        consList.Max(ccc =>
                            GUI.skin.label.CalcSize(new GUIContent($"{ccc.numFood:0.0}")).x) +
                        30, // 24 + 6
                        consList.Count * (Text.LineHeight + listing.verticalSpacing)));
                    break;
            }

            // draw stats in each column below the title
            var buf = false;
            foreach (var c in consList.Where(cc => cc.numTotal > 0))
            {
                GUI.color = Color.white;
                switch (i)
                {
                    case 0:
                        listing.CheckboxLabeled(c.numTotal.ToString(), ref buf);
                        break;
                    case 1:
                        listing.CheckboxLabeled(c.numAdult.ToString(), ref buf);
                        break;
                    case 2:
                        listing.CheckboxLabeled(c.numTeen.ToString(), ref buf);
                        break;
                    case 3:
                        listing.CheckboxLabeled(c.numInfant.ToString(), ref buf);
                        break;
                    case 4:
                        listing.CheckboxLabeled($"{c.totalNutr:0.00}", ref buf);
                        break;
                    case 5:
                        listing.CheckboxLabeled($"{c.numFood:0.0}", ref buf);
                        break;
                }
            }

            // last row of total values
            switch (i)
            {
                case 0:
                    listing.Label(consList.Sum(c => c.numTotal).ToString());
                    break;
                case 1:
                    listing.Label(consList.Sum(c => c.numAdult).ToString());
                    break;
                case 2:
                    listing.Label(consList.Sum(c => c.numTeen).ToString());
                    break;
                case 3:
                    listing.Label(consList.Sum(c => c.numInfant).ToString());
                    break;
                case 4:
                    listing.Label($"{consList.Sum(c => c.totalNutr):0.00}");
                    break;
                case 5:
                    listing.Label($"{consList.Sum(c => c.numFood):0.0}");
                    break;
            }

            listing.End();

            /*
            if (Widgets.ButtonInvisible(defLabel))
            {
                if (OrderBy == Order.Efficiency && OrderByCapDef == CapDefs[i])
                {
                    Asc = !Asc;
                }
                else
                {
                    OrderBy = Order.Efficiency;
                    OrderByCapDef = CapDefs[i];
                    Asc = true;
                }
                IsDirty = true;
            }
            //*/

            TooltipHandler.TipRegion(labelRect, "ClickToSortBy" + headerNames[i]);
            Widgets.DrawHighlightIfMouseover(labelRect);

            offset = !offset;
        }

        GUI.color = Color.gray;
        for (var k = 0; k < consList.Count(c => c.numTotal > 0) + 1; k++)
        {
            Widgets.DrawLineHorizontal(0f, nameRect.yMax + (k * (Text.LineHeight + listing.verticalSpacing)),
                rect.width);
        }

        GUI.EndGroup();
    }

    //graphics 
    private enum TabType : byte
    {
        Graph = 0,
        Analytics = 1,
        Help = 2,
        DetailedList = 3,
        Note = 4
    }

    [Serializable]
    private class DataPoint
    {
        private string date;

        public DataPoint(string s)
        {
            date = s;
        }
    }

    private class Consumer
    {
        public readonly string label;
        public readonly int numAdult;
        public readonly float numFood;
        public readonly int numInfant;
        public readonly int numTeen;
        public readonly int numTotal;

        public readonly float totalNutr;
        public RaceProperties prop;

        public Consumer(string s, RaceProperties r, int infant, int teen, int adult, float nut, float food)
        {
            label = s;
            numAdult = adult;
            numTeen = teen;
            numInfant = infant;
            numTotal = numAdult + numTeen + numInfant;
            totalNutr = nut;
            numFood = food;
            prop = r;
        }
    }
}