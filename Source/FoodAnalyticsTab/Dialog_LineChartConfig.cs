using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

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

            if (this.setting.ShowDeficiency)
            {
                if (listing_Standard.ButtonText("Deficiency", null))
                {
                    this.setting.ShowDeficiency = false;
                }
            }
            else if (listing_Standard.ButtonText("No Deficiency", null))
            {
                this.setting.ShowDeficiency = true;
            }

            if (this.setting.DrawPoints)
            {
                if (listing_Standard.ButtonText("Draw Points", null))
                {
                    this.setting.DrawPoints = false;
                }
            }
            else if (listing_Standard.ButtonText("No Points", null))
            {
                this.setting.DrawPoints = true;
            }

            if (this.setting.UseAntiAliasedLines)
            {
                if (listing_Standard.ButtonText("Anti-Alias", null))
                {
                    this.setting.UseAntiAliasedLines = false;
                }
            }
            else if (listing_Standard.ButtonText("No Anti-Alias", null))
            {
                this.setting.UseAntiAliasedLines = true;
            }

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
            //Dialog_GraphConfig.DoThingFilterConfigWindow(rect3, ref this.scrollPosition, this.bill.ingredientFilter, this.bill.recipe.fixedIngredientFilter, 4);

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
            Rect viewRect = new Rect(0, 0f, centerRect.width - 16f, DefDatabase<ThingDef>.AllDefs.Where(x => x.plant != null && x.plant.Sowable).Count()*20);
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
