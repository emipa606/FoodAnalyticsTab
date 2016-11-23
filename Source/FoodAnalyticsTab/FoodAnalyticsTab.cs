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
            Graph= 0,
            Analytics = 1,
            Help = 2
        }
        private MainTabWindow_Estimator.FoodAnalyticsTab curTab = FoodAnalyticsTab.Graph, prevTab = FoodAnalyticsTab.Analytics;
        Vector2[] scrollPos = new Vector2[3] { Vector2.zero, Vector2.zero, Vector2.zero }; // for scrolling view

        protected float lastUpdate = -1f;
        private int lastUpdateTick = 0;

        // analytics
        private float totalNeededHay, dailyKibbleConsumption, dailyHayConsumption;
        private int numAnimals, numRoughAnimals, numKibbleAnimals, numHaygrass, numHay, numMeat;
        private class prediction
        {
            public class MinMax
            {
                public bool showDeficiency { get; set; } = false;
                private float _min, _max;
                public float min
                {
                    get { return _min; }
                    set
                    {
                        _min = value;
                        if (showDeficiency != true && value < 0)
                        {
                            _min = 0;
                        }
                    }
                }
                public float max
                {
                    get { return _max; }
                    set
                    {
                        _max = value;
                        if (showDeficiency != true && value < 0)
                        {
                           _max = 0;        
                        }
                    }
                }
            }
            private bool _showDeficiency;
            public bool showDeficiency
            {
                get { return _showDeficiency; }
                set
                {
                    _showDeficiency = hay_yield.showDeficiency = hay_stock.showDeficiency = meat_stock.showDeficiency =
                        animal_population.showDeficiency = hay_consumption.showDeficiency = value;
                }
            }

            public prediction(MinMax a, MinMax b, MinMax c =null, MinMax d = null, MinMax e = null)
            {
                hay_yield = a;
                hay_consumption = (e == null) ? new MinMax() : e;
                hay_stock = b;
                meat_stock = (c == null) ? new MinMax() : c;
                animal_population = (d == null) ? new MinMax() : d;
                showDeficiency = false;
            }
            public MinMax hay_yield { get; set; }
            public MinMax hay_stock { get; set; }
            public MinMax meat_stock { get; set; }
            public MinMax animal_population { get; set; }
            public MinMax hay_consumption { get; set; }

        };

        private static int nextNDays = 25; // default display 25 days

        List<prediction> projectedRecords = new List<prediction>(); 
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
        List<haygrassGrowth> allHaygrassGrowth = new List<haygrassGrowth>();

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

        private class LineGraph
        {
            private List<CurveMark> marks = new List<CurveMark>();
            private Dictionary<String, SimpleCurveDrawInfo> curves = new Dictionary<String, SimpleCurveDrawInfo>();
            private SimpleCurveDrawerStyle curveDrawerStyle = new SimpleCurveDrawerStyle();

            public float scrollPos_curr { get; set; }
            public float scrollPos_prev { get; set; }
            public bool changed { get { return scrollPos_curr != scrollPos_prev; } }
            static int min_day = 1, max_day = 60;

            public LineGraph(float default_day)
            {
                this.scrollPos_curr = this.scrollPos_prev = default_day;
                curveDrawerStyle.UseFixedSection = true;
                curveDrawerStyle.FixedSection = new Vector2(0, scrollPos_curr);
                curveDrawerStyle.LabelY = "Hay #";
                curveDrawerStyle.LabelX = "Day";
                curveDrawerStyle.UseFixedScale = false;
                curveDrawerStyle.DrawBackground = true; // draw gray background behind graph
                curveDrawerStyle.DrawBackgroundLines = true; // 
                curveDrawerStyle.DrawMeasures = true;
                curveDrawerStyle.MeasureLabelsXCount = (int) this.scrollPos_curr; // number of marks on x axis 
                curveDrawerStyle.MeasureLabelsYCount = 5;
                curveDrawerStyle.DrawPoints = false; // draw white points for each data
                curveDrawerStyle.DrawLegend = true; //
                curveDrawerStyle.DrawCurveMousePoint = true; // hover over graph shows details
                curveDrawerStyle.UseAntiAliasedLines = true; // smooth lines
            }
            public void SetMarks(float x, string message, Color color)
            {
                if (!this.marks.Where(s => s.message == message).Any()) {
                    this.marks.Add(new CurveMark(x, message, color));
                    this.marks.Add(new CurveMark(x, message, color)); // fix i++ bug.
                } else {
                    foreach (CurveMark m in this.marks.Where(s => s.message == message))
                    {
                        m.x = x;
                    }
                }
            }
            public void SetCurve(String label, Color color, List<float> points)
            {
                if (!this.curves.ContainsKey(label))
                {
                    this.curves.Add(label, new SimpleCurveDrawInfo());
                    this.curves[label].color = color;
                    this.curves[label].label = label;
                }
                this.curves[label].curve = new SimpleCurve();
                for (int day = 0; day < points.Count(); day++)
                {
                    this.curves[label].curve.Add(new CurvePoint(day, points[day]));
                }
            }
            public float Draw(Rect rect)
            {
                curveDrawerStyle.FixedSection = new Vector2(0, this.scrollPos_curr);
                curveDrawerStyle.MeasureLabelsXCount = (int)this.scrollPos_curr; // number of marks on x axis 

                Rect graphRect = new Rect(0f, 10f, rect.width * .95f, 450f);
                Rect legendRect = new Rect(0f, graphRect.yMax, graphRect.width, 40f);
                Rect sliderRect = new Rect(0, legendRect.yMax, graphRect.width, 50f);
                Rect rect2 = new Rect(0, 0, graphRect.width, graphRect.height + legendRect.height + sliderRect.height);
                SimpleCurveDrawer.DrawCurves(graphRect, curves.Values.ToList(), curveDrawerStyle, this.marks, legendRect);
                scrollPos_prev = scrollPos_curr;
                scrollPos_curr = Widgets.HorizontalSlider(sliderRect, scrollPos_curr, min_day, max_day);
                return scrollPos_curr;
            }
        }
        List<LineGraph> graphList = new List<LineGraph>() {new LineGraph(nextNDays) };

        /* settings for each graph:
           what data to show
             projected yield, stock, population
             work, time

           show deficiency
           
           internal:
             SimpleCurveDrawerStyle
             are settings changed
           
           data selector checkbox
           add graph button
        */

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
            UpdateCalculations();
            lastUpdate = Time.time;
        }

        private void GetInGameData()
        {
            var pawns = (from p in Find.MapPawns.PawnsInFaction(Faction.OfPlayer)
                         select p).ToList();
            var allHaygrass = Find.ListerThings.AllThings.Where(x => x.Label == "haygrass");
            numMeat = Find.ResourceCounter.GetCountIn(ThingCategoryDefOf.MeatRaw);
            numAnimals = pawns.Where(x => x.RaceProps.Animal).Count();

            var roughAnimals = pawns
                           .Where(x => x.RaceProps.Animal && x.RaceProps.Eats(FoodTypeFlags.Plant));
            numRoughAnimals = roughAnimals.Count();
            totalNeededHay = roughAnimals
                           .Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay / hayNut;
            debug_val[1] = hayNut;
            //TODO: find animals' hunger rate when they are full instead whatever state they are in now.

            var kibbleAnimals = pawns.Where(x => x.RaceProps.Animal && !x.RaceProps.Eats(FoodTypeFlags.Plant) && x.RaceProps.Eats(FoodTypeFlags.Kibble));
            numKibbleAnimals = kibbleAnimals.Count();
            dailyKibbleConsumption = kibbleAnimals
                           .Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay / hayNut;
            dailyHayConsumption = dailyKibbleConsumption * 2f / 5f + totalNeededHay;

            numHaygrass = allHaygrass.Count();
            numHay = Find.ListerThings.AllThings.Where(x => x.def.label == "hay").Sum(x => x.stackCount);

            allHaygrassGrowth.Clear();
            foreach (var h in allHaygrass) // add today's growth data
            {
                allHaygrassGrowth.Add(new haygrassGrowth(
                    ((Plant)h).Growth + GenDate.TicksPerHour * (19 - GenDate.HourOfDay) * ((Plant)h).GrowthRate / (GenDate.TicksPerDay * h.def.plant.growDays),
                    ((Plant)h).GrowthRate / (GenDate.TicksPerDay * h.def.plant.growDays)));
            }
        }

        private void MakePrediction()
        {
            // calculate yield for today
            projectedRecords.Clear();
            projectedRecords.Add(new prediction(new prediction.MinMax { min = 0, max = 0 }, new prediction.MinMax { min = numHay, max = numHay },
                                 new prediction.MinMax { min = numMeat, max = numMeat }));
            if (GenDate.HourOfDay >= 4 && GenDate.HourOfDay <= 19)
            {
                if (allHaygrassGrowth.LastOrDefault().Growth >= 1.0f)
                {
                    allHaygrassGrowth.LastOrDefault().Growth = 0.5f;
                    projectedRecords[0].hay_yield.max += haygrass_yieldMax;
                    projectedRecords[0].hay_yield.min += haygrass_yieldMin;
                }
                projectedRecords[0].hay_stock.max += projectedRecords[0].hay_yield.max;
                projectedRecords[0].hay_stock.min += projectedRecords[0].hay_yield.min;
            }
            projectedRecords[0].hay_stock.max -= GenDate.CurrentDayPercent * dailyHayConsumption; // only count the rest of day
            projectedRecords[0].hay_stock.min -= GenDate.CurrentDayPercent * dailyHayConsumption;
            projectedRecords[0].meat_stock.max -= GenDate.CurrentDayPercent * dailyKibbleConsumption * 2f / 5f; // convert every 50 kibbles to 20 meat
            projectedRecords[0].meat_stock.min -= GenDate.CurrentDayPercent * dailyKibbleConsumption * 2f / 5f;

            // calculate yields and stocks after today
            for (int day = 1; day < nextNDays; day++)
            {
                projectedRecords.Add(new prediction(new prediction.MinMax { min = 0, max = 0 },
                    new prediction.MinMax { max = projectedRecords[day - 1].hay_stock.max, min = projectedRecords[day - 1].hay_stock.min },
                    new prediction.MinMax { max = projectedRecords[day - 1].meat_stock.max, min = projectedRecords[day - 1].meat_stock.min }));
                foreach (haygrassGrowth k in allHaygrassGrowth)
                {
                    k.Growth += k.GrowthPerTick * GenDate.TicksPerDay * 0.55f; // 0.55 is 55% of time plant spent growing
                    //debug_val[6] = k.GrowthPerTick;
                    if (k.Growth >= 1.0f)
                    {
                        projectedRecords[day].hay_yield.max += haygrass_yieldMax;
                        projectedRecords[day].hay_yield.min += haygrass_yieldMin;
                        k.Growth = 0.05f; // if it's fully grown, replant and their growths start at 5%.
                    }
                }
                projectedRecords[day].hay_stock.max += projectedRecords[day].hay_yield.max;
                projectedRecords[day].hay_stock.min += projectedRecords[day].hay_yield.min;

                projectedRecords[day].hay_stock.max -= dailyHayConsumption;
                projectedRecords[day].hay_stock.min -= dailyHayConsumption;
                projectedRecords[day].meat_stock.max -= dailyKibbleConsumption * 2f / 5f;
                projectedRecords[day].meat_stock.min -= dailyKibbleConsumption * 2f / 5f;


            }
        }

        private void UpdateCalculations()
        {
            GetInGameData();
            UpdateDates();
            MakePrediction();
        }
        // calculating number of days until certain dates
        private void UpdateDates()
        {

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
            }
            else
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
            if (graphList[0].changed || GenTicks.TicksAbs - lastUpdateTick >= (GenDate.TicksPerHour/5)) // Find.TickManager.Paused
            {
                lastUpdateTick = GenTicks.TicksAbs;
                UpdateCalculations();
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

            list.Add(new TabRecord("Help", delegate
            {
                this.curTab = FoodAnalyticsTab.Help;
            }, this.curTab == FoodAnalyticsTab.Help));

            TabDrawer.DrawTabs(rect2, list);

            if (this.curTab == FoodAnalyticsTab.Graph)
            {
                this.DisplayGraphPage(rect2);
                //this.prevTab = FoodAnalyticsTab.Graph;
            } else if (this.curTab == FoodAnalyticsTab.Analytics)
            {
                this.DisplayAnalyticsPage(rect2);
                //this.prevTab = FoodAnalyticsTab.Analytics;
            } else if (this.curTab == FoodAnalyticsTab.Help)
            {
                this.DisplayHelpPage(rect2);
                //this.prevTab = FoodAnalyticsTab.Graph;
            }
        }

        private void DisplayAnalyticsPage(Rect rect)
        {
            // constructing string
            string analysis = "Number of animals you have = " + (int)numAnimals + ", hay animals = " + numRoughAnimals + ", kibble animals = " + numKibbleAnimals +
                            "\nNumber of hay in stockpiles and on the floors = " + (int)numHay + ", number of meat = " + numMeat +
                            "\nEstimated number of hay needed daily for hay-eaters only= " + (int)totalNeededHay +
                            "\nEstimated number of kibble needed for all kibble-eaters = " + (int) dailyKibbleConsumption +
                            "\nEstimated number of hay needed daily for all animals = " + (int)dailyHayConsumption +
                            "\nNumber of days until hay in stockpiles run out = " + String.Format("{0:0.0}", numHay / dailyHayConsumption) +
                            "\nDays Until Winter = " + this.daysUntilWinter + ", Days until growing period over = " + this.daysUntilGrowingPeriodOver +
                            ", Days until the end of winter = " + this.daysUntilEndofWinter + ", Days until next harvest season = " + this.daysUntilNextHarvestSeason +
                            "\nEstimated number of hay needed until winter for hay-eaters only = " + (int)(totalNeededHay * this.daysUntilWinter) +
                            "\nEstimated number of hay needed until winter for all animals = " + (int)(dailyHayConsumption * this.daysUntilWinter) +
                            "\nEstimated number of hay needed until the end of winter for hay-eaters only = " + (int)(totalNeededHay * this.daysUntilEndofWinter) +
                            "\nEstimated number of hay needed until the end of winter for all animals = " + (int)(dailyHayConsumption * this.daysUntilEndofWinter) +
                            "\nEstimated number of hay needed yearly for hay-eaters only = " + (int)(totalNeededHay * GenDate.DaysPerMonth * GenDate.MonthsPerYear) + // 60 days
                            "\nEstimated number of hay needed yearly for all animals = " + (int)(dailyHayConsumption * GenDate.DaysPerMonth * GenDate.MonthsPerYear) + // 60 days
                            "\nEstimated number of hay needed until next harvest season(10th of Spring) for all animals = " + (int)(dailyHayConsumption * this.daysUntilNextHarvestSeason) +
                            "\nNumber of haygrass plant = " + (int)numHaygrass +
                            "\nEstimate of projected harvest production:\n";
            analysis += "Day\t Max Yield Min Yield Max Stock Min Stock\n";
            int i = 0;
            foreach (prediction k in projectedRecords)
            {
                analysis += String.Format("{0,-2}\t {1,-6}\t {2,-6}\t {3,-6}\t {4,-6}\n",
                    i, (int)k.hay_yield.max, (int)k.hay_yield.min, (int)k.hay_stock.max, (int)k.hay_stock.min);
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
            Widgets.BeginScrollView(rect, ref this.scrollPos[(int)FoodAnalyticsTab.Analytics], rect2);
            Widgets.Label(rect2, analysis);
            Widgets.EndScrollView();
        }

        private void DisplayGraphPage(Rect rect)
        {
            //marks add dots on top of a graph, the text label is the text in the popup box
            graphList[0].SetMarks(this.daysUntilNextHarvestSeason, "Days until the Next Harvest Season", Color.green);
            graphList[0].SetMarks(this.daysUntilGrowingPeriodOver, "Days until Growing Period is Over", Color.red);
            graphList[0].SetMarks(this.daysUntilWinter, "Days until the Winter", Color.white);
            graphList[0].SetMarks(this.daysUntilEndofWinter, "Days until the End of Winter", Color.yellow);

            // plotting graphs   
            graphList[0].SetCurve("Hay Yield(Max)", Color.green, projectedRecords.Select(y => y.hay_yield.max).ToList());
            graphList[0].SetCurve("Hay Yield(Min)", Color.red, projectedRecords.Select(y => y.hay_yield.min).ToList());
            graphList[0].SetCurve("Hay Stock(Max)", Color.white, projectedRecords.Select(y => y.hay_stock.max).ToList());
            graphList[0].SetCurve("Hay Stock(Min)", Color.magenta, projectedRecords.Select(y => y.hay_stock.min).ToList());
            graphList[0].SetCurve("Meat Stock", Color.blue, projectedRecords.Select(y => y.meat_stock.max).ToList());

            Widgets.BeginScrollView(rect, ref this.scrollPos[(int)FoodAnalyticsTab.Graph], new Rect(0, 0, rect.width, rect.height*2));
            nextNDays = (int) graphList[0].Draw(rect);

            Widgets.EndScrollView();
        }

        private void DisplayHelpPage(Rect rect)
        {

        }
    }
}
