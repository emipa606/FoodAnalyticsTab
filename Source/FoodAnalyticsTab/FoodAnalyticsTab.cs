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
using Verse;
using RimWorld;
using UnityEngine;

namespace FoodAnalyticsTab
{
    [StaticConstructorOnStartup]
    public class MainTabWindow_Estimator : MainTabWindow
    {
        //graphics 
        private enum FoodAnalyticsTab : byte
        {
            Graph,
            Analytics
        }
        private Vector2 graphSection = default(Vector2);
        private MainTabWindow_Estimator.FoodAnalyticsTab curTab = FoodAnalyticsTab.Graph, prevTab = FoodAnalyticsTab.Analytics;
        private List<CurveMark> marks = new List<CurveMark>();
        Vector2 scrollPos = Vector2.zero; // for scrolling view

        protected float lastUpdate = -1f;
        private int lastUpdateTick = 0;

        // analytics
        private float totalNeededHay, totalNeededKibble, totalNeededHayIncludingKibble;
        private int numAnimals, numHaygrass, numHay;
        private class hayProjection
        {
            public hayProjection(float a, float b, float c, float d)
            {
                maxYield = a;
                minYield = b;
                stockMax = c;
                stockMin = d;
            }
            public float maxYield { get; set; }
            public float minYield { get; set; }
            public float stockMax { get; set; }
            public float stockMin { get; set; }
        };
        private static int nextNDays = 25; // default display 25 days
        private float dayNumSlider_curr = nextNDays, dayNumSlider_prev = nextNDays;
        
        List<hayProjection> projectedHayRecords = new List<hayProjection>(); 
        private class haygrassGrowth
        {
            public haygrassGrowth(float a, float b)
            {
                Growth = a;
                GrowthPerTick = b;
            }
            public float Growth { get; set; }
            public float GrowthPerTick { get; set; }
        };
        private float[] debug_val = new float[10];
        // important dates
        private int daysUntilWinter;// to Dec 1st
        private int daysUntilEndofWinter; // to February 5th
        private int daysUntilGrowingPeriodOver; // to 10th of Fall, Oct 5th
        private int daysUntilNextHarvestSeason; // to 10th of Spring, April 5th
        static float hayNut = (from d in DefDatabase<ThingDef>.AllDefs.Where(x => x.defName == "Hay")
                               select d).FirstOrDefault().ingestible.nutrition;
        static float haygrass_yieldMax = GenMath.RoundRandom(
            (from d in DefDatabase<ThingDef>.AllDefs.Where(x => x.defName == "PlantHaygrass")
             select d).FirstOrDefault().plant.harvestYield );
        static float haygrass_yieldMin = GenMath.RoundRandom(
            (from d in DefDatabase<ThingDef>.AllDefs.Where(x => x.defName == "PlantHaygrass")
             select d).FirstOrDefault().plant.harvestYield * 0.5f * 0.5f// 1st 0.5 is harvesting at 65% growth, 2nd 0.5 is lowest health.
            ); 

        // functions
        public override Vector2 RequestedTabSize
        {
            get
            {
                return new Vector2(1010f, Screen.height);
            }
        }

        public override void PreOpen()
        {
            base.PreOpen();
            updateCalculations();
            lastUpdate = Time.time;
        }

        private void updateCalculations()
        {

            var pawns = (from p in Find.MapPawns.PawnsInFaction(Faction.OfPlayer)
                         select p).ToList();
            var allHaygrass = Find.ListerThings.AllThings.Where(x => x.Label == "haygrass");
            numAnimals = pawns.Where(x => x.RaceProps.Animal).Count();

            totalNeededHay = pawns
                           .Where(x => x.RaceProps.Animal &&
                                (x.RaceProps.foodType == FoodTypeFlags.OmnivoreRoughAnimal ||
                                x.RaceProps.foodType == FoodTypeFlags.VegetableOrFruit ||
                                x.RaceProps.foodType == FoodTypeFlags.VegetarianAnimal ||
                                x.RaceProps.foodType == FoodTypeFlags.VegetarianRoughAnimal))
                           .Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay / hayNut;
            debug_val[1] = hayNut;
            //TODO: find animals' hunger rate when they are full instead whatever state they are in now.

            totalNeededKibble = pawns.Where(x => x.RaceProps.Animal &&
                           (x.RaceProps.foodType == FoodTypeFlags.OmnivoreAnimal ||
                           x.RaceProps.foodType == FoodTypeFlags.CarnivoreAnimal))
                           .Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay / hayNut;
            totalNeededHayIncludingKibble = totalNeededKibble * 2f / 5f + totalNeededHay;

            numHaygrass = allHaygrass.Count();
            numHay = Find.ListerThings.AllThings.Where(x => x.def.label == "hay").Sum(x => x.stackCount);

            // calculate yield for today
            List<haygrassGrowth> allHaygrassGrowth = new List<haygrassGrowth>();
            projectedHayRecords.Clear();
            projectedHayRecords.Add(new hayProjection(0, 0, numHay, numHay));
            if (GenDate.HourOfDay >= 4 && GenDate.HourOfDay <= 19)
            {
                foreach (var h in allHaygrass)
                {
                    //debug_val[0] = ((Plant)h).Growth;
                    //debug_val[1] = ((Plant)h).GrowthRate / (GenDate.TicksPerDay * h.def.plant.growDays);
                    allHaygrassGrowth.Add(new haygrassGrowth(
                        ((Plant)h).Growth + GenDate.TicksPerHour * (19 - GenDate.HourOfDay) * ((Plant)h).GrowthRate / (GenDate.TicksPerDay * h.def.plant.growDays),
                        ((Plant)h).GrowthRate / (GenDate.TicksPerDay * h.def.plant.growDays)));
                    if (allHaygrassGrowth.LastOrDefault().Growth >= 1.0f)
                    {
                        projectedHayRecords[0].maxYield += haygrass_yieldMax;//18;//TODO: hardcoded here, need to find def in xml files
                        projectedHayRecords[0].minYield += haygrass_yieldMin;// (yieldMin = h.def.plant.harvestYield * 0.5f); // min
                    }
                }
                projectedHayRecords[0].stockMax += projectedHayRecords[0].maxYield;
                projectedHayRecords[0].stockMin += projectedHayRecords[0].minYield;
            }
            projectedHayRecords[0].stockMax -= totalNeededHayIncludingKibble;
            projectedHayRecords[0].stockMin -= totalNeededHayIncludingKibble;
            if (projectedHayRecords[0].stockMax <= 0f)
            {
                projectedHayRecords[0].stockMax = 0;
            }
            if (projectedHayRecords[0].stockMin <= 0f)
            {
                projectedHayRecords[0].stockMin = 0;
            }
            // calculate yields and stocks after today
            for (int day = 1; day < nextNDays; day++)
            {
                projectedHayRecords.Add(new hayProjection(0, 0, projectedHayRecords[day - 1].stockMax, projectedHayRecords[day - 1].stockMin));
                foreach (haygrassGrowth k in allHaygrassGrowth)
                {
                    k.Growth += k.GrowthPerTick * GenDate.TicksPerDay * 0.55f; // 0.55 is 55% of time plant spent growing
                    //debug_val[6] = k.GrowthPerTick;
                    if (k.Growth >= 1.0f)
                    {
                        projectedHayRecords[day].maxYield += haygrass_yieldMax;
                        projectedHayRecords[day].minYield += haygrass_yieldMin;
                        k.Growth = 0.05f; // if it's fully grown, replant and their growths start at 5%.
                    }
                }
                projectedHayRecords[day].stockMax += projectedHayRecords[day].maxYield;
                projectedHayRecords[day].stockMin += projectedHayRecords[day].minYield;

                projectedHayRecords[day].stockMax -= totalNeededHayIncludingKibble;
                projectedHayRecords[day].stockMin -= totalNeededHayIncludingKibble;

                if (projectedHayRecords[day].stockMax <= 0f)
                {
                    projectedHayRecords[day].stockMax = 0;
                }
                if (projectedHayRecords[day].stockMin <= 0f)
                {
                    projectedHayRecords[day].stockMin = 0;
                }
            }
            //Yield × (1 − 0.45[Plant Resting])×[([Fertility of Soil] × Fertility Factor) + 1 − Fertility Factor) ÷ Growth Time]
            
            // calculating number of days until certain dates
            this.daysUntilWinter = ((Month.Dec - GenDate.CurrentMonth - 1) * GenDate.DaysPerMonth + (GenDate.DaysPerMonth - GenDate.DayOfMonth)); // to Dec 1st

            if (GenDate.CurrentMonth > Month.Feb)
            {
                this.daysUntilEndofWinter = ((13 - (int)GenDate.CurrentMonth) * GenDate.DaysPerMonth + (GenDate.DaysPerMonth - GenDate.DayOfMonth));
            }
            else
            {
                this.daysUntilEndofWinter = ((Month.Feb - GenDate.CurrentMonth) * GenDate.DaysPerMonth + (GenDate.DaysPerMonth - GenDate.DayOfMonth));
            }

            if (GenDate.CurrentMonth >= Month.Mar && GenDate.CurrentMonth < Month.Nov)
            {
                this.daysUntilGrowingPeriodOver = ((Month.Oct - GenDate.CurrentMonth) * GenDate.DaysPerMonth + (GenDate.DaysPerMonth - GenDate.DayOfMonth));
            } else
            {
                this.daysUntilGrowingPeriodOver = 0;
            }

            if (GenDate.CurrentMonth > Month.Apr && GenDate.CurrentMonth <= Month.Dec)
            {
                this.daysUntilNextHarvestSeason = ((15 - (int)GenDate.CurrentMonth) * GenDate.DaysPerMonth + (GenDate.DaysPerMonth - GenDate.DayOfMonth));
            }
            else
            {
                this.daysUntilNextHarvestSeason = ((Month.Apr - GenDate.CurrentMonth) * GenDate.DaysPerMonth + (GenDate.DaysPerMonth - GenDate.DayOfMonth));
            }
        }
        public override void DoWindowContents(Rect inRect)
        {
            base.DoWindowContents(inRect);
            bool updated = false;
            if (dayNumSlider_curr != dayNumSlider_prev || GenTicks.TicksAbs - lastUpdateTick >= (GenDate.TicksPerHour/5)) // Find.TickManager.Paused
            {
                lastUpdateTick = GenTicks.TicksAbs;
                nextNDays = (int)dayNumSlider_curr;
                updateCalculations();
                updated = true;
            }

            
            Rect rect2 = inRect;
            rect2.yMin += 45f; // move top border downs
            List<TabRecord> list = new List<TabRecord>();
            list.Add(new TabRecord("Graph".Translate(), delegate
            {
                this.curTab = FoodAnalyticsTab.Graph;
            }, this.curTab == FoodAnalyticsTab.Graph));

            list.Add(new TabRecord("Analytics", delegate
            {
                this.curTab = FoodAnalyticsTab.Analytics;
            }, this.curTab == FoodAnalyticsTab.Analytics));

            TabDrawer.DrawTabs(rect2, list);

            if (this.curTab == FoodAnalyticsTab.Graph)
            {
                this.DisplayGraphPage(rect2);
                //this.prevTab = FoodAnalyticsTab.Graph;
            } else if (this.curTab == FoodAnalyticsTab.Analytics)
            {
                this.DisplayAnalyticsPage(rect2);
                //this.prevTab = FoodAnalyticsTab.Analytics;
            }            
        }

        private void DisplayAnalyticsPage(Rect rect)
        {
            // constructing string
            string analysis = "Number of animals you have = " + (int)numAnimals +
                            "\nNumber of hay in stockpiles and on the floors = " + (int)numHay +
                            "\nEstimated number of hay needed daily for hay-eaters only= " + (int)totalNeededHay +
                            "\nEstimated number of kibble needed for all kibble-eaters = " + (int) totalNeededKibble +
                            "\nEstimated number of hay needed daily for all animals = " + (int)totalNeededHayIncludingKibble +
                            "\nNumber of days until hay in stockpiles run out = " + String.Format("{0:0.0}", numHay / totalNeededHayIncludingKibble) +
                            "\nDays Until Winter = " + this.daysUntilWinter + ", Days until growing period over = " + this.daysUntilGrowingPeriodOver +
                            ", Days until the end of winter = " + this.daysUntilEndofWinter + ", Days until next harvest season = " + this.daysUntilNextHarvestSeason +
                            "\nEstimated number of hay needed until winter for hay-eaters only = " + (int)(totalNeededHay * this.daysUntilWinter) +
                            "\nEstimated number of hay needed until winter for all animals = " + (int)(totalNeededHayIncludingKibble * this.daysUntilWinter) +
                            "\nEstimated number of hay needed until the end of winter for hay-eaters only = " + (int)(totalNeededHay * this.daysUntilEndofWinter) +
                            "\nEstimated number of hay needed until the end of winter for all animals = " + (int)(totalNeededHayIncludingKibble * this.daysUntilEndofWinter) +
                            "\nEstimated number of hay needed yearly for hay-eaters only = " + (int)(totalNeededHay * GenDate.DaysPerMonth * GenDate.MonthsPerYear) + // 60 days
                            "\nEstimated number of hay needed yearly for all animals = " + (int)(totalNeededHayIncludingKibble * GenDate.DaysPerMonth * GenDate.MonthsPerYear) + // 60 days
                            "\nEstimated number of hay needed until next harvest season(10th of Spring) for all animals = " + (int)(totalNeededHayIncludingKibble * this.daysUntilNextHarvestSeason) +
                            "\nNumber of haygrass plant = " + (int)numHaygrass +
                            "\nEstimate of projected harvest production:\n";
            analysis += "Day\t Max Yield Min Yield Max Stock Min Stock\n";
            int i = 0;
            foreach (hayProjection k in projectedHayRecords)
            {
                analysis += String.Format("{0,-2}\t {1,-6}\t {2,-6}\t {3,-6}\t {4,-6}\n",
                    i, (int)k.maxYield, (int)k.minYield, (int)k.stockMax, (int)k.stockMin);
                i++;
            }
            foreach (var v in debug_val)
            {
                analysis += v + ",";
            }
            analysis += ",\n" + GenDate.CurrentMonth + "," + GenDate.CurrentSeason + "," +
                                        GenDate.DayOfMonth + "," + GenDate.DayOfYear + "," +
                                        GenTicks.TicksAbs + "," + Find.TickManager.TicksAbs + ",";

            // draw text
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            Rect rect2 = new Rect(0, 0, rect.width, analysis.Split('\n').Length * 20);
            Widgets.BeginScrollView(rect, ref this.scrollPos, rect2);
            Widgets.Label(rect2, analysis);
            Widgets.EndScrollView();
        }

        private void DisplayGraphPage(Rect rect)
        {
            rect.yMin += 17f;
            GUI.BeginGroup(rect);
            Rect graphRect = new Rect(0f, 0f, rect.width, 450f);
            Rect legendRect = new Rect(0f, graphRect.yMax, rect.width, 40f);

            //marks add dots on top of a graph, the text label is the text in the popup box
            this.marks.Clear();            
            this.marks.Add(new CurveMark(this.daysUntilNextHarvestSeason, "Days until the Next Harvest Season", Color.green));
            this.marks.Add(new CurveMark(this.daysUntilNextHarvestSeason, "Days until the Next Harvest Season", Color.green));// fix i++ bug
            this.marks.Add(new CurveMark(this.daysUntilGrowingPeriodOver, "Days until Growing Period is Over", Color.red));
            this.marks.Add(new CurveMark(this.daysUntilGrowingPeriodOver, "Days until Growing Period is Over", Color.red));
            this.marks.Add(new CurveMark(this.daysUntilWinter, "Days until the Winter", Color.white));
            this.marks.Add(new CurveMark(this.daysUntilWinter, "Days until the Winter", Color.white));
            this.marks.Add(new CurveMark(this.daysUntilEndofWinter, "Days until the End of Winter", Color.yellow));
            this.marks.Add(new CurveMark(this.daysUntilEndofWinter, "Days until the End of Winter", Color.yellow));
            //debug_val[0] = marks.Count;
            // plotting graphs
            List<SimpleCurveDrawInfo> curves= new List<SimpleCurveDrawInfo>();

            SimpleCurveDrawInfo simpleCurveDrawInfo = new SimpleCurveDrawInfo();
            simpleCurveDrawInfo.color = Color.green;
            simpleCurveDrawInfo.label = "Hay Max Yield";
            simpleCurveDrawInfo.curve = new SimpleCurve();
            for (int i = 0; i < nextNDays; i++)
            {
                simpleCurveDrawInfo.curve.Add(new CurvePoint(i, projectedHayRecords[i].maxYield));
            }
            curves.Add(simpleCurveDrawInfo);

            simpleCurveDrawInfo = new SimpleCurveDrawInfo();
            simpleCurveDrawInfo.color = Color.red;
            simpleCurveDrawInfo.label = "Hay Min Yield";
            simpleCurveDrawInfo.curve = new SimpleCurve();
            for (int i = 0; i < nextNDays; i++)
            {
                simpleCurveDrawInfo.curve.Add(new CurvePoint(i, projectedHayRecords[i].minYield));
            }
            curves.Add(simpleCurveDrawInfo);

            simpleCurveDrawInfo = new SimpleCurveDrawInfo();
            simpleCurveDrawInfo.color = Color.white;
            simpleCurveDrawInfo.label = "Hay Max Stock";
            simpleCurveDrawInfo.curve = new SimpleCurve();
            for (int i = 0; i < nextNDays; i++)
            {
                simpleCurveDrawInfo.curve.Add(new CurvePoint(i, projectedHayRecords[i].stockMax));
            }
            curves.Add(simpleCurveDrawInfo);

            simpleCurveDrawInfo = new SimpleCurveDrawInfo();
            simpleCurveDrawInfo.color = Color.magenta;
            simpleCurveDrawInfo.label = "Hay Min Stock";
            simpleCurveDrawInfo.curve = new SimpleCurve();
            for (int i = 0; i < nextNDays; i++)
            {
                simpleCurveDrawInfo.curve.Add(new CurvePoint(i, projectedHayRecords[i].stockMin));
            }
            curves.Add(simpleCurveDrawInfo);

            SimpleCurveDrawerStyle curveDrawerStyle = new SimpleCurveDrawerStyle();
            curveDrawerStyle.UseFixedSection = true;
            curveDrawerStyle.FixedSection = new Vector2(0, nextNDays);
            curveDrawerStyle.LabelY = "Hay #";
            curveDrawerStyle.LabelX = "Day";
            curveDrawerStyle.UseFixedScale = false;
            curveDrawerStyle.FixedScale = new Vector2(500,900);
            curveDrawerStyle.DrawBackground = true; // draw gray background behind graph
            curveDrawerStyle.DrawBackgroundLines = true; // 
            curveDrawerStyle.DrawMeasures = true;
            curveDrawerStyle.MeasureLabelsXCount = nextNDays; // number of marks on x axis 
            curveDrawerStyle.MeasureLabelsYCount = 5;
            curveDrawerStyle.DrawPoints = false; // draw white points for each data
            curveDrawerStyle.DrawLegend = true; //
            curveDrawerStyle.DrawCurveMousePoint = true; // hover over graph shows details
                                                         //curveDrawerStyle.OnlyPositiveValues = false;
                                                         //curveDrawerStyle.PointsRemoveOptimization = false;
            curveDrawerStyle.UseAntiAliasedLines = true; // smooth lines
            

            Text.Anchor = TextAnchor.UpperLeft;
            SimpleCurveDrawer.DrawCurves(graphRect, curves, curveDrawerStyle, this.marks, legendRect);
            dayNumSlider_prev = dayNumSlider_curr;
            dayNumSlider_curr =  Widgets.HorizontalSlider(new Rect(0, legendRect.yMax, rect.width, 50), dayNumSlider_curr, 1, 60);
            GUI.EndGroup();
        }

        
    }
}
