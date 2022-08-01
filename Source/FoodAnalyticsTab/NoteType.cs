using Verse;

namespace FoodAnalyticsTab;

public class NoteType : MapComponent
{
    public string text = "";

    public NoteType(Map map) : base(map)
    {
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref text, "NotePageText");
    }
}