using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;
using System.Globalization;

namespace FoodAnalyticsTab
{

    [StaticConstructorOnStartup]
    public class Dialog_LineChartConfig : Window
    {
        private Vector2 scrollPosition = new Vector2();
        private ChartSettings setting;
        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(700f, 600f);
            }
        }

        public Dialog_LineChartConfig(ref ChartSettings setting) : base()
        {
            this.setting = setting;
            this.forcePause = false;
            this.doCloseX = true;
            this.closeOnEscapeKey = true;
            this.doCloseButton = false;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = true;
        }


        public override void WindowUpdate()
        {
        }

        private void AddBinarySelector(ref Listing_Standard s, String on, String off, ref bool flag)
        {
            if (flag)
            {
                if (s.ButtonText(on, null))
                {
                    flag = false;
                }
            }
            else if (s.ButtonText(off, null))
            {
                flag = true;
            }
        }
        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(0f, 0f, 400f, 50f);
            Widgets.Label(titleRect, "Graph Settings");
            Text.Font = GameFont.Small;
            Rect listerRect = new Rect(0f, titleRect.yMax, 180f, inRect.height - titleRect.height);
            Listing_Standard listing_Standard = new Listing_Standard(listerRect);

            if (listing_Standard.ButtonText("Default", null))
            {
                this.setting.SetDefault();
            }

            AddBinarySelector(ref listing_Standard, "Deficiency", "No Deficiency", ref this.setting.ShowDeficiency);
            AddBinarySelector(ref listing_Standard, "Draw Points", "No Points", ref this.setting.DrawPoints);
            AddBinarySelector(ref listing_Standard, "Anti-Alias", "No Anti-Alias", ref this.setting.UseAntiAliasedLines);
            AddBinarySelector(ref listing_Standard, "Outdoor Animal", "No Outdoor Animal", ref this.setting.EnableOutdoorAnimalDetection);
            AddBinarySelector(ref listing_Standard, "Growing Season", "No Growing Season", ref this.setting.EnableOutdoorNoGrowWinter);
            
            // TODO: add floating menu here
            if (this.setting.predictorModel == Predictor.ModelType.learning)
            {
                if (this.setting.EnableLearning)
                {
                    if (listing_Standard.ButtonText("Enable Learning", null))
                    {
                        this.setting.EnableLearning = false;
                    }
                }
                else if (listing_Standard.ButtonText("No Learning", null))
                {
                    this.setting.EnableLearning = true;
                }
            }
            /*
            if (listing_Standard.ButtonText(this.bill.repeatMode.GetLabel(), null))
            {
                BillRepeatModeUtility.MakeConfigFloatMenu(this.bill);
            }
            string label = ("BillStoreMode_" + this.bill.storeMode).Translate();
            if (listing_Standard.ButtonText(label, null))
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                using (IEnumerator enumerator = Enum.GetValues(typeof(BillStoreMode)).GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        BillStoreMode billStoreMode = (BillStoreMode)((byte)enumerator.Current);
                        BillStoreMode smLocal = billStoreMode;
                        list.Add(new FloatMenuOption(("BillStoreMode_" + billStoreMode).Translate(), delegate
                        {
                            this.bill.storeMode = smLocal;
                        }, MenuOptionPriority.Medium, null, null, 0f, null));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(list));
            }
            listing_Standard.Gap(12f);
            if (this.bill.repeatMode == BillRepeatMode.RepeatCount)
            {
                listing_Standard.Label("RepeatCount".Translate(new object[]
                {
                this.bill.RepeatInfoText
                }));
                listing_Standard.IntSetter(ref this.bill.repeatCount, 1, "1", 42f);
                listing_Standard.IntAdjuster(ref this.bill.repeatCount, 1, 0);
                listing_Standard.IntAdjuster(ref this.bill.repeatCount, 25, 0);
            }
            else if (this.bill.repeatMode == BillRepeatMode.TargetCount)
            {
                string text = "CurrentlyHave".Translate() + ": ";
                text += this.bill.recipe.WorkerCounter.CountProducts(this.bill);
                text += " / ";
                text += ((this.bill.targetCount >= 999999) ? "Infinite".Translate().ToLower() : this.bill.targetCount.ToString());
                string text2 = this.bill.recipe.WorkerCounter.ProductsDescription(this.bill);
                if (!text2.NullOrEmpty())
                {
                    string text3 = text;
                    text = string.Concat(new string[]
                    {
                    text3,
                    "\n",
                    "CountingProducts".Translate(),
                    ": ",
                    text2
                    });
                }
                listing_Standard.Label(text);
                listing_Standard.IntSetter(ref this.bill.targetCount, 1, "1", 42f);
                listing_Standard.IntAdjuster(ref this.bill.targetCount, 1, 1);
                listing_Standard.IntAdjuster(ref this.bill.targetCount, 25, 1);
                listing_Standard.IntAdjuster(ref this.bill.targetCount, 250, 1);
            }
            listing_Standard.Gap(12f);
            listing_Standard.Label("IngredientSearchRadius".Translate() + ": " + this.bill.ingredientSearchRadius.ToString("F0"));
            this.bill.ingredientSearchRadius = listing_Standard.Slider(this.bill.ingredientSearchRadius, 3f, 100f);
            if (this.bill.ingredientSearchRadius >= 100f)
            {
                this.bill.ingredientSearchRadius = 999f;
            }
            if (this.bill.recipe.workSkill != null)
            {
                listing_Standard.Label("AllowedSkillRange".Translate(new object[]
                {
                this.bill.recipe.workSkill.label.ToLower()
                }));
                listing_Standard.IntRange(ref this.bill.allowedSkillRange, 0, 20);
            }
            */

            //*
            listing_Standard.End();


            Rect centerRect = new Rect(listerRect.xMax + 6f, titleRect.yMax, 280f, -1f);
            centerRect.yMax = inRect.height - this.CloseButSize.y - 6f;

            Widgets.DrawMenuSection(centerRect, true);
            Text.Font = GameFont.Tiny;
            float num = centerRect.width - 2f;
            Rect rect2 = new Rect(centerRect.x + 1f, centerRect.y + 1f, num / 2f, 24f);
            if (Widgets.ButtonText(rect2, "ClearAll".Translate(), true, false, true))
            {
                foreach (String s in setting.graphEnable.Keys.ToList())
                {
                    setting.graphEnable[s] = false;
                }
            }
            Rect rect3 = new Rect(rect2.xMax + 1f, rect2.y, num / 2f, 24f);
            if (Widgets.ButtonText(rect3, "AllowAll".Translate(), true, false, true))
            {
                foreach (String s in setting.graphEnable.Keys.ToList())
                {
                    setting.graphEnable[s] = true;
                }
            }
            Text.Font = GameFont.Small;
            centerRect.yMin = rect2.yMax;
            Rect viewRect = new Rect(0, 0f, centerRect.width - 16f, setting.graphEnable.Count()*20);
            Widgets.BeginScrollView(centerRect, ref scrollPosition, viewRect);
            listing_Standard = new Listing_Standard(new Rect(6, 0, viewRect.width-6, viewRect.height));
            foreach (String s in setting.graphEnable.Keys.ToList())
            {
                bool flag = setting.graphEnable[s];
                listing_Standard.CheckboxLabeled(s, ref flag);
                setting.graphEnable[s] = flag;
            }
            listing_Standard.End();
            Widgets.EndScrollView();

            //*/
            /*StringBuilder stringBuilder = new StringBuilder();


           if (this.bill.recipe.description != null)
           {
               stringBuilder.AppendLine(this.bill.recipe.description);
               stringBuilder.AppendLine();
           }
           stringBuilder.AppendLine("WorkAmount".Translate() + ": " + this.bill.recipe.WorkAmountTotal(null).ToStringWorkAmount());
           stringBuilder.AppendLine();
           for (int i = 0; i < this.bill.recipe.ingredients.Count; i++)
           {
               IngredientCount ingredientCount = this.bill.recipe.ingredients[i];
               if (!ingredientCount.filter.Summary.NullOrEmpty())
               {
                   stringBuilder.AppendLine(this.bill.recipe.IngredientValueGetter.BillRequirementsDescription(ingredientCount));
               }
           }
           stringBuilder.AppendLine();
           string text4 = this.bill.recipe.IngredientValueGetter.ExtraDescriptionLine(this.bill.recipe);
           if (text4 != null)
           {
               stringBuilder.AppendLine(text4);
               stringBuilder.AppendLine();
           }
           stringBuilder.AppendLine("MinimumSkills".Translate());
           stringBuilder.AppendLine(this.bill.recipe.MinSkillString);
           Text.Font = GameFont.Small;
           string text5 = stringBuilder.ToString();
           if (Text.CalcHeight(text5, rect4.width) > rect4.height)
           {
               Text.Font = GameFont.Tiny;
           }
           Widgets.Label(rect4, text5);
           Text.Font = GameFont.Small;
           if (this.bill.recipe.products.Count == 1)
           {
               Widgets.InfoCardButton(rect4.x, rect3.y, this.bill.recipe.products[0].thingDef);
           }
           */
        }

    }
}
