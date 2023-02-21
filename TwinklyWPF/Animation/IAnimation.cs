namespace TwinklyWPF.Animation
{
    public interface IAnimation
    {
        public void Initialize(RealtimeMovie context);
        public void Draw(byte[] _frameData);
        public string Name { get; }
    }

    public class AnimationPlaceholder : IAnimation
    {
        string _name;

        public AnimationPlaceholder(string name)
        {
            _name = name;
        }

        public string Name
        {
            get { return _name; }
        }

        public void Draw(byte[] _frameData)
        {
            throw new System.NotImplementedException();
        }

        public void Initialize(RealtimeMovie context)
        {
            throw new System.NotImplementedException();
        }
    }
}
