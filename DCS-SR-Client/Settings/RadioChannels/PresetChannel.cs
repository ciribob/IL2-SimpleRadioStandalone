namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels
{
    public class PresetChannel
    {
        public string Text { get; set; }
        public object Value { get; set; }
        public int Channel { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}