using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Verse;

namespace FoodAnalyticsTab;
/* settings for each graph:
   what data to show
     projected yield, stock, population
     work, time

   TODO:show deficiency, it turns out that if the current Predictor class design use MinMax object to show deficiency, then deficiency will be shown to all graph
   unless it's computed every time for each graph which is not optimal, or two versions can be kept, which is also not optimal

   internal:
     SimpleCurveDrawerStyle
     are settings changed        
*/

public class LineChart
{
    private static readonly int min_day = 1;
    private static readonly int max_day = 60;
    private readonly SimpleCurveDrawerStyle curveDrawerStyle = new SimpleCurveDrawerStyle();
    private readonly Dictionary<string, SimpleCurveDrawInfo> curves = new Dictionary<string, SimpleCurveDrawInfo>();
    private readonly List<CurveMark> marks = new List<CurveMark>();
    public bool changed = false;
    public bool remove;

    private float scrollPos_curr;
    private float scrollPos_prev;
    public ChartSettings setting;

    public LineChart(float default_day, ref Predictor p, Map map)
    {
        setting = new ChartSettings(map);
        scrollPos_curr = scrollPos_prev = default_day;
        setting.graphEnable = p.allPredType.ToDictionary(k => k.Key, _ => false);
        setting.SetDefault();
        SetDefaultStyle();
    }

    public LineChart(LineChart lg, Map map)
    {
        setting = new ChartSettings(map);
        scrollPos_curr = scrollPos_prev = lg.scrollPos_curr;
        SetDefaultStyle();
        marks = new List<CurveMark>(lg.marks);
        curves = new Dictionary<string, SimpleCurveDrawInfo>(lg.curves);
        setting.graphEnable = lg.setting.graphEnable;
    }

    public Rect rect { get; private set; } // region defines this LineGraph

    private void SetDefaultStyle()
    {
        curveDrawerStyle.UseFixedSection = true;
        curveDrawerStyle.FixedSection = new FloatRange(0, scrollPos_curr);
        //curveDrawerStyle.LabelY = "#";
        curveDrawerStyle.LabelX = "Day";
        curveDrawerStyle.UseFixedScale =
            false; // TODO: hopefully can figure out how to have y axis adjust automatically when x axis max changes
        curveDrawerStyle.DrawBackground = true; // draw gray background behind graph
        curveDrawerStyle.DrawBackgroundLines = true; // 
        curveDrawerStyle.DrawMeasures = true;
        curveDrawerStyle.MeasureLabelsXCount = (int)scrollPos_curr; // number of marks on x axis 
        curveDrawerStyle.MeasureLabelsYCount = 5;
        curveDrawerStyle.DrawPoints = false; // draw white points for each data
        curveDrawerStyle.DrawLegend = true; //
        curveDrawerStyle.DrawCurveMousePoint = true; // hover over graph shows details
        curveDrawerStyle.UseAntiAliasedLines = true; // smooth lines
    }

    public void SetMarks(float x, string message, Color color)
    {
        if (!marks.Any(s => s.Message == message))
        {
            marks.Add(new CurveMark(x, message, color));
            marks.Add(new CurveMark(x, message, color)); // fix i++ bug.
        }
        else
        {
            for (var i = 0; i < marks.Count; i++)
            {
                if (marks[i].Message == message)
                {
                    marks[i] = new CurveMark(x, message, marks[i].Color);
                }
            }
            //foreach (CurveMark m in this.marks.Where(s => s.Message == message))
            //{
            //    m.x = x;
            //}
        }
    }

    public void SetCurve(string label, Color color, List<float> points)
    {
        //TODO: fix labels of legends issue of fixed text width
        //TODO: expand rect of legend accordingly
        if (!curves.ContainsKey(label))
        {
            curves.Add(label, new SimpleCurveDrawInfo());
            curves[label].color = color;
            curves[label].label = label;
        }

        curves[label].curve = new SimpleCurve();
        for (var day = 0; day < points.Count; day++)
        {
            curves[label].curve.Add(new CurvePoint(day, points[day]));
        }
    }

    public void RemoveCurve(string label)
    {
        if (curves.ContainsKey(label))
        {
            curves.Remove(label);
        }
    }

    public void Draw(Rect rect)
    {
        curveDrawerStyle.FixedSection = new FloatRange(0, scrollPos_curr);
        curveDrawerStyle.MeasureLabelsXCount = (int)scrollPos_curr; // number of marks on x axis 

        var graphRect = new Rect(rect.x, rect.y, rect.width * .9f, 450f);
        var legendRect = new Rect(rect.x, graphRect.yMax, graphRect.width, 40f);
        var sliderRect = new Rect(rect.x, legendRect.yMax, graphRect.width, 50f);

        SimpleCurveDrawer.DrawCurves(graphRect, curves.Values.ToList(), curveDrawerStyle, marks, legendRect);
        scrollPos_prev = scrollPos_curr;
        scrollPos_curr = Widgets.HorizontalSlider(sliderRect, scrollPos_curr, min_day, max_day);

        this.rect = new Rect(graphRect.x, graphRect.y, graphRect.width,
            graphRect.height + legendRect.height + sliderRect.height);

        var deleteBtn = new Rect(graphRect.xMax + 6, graphRect.yMin, (rect.width - graphRect.width) / 1.5f, 40f);
        if (Widgets.ButtonText(deleteBtn, "Delete".Translate()))
        {
            remove = true;
        }

        if (Widgets.ButtonText(new Rect(deleteBtn.x, deleteBtn.yMax, deleteBtn.width, deleteBtn.height), "Setting"))
        {
            Find.WindowStack.Add(new Dialog_LineChartConfig(ref setting));
        }

        UpdateSetting();
    }

    private void UpdateSetting()
    {
        curveDrawerStyle.DrawPoints = setting.DrawPoints;
        curveDrawerStyle.UseAntiAliasedLines = setting.UseAntiAliasedLines;
    }

    public void UpdateData(ref Predictor predictor)
    {
        //marks add dots on top of a graph, the text label is the text in the popup box
        SetMarks(Predictor.daysUntilNextHarvestSeason, "Days until the Next Harvest Season", Color.green);
        SetMarks(Predictor.daysUntilGrowingPeriodOver, "Days until Growing Period is Over", Color.red);
        SetMarks(Predictor.daysUntilWinter, "Days until the Winter", Color.white);
        SetMarks(Predictor.daysUntilEndofWinter, "Days until the End of Winter", Color.yellow);

        foreach (var s in setting.graphEnable.Where(x => x.Value).Select(x => x.Key))
        {
            GenerateRandomColorPair(setting.graphEnable.Keys.ToList().FindIndex(x => x == s), out var c1, out var c2);
            if (predictor.allPredType[s].def.plant.harvestedThingDef.label == "wood")
            {
                SetCurve(s + " Wood Yield(Max)", c1,
                    predictor.allPredType[s].projectedPred.Select(x => (float)x.yield.max).ToList());
                SetCurve(s + " Wood Yield(Min)", c2,
                    predictor.allPredType[s].projectedPred.Select(x => (float)x.yield.min).ToList());
            }
            else if (predictor.allPredType[s].def.plant.harvestedThingDef.defName == "Hay")
            {
                SetCurve("Hay Yield(Max)",
                    c1, predictor.allPredType[s].projectedPred.Select(x => (float)x.yield.max).ToList());
                SetCurve("Hay Yield(Min)",
                    c2, predictor.allPredType[s].projectedPred.Select(x => (float)x.yield.min).ToList());
                SetCurve("Hay Stock(Max)", Color.white,
                    predictor.allPredType[s].projectedPred.Select(x => (float)x.stock.max).ToList());
                SetCurve("Hay Stock(Min)", Color.black,
                    predictor.allPredType[s].projectedPred.Select(x => (float)x.stock.min).ToList());
            }
            else
            {
                SetCurve(
                    CultureInfo.CurrentCulture.TextInfo.ToTitleCase(predictor.allPredType[s].def.plant.harvestedThingDef
                        .label) + " Yield(Max)",
                    c1, predictor.allPredType[s].projectedPred.Select(x => (float)x.yield.max).ToList());
                SetCurve(
                    CultureInfo.CurrentCulture.TextInfo.ToTitleCase(predictor.allPredType[s].def.plant.harvestedThingDef
                        .label) + " Yield(Min)",
                    c2, predictor.allPredType[s].projectedPred.Select(x => (float)x.yield.min).ToList());
            }
        }

        foreach (var s in setting.graphEnable.Where(x => x.Value == false).Select(x => x.Key))
        {
            if (predictor.allPredType[s].def.plant.harvestedThingDef.label == "wood")
            {
                RemoveCurve(s + " Wood Yield(Max)");
                RemoveCurve(s + " Wood Yield(Min)");
            }
            else if (predictor.allPredType[s].def.plant.harvestedThingDef.defName == "Hay")
            {
                RemoveCurve("Hay Yield(Max)");
                RemoveCurve("Hay Yield(Min)");
                RemoveCurve("Hay Stock(Max)");
                RemoveCurve("Hay Stock(Min)");
            }
            else
            {
                RemoveCurve(
                    CultureInfo.CurrentCulture.TextInfo.ToTitleCase(predictor.allPredType[s].def.plant.harvestedThingDef
                        .label) + " Yield(Max)");
                RemoveCurve(
                    CultureInfo.CurrentCulture.TextInfo.ToTitleCase(predictor.allPredType[s].def.plant.harvestedThingDef
                        .label) + " Yield(Min)");
            }
        }
    }

    private void GenerateRandomColorPair(int i, out Color c1, out Color c2)
    {
        var h = (float)i / setting.graphEnable.Count;
        c1 = Color.HSVToRGB(h, 0.7f, 0.75f);
        c2 = Color.HSVToRGB(h, 0.3f, 0.75f);
    }
}