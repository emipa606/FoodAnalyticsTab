using System.Linq;
using UnityEngine;
using Verse;

namespace FoodAnalyticsTab;

[StaticConstructorOnStartup]
public class Dialog_LineChartConfig : Window
{
    private readonly ChartSettings setting;
    private Vector2 scrollPosition;

    public Dialog_LineChartConfig(ref ChartSettings setting)
    {
        this.setting = setting;
        forcePause = false;
        doCloseX = true;
        closeOnCancel = true;
        doCloseButton = false;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
    }

    public override Vector2 InitialSize => new Vector2(700f, 600f);


    public override void WindowUpdate()
    {
    }

    private void AddBinarySelector(ref Listing_Standard s, string on, string off, ref bool flag)
    {
        if (flag)
        {
            if (s.ButtonText(on))
            {
                flag = false;
            }
        }
        else if (s.ButtonText(off))
        {
            flag = true;
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        var titleRect = new Rect(0f, 0f, 400f, 50f);
        Widgets.Label(titleRect, "Graph Settings"); // TODO: add renaming button
        Text.Font = GameFont.Small;
        var listerRect = new Rect(0f, titleRect.yMax, 180f, inRect.height - titleRect.height);
        var listing_Standard = new Listing_Standard();
        listing_Standard.Begin(listerRect);

        if (listing_Standard.ButtonText("Default"))
        {
            setting.SetDefault();
        }

        AddBinarySelector(ref listing_Standard, "Deficiency", "No Deficiency", ref setting.ShowDeficiency);
        AddBinarySelector(ref listing_Standard, "Draw Points", "No Points", ref setting.DrawPoints);
        AddBinarySelector(ref listing_Standard, "Anti-Alias", "No Anti-Alias", ref setting.UseAntiAliasedLines);
        AddBinarySelector(ref listing_Standard, "Outdoor Animal", "No Outdoor Animal",
            ref setting.EnableOutdoorAnimalDetection);
        AddBinarySelector(ref listing_Standard, "Growing Season", "No Growing Season",
            ref setting.EnableOutdoorNoGrowWinter);

        // TODO: add floating menu here
        if (setting.predictorModel == Predictor.ModelType.learning)
        {
            if (setting.EnableLearning)
            {
                if (listing_Standard.ButtonText("Enable Learning"))
                {
                    setting.EnableLearning = false;
                }
            }
            else if (listing_Standard.ButtonText("No Learning"))
            {
                setting.EnableLearning = true;
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


        var centerRect = new Rect(listerRect.xMax + 6f, titleRect.yMax, 280f, -1f)
        {
            yMax = inRect.height - CloseButSize.y - 6f
        };

        Widgets.DrawMenuSection(centerRect);
        Text.Font = GameFont.Tiny;
        var num = centerRect.width - 2f;
        var rect2 = new Rect(centerRect.x + 1f, centerRect.y + 1f, num / 2f, 24f);
        if (Widgets.ButtonText(rect2, "ClearAll".Translate(), true, false))
        {
            foreach (var s in setting.graphEnable.Keys.ToList())
            {
                setting.graphEnable[s] = false;

                MainTabWindow_Estimator.predictor.allPredType[s].enabled = false;
            }
        }

        var rect3 = new Rect(rect2.xMax + 1f, rect2.y, num / 2f, 24f);
        if (Widgets.ButtonText(rect3, "AllowAll".Translate(), true, false))
        {
            foreach (var s in setting.graphEnable.Keys.ToList())
            {
                setting.graphEnable[s] = true;
                MainTabWindow_Estimator.predictor.allPredType[s].enabled = true;
                MainTabWindow_Estimator.predictor.MakePrediction(0); // TODO: if paused prevent calling 2nd time
            }
        }

        Text.Font = GameFont.Small;
        centerRect.yMin = rect2.yMax;
        var viewRect = new Rect(0, 0f, centerRect.width - 16f,
            setting.graphEnable.Count * (Text.LineHeight + listing_Standard.verticalSpacing));
        Widgets.BeginScrollView(centerRect, ref scrollPosition, viewRect);
        listing_Standard = new Listing_Standard();
        listing_Standard.Begin(new Rect(6, 0, viewRect.width - 6, viewRect.height));
        foreach (var s in setting.graphEnable.Keys.ToList())
        {
            var checkOn = setting.graphEnable[s];
            var selected = false;
            listing_Standard.CheckboxLabeledSelectable(s, ref selected,
                ref checkOn); // TODO: use select flag to show a rect on the right to allow selecting yield, consumption, stock, population
            if (checkOn == setting.graphEnable[s])
            {
                continue;
            }

            setting.graphEnable[s] = checkOn;
            MainTabWindow_Estimator.predictor.allPredType[s].enabled = checkOn;

            MainTabWindow_Estimator.predictor.MakePrediction(0);
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