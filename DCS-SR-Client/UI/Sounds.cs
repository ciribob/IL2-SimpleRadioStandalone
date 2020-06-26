using System;
using System.IO;
using System.Media;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    public static class Sounds
    {
        public static SoundPlayer BeepConnected;
        public static SoundPlayer BeepDisconnected;

        public static void Init()
        {
            // Audio file taken from https://freesound.org/people/pan14/sounds/263124/ @ 2018-10-03
            BeepConnected = new SoundPlayer(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AudioEffects", "beep-connected.wav"));
            // Audio file taken from https://freesound.org/people/pan14/sounds/263123/ @ 2018-10-03
            BeepDisconnected = new SoundPlayer(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AudioEffects", "beep-disconnected.wav"));
        }
    }
}
