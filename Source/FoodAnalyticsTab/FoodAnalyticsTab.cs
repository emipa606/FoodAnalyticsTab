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
using System.IO;
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
            Help = 2,
            DetailedList = 3
        }
        private MainTabWindow_Estimator.FoodAnalyticsTab curTab = FoodAnalyticsTab.Graph, prevTab = FoodAnalyticsTab.Analytics;
        Vector2[] scrollPos = new Vector2[3] { Vector2.zero, Vector2.zero, Vector2.zero }; // for scrolling view

        protected float lastUpdate = -1f;
        private int lastUpdateTick = 0;

        // analytics
        private float dailyHayConsumptionIndoorAnimals, dailyKibbleConsumption, dailyHayConsumption, dailyHayConsumptionRoughAnimals;
        private int numAnimals, numRoughAnimals, numKibbleAnimals, numHaygrass, numHay, numMeat, numEgg, numHen, numColonist, numHerbivoreIndoor, numHerbivoreOutdoor;
        

        private static int nextNDays = 60; // default display 25 days

        List<Prediction> projectedRecords = new List<Prediction>(); 
        private class PlantGrowth
        {
            public PlantGrowth(float a, float b)
            {
                Growth = a;
                GrowthPerTick = b;
            }
            public float Growth { get; set; }
            public float GrowthPerTick { get; set; }
            public bool IsOutdoor { get; set; }
        };
        List<PlantGrowth> allHaygrassGrowth = new List<PlantGrowth>();

        public static float[] debug_val = new float[10];
        // important dates
        private int daysUntilWinter;// to Dec 1st
        private int daysUntilEndofWinter; // to February 5th
        private int daysUntilGrowingPeriodOver; // to 10th of Fall, Oct 5th
        private int daysUntilNextHarvestSeason; // to 10th of Spring, April 5th
        static float hayNut = 0, haygrass_yieldMax = 0, haygrass_yieldMin = 0;

        List<LineChart> chartList = new List<LineChart>() {new LineChart(nextNDays) };

        //List<ThingDef> plantDef = new List<ThingDef>();
        private Predictor predictor = new Predictor();

        [Serializable]
        class DataPoint
        {
            string date;
            public DataPoint(string s) {
                date = s;
            }
        }
        List<DataPoint> dpList = new List<DataPoint>();
        // functions
        public MainTabWindow_Estimator () : base()
        {

            this.doCloseX = true;
            this.doCloseButton = false;
            this.closeOnClickedOutside = true;
            MainTabWindow_Estimator.hayNut = (from d in DefDatabase<ThingDef>.AllDefs.Where(x => x.defName == "Hay")
                                   select d).FirstOrDefault().ingestible.nutrition;
            MainTabWindow_Estimator.haygrass_yieldMax = GenMath.RoundRandom(
                (from d in DefDatabase<ThingDef>.AllDefs.Where(x => x.defName == "PlantHaygrass")
                 select d).FirstOrDefault().plant.harvestYield);
            MainTabWindow_Estimator.haygrass_yieldMin = GenMath.RoundRandom(
                (from d in DefDatabase<ThingDef>.AllDefs.Where(x => x.defName == "PlantHaygrass")
                 select d).FirstOrDefault().plant.harvestYield * 0.5f * 0.5f// 1st 0.5 is harvesting at 65% growth, 2nd 0.5 is lowest health.
                );
            //plantDef = DefDatabase<ThingDef>.AllDefs.Where(x => x.plant != null && x.plant.Sowable).ToList();

            dpList.Add(new DataPoint(GenDate.DateFullStringAt(GenTicks.TicksAbs)));
            //ML.WriteToXmlFile<List<DataPoint>>("C://datapoint.xml", dpList);
            //XmlSaver.SaveDataObject(dpList, "./datapoint.xml");
        }
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
            if (Find.TickManager.Paused)
            {
                UpdateCalculations();
            }
            //List<DataPoint> dpList = ML.ReadFromXmlFile<List<DataPoint>>("./datapoint.xml");
            dpList.Add(new DataPoint(GenDate.DateFullStringAt(GenTicks.TicksAbs)));
            //ML.WriteToXmlFile<List<DataPoint>>("datapoint.xml", dpList); // didn't work
            ML.WriteToBinaryFile<List<DataPoint>>("datapoint.dat", dpList);// save file under main dir
        }

        private void GetInGameData()
        {
            var pawns = Find.MapPawns.PawnsInFaction(Faction.OfPlayer);
            var allHaygrass = Find.ListerThings.AllThings.Where(x => x.Label == "haygrass");
            numColonist = pawns.Where(x => x.RaceProps.Humanlike).Count();
            numMeat = Find.ResourceCounter.GetCountIn(ThingCategoryDefOf.MeatRaw);
            numEgg = Find.ListerThings.AllThings.Where(x => x.def.defName == "EggChickenUnfertilized").Count();
            numAnimals = pawns.Where(x => x.RaceProps.Animal).Count();

            var roughAnimals = pawns
                           .Where(x => x.RaceProps.Animal && x.RaceProps.Eats(FoodTypeFlags.Plant));
            numRoughAnimals = roughAnimals.Count();
            numHerbivoreOutdoor = roughAnimals.Where(a => a.Position.GetRoomOrAdjacent().UsesOutdoorTemperature).Count();
            numHerbivoreIndoor = numRoughAnimals - numHerbivoreOutdoor;

            numHen = roughAnimals.
                Where(x => x.def.defName == "Chicken" && x.gender == Gender.Female &&
                      x.ageTracker.CurLifeStage.defName == "AnimalAdult").Count(); // x.def.defName == "Chicken" worked, but x.Label == "chicken" didn't work

            dailyHayConsumptionIndoorAnimals = roughAnimals.Where(a => a.Position.GetRoomOrAdjacent().UsesOutdoorTemperature)
                           .Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay / hayNut;
            dailyHayConsumptionRoughAnimals = roughAnimals.Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay / hayNut;
            //TODO: find animals' hunger rate when they are full instead whatever state they are in now.

            var kibbleAnimals = pawns.Where(x => x.RaceProps.Animal && !x.RaceProps.Eats(FoodTypeFlags.Plant) && x.RaceProps.Eats(FoodTypeFlags.Kibble));
            numKibbleAnimals = kibbleAnimals.Count();
            dailyKibbleConsumption = kibbleAnimals
                           .Sum(x => x.needs.food.FoodFallPerTick) * GenDate.TicksPerDay / hayNut;
            

            numHaygrass = allHaygrass.Count();
            numHay = Find.ListerThings.AllThings.Where(x => x.def.label == "hay").Sum(x => x.stackCount);

            allHaygrassGrowth.Clear();
            
            foreach (var h in allHaygrass) // add current growth data
            {
                allHaygrassGrowth.Add(new PlantGrowth(
                    ((Plant)h).Growth,
                    ((Plant)h).GrowthRate / (GenDate.TicksPerDay * h.def.plant.growDays)));
                allHaygrassGrowth.Last().IsOutdoor = h.Position.GetRoomOrAdjacent().UsesOutdoorTemperature;
            }
            // TODO: Find.ZoneManager.AllZones, potentially look at unplannted cells in growing zones and predict work amount and complete time.
            // look at Zone_Growing class , ZoneManager class
        }

        private void MakePrediction()
        {
            // calculate yield for today
            projectedRecords.Clear();
            projectedRecords.Add(
                new Prediction(
                    new Prediction.MinMax { min = 0, max = 0 }, 
                    new Prediction.MinMax { min = numHay, max = numHay },
                    new Prediction.MinMax { min = dailyHayConsumption , max = dailyHayConsumption },
                    new Prediction.MinMax { min = numMeat, max = numMeat },
                    new Prediction.MinMax { min = numAnimals, max = numAnimals }));

            float numTicksBeforeResting = 0;
            // exclude resting plants' growth
            if (GenDate.CurrentDayPercent < 0.25f) // if resting before 6am
            {
                numTicksBeforeResting = (float)GenDate.TicksPerDay * 0.55f; // will grow the full day
            } else if (GenDate.CurrentDayPercent > 0.8f) // if resting after 7.2pm
            {
                numTicksBeforeResting = 0; // won't grow anymore
            } else // from .25 to .8 
            {
                numTicksBeforeResting = GenDate.TicksPerDay * (0.8f - GenDate.CurrentDayPercent);
            }
            foreach (PlantGrowth g in allHaygrassGrowth)
            {
                g.Growth += numTicksBeforeResting * g.GrowthPerTick;

                if (g.Growth >= 1.0f)
                {
                    g.Growth = Plant.BaseGrowthPercent;
                    projectedRecords[0].hay_yield.max += haygrass_yieldMax;
                    projectedRecords[0].hay_yield.min += haygrass_yieldMin;
                }
            }
            if (daysUntilGrowingPeriodOver > 0)
            {
                dailyHayConsumption = dailyKibbleConsumption * 2f / 5f + dailyHayConsumptionIndoorAnimals;
            } else
            {
                dailyHayConsumption = dailyKibbleConsumption * 2f / 5f + dailyHayConsumptionRoughAnimals;
            }

            projectedRecords[0].hay_stock.max += projectedRecords[0].hay_yield.max;
            projectedRecords[0].hay_stock.min += projectedRecords[0].hay_yield.min;


            projectedRecords[0].hay_stock.max -= (1 - GenDate.CurrentDayPercent) * dailyHayConsumption; // only count the rest of day
            projectedRecords[0].hay_stock.min -= (1 - GenDate.CurrentDayPercent) * dailyHayConsumption;
            projectedRecords[0].meat_stock.max -= (1 - GenDate.CurrentDayPercent) * dailyKibbleConsumption * 2f / 5f; // convert every 50 kibbles to 20 meat
            projectedRecords[0].meat_stock.min -= (1 - GenDate.CurrentDayPercent) * dailyKibbleConsumption * 2f / 5f;

            // calculate yields and stocks after today
            for (int day = 1; day < nextNDays; day++)
            {
                projectedRecords.Add(
                    new Prediction(
                        new Prediction.MinMax { min = 0, max = 0 },
                        new Prediction.MinMax { max = projectedRecords[day - 1].hay_stock.max, min = projectedRecords[day - 1].hay_stock.min },
                        new Prediction.MinMax { max = projectedRecords[day - 1].hay_consumption.max, min = projectedRecords[day - 1].hay_consumption.min },
                        new Prediction.MinMax { max = projectedRecords[day - 1].meat_stock.max, min = projectedRecords[day - 1].meat_stock.min },
                        new Prediction.MinMax { max = projectedRecords[day - 1].animal_population.max, min = projectedRecords[day - 1].animal_population.min }));

                foreach (PlantGrowth k in allHaygrassGrowth)
                {
                    if (k.IsOutdoor && !(day <= daysUntilGrowingPeriodOver))
                    {
                        continue; // don't count outdoor crop if it's growing period is over
                    }
                    k.Growth += k.GrowthPerTick * GenDate.TicksPerDay * 0.55f; // 0.55 is 55% of time plant spent growing
                    
                    if (k.Growth >= 1.0f)
                    {
                        projectedRecords[day].hay_yield.max += haygrass_yieldMax;
                        projectedRecords[day].hay_yield.min += haygrass_yieldMin;
                        k.Growth = Plant.BaseGrowthPercent; // if it's fully grown, replant and their growths start at 5%.
                    }
                }
                if (day <= daysUntilGrowingPeriodOver)
                {
                    dailyHayConsumption = dailyKibbleConsumption * 2f / 5f + dailyHayConsumptionIndoorAnimals;
                }
                else
                {
                    dailyHayConsumption = dailyKibbleConsumption * 2f / 5f + dailyHayConsumptionRoughAnimals;
                }
                projectedRecords[day].hay_stock.max += projectedRecords[day].hay_yield.max - dailyHayConsumption;
                projectedRecords[day].hay_stock.min += projectedRecords[day].hay_yield.min - dailyHayConsumption;

                projectedRecords[day].meat_stock.max -= dailyKibbleConsumption * 2f / 5f;
                projectedRecords[day].meat_stock.min -= dailyKibbleConsumption * 2f / 5f;
            }
        }

        private void UpdateCalculations()
        {
            GetInGameData();
            UpdateDates();
            MakePrediction();
            predictor.MakePrediction(0);
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
            if (GenTicks.TicksAbs - lastUpdateTick >= (GenDate.TicksPerHour/6)) 
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
            list.Add(new TabRecord("Detailed List", delegate
            {
                this.curTab = FoodAnalyticsTab.DetailedList;
            }, this.curTab == FoodAnalyticsTab.DetailedList));
            list.Add(new TabRecord("Help", delegate
            {
                this.curTab = FoodAnalyticsTab.Help;
            }, this.curTab == FoodAnalyticsTab.Help));

            TabDrawer.DrawTabs(rect2, list);

            if (this.curTab == FoodAnalyticsTab.Graph)
            {
                this.DisplayGraphPage(rect2);
            } else if (this.curTab == FoodAnalyticsTab.Analytics)
            {
                this.DisplayAnalyticsPage(rect2);
            } else if (this.curTab == FoodAnalyticsTab.Help)
            {
                this.DisplayHelpPage(rect2);
            } else if (this.curTab == FoodAnalyticsTab.DetailedList)
            {
                this.DisplayDetailedListPage(rect2);
            }
        }

        private void DisplayAnalyticsPage(Rect rect)
        {
            // constructing string
            string analysis = "Number of animals you have = " + (int)numAnimals + ", hay animals = " + numRoughAnimals + ", kibble animals = " + numKibbleAnimals +
                            "\nNumber of colonist = " + numColonist +
                            "\nNumber of hay in stockpiles and on the floors = " + (int)numHay + ", number of meat = " + numMeat + ", number of egg = " + numEgg +
                            "\nNumber of hen = " + numHen +
                            "\nEstimated number of hay needed daily for hay-eaters only= " + (int)dailyHayConsumptionIndoorAnimals +
                            "\nEstimated number of kibble needed for all kibble-eaters = " + (int)dailyKibbleConsumption + ", meat =" + dailyKibbleConsumption*2/5 + ", egg=" + dailyKibbleConsumption*4/50 +
                            "\nEstimated number of hay needed daily for all animals = " + (int)dailyHayConsumption +
                            "\nNumber of days until hay in stockpiles run out = " + String.Format("{0:0.0}", numHay / dailyHayConsumption) +
                            "\nDays Until Winter = " + this.daysUntilWinter + ", Days until growing period over = " + this.daysUntilGrowingPeriodOver +
                            ", Days until the end of winter = " + this.daysUntilEndofWinter + ", Days until next harvest season = " + this.daysUntilNextHarvestSeason +
                            "\nEstimated number of hay needed until winter for hay-eaters only = " + (int)(dailyHayConsumptionIndoorAnimals * this.daysUntilWinter) +
                            "\nEstimated number of hay needed until winter for all animals = " + (int)(dailyHayConsumption * this.daysUntilWinter) +
                            "\nEstimated number of hay needed until the end of winter for hay-eaters only = " + (int)(dailyHayConsumptionIndoorAnimals * this.daysUntilEndofWinter) +
                            "\nEstimated number of hay needed until the end of winter for all animals = " + (int)(dailyHayConsumption * this.daysUntilEndofWinter) +
                            "\nEstimated number of hay needed yearly for hay-eaters only = " + (int)(dailyHayConsumptionIndoorAnimals * GenDate.DaysPerMonth * GenDate.MonthsPerYear) + // 60 days
                            "\nEstimated number of hay needed yearly for all animals = " + (int)(dailyHayConsumption * GenDate.DaysPerMonth * GenDate.MonthsPerYear) + // 60 days
                            "\nEstimated number of hay needed until next harvest season(10th of Spring) for all animals = " + (int)(dailyHayConsumption * this.daysUntilNextHarvestSeason) +
                            "\nNumber of haygrass plant = " + (int)numHaygrass + ", outdoor = " + allHaygrassGrowth.Where(h => h.IsOutdoor).Count() +
                            "\nNumber of haygrass needed = " + dailyHayConsumption / 20 * 10 +
                            "\nEstimate of projected harvest production:\n";
            analysis += "Day\t Max Yield Min Yield Max Stock Min Stock\n";
            int i = 0;
            foreach (Prediction k in projectedRecords)
            {
                analysis += String.Format("{0,-2}\t {1,-6}\t {2,-6}\t {3,-6}\t {4,-6}\n",
                    i, (int)k.hay_yield.max, (int)k.hay_yield.min, (int)k.hay_stock.max, (int)k.hay_stock.min);
                i++;
            }
            
            foreach (var v in debug_val)
            {
                analysis += v + ",";
            }
            /*
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
            Rect rect2 = new Rect(0, 0, rect.width, analysis.Split('\n').Length * 20);
            Widgets.BeginScrollView(rect, ref this.scrollPos[(int)FoodAnalyticsTab.Analytics], rect2);
            Widgets.Label(rect2, analysis);
            Widgets.EndScrollView();
        }

        private void DisplayGraphPage(Rect rect)
        {
            //marks add dots on top of a graph, the text label is the text in the popup box
            chartList[0].SetMarks(this.daysUntilNextHarvestSeason, "Days until the Next Harvest Season", Color.green);
            chartList[0].SetMarks(this.daysUntilGrowingPeriodOver, "Days until Growing Period is Over", Color.red);
            chartList[0].SetMarks(this.daysUntilWinter, "Days until the Winter", Color.white);
            chartList[0].SetMarks(this.daysUntilEndofWinter, "Days until the End of Winter", Color.yellow);
            
            // plotting graphs   
            chartList[0].SetCurve("Hay Yield(Max)", Color.green, projectedRecords.Select(y => y.hay_yield.max).ToList());
            chartList[0].SetCurve("Hay Yield(Min)", Color.red, projectedRecords.Select(y => y.hay_yield.min).ToList());
            chartList[0].SetCurve("Hay Stock(Max)", Color.white, projectedRecords.Select(y => y.hay_stock.max).ToList());
            chartList[0].SetCurve("Hay Stock(Min)", Color.magenta, projectedRecords.Select(y => y.hay_stock.min).ToList());
            chartList[0].SetCurve("Meat Stock", Color.blue, projectedRecords.Select(y => y.meat_stock.max).ToList());
            

            foreach (LineChart c in chartList) {
                c.UpdateData(ref predictor);
            }

            Widgets.BeginScrollView(rect, ref this.scrollPos[(int)FoodAnalyticsTab.Graph], 
                new Rect(rect.x, rect.y, chartList[0].rect.width, chartList[0].rect.height * chartList.Count())); //TODO: figure out how to obtain viewRect
            //nextNDays = (int) graphList[0].Draw(rect);
            Rect btn = new Rect(rect.xMin, rect.yMin, 110f, 40f);
            if (Widgets.ButtonText(btn, "New Chart", true, false, true))
            {
                //chartList.Add(new LineChart(chartList[0]));
                chartList.Add(new LineChart(60));
            }
            Rect newRect = new Rect(rect.xMin, btn.yMax, rect.width, rect.height);
            foreach (LineChart g in chartList)
            {
                g.Draw(newRect);
                if (g.remove == true)
                {
                    newRect = new Rect(g.rect.x, g.rect.yMin, rect.width, rect.height);
                }
                else
                {
                    newRect = new Rect(g.rect.x, g.rect.yMax, rect.width, rect.height);
                }
            }
            chartList.RemoveAll(g => g.remove == true);

            predictor.EnablePrediction(chartList);
            

            Widgets.EndScrollView();
        }

        private void DisplayHelpPage(Rect rect)
        {
            
        }
        private void DisplayDetailedListPage(Rect rect)
        {
            GUI.BeginGroup(rect);
            Widgets.ButtonText(new Rect(0, 0, 110f, 40f), "test".Translate(), true, false, true);
            GUI.EndGroup();
        }
    }
}
