using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;


namespace FoodAnalyticsTab
{
    /* settings for each graph:
       what data to show
         projected yield, stock, population
         work, time

       TODO:show deficiency

       internal:
         SimpleCurveDrawerStyle
         are settings changed        
    */

    public class ChartSettings
    {
        public Dictionary<String, bool> graphEnable = new Dictionary<String, bool>();
        public bool showDeficiency = false;
    }

    class LineChart
    {
        private List<CurveMark> marks = new List<CurveMark>();
        private Dictionary<String, SimpleCurveDrawInfo> curves = new Dictionary<String, SimpleCurveDrawInfo>();
        private SimpleCurveDrawerStyle curveDrawerStyle = new SimpleCurveDrawerStyle();

        private float scrollPos_curr;
        private float scrollPos_prev;
        public Rect rect { get; private set; } // region defines this LineGraph
        public bool changed { get { return (int)scrollPos_curr != (int)scrollPos_prev; } }
        static int min_day = 1, max_day = 60;
        public bool remove = false;
        private ChartSettings setting = new ChartSettings();

        public LineChart(float default_day)
        {
            this.scrollPos_curr = this.scrollPos_prev = default_day;
            SetDefaultStyle();
        }
        public LineChart(LineChart lg)
        {
            this.scrollPos_curr = this.scrollPos_prev = lg.scrollPos_curr;
            SetDefaultStyle();
            this.marks = new List<CurveMark>(lg.marks);
            this.curves = new Dictionary<String, SimpleCurveDrawInfo>(lg.curves);
        }
        private void SetDefaultStyle()
        {
            curveDrawerStyle.UseFixedSection = true;
            curveDrawerStyle.FixedSection = new Vector2(0, scrollPos_curr);
            curveDrawerStyle.LabelY = "Hay #";
            curveDrawerStyle.LabelX = "Day";
            curveDrawerStyle.UseFixedScale = false;
            curveDrawerStyle.DrawBackground = true; // draw gray background behind graph
            curveDrawerStyle.DrawBackgroundLines = true; // 
            curveDrawerStyle.DrawMeasures = true;
            curveDrawerStyle.MeasureLabelsXCount = (int)this.scrollPos_curr; // number of marks on x axis 
            curveDrawerStyle.MeasureLabelsYCount = 5;
            curveDrawerStyle.DrawPoints = false; // draw white points for each data
            curveDrawerStyle.DrawLegend = true; //
            curveDrawerStyle.DrawCurveMousePoint = true; // hover over graph shows details
            curveDrawerStyle.UseAntiAliasedLines = true; // smooth lines

            foreach (ThingDef x in DefDatabase<ThingDef>.AllDefs.Where(x => x.plant != null && x.plant.Sowable))
            {
                setting.graphEnable.Add(x.label, false);
            }
        }
        public void SetMarks(float x, string message, Color color)
        {
            if (!this.marks.Where(s => s.message == message).Any())
            {
                this.marks.Add(new CurveMark(x, message, color));
                this.marks.Add(new CurveMark(x, message, color)); // fix i++ bug.
            }
            else
            {
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
        public void Draw(Rect rect)
        {
            curveDrawerStyle.FixedSection = new Vector2(0, this.scrollPos_curr);
            curveDrawerStyle.MeasureLabelsXCount = (int)this.scrollPos_curr; // number of marks on x axis 

            Rect graphRect = new Rect(rect.x, rect.y, rect.width * .9f, 450f);
            Rect legendRect = new Rect(rect.x, graphRect.yMax, graphRect.width, 40f);
            Rect sliderRect = new Rect(rect.x, legendRect.yMax, graphRect.width, 50f);

            SimpleCurveDrawer.DrawCurves(graphRect, this.curves.Values.ToList(), this.curveDrawerStyle, this.marks, legendRect);
            this.scrollPos_prev = this.scrollPos_curr;
            this.scrollPos_curr = Widgets.HorizontalSlider(sliderRect, this.scrollPos_curr, min_day, max_day);

            this.rect = new Rect(graphRect.x, graphRect.y, graphRect.width, graphRect.height + legendRect.height + sliderRect.height);

            Rect deleteBtn = new Rect(graphRect.xMax + 6, graphRect.yMin, (rect.width - graphRect.width)/1.5f, 40f);
            if (Widgets.ButtonText(deleteBtn, "Delete".Translate(), true, true, true))
            {
                this.remove = true;
            }
            if (Widgets.ButtonText(new Rect(deleteBtn.x, deleteBtn.yMax, deleteBtn.width, deleteBtn.height),"Setting", true, true, true))
            {
                Find.WindowStack.Add(new Dialog_LineChartConfig(ref this.setting));
            }
        }

        
    }

}
