using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;
using System.Globalization;

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
    public class Predictor
    {
        public enum ModelType
        {
            analytical, iterative, learning
        }

        private class GrowthTracker
        {
            public GrowthTracker(float a, float b)
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
                }
            }

        };
        Dictionary<string, Prediction> all;

        public Dictionary<String, bool> predictionEnable = new Dictionary<String, bool>();

        public Predictor()
        {
            foreach (ThingDef x in DefDatabase<ThingDef>.AllDefs.Where(x => x.plant != null && x.plant.Sowable).OrderBy(x => x.label))
            {
                predictionEnable.Add(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.label.ToLower()), false);
            }
        }

        void MakePrediction(int days)
        {
            foreach (var t in this.all)
            {
                t.Value.update();
            }
        }

        public void EnablePrediction(List<LineChart> chartList)
        {
            predictionEnable = predictionEnable.ToDictionary(p => p.Key, p => false);
            foreach (LineChart c in chartList)
            {
                foreach (string s in c.setting.graphEnable.Where(x => x.Value == true).Select(x => x.Key).ToList())
                {
                    predictionEnable[s] = true;
                }
            }
        }
    }
}
