using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace FoodAnalyticsTab
{
    class Prediction
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

        public Prediction(MinMax a, MinMax b, MinMax c, MinMax d, MinMax e)
        {
            hay_yield = a;
            hay_stock = b;
            hay_consumption = c;
            meat_stock = d;
            animal_population = e;
            showDeficiency = false;
        }
        public MinMax hay_yield { get; set; }
        public MinMax hay_stock { get; set; }
        public MinMax meat_stock { get; set; }
        public MinMax animal_population { get; set; }
        public MinMax hay_consumption { get; set; }
    };

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

    */
    /*
       data selector checkbox
       add graph button
    */
    class Prediction2
    {
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
        private class Prediction // should contain update rule
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
            public class PredTerm
            {
                public MinMax yield, consumption, stock;
                public bool enabled;
                public void SetUpdateRule(float v0, float v)
                {

                }
                public void UpdateOnce()
                {

                }
            }
            private bool _showDeficiency;
            public bool showDeficiency
            {
                get { return _showDeficiency; }
                set
                {
                    //_showDeficiency = hay_yield.showDeficiency = hay_stock.showDeficiency = meat_stock.showDeficiency =
                    //   animal_population.showDeficiency = hay_consumption.showDeficiency = value;
                }
            }
            public bool enabled { get; set; }

            public Prediction(MinMax a, MinMax b, MinMax c, MinMax d, MinMax e)
            {
                showDeficiency = false;
            }
            public List<PredTerm> PredTerms { get; set; }

            public void update()
            {
                if (enabled)
                {
                    /*
                    // crop
                    if (GenDate.HourOfDay >= 4 && GenDate.HourOfDay <= 19)
                    {
                        if (allHaygrassGrowth.LastOrDefault().Growth >= 1.0f)
                        {
                            allHaygrassGrowth.LastOrDefault().Growth = Plant.BaseGrowthPercent;
                            projectedRecords[0].hay_yield.max += haygrass_yieldMax;
                            projectedRecords[0].hay_yield.min += haygrass_yieldMin;
                        }
                        projectedRecords[0].hay_stock.max += projectedRecords[0].hay_yield.max;
                        projectedRecords[0].hay_stock.min += projectedRecords[0].hay_yield.min;
                    }
                    stock. -= GenDate.CurrentDayPercent * dailyHayConsumption; // only count the rest of day
                    projectedRecords[0].hay_stock.min -= GenDate.CurrentDayPercent * dailyHayConsumption;
                    projectedRecords[0].meat_stock.max -= GenDate.CurrentDayPercent * dailyKibbleConsumption * 2f / 5f; // convert every 50 kibbles to 20 meat
                    projectedRecords[0].meat_stock.min -= GenDate.CurrentDayPercent * dailyKibbleConsumption * 2f / 5f;

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
                                break;
                            }
                            k.Growth += k.GrowthPerTick * GenDate.TicksPerDay * 0.55f; // 0.55 is 55% of time plant spent growing
                                                                                       //debug_val[6] = k.GrowthPerTick;
                            if (k.Growth >= 1.0f)
                            {
                                projectedRecords[day].hay_yield.max += haygrass_yieldMax;
                                projectedRecords[day].hay_yield.min += haygrass_yieldMin;
                                k.Growth = Plant.BaseGrowthPercent; // if it's fully grown, replant and their growths start at 5%.
                            }
                        }

                        projectedRecords[day].hay_stock.max += projectedRecords[day].hay_yield.max;
                        projectedRecords[day].hay_stock.min += projectedRecords[day].hay_yield.min;
                        projectedRecords[day].hay_stock.max -= dailyHayConsumption;
                        projectedRecords[day].hay_stock.min -= dailyHayConsumption;
                        projectedRecords[day].meat_stock.max -= dailyKibbleConsumption * 2f / 5f;
                        projectedRecords[day].meat_stock.min -= dailyKibbleConsumption * 2f / 5f;
                        */

                }
            }

        };
        Dictionary<string, Prediction> all;
        void MakePrediction(int days)
        {
            foreach (var t in this.all)
            {
                t.Value.update();
            }
        }
    }
}
