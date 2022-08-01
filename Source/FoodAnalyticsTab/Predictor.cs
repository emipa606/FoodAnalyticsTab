using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using Verse;

namespace FoodAnalyticsTab;

/* prediction class:
   making prediction 
   types: crop/population/meat yield, vegie/meat/meal/drug/leather/population stock, 

   type of update: 1st day, after
   data structure
   list of crops
     yield
     stock
     consumption

   model:
   1.
   chicken -> egg--↘
   animals -> meat -> kibble -> carnivores animal
   merchant->       ^ meals -> conlonists
   hunting ->       | training -> animals
                    |           ^
                    |__cooking  |__feeding
   2.
   growers -> haygrass -> hay -> herbivores
            ^           ^     ↘ kibble
            |           |
            |__planting |__harvesting
   3.
   grower -> rice plant -> rice -------------> meals -> colonist
          -> corn plant -> corn                      |
          -> potato plant -> potato                  |
          -> strawberry plant -> strawberry ----------
   4.
   cotton
   muffalo     -> wool -> armchair, 
   alpaca              -> clothing
   Megatherium         -> medicine
   Dromedary

   5.
   fertilized eggs -> chicken babies -> meat -> carnivores
                                      ^
                                      |_butchering

    data structure:
    Predictor contain all prediction items, eg, prediction for hay, population
    Prediction item include prediction terms, eg, stock, yield, consumption
    Q.what should contain the 60-day prediction? Prediction obj or PredTerm?
    I think PredTerm is for daily prediction result
*/
public class Predictor
{
    public enum ModelType
    {
        analytical,
        iterative,
        learning
    }

    // important dates
    public static int daysUntilWinter, // to Dec 1st
        daysUntilEndofWinter, // to February 5th
        daysUntilGrowingPeriodOver, // to 10th of Fall, Oct 5th
        daysUntilNextHarvestSeason, // to 10th of Spring, April 5th
        numTicksBeforeResting,
        nextNDays = 60;

    private readonly List<ThingDef> plantDefs = new List<ThingDef>();

    public Dictionary<string, PredType> allPredType = new Dictionary<string, PredType>();

    public Predictor()
    {
        plantDefs = DefDatabase<ThingDef>.AllDefs
            .Where(x => x.plant is { Sowable: true, harvestedThingDef: { } }).ToList();
        foreach (var x in plantDefs.OrderBy(x => x.label))
        {
            allPredType.Add(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.label.ToLower()), new PredType(x));
        }
    }

    public void MakePrediction(int days)
    {
        var currentMap = Find.CurrentMap;
        // exclude resting plants' growth
        if (GenLocalDate.DayPercent(currentMap) < 0.25f) // if resting before 6am
        {
            numTicksBeforeResting = (int)(GenDate.TicksPerDay * 0.55f); // will grow the full day
        }
        else if (GenLocalDate.DayPercent(currentMap) > 0.8f) // if resting after 7.2pm
        {
            numTicksBeforeResting = 0; // won't grow anymore
        }
        else // from .25 to .8 
        {
            numTicksBeforeResting = (int)(GenDate.TicksPerDay * (0.8f - GenLocalDate.DayPercent(currentMap)));
        }

        UpdateDates();
        foreach (var s in allPredType.Keys)
        {
            if (!allPredType[s].enabled)
            {
                continue;
            }

            allPredType[s].GetCurrentStat();
            allPredType[s].UpdatePrediction();
        }
    }

    public void EnablePrediction(List<LineChart> chartList)
    {
        foreach (var s in allPredType.Keys)
        {
            allPredType[s].enabled = chartList.Any(c => c.setting.graphEnable[s]);
        }
    }

    // calculating number of days until certain dates
    private void UpdateDates()
    {
        var currentMap = Find.CurrentMap;
        var dayOfSeason = GenLocalDate.DayOfSeason(currentMap);
        var dayOfTwelfth = GenLocalDate.DayOfTwelfth(currentMap);
        var season = GenLocalDate.Season(currentMap);
        var twelfth = GenLocalDate.Twelfth(currentMap);
        var growingPeriodTwelfths = GenTemperature.TwelfthsInAverageTemperatureRange(currentMap.Tile, 6f, 42f);

        switch (season)
        {
            case Season.Spring:
                daysUntilWinter = GenDate.DaysPerSeason - dayOfSeason + (GenDate.DaysPerSeason * 2);
                daysUntilEndofWinter = GenDate.DaysPerSeason - dayOfSeason + (GenDate.DaysPerSeason * 3);
                break;
            case Season.Summer:
                daysUntilWinter = GenDate.DaysPerSeason - dayOfSeason + GenDate.DaysPerSeason;
                daysUntilEndofWinter = GenDate.DaysPerSeason - dayOfSeason + (GenDate.DaysPerSeason * 2);
                break;
            case Season.Fall:
                daysUntilWinter = GenDate.DaysPerSeason - dayOfSeason;
                daysUntilEndofWinter = GenDate.DaysPerSeason - dayOfSeason + GenDate.DaysPerSeason;
                break;
            case Season.Winter:
                daysUntilWinter = GenDate.DaysPerSeason - dayOfSeason + (GenDate.DaysPerSeason * 3);
                daysUntilEndofWinter = GenDate.DaysPerSeason - dayOfSeason;
                break;
            default:
                return;
        }

        if (!growingPeriodTwelfths.Any() || growingPeriodTwelfths.Count == 12)
        {
            return;
        }

        if (growingPeriodTwelfths.Contains(twelfth))
        {
            daysUntilGrowingPeriodOver =
                ((growingPeriodTwelfths.Count - growingPeriodTwelfths.IndexOf(twelfth) - 1) *
                 GenDate.DaysPerTwelfth) - dayOfTwelfth;
            //daysUntilNextHarvestSeason = GenDate.DaysPerYear - dayOfTwelfth -
            //                             (growingPeriodTwelfths.IndexOf(twelfth) * GenDate.DaysPerTwelfth);
        }
        else
        {
            daysUntilGrowingPeriodOver = growingPeriodTwelfths.Count * GenDate.DaysPerTwelfth;
            daysUntilGrowingPeriodOver += GenDate.DaysPerTwelfth - dayOfTwelfth;
            while (!growingPeriodTwelfths.Contains(TwelfthUtility.TwelfthAfter(twelfth)))
            {
                daysUntilGrowingPeriodOver += GenDate.DaysPerTwelfth;
                twelfth = twelfth.NextTwelfth();
            }

            //daysUntilNextHarvestSeason = GenDate.DaysPerYear - dayOfTwelfth -
            //                             (growingPeriodTwelfths.IndexOf(twelfth) * GenDate.DaysPerTwelfth);
        }
    }

    private class GrowthTracker
    {
        public GrowthTracker(float a, float b)
        {
            Growth = a;
            GrowthPerTick = b;
        }

        public float Growth { get; set; }
        public float GrowthPerTick { get; }
        public bool IsOutdoor { get; set; }
    }

    public class PredType // should contain update rule
    {
        private readonly List<GrowthTracker> allGrowth = new List<GrowthTracker>();

        public int consumption0, consumption;

        public ThingDef def;
        public List<DayPred> projectedPred = new List<DayPred>();

        public PredType(ThingDef def)
        {
            showDeficiency = false;
            enabled = false;
            this.def = def;
            analysis = "";
        }

        public bool enabled { get; set; }

        public bool showDeficiency { get; set; }

        public string analysis { private set; get; }

        public void SetUpdateRule(int v0, int v)
        {
            consumption0 = v0;
            consumption = v;
        }

        public void GetCurrentStat()
        {
            var currentMap = Find.CurrentMap;
            allGrowth.Clear();
            // get current growth stats
            foreach (var h in currentMap.listerThings.AllThings.Where(x => x.def.defName == def.defName))
            {
                allGrowth.Add(new GrowthTracker(((Plant)h).Growth,
                    ((Plant)h).GrowthRate / (GenDate.TicksPerDay * h.def.plant.growDays)));
                allGrowth.Last().IsOutdoor = h.Position.GetRoomOrAdjacent(currentMap).UsesOutdoorTemperature;
            }

            if (def.plant.harvestedThingDef == null)
            {
                return;
            }

            // get current harvest stock
            projectedPred.Clear();
            projectedPred.Add(new DayPred(0));
            projectedPred.Last().stock.max = projectedPred.Last().stock.min =
                currentMap.listerThings.AllThings.Where(x => x.def.defName == def.plant.harvestedThingDef.defName)
                    .Sum(x => x.stackCount);
            projectedPred.Last().consumption.max =
                projectedPred.Last().consumption.min = consumption0;
        }

        public void UpdatePrediction()
        {
            if (!enabled)
            {
                return;
            }

            //*
            // calculate yield for today                   
            foreach (var g in allGrowth)
            {
                g.Growth += numTicksBeforeResting * g.GrowthPerTick;

                if (!(g.Growth >= 1.0f))
                {
                    continue;
                }

                g.Growth = Plant.BaseGrowthPercent;
                projectedPred[0].yield.max =
                    (int)(def.plant.harvestYield * Find.Storyteller.difficulty.cropYieldFactor);
                projectedPred[0].yield.min = (int)(def.plant.harvestYield * 0.5f * .5f *
                                                   Find.Storyteller.difficulty.cropYieldFactor);
            }

            //*
            if (def.plant.harvestedThingDef.defName == "Hay")
            {
                projectedPred[0].stock.max -= projectedPred[0].consumption.max;
                projectedPred[0].stock.min -= projectedPred[0].consumption.min;
            }
            //*/

            projectedPred[0].stock.max += projectedPred[0].yield.max;
            projectedPred[0].stock.min += projectedPred[0].yield.min;

            /*
                projectedRecords[0].meat_stock.max -= (1 - GenDate.CurrentDayPercent) * dailyKibbleConsumption * 2f / 5f; // convert every 50 kibbles to 20 meat
                projectedRecords[0].meat_stock.min -= (1 - GenDate.CurrentDayPercent) * dailyKibbleConsumption * 2f / 5f;
                */

            // calculate yields and stocks after today
            for (var day = 1; day < nextNDays; day++)
            {
                projectedPred.Add(new DayPred(day));

                foreach (var g in allGrowth)
                {
                    if (g.IsOutdoor && !(day <= daysUntilGrowingPeriodOver))
                    {
                        continue; // don't count outdoor crop if it's growing period is over
                    }

                    g.Growth += g.GrowthPerTick * GenDate.TicksPerDay *
                                0.55f; // 0.55 is 55% of time plant spent growing

                    if (!(g.Growth >= 1.0f))
                    {
                        continue;
                    }

                    projectedPred[day].yield.max += (int)def.plant.harvestYield;
                    projectedPred[day].yield.min += (int)(def.plant.harvestYield * 0.5f * 0.5f);
                    g.Growth = Plant
                        .BaseGrowthPercent; // if it's fully grown, replant and their growths start at 5%.
                }

                if (def.plant.harvestedThingDef.defName != "Hay")
                {
                    continue;
                }

                projectedPred[day].stock.max = projectedPred[day - 1].stock.max + projectedPred[day].yield.max -
                                               consumption; // projectedPred[1].consumption.max;
                projectedPred[day].stock.min = projectedPred[day - 1].stock.min + projectedPred[day].yield.min -
                                               consumption; // projectedPred[1].consumption.min;
                /*
                    projectedRecords[day].meat_stock.max -= dailyKibbleConsumption * 2f / 5f;
                    projectedRecords[day].meat_stock.min -= dailyKibbleConsumption * 2f / 5f;
                    */
            }
            //*/
        }

        public void GenerateAnalysis()
        {
            analysis =
                "\n\n" + def.defName + " Specific Stats:" +
                //"\nEstimated number of hay needed daily for hay-eaters only= " + (int)dailyHayConsumptionIndoorAnimals +
                "\nNumber of haygrass planted = " + allGrowth.Count + ", outdoor = " +
                allGrowth.Count(h => h.IsOutdoor) +
                "\nEstimated haygrass needed daily = " +
                (consumption / 20 * 10) + // /20 is yield per haygrass * 10 = 10 days growth
                "\nNumber of " + def.plant.harvestedThingDef.defName + " in stockpiles and on the floor " +
                projectedPred[0].stock.max +
                "\nNumber of days until hay in stockpiles run out = " +
                $"{projectedPred[0].stock.max / (float)consumption:0.0}" +
                "\nEstimated hay needed daily for all animals = " + consumption +
                //"\nEstimated hay needed until winter for hay-eaters only = " + (int)(dailyHayConsumptionIndoorAnimals * Predictor.daysUntilWinter) +
                "\nEstimated hay needed until winter for all animals = " + (consumption * daysUntilWinter) +
                //"\nEstimated hay needed until the end of winter for hay-eaters only = " + (int)(dailyHayConsumptionIndoorAnimals * Predictor.daysUntilEndofWinter) +
                "\nEstimated hay needed until the end of winter for all animals = " +
                (consumption * daysUntilEndofWinter) +
                //"\nEstimated hay needed yearly for hay-eaters only = " + (int)(dailyHayConsumptionIndoorAnimals * GenDate.DaysPerMonth * GenDate.MonthsPerYear) + // 60 days
                "\nEstimated hay needed yearly for all animals = " + (consumption * GenDate.DaysPerYear) + // 60 days
                "\nEstimated hay needed until next harvest season(10th of Spring) for all animals = " +
                (consumption * daysUntilNextHarvestSeason) +
                "\n\nProjected " + def.plant.harvestedThingDef.defName + " production:\n";


            analysis += "Day\t Max Yield Min Yield Max Stock Min Stock\n";
            for (var i = 0; i < projectedPred.Count; i++)
            {
                analysis +=
                    $"{i,-2}\t {projectedPred[i].yield.max,-6}\t {projectedPred[i].yield.min,-6}\t {projectedPred[i].stock.max,-6}\t {projectedPred[i].stock.min,-6}\n";
            }
        }

        public class MinMax
        {
            private int _min, _max;

            public MinMax()
            {
            }

            public MinMax(int max, int min)
            {
                _max = max;
                _min = min;
            }

            public bool showDeficiency { get; set; } = false;

            public int min
            {
                get => _min;
                set
                {
                    _min = value;
                    if (showDeficiency != true && value < 0)
                    {
                        _min = 0;
                    }
                }
            }

            public int max
            {
                get => _max;
                set
                {
                    _max = value;
                    if (showDeficiency != true && value < 0)
                    {
                        _max = 0;
                    }
                }
            }

            public static implicit operator MinMax(int val)
            {
                return new MinMax(val, val);
            }
        }

        public class DayPred
        {
            public int day;
            public MinMax yield = new MinMax(), consumption = new MinMax(), stock = new MinMax();

            public DayPred(int day)
            {
                this.day = day;
            }
        }
    }
}