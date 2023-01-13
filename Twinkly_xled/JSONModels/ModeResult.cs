namespace Twinkly_xled.JSONModels
{
    public class Mode
    {
        public string mode { get; set; }
        public override string ToString() => mode;
    }

    public class ModeResult : Mode
    {
        public int code { get; set; }
        public override string ToString() => $"{mode} ({code})";
    }

    public enum LedModes
    {
        off,        //- turns off lights
        color,      //- static color
        demo,       //- starts predefined sequence of effects that are changed after few seconds
        effect,     //- plays a predefined effect
        movie,      //- plays predefined or uploaded effect 
        playlist,   //- (since 2.5.6) cycles thru playlist of uploaded movies
        rt          //- receive effect in real time
    }
}


